using System.Collections.Concurrent;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Git;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models.Surreal;
using Microsoft.Extensions.Logging;
public class MetricsCollector
{
    private readonly Channel<LspMessage> _lspChannel;
    private readonly Channel<GitEvent> _gitChannel;
    private readonly Channel<NodeUpdate> _updateChannel;
    private readonly Channel<DependencyEdge> _dependencyChannel;
    private readonly LizardAnalyzer _lizard;
    private readonly GitWatcher _gitWatcher;
    private readonly SurrealDbRepository _repository;
    private readonly LspReferenceTracker _referenceTracker;
    private readonly LspClient _lspClient; // New: for sending requests
    private readonly ILogger<MetricsCollector> _logger;

    // Cache of symbols by file for quick lookup
    private readonly ConcurrentDictionary<string, List<DocumentSymbol>> _symbolCache = new();

    public MetricsCollector(
        GitWatcher gitWatcher,
        SurrealDbRepository repository,
        LspClient lspClient,
        LizardAnalyzer lizard,
        ILogger<MetricsCollector> logger)
    {
        _lspChannel = Channel.CreateUnbounded<LspMessage>();
        _gitChannel = Channel.CreateUnbounded<GitEvent>();
        _updateChannel = Channel.CreateUnbounded<NodeUpdate>();
        _dependencyChannel = Channel.CreateUnbounded<DependencyEdge>();
        _lizard = lizard;
        _gitWatcher = gitWatcher;
        _repository = repository;
        _referenceTracker = new LspReferenceTracker();
        _lspClient = lspClient;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("MetricsCollector starting processing pipelines.");
        var lspTask = ProcessLspMessages(ct);
        var gitTask = ProcessGitEvents(ct);
        var updateTask = ProcessNodeUpdates(ct);
        var dependencyTask = ProcessDependencies(ct);

        await Task.WhenAll(lspTask, gitTask, updateTask, dependencyTask);
        _logger.LogInformation("MetricsCollector pipelines stopped.");
    }

    private async Task ProcessLspMessages(CancellationToken ct)
    {
        await foreach (var message in _lspChannel.Reader.ReadAllAsync(ct))
        {
            if (message.Result == null) continue;

            // Check if this is a response to a tracked request
            if (message.Id != null && _referenceTracker.GetRequest(message.Id) is { } request)
            {
                await ProcessTrackedReferenceResponse(message, request, ct);
                _referenceTracker.CompleteRequest(message.Id);
                continue;
            }

            // Parse document symbols
            try
            {
                var symbols = LspMessageParser.ParseDocumentSymbols(message.Result.Value);
                if (symbols != null && symbols.Length > 0)
                {
                    var fileUri = ExtractFileUri(message);
                    if (fileUri != null)
                    {
                        await ProcessDocumentSymbols(symbols, fileUri, ct);

                        // After indexing symbols, request dependencies
                        await RequestDependenciesForFile(fileUri, symbols, ct);
                    }
                    continue;
                }
            }
            catch { /* Not document symbols */ }
        }
    }

    private async Task ProcessDocumentSymbols(
        DocumentSymbol[] symbols,
        string fileUri,
        CancellationToken ct)
    {
        _logger.LogDebug("Processing {Count} symbols from {FileUri}", symbols.Length, fileUri);
        // Cache symbols for this file
        _symbolCache[fileUri] = symbols.ToList();

        // Process hierarchy
        foreach (var symbol in symbols)
        {
            await ProcessSymbolHierarchy(symbol, fileUri, parentNamespace: null, ct);
        }
    }

    private async Task RequestDependenciesForFile(
        string fileUri,
        DocumentSymbol[] symbols,
        CancellationToken ct)
    {
        // For each method/function, find what it calls
        await RequestDependenciesForSymbols(fileUri, symbols, ct);
    }

    private async Task RequestDependenciesForSymbols(
        string fileUri,
        IEnumerable<DocumentSymbol> symbols,
        CancellationToken ct)
    {
        foreach (var symbol in symbols)
        {
            // Only track calls from methods/functions
            if (symbol.Kind is SymbolKind.Method or SymbolKind.Function)
            {
                var nodeId = GenerateNodeIdFromSymbol(fileUri, symbol);

                // Request definition for this symbol to understand its type
                var definitionRequestId = Guid.NewGuid();
                await _lspClient.RequestDefinitionAsync(
                    definitionRequestId,
                    fileUri,
                    symbol.SelectionRange.Start);

                // For now, we'll parse the symbol's body to find call sites
                // This is where we'd request references for each identifier used
                // But LSP doesn't give us the body directly - we need to analyze

                // Alternative: Use "call hierarchy" LSP feature if available
                await RequestCallHierarchy(nodeId, fileUri, symbol, ct);
            }

            // Handle inheritance
            if (symbol.Kind is SymbolKind.Class or SymbolKind.Interface)
            {
                // Check symbol detail for base class/interface info
                if (symbol.Detail != null)
                {
                    await ExtractInheritanceRelationships(fileUri, symbol, ct);
                }
            }

            // Recurse into children
            if (symbol.Children != null)
            {
                await RequestDependenciesForSymbols(fileUri, symbol.Children, ct);
            }
        }
    }

    private async Task RequestCallHierarchy(
        string fromNodeId,
        string fileUri,
        DocumentSymbol symbol,
        CancellationToken ct)
    {
        // LSP call hierarchy request
        var requestId = Guid.NewGuid();

        _referenceTracker.TrackRequest(requestId, new LspReferenceTracker.ReferenceRequest
        {
            FromNodeId = fromNodeId,
            TargetSymbol = symbol.Name,
            FileUri = fileUri,
            Position = symbol.SelectionRange.Start,
            ExpectedType = LspReferenceTracker.RelationshipType.Calls
        });

        await _lspClient.RequestOutgoingCallsAsync(requestId, fileUri, symbol.SelectionRange.Start);
    }

    private async Task ExtractInheritanceRelationships(
        string fileUri,
        DocumentSymbol symbol,
        CancellationToken ct)
    {
        // Parse detail string for base types
        // Example: "class UserService : BaseService, IUserService"

        if (symbol.Detail == null) return;

        var fromNodeId = GenerateNodeIdFromSymbol(fileUri, symbol);

        // Simple parsing - this would need to be more robust
        var parts = symbol.Detail.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return;

        var baseTypes = parts[1].Split(',', StringSplitOptions.TrimEntries);

        foreach (var baseType in baseTypes)
        {
            var cleanType = baseType.Trim();

            // Determine if it's interface or class based on naming convention
            var isInterface = cleanType.StartsWith('I') && cleanType.Length > 1 && char.IsUpper(cleanType[1]);

            var edge = new DependencyEdge
            {
                FromNodeId = fromNodeId,
                ToSymbolName = cleanType,
                RelationshipType = isInterface ? "implements" : "inherits",
                SourceFileUri = fileUri
            };

            await _dependencyChannel.Writer.WriteAsync(edge, ct);
        }
    }

    private async Task ProcessTrackedReferenceResponse(
        LspMessage message,
        LspReferenceTracker.ReferenceRequest request,
        CancellationToken ct)
    {
        // This is a response to our reference/call hierarchy request

        // Try parsing as call hierarchy outgoing calls
        try
        {
            var calls = ParseOutgoingCalls(message.Result!.Value);
            if (calls != null)
            {
                foreach (var call in calls)
                {
                    var edge = new DependencyEdge
                    {
                        FromNodeId = request.FromNodeId,
                        ToSymbolName = call.Name,
                        ToFileUri = call.Uri,
                        ToLine = call.Range.Start.Line,
                        RelationshipType = "calls",
                        SourceFileUri = request.FileUri
                    };

                    await _dependencyChannel.Writer.WriteAsync(edge, ct);
                }
            }
        }
        catch { /* Not call hierarchy */ }

        // Try parsing as references
        try
        {
            var references = LspMessageParser.ParseReferences(message.Result!.Value);
            if (references != null)
            {
                foreach (var reference in references)
                {
                    var edge = new DependencyEdge
                    {
                        FromNodeId = request.FromNodeId,
                        ToFileUri = reference.Uri,
                        ToLine = reference.Range.Start.Line,
                        RelationshipType = request.ExpectedType.ToString().ToLowerInvariant(),
                        SourceFileUri = request.FileUri
                    };

                    await _dependencyChannel.Writer.WriteAsync(edge, ct);
                }
            }
        }
        catch { /* Not references */ }
    }

    private CallHierarchyItem[]? ParseOutgoingCalls(JsonElement result)
    {
        // Parse LSP call hierarchy response
        // Format: CallHierarchyOutgoingCall[]
        try
        {

            return JsonSerializer.Deserialize<CallHierarchyItem[]>(result.GetRawText(), ACCJsonContext.Default.CallHierarchyItemArray);
        }
        catch
        {
            return null;
        }
    }

    private async Task ProcessDependencies(CancellationToken ct)
    {
        await foreach (var edge in _dependencyChannel.Reader.ReadAllAsync(ct))
        {
            // Resolve the "to" node
            string? toNodeId = null;

            if (edge.ToFileUri != null && edge.ToLine.HasValue)
            {
                // Find node at this location
                toNodeId = await _repository.FindNodeAtLocationAsync(
                    UriToFilePath(edge.ToFileUri),
                    edge.ToLine.Value);
            }
            else if (edge.ToSymbolName != null)
            {
                // Search for node by name (might need disambiguation)
                toNodeId = await _repository.FindNodeByNameAsync(edge.ToSymbolName);
            }

            if (toNodeId != null)
            {
                _logger.LogDebug("Creating dependency {From} -{Type}-> {To}", edge.FromNodeId, edge.RelationshipType, toNodeId);
                await _repository.UpsertDependencyAsync(
                    edge.FromNodeId,
                    toNodeId,
                    edge.RelationshipType);
            }
        }
    }


    // Public method to feed LSP messages from stdio
    public async Task EnqueueLspMessageAsync(LspMessage message, CancellationToken ct = default)
    {
        await _lspChannel.Writer.WriteAsync(message, ct);
    }


    private async Task ProcessSymbolHierarchy(
        DocumentSymbol symbol,
        string fileUri,
        string? parentNamespace,
        CancellationToken ct)
    {
        // Build namespace chain
        var currentNamespace = parentNamespace != null
            ? $"{parentNamespace}.{symbol.Name}"
            : symbol.Name;

        // Only create nodes for classes, methods, functions
        if (symbol.Kind is SymbolKind.Class or SymbolKind.Method or SymbolKind.Function
            or SymbolKind.Interface or SymbolKind.Module)
        {
            var nodeId = GenerateNodeIdFromSymbol(fileUri, symbol);

            var update = new NodeUpdate
            {
                NodeId = nodeId,
                Type = MapSymbolKindToType(symbol.Kind),
                Language = DetectLanguageFromUri(fileUri),
                Name = symbol.Name,
                FilePath = UriToFilePath(fileUri),
                LineStart = symbol.Range.Start.Line,
                LineEnd = symbol.Range.End.Line,
                Namespace = parentNamespace,
                Signature = symbol.Detail,
                ReturnType = ExtractReturnType(symbol.Detail)
            };

            await _updateChannel.Writer.WriteAsync(update, ct);
        }

        // Process children recursively
        if (symbol.Children != null)
        {
            foreach (var child in symbol.Children)
            {
                await ProcessSymbolHierarchy(child, fileUri, currentNamespace, ct);
            }
        }
    }

    private string GenerateNodeIdFromSymbol(string fileUri, DocumentSymbol symbol)
    {
        var filePath = UriToFilePath(fileUri);
        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, filePath);
        return $"{relativePath}:{symbol.Name}:{symbol.Range.Start.Line}";
    }

    private string GenerateNodeIdFromLocation(Location location, string name)
    {
        var filePath = UriToFilePath(location.Uri);
        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, filePath);
        return $"{relativePath}:{name}:{location.Range.Start.Line}";
    }

    private string MapSymbolKindToType(SymbolKind kind) => kind switch
    {
        SymbolKind.Class => "class",
        SymbolKind.Method => "method",
        SymbolKind.Function => "function",
        SymbolKind.Interface => "interface",
        SymbolKind.Module => "module",
        SymbolKind.Namespace => "namespace",
        _ => "unknown"
    };

    private string DetectLanguageFromUri(string uri)
    {
        var extension = Path.GetExtension(uri).ToLowerInvariant();
        return extension switch
        {
            ".cs" => "csharp",
            ".ts" => "typescript",
            ".js" => "javascript",
            ".py" => "python",
            ".go" => "go",
            _ => "unknown"
        };
    }

    private string UriToFilePath(string uri)
    {
        // Convert file:// URI to filesystem path
        if (uri.StartsWith("file://"))
        {
            return Uri.UnescapeDataString(uri.Substring(7));
        }
        return uri;
    }

    private string? ExtractFileUri(LspMessage message)
    {
        // Try to extract textDocument.uri from params
        if (message.Params?.TryGetProperty("textDocument", out var textDoc) == true)
        {
            if (textDoc.TryGetProperty("uri", out var uri))
            {
                return uri.GetString();
            }
        }
        return null;
    }

    private string? ExtractReturnType(string? detail)
    {
        // Parse return type from signature detail
        // This is language-specific and would need proper parsing
        // For now, return null and let Lizard/language-specific tools handle it
        return null;
    }

    private async Task ProcessGitEvents(CancellationToken ct)
    {
        await foreach (var gitEvent in _gitChannel.Reader.ReadAllAsync(ct))
        {
            // Skip deleted files
            if (gitEvent.Type == GitEventType.Deleted) continue;

            _logger.LogDebug("Processing git event {EventType} for {FilePath}", gitEvent.Type, gitEvent.FilePath);
            // Run lizard on the file
            var lizardResult = await _lizard.AnalyzeFileAsync(gitEvent.FilePath, ct);

            // Extract git history
            var history = _gitWatcher.ExtractHistory(gitEvent.FilePath);

            if (lizardResult?.FunctionList != null)
            {
                foreach (var function in lizardResult.FunctionList)
                {
                    // Merge with LSP data if available
                    var nodeId = GenerateNodeId(gitEvent.FilePath, function.Name, function.StartLine);

                    var update = new NodeUpdate
                    {
                        NodeId = nodeId,
                        Type = "function",
                        Language = DetectLanguage(gitEvent.FilePath),
                        Name = function.Name,
                        FilePath = gitEvent.FilePath,
                        LineStart = function.StartLine,
                        LineEnd = function.EndLine,
                        Signature = function.LongName,

                        // Lizard metrics
                        LinesOfCode = function.Nloc,
                        CyclomaticComplexity = function.CyclomaticComplexity,
                        Parameters = function.ParameterCount,

                        // Git metrics
                        GitHistory = history
                    };

                    await _updateChannel.Writer.WriteAsync(update, ct);
                }
            }
        }
    }

    private async Task ProcessNodeUpdates(CancellationToken ct)
    {
        await foreach (var update in _updateChannel.Reader.ReadAllAsync(ct))
        {
            _logger.LogDebug("Upserting node {NodeId}", update.NodeId);
            // Upsert to SurrealDB - merges LSP + Lizard + Git data
            await _repository.UpsertNodeAsync(update);
        }
    }

    private string GenerateNodeId(string filePath, string functionName, int lineStart)
    {
        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, filePath);
        return $"{relativePath}:{functionName}:{lineStart}";
    }

    private string DetectLanguage(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" => "csharp",
            ".ts" => "typescript",
            ".js" => "javascript",
            ".py" => "python",
            ".go" => "go",
            _ => "unknown"
        };
    }
}
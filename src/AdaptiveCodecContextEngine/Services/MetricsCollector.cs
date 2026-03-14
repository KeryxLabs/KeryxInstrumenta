using System.Collections.Concurrent;
using System.Text;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Git;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models.Surreal;
using Microsoft.Extensions.Logging;
public class MetricsCollector
{

    private readonly Channel<LspMessageWithContext> _lspChannel;
    private readonly Channel<GitEvent> _gitChannel;
    private readonly Channel<NodeUpdate> _updateChannel;
    private readonly Channel<DependencyEdge> _dependencyChannel;
    private readonly LizardAnalyzer _lizard;
    private readonly GitWatcher _gitWatcher;
    private readonly SurrealDbRepository _repository;
    private readonly ILogger<MetricsCollector> _logger;

    // Cache symbols by file for reference
    private readonly ConcurrentDictionary<string, List<DocumentSymbol>> _symbolCache = new();

    public MetricsCollector(
        Channel<LspMessageWithContext> lspChannel,
        Channel<GitEvent> gitChannel,
        Channel<NodeUpdate> updateChannel,
        Channel<DependencyEdge> dependencyChannel,
        GitWatcher gitWatcher,
        SurrealDbRepository repository,
        LizardAnalyzer analyzer,
        ILogger<MetricsCollector> logger)
    {
        _lspChannel = lspChannel;
        _gitChannel = gitChannel;
        _updateChannel = updateChannel;
        _dependencyChannel = dependencyChannel;
        _lizard = analyzer;
        _gitWatcher = gitWatcher;
        _repository = repository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var lspTask = ProcessLspMessages(ct);
        var gitTask = ProcessGitEvents(ct);
        var updateTask = ProcessNodeUpdates(ct);
        var dependencyTask = ProcessDependencies(ct);

        await Task.WhenAll(lspTask, gitTask, updateTask, dependencyTask);
    }

    private async Task ProcessLspMessages(CancellationToken ct)
    {
        await foreach (var messageWithContext in _lspChannel.Reader.ReadAllAsync(ct))
        {
            _logger.LogDebug("Processing LSP Message");
            var message = messageWithContext.Message;
            var language = messageWithContext.Language;

            // We only care about LSP responses that contain data
            if (message.Result == null) continue;

            // Try to parse as document symbols (hierarchical)
            try
            {
                var symbols = LspMessageParser.ParseDocumentSymbols(message.Result.Value);
                if (symbols != null && symbols.Length > 0)
                {
                    var fileUri = ExtractFileUri(message);
                    if (fileUri != null)
                    {
                        await ProcessDocumentSymbols(symbols, fileUri, language, ct);
                    }
                    continue;
                }
            }
            catch { /* Not document symbols */ }

            // Try to parse as symbol information (flat)
            try
            {
                var symbolInfo = LspMessageParser.ParseSymbolInformation(message.Result.Value);
                if (symbolInfo != null && symbolInfo.Length > 0)
                {
                    await ProcessSymbolInformation(symbolInfo, language, ct);
                    continue;
                }
            }
            catch { /* Not symbol information */ }

            // Try to parse as references (for building dependency edges)
            try
            {
                var references = LspMessageParser.ParseReferences(message.Result.Value);
                if (references != null && references.Length > 0)
                {
                    // We can still use reference data if the editor sends it
                    // but we don't request it ourselves
                    await ProcessReferences(references, language, ct);
                    continue;
                }
            }
            catch { /* Not references */ }
        }
    }

    private async Task ProcessDocumentSymbols(
        DocumentSymbol[] symbols,
        string fileUri,
        string language,
        CancellationToken ct)
    {
        // Cache symbols for this file
        _symbolCache[fileUri] = symbols.ToList();

        // Process hierarchy and create nodes
        foreach (var symbol in symbols)
        {
            await ProcessSymbolHierarchy(symbol, fileUri, language, parentNamespace: null, ct);
        }
    }

    private async Task ProcessSymbolHierarchy(
        DocumentSymbol symbol,
        string fileUri,
        string language,
        string? parentNamespace,
        CancellationToken ct)
    {
        // Build namespace chain
        var currentNamespace = parentNamespace != null
            ? $"{parentNamespace}.{symbol.Name}"
            : symbol.Name;

        // Create nodes for classes, methods, functions
        if (symbol.Kind is SymbolKind.Class or SymbolKind.Method or SymbolKind.Function
            or SymbolKind.Interface or SymbolKind.Module)
        {
            var nodeId = GenerateNodeIdFromSymbol(fileUri, symbol);

            var update = new NodeUpdate
            {
                NodeId = nodeId,
                Type = MapSymbolKindToType(symbol.Kind),
                Language = language,
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

        // Handle inheritance relationships from symbol detail
        if (symbol.Kind is SymbolKind.Class or SymbolKind.Interface && symbol.Detail != null)
        {
            await ExtractInheritanceRelationships(fileUri, symbol, ct);
        }

        // Process children recursively
        if (symbol.Children != null)
        {
            foreach (var child in symbol.Children)
            {
                await ProcessSymbolHierarchy(child, fileUri, language, currentNamespace, ct);
            }
        }
    }

    private async Task ProcessSymbolInformation(SymbolInformation[] symbols, string language, CancellationToken ct)
    {
        foreach (var symbol in symbols)
        {
            if (symbol.Kind is SymbolKind.Class or SymbolKind.Method or SymbolKind.Function)
            {
                var nodeId = GenerateNodeIdFromLocation(symbol.Location, symbol.Name);

                var update = new NodeUpdate
                {
                    NodeId = nodeId,
                    Type = MapSymbolKindToType(symbol.Kind),
                    Language = language,
                    Name = symbol.Name,
                    FilePath = UriToFilePath(symbol.Location.Uri),
                    LineStart = symbol.Location.Range.Start.Line,
                    LineEnd = symbol.Location.Range.End.Line,
                    Namespace = symbol.ContainerName
                };

                await _updateChannel.Writer.WriteAsync(update, ct);
            }
        }
    }

    private async Task ProcessReferences(Location[] references, string language, CancellationToken ct)
    {
        // If the editor/plugin sends us reference data, we can use it
        // to build dependency edges, but we don't actively request it

        // Group by file
        var referencesByFile = references.GroupBy(r => r.Uri);

        foreach (var fileGroup in referencesByFile)
        {
            foreach (var reference in fileGroup)
            {
                // Try to find nodes at these locations to build edges
                var nodeId = await _repository.FindNodeAtLocationAsync(
                    UriToFilePath(reference.Uri),
                    reference.Range.Start.Line);

                if (nodeId != null)
                {
                    // Store for potential edge creation
                    // (would need context about what's being referenced)
                    _logger.LogDebug($"Found reference at {nodeId}");
                }
            }
        }
    }

    private async Task ExtractInheritanceRelationships(
        string fileUri,
        DocumentSymbol symbol,
        CancellationToken ct)
    {
        if (symbol.Detail == null) return;

        var fromNodeId = GenerateNodeIdFromSymbol(fileUri, symbol);

        // Parse detail string for base types
        // Example: "class UserService : BaseService, IUserService"
        var parts = symbol.Detail.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return;

        var baseTypes = parts[1].Split(',', StringSplitOptions.TrimEntries);

        foreach (var baseType in baseTypes)
        {
            var cleanType = baseType.Trim();

            // Determine if it's interface or class
            var isInterface = cleanType.StartsWith('I') &&
                             cleanType.Length > 1 &&
                             char.IsUpper(cleanType[1]);

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

    private string GenerateNodeIdFromSymbol(string fileUri, DocumentSymbol symbol)
    {
        var filePath = UriToFilePath(fileUri);
        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, filePath);
        var full_id = $"{relativePath}:{symbol.Name}:{symbol.Range.Start.Line}";
        return $"node_{ComputeStableHash(full_id)}";
    }

    private string GenerateNodeIdFromLocation(Location location, string name)
    {
        var filePath = UriToFilePath(location.Uri);
        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, filePath);
        var full_id = $"{relativePath}:{name}:{location.Range.Start.Line}";
        return $"node_{ComputeStableHash(full_id)}";
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
                    //_logger.LogInformation("Building function");
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

    // private async Task ProcessNodeUpdates(CancellationToken ct)
    // {
    //     await foreach (var update in _updateChannel.Reader.ReadAllAsync(ct))
    //     {
    //         _logger.LogInformation("Upserting node {NodeId} for {FilePath}", update.NodeId, update.FilePath);
    //         // Upsert to SurrealDB - merges LSP + Lizard + Git data

    //         try
    //         {

    //             await _repository.UpsertNodeAsync(update);
    //         }
    //         catch (Exception ex)
    //         {
    //             _logger.LogError(ex, "Unable to upsert {NodeId}", update.NodeId);
    //             throw;
    //         }
    //     }
    // }



    private async Task ProcessNodeUpdates(CancellationToken ct)
    {
        var batch = new List<NodeUpdate>();
        var batchSize = 100;

        await foreach (var update in _updateChannel.Reader.ReadAllAsync(ct))
        {
            batch.Add(update);

            if (batch.Count >= batchSize)
            {
                await ProcessBatch(batch, ct);
                batch.Clear();

                // Brief delay to avoid overwhelming the connection
                await Task.Delay(100, ct);
            }
        }

        // Process remaining
        if (batch.Any())
        {
            await ProcessBatch(batch, ct);
        }
    }

    private async Task ProcessBatch(List<NodeUpdate> updates, CancellationToken ct)
    {

        try
        {
            await _repository.UpsertNodeListAsync(updates);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout upserting nodes, will retry");
            // Optionally re-queue or log for manual review
        }


    }
    private string GenerateNodeId(string filePath, string functionName, int lineStart)
    {
        var fullId = $"{filePath}:{functionName}:{lineStart}";
        return $"node_{ComputeStableHash(fullId)}";
    }

    private static string ComputeStableHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes[..16]).ToLowerInvariant();
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
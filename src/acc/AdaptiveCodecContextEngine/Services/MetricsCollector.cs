using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AdaptiveCodecContextEngine.Diagnostics;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Git;
using AdaptiveCodecContextEngine.Models.Lizard;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models.Surreal;
using Microsoft.Extensions.Logging;

public class MetricsCollector
{
    private readonly Channel<LspMessageWithContext> _lspChannel;
    private readonly Channel<GitEventWithContext> _gitChannel;
    private readonly Channel<NodeUpdateWithContext> _updateChannel;
    private readonly Channel<DependencyEdgeWithContext> _dependencyChannel;
    private readonly LizardAnalyzer _lizard;
    private readonly GitWatcher _gitWatcher;
    private readonly SurrealDbRepository _repository;
    private readonly ILogger<MetricsCollector> _logger;
    private readonly AdaptiveContextInstrumentation _instrumentation;

    // Cache symbols by file for reference
    private readonly ConcurrentDictionary<string, List<DocumentSymbol>> _symbolCache = new();

    public MetricsCollector(
        Channel<LspMessageWithContext> lspChannel,
        Channel<GitEventWithContext> gitChannel,
        Channel<NodeUpdateWithContext> updateChannel,
        Channel<DependencyEdgeWithContext> dependencyChannel,
        GitWatcher gitWatcher,
        SurrealDbRepository repository,
        LizardAnalyzer analyzer,
        ILogger<MetricsCollector> logger,
        AdaptiveContextInstrumentation instrumentation
    )
    {
        _lspChannel = lspChannel;
        _gitChannel = gitChannel;
        _updateChannel = updateChannel;
        _dependencyChannel = dependencyChannel;
        _lizard = analyzer;
        _gitWatcher = gitWatcher;
        _repository = repository;
        _logger = logger;
        _instrumentation = instrumentation;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var lspTask = ProcessLspMessages(ct);
        var gitTask = ProcessGitEvents(ct);
        var updateTask = ProcessNodeUpdates(ct);
        var dependencyTask = ProcessDependencies(ct);

        _ = Task.WhenAll(lspTask, gitTask, updateTask, dependencyTask);
    }

    private async Task ProcessLspMessages(CancellationToken ct)
    {
        await foreach (var messageWithContext in _lspChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ProcessLspMessage(messageWithContext, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing lsp message {Id}",
                    messageWithContext.Message.Id
                );
            }
        }
    }

    private async Task ProcessLspMessage(
        LspMessageWithContext messageWithContext,
        CancellationToken ct
    )
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ProcessLspMessage),
            ActivityKind.Consumer,
            parentContext: messageWithContext.ParentContext ?? new()
        );
        var message = messageWithContext.Message;
        var language = messageWithContext.Language;

        activity?.SetTag("lsp.stream.language", language);
        activity?.SetTag("lsp.stream.message.id", message.Id);
        activity?.SetTag("lsp.stream.message.method", message.Method);
        _logger.LogLspReceipt(message.Method, message.Result.HasValue, language);

        if (message.Result == null || !message.Result.HasValue)
        {
            activity?.AddEvent(
                new("Message content is empty", tags: new() { ["message.id"] = message.Id })
            );
            return;
        }

        try
        {
            var symbols = LspMessageParser.ParseDocumentSymbols(message.Result.Value);

            activity?.SetTag("lsp.stream.symbols.count", symbols?.Length ?? 0);

            if (symbols != null && symbols.Length > 0)
            {
                var fileUri = ExtractFileUri(message);
                activity?.SetTag("lsp.stream.file.uri", fileUri);

                if (fileUri != null)
                {
                    await ProcessDocumentSymbols(symbols, fileUri, language, ct);
                    _logger.LogInformation("Finished processing document symbols");
                }
                else
                {
                    _logger.LogWarning("Could not extract fileUri from message");
                }
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse as document symbols");
        }

        _logger.LogDebug("Message not recognized as document symbols");
    }

    private async Task ProcessDocumentSymbols(
        DocumentSymbol[] symbols,
        string fileUri,
        string language,
        CancellationToken ct
    )
    {
        // Cache symbols for this file
        _symbolCache[fileUri] = symbols.ToList();
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ProcessDocumentSymbols)
        );
        activity?.SetTag("lsp.stream.file.uri", fileUri);
        activity?.SetTag("lsp.stream.language", language);
        activity?.SetTag("lsp.stream.symbols.count", symbols.Length);

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
        CancellationToken ct
    )
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ProcessSymbolHierarchy)
        );
        activity?.SetTag("symbol.name", symbol.Name);
        activity?.SetTag("symbol.kind", symbol.Kind.ToString());
        activity?.SetTag("symbol.range.start", symbol.Range.Start.Line);
        activity?.SetTag("symbol.range.end", symbol.Range.End.Line);

        // Build namespace chain
        var currentNamespace =
            parentNamespace != null ? $"{parentNamespace}.{symbol.Name}" : symbol.Name;

        // Create nodes for classes, methods, functions
        if (
            symbol.Kind
            is SymbolKind.Class
                or SymbolKind.Method
                or SymbolKind.Function
                or SymbolKind.Interface
                or SymbolKind.Module
        )
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
                Namespace = parentNamespace ?? symbol.Name ?? "default", // This is a recursive call so the parent namespace will be null guaranteeing the use of symbol.Name as the namespace
                Signature = symbol.Detail,
                ReturnType = ExtractReturnType(symbol.Detail),
            };

            activity?.SetTag("node.id", nodeId);
            await _updateChannel.Writer.WriteAsync(
                new NodeUpdateWithContext(update, activity?.Context),
                ct
            );
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

    private async Task ProcessSymbolInformation(
        SymbolInformation[] symbols,
        string language,
        CancellationToken ct
    )
    {
        foreach (var symbol in symbols)
        {
            using var activity = _instrumentation.ActivitySource.StartActivity(
                nameof(ProcessSymbolInformation)
            );

            if (symbol.Kind is SymbolKind.Class or SymbolKind.Method or SymbolKind.Function)
            {
                var nodeId = GenerateNodeIdFromLocation(symbol.Location, symbol.Name);
                activity?.SetTag("node.id", nodeId);
                activity?.SetTag("symbol.kind", symbol.Kind);
                activity?.SetTag("symbol.name", symbol.Name);
                var update = new NodeUpdate
                {
                    NodeId = nodeId,
                    Type = MapSymbolKindToType(symbol.Kind),
                    Language = language,
                    Name = symbol.Name,
                    FilePath = UriToFilePath(symbol.Location.Uri),
                    LineStart = symbol.Location.Range.Start.Line,
                    LineEnd = symbol.Location.Range.End.Line,
                    Namespace = symbol.ContainerName,
                };

                await _updateChannel.Writer.WriteAsync(new(update, activity?.Context), ct);
                activity?.AddEvent(
                    new("Updated Node From Symbol", tags: new() { ["node.id"] = nodeId })
                );
            }
        }
    }

    private async Task ProcessReferences(
        Location[] references,
        string language,
        CancellationToken ct
    )
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
                    reference.Range.Start.Line
                );

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
        CancellationToken ct
    )
    {
        if (symbol.Detail == null)
            return;

        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ExtractInheritanceRelationships)
        );
        activity?.SetTag("inheritance.source", symbol.Name);

        var fromNodeId = GenerateNodeIdFromSymbol(fileUri, symbol);
        activity?.SetTag("from_node.id", fromNodeId);
        // Parse detail string for base types
        // Example: "class UserService : BaseService, IUserService"
        var parts = symbol.Detail.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return;

        var baseTypes = parts[1].Split(',', StringSplitOptions.TrimEntries);
        activity?.SetTag("inheritance.base.count", baseTypes.Length);

        foreach (var baseType in baseTypes)
        {
            var cleanType = baseType.Trim();

            // Determine if it's interface or class
            var isInterface =
                cleanType.StartsWith('I') && cleanType.Length > 1 && char.IsUpper(cleanType[1]);

            var edge = new DependencyEdge
            {
                FromNodeId = fromNodeId,
                ToSymbolName = cleanType,
                RelationshipType = isInterface ? "implements" : "inherits",
                SourceFileUri = fileUri,
            };

            activity?.AddEvent(
                new(
                    "Inheritance relationship extracted",
                    tags: new()
                    {
                        ["to.type"] = cleanType,
                        ["relationship"] = edge.RelationshipType,
                    }
                )
            );

            await _dependencyChannel.Writer.WriteAsync(
                new DependencyEdgeWithContext(edge, activity?.Context),
                ct
            );
        }
    }

    private CallHierarchyItem[]? ParseOutgoingCalls(JsonElement result)
    {
        // Parse LSP call hierarchy response
        // Format: CallHierarchyOutgoingCall[]
        try
        {
            return JsonSerializer.Deserialize<CallHierarchyItem[]>(
                result.GetRawText(),
                ACCJsonContext.Default.CallHierarchyItemArray
            );
        }
        catch
        {
            return null;
        }
    }

    private async Task ProcessDependencies(CancellationToken ct)
    {
        await foreach (var edgeWithContext in _dependencyChannel.Reader.ReadAllAsync(ct))
        {
            using var activity = _instrumentation.ActivitySource.StartActivity(
                nameof(ProcessDependencies),
                ActivityKind.Consumer,
                parentContext: edgeWithContext.Context ?? new ActivityContext()
            );
            var edge = edgeWithContext.Edge;

            activity?.SetTag("dependency.from", edge.FromNodeId);
            activity?.SetTag("dependency.type", edge.RelationshipType);
            // Resolve the "to" node
            string? toNodeId = null;

            if (edge.ToFileUri != null && edge.ToLine.HasValue)
            {
                // Find node at this location
                toNodeId = await _repository.FindNodeAtLocationAsync(
                    UriToFilePath(edge.ToFileUri),
                    edge.ToLine.Value
                );
            }
            else if (edge.ToSymbolName != null)
            {
                // Search for node by name (might need disambiguation)
                toNodeId = await _repository.FindNodeByNameAsync(edge.ToSymbolName);
            }

            if (toNodeId != null)
            {
                await _repository.UpsertDependencyAsync(
                    edge.FromNodeId,
                    toNodeId,
                    edge.RelationshipType
                );
                activity?.AddEvent(
                    new(
                        "Dependency upserted",
                        tags: new()
                        {
                            ["to.node"] = toNodeId,
                            ["from.node"] = edge.FromNodeId,
                            ["relationship.type"] = edge.RelationshipType,
                        }
                    )
                );
            }
        }
    }

    private string GenerateNodeId(string filePath, string functionName, int lineStart)
    {
        var relativePath = Path.GetRelativePath(_gitWatcher.RepoPath, filePath);
        var fullId = $"{relativePath}:{functionName}:{lineStart}".ToLowerInvariant();
        return $"node_{ComputeStableHash(fullId)}";
    }

    private string GenerateNodeIdFromSymbol(string fileUri, DocumentSymbol symbol)
    {
        var filePath = UriToFilePath(fileUri);
        var relativePath = Path.GetRelativePath(_gitWatcher.RepoPath, filePath);
        var full_id = $"{relativePath}:{symbol.Name}:{symbol.Range.Start.Line}".ToLowerInvariant();
        return $"node_{ComputeStableHash(full_id)}";
    }

    private string GenerateNodeIdFromLocation(Location location, string name)
    {
        var filePath = UriToFilePath(location.Uri);
        var relativePath = Path.GetRelativePath(_gitWatcher.RepoPath, filePath);
        var full_id = $"{relativePath}:{name}:{location.Range.Start.Line}".ToLowerInvariant();
        return $"node_{ComputeStableHash(full_id)}";
    }

    private string MapSymbolKindToType(SymbolKind kind) =>
        kind switch
        {
            SymbolKind.Class => "class",
            SymbolKind.Method => "method",
            SymbolKind.Function => "function",
            SymbolKind.Interface => "interface",
            SymbolKind.Module => "module",
            SymbolKind.Namespace => "namespace",
            _ => "unknown",
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
            _ => "unknown",
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
        await foreach (var gitEventWithContext in _gitChannel.Reader.ReadAllAsync(ct))
        {
            using var activity = _instrumentation.ActivitySource.StartActivity(
                nameof(ProcessGitEvents),
                ActivityKind.Consumer,
                gitEventWithContext.Context ?? new()
            );

            var gitEvent = gitEventWithContext.Event;

            activity?.SetTag("git.file", gitEvent.FilePath);
            activity?.SetTag("git.event", gitEvent.Type.ToString());

            // Skip deleted files
            if (gitEvent.Type == GitEventType.Deleted)
                continue;

            _logger.LogDebug(
                "Processing git event {EventType} for {FilePath}",
                gitEvent.Type,
                gitEvent.FilePath
            );
            // Run lizard on the file
            var lizardResult = await _lizard.AnalyzeFileAsync(gitEvent.FilePath, ct);

            // Extract git history
            var history = _gitWatcher.ExtractHistory(gitEvent.FilePath);

            activity?.SetTag("git.history.commits", history?.TotalCommits);
            activity?.SetTag("git.history.contributors", history?.Contributors);
            activity?.SetTag("git.history.frequency", history?.RecentFrequency);

            if (lizardResult?.FunctionList != null)
            {
                await ProcessLizarFunctions(lizardResult, gitEventWithContext, history, ct);
            }
        }
    }

    private async Task ProcessLizarFunctions(
        LizardResult lizardResult,
        GitEventWithContext gitEventWithContext,
        GitHistory? history,
        CancellationToken ct
    )
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ProcessLizarFunctions),
            ActivityKind.Consumer
        );
        var gitEvent = gitEventWithContext.Event;
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
                GitHistory = history,
            };

            await _updateChannel.Writer.WriteAsync(new(update, activity?.Context), ct);
            activity?.AddEvent(
                new(
                    "Lizard function update queued",
                    tags: new() { ["function.name"] = function.Name }
                )
            );
            activity?.SetTag("code.nloc", update.LinesOfCode);
            activity?.SetTag("code.cyclomatic_complexity", update.CyclomaticComplexity);
            activity?.SetTag("code.parameter_count", update.Parameters);
            activity?.SetTag("code.signature", update.Signature);

            _instrumentation.ComplexityHistogram.Record(
                update.CyclomaticComplexity ?? 0,
                new KeyValuePair<string, object?>("code.language", update.Language)
            );
        }
    }

    private async Task ProcessNodeUpdates(CancellationToken ct)
    {
        var batch = new List<NodeUpdateWithContext>();
        var batchSize = 100;
        var maxWaitMs = 50;
        var lastBatchProcessedAt = Stopwatch.GetTimestamp();
        await foreach (var updateWithContext in _updateChannel.Reader.ReadAllAsync(ct))
        {
            batch.Add(updateWithContext);

            if (
                batch.Count >= batchSize
                || (
                    batch.Count < batchSize
                    && Stopwatch.GetElapsedTime(lastBatchProcessedAt).Milliseconds > maxWaitMs
                )
            )
            {
                await ProcessBatch(batch, ct);
                batch.Clear();

                lastBatchProcessedAt = Stopwatch.GetTimestamp();
            }
        }

        // Process remaining
        if (batch.Any())
        {
            await ProcessBatch(batch, ct);
        }
    }

    private async Task ProcessBatch(
        List<NodeUpdateWithContext> updatesWithContext,
        CancellationToken ct
    )
    {
        var batchContext = new ActivityContext();

        using var activity = _instrumentation.ActivitySource.StartActivity(
            nameof(ProcessBatch),
            ActivityKind.Internal,
            parentContext: batchContext,
            links: updatesWithContext.Select(u => new ActivityLink(u.Context ?? batchContext))
        );

        activity?.SetTag("batch.size", updatesWithContext.Count);

        try
        {
            await _repository.UpsertNodeListAsync(updatesWithContext.Select(u => u.Update));
            activity?.AddEvent(new("Batch upsert successful"));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout upserting nodes, will retry");
            activity?.AddEvent(
                new("Batch upsert timeout", tags: new() { ["exception"] = ex.Message })
            );
            // Optionally re-queue or log for manual review
        }
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
            _ => "unknown",
        };
    }
}

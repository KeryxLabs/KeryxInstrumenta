using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Git;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models.Surreal;
public class MetricsCollector
{
    private readonly Channel<LspMessage> _lspChannel;
    private readonly Channel<GitEvent> _gitChannel;
    private readonly Channel<NodeUpdate> _updateChannel;
    private readonly LizardAnalyzer _lizard;
    private readonly GitWatcher _gitWatcher;
    private readonly SurrealDbRepository _repository;
    
    // Track which files we've requested symbols for
    private readonly Dictionary<string, List<DocumentSymbol>> _symbolCache = new();
    
    public MetricsCollector(GitWatcher gitWatcher, SurrealDbRepository repository)
    {
        _lspChannel = Channel.CreateUnbounded<LspMessage>();
        _gitChannel = Channel.CreateUnbounded<GitEvent>();
        _updateChannel = Channel.CreateUnbounded<NodeUpdate>();
        _lizard = new LizardAnalyzer();
        _gitWatcher = gitWatcher;
        _repository = repository;
    }
    
    public async Task StartAsync(CancellationToken ct)
    {
        // Spawn all processors
        var lspTask = ProcessLspMessages(ct);
        var gitTask = ProcessGitEvents(ct);
        var updateTask = ProcessNodeUpdates(ct);
        
        await Task.WhenAll(lspTask, gitTask, updateTask);
    }
    
    // Public method to feed LSP messages from stdio
    public async Task EnqueueLspMessageAsync(LspMessage message, CancellationToken ct = default)
    {
        await _lspChannel.Writer.WriteAsync(message, ct);
    }
    
    private async Task ProcessLspMessages(CancellationToken ct)
    {
        await foreach (var message in _lspChannel.Reader.ReadAllAsync(ct))
        {
            // Handle different LSP message types
            if (message.Method == "textDocument/publishDiagnostics")
            {
                // Could extract test coverage info from diagnostics if available
                continue;
            }
            
            if (message.Result == null) continue;
            
            // Try to parse as document symbols (hierarchical)
            try
            {
                var symbols = LspMessageParser.ParseDocumentSymbols(message.Result.Value);
                if (symbols != null && symbols.Length > 0)
                {
                    await ProcessDocumentSymbols(symbols, message, ct);
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
                    await ProcessSymbolInformation(symbolInfo, ct);
                    continue;
                }
            }
            catch { /* Not symbol information */ }
            
            // Try to parse as references
            try
            {
                var references = LspMessageParser.ParseReferences(message.Result.Value);
                if (references != null && references.Length > 0)
                {
                    await ProcessReferences(references, ct);
                    continue;
                }
            }
            catch { /* Not references */ }
        }
    }
    
    private async Task ProcessDocumentSymbols(DocumentSymbol[] symbols, LspMessage message, CancellationToken ct)
    {
        // Extract file URI from the request params if available
        string? fileUri = ExtractFileUri(message);
        if (fileUri == null) return;
        
        // Cache symbols for this file
        _symbolCache[fileUri] = symbols.ToList();
        
        // Recursively process all symbols
        foreach (var symbol in symbols)
        {
            await ProcessSymbolHierarchy(symbol, fileUri, parentNamespace: null, ct);
        }
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
    
    private async Task ProcessSymbolInformation(SymbolInformation[] symbols, CancellationToken ct)
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
                    Language = DetectLanguageFromUri(symbol.Location.Uri),
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
    
    private async Task ProcessReferences(Location[] references, CancellationToken ct)
    {
        // References tell us about dependencies (calls, imports, etc.)
        // Group by file to batch process
        var referencesByFile = references.GroupBy(r => r.Uri);
        
        foreach (var fileGroup in referencesByFile)
        {
            foreach (var reference in fileGroup)
            {
                // Find the symbol at this reference location
                var fromNodeId = await FindNodeAtLocation(reference, ct);
                
                // This is tricky - we need context about WHAT is being referenced
                // For now, we'll need to correlate with the original request
                // that triggered the reference search
                
                // TODO: Track reference request context to build accurate dependency edges
            }
        }
    }
    
    private async Task<string?> FindNodeAtLocation(Location location, CancellationToken ct)
    {
        // Query SurrealDB for node at this file/line
        var filePath = UriToFilePath(location.Uri);
        var line = location.Range.Start.Line;
        
        // This would need a query like:
        // SELECT * FROM node WHERE file_path = $path AND line_start <= $line AND line_end >= $line
        
        // For now, generate expected ID
        return GenerateNodeIdFromLocation(location, "unknown");
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
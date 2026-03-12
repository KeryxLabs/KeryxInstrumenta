using AdaptiveCodecContextEngine.Models.Lsp;

public class LspMessageParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = LspJsonContext.Default
    };
    
    public static LspMessage? Parse(string json)
    {
        return JsonSerializer.Deserialize<LspMessage>(json, Options);
    }
    
    public static DocumentSymbol[]? ParseDocumentSymbols(JsonElement result)
    {
        return JsonSerializer.Deserialize<DocumentSymbol[]>(result, Options);
    }
    
    public static SymbolInformation[]? ParseSymbolInformation(JsonElement result)
    {
        return JsonSerializer.Deserialize<SymbolInformation[]>(result, Options);
    }
    
    public static Location[]? ParseReferences(JsonElement result)
    {
        return JsonSerializer.Deserialize<Location[]>(result, Options);
    }
    
    public static ReferenceParams CreateReferenceParams(string uri, int line, int character, bool includeDeclaration = true)
    {
        return new ReferenceParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = line, Character = character },
            Context = new ReferenceContext { IncludeDeclaration = includeDeclaration }
        };
    }
}
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Lsp;

public class LspMessageParser
{
    private static readonly ACCJsonContext Ctx = ACCJsonContext.Default;

    public static LspMessage? Parse(string json)
    {
        return JsonSerializer.Deserialize<LspMessage>(json, Ctx.LspMessage);
    }

    public static DocumentSymbol[]? ParseDocumentSymbols(JsonElement result)
    {
        return JsonSerializer.Deserialize<DocumentSymbol[]>(result, Ctx.DocumentSymbolArray);
    }

    public static SymbolInformation[]? ParseSymbolInformation(JsonElement result)
    {
        return JsonSerializer.Deserialize<SymbolInformation[]>(result, Ctx.SymbolInformationArray);
    }

    public static Location[]? ParseReferences(JsonElement result)
    {
        return JsonSerializer.Deserialize<Location[]>(result, Ctx.LocationArray);
    }

    public static ReferenceParams CreateReferenceParams(
        string uri,
        int line,
        int character,
        bool includeDeclaration = true
    )
    {
        return new ReferenceParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position { Line = line, Character = character },
            Context = new ReferenceContext { IncludeDeclaration = includeDeclaration },
        };
    }
}

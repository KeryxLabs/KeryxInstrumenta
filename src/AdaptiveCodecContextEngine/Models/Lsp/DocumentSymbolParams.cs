namespace AdaptiveCodecContextEngine.Models.Lsp;


public record DocumentSymbolParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = null!;
}
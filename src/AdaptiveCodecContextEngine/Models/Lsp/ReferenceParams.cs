namespace AdaptiveCodecContextEngine.Models.Lsp;

public record ReferenceParams
{
    [JsonPropertyName("textDocument")]
    public TextDocumentIdentifier TextDocument { get; init; } = null!;
    
    [JsonPropertyName("position")]
    public Position Position { get; init; } = null!;
    
    [JsonPropertyName("context")]
    public ReferenceContext Context { get; init; } = null!;
}

namespace AdaptiveCodecContextEngine.Models.Lsp;

public record TextDocumentIdentifier
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = null!;
}
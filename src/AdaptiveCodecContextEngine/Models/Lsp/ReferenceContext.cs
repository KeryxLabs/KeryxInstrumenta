namespace AdaptiveCodecContextEngine.Models.Lsp;

public record ReferenceContext
{
    [JsonPropertyName("includeDeclaration")]
    public bool IncludeDeclaration { get; init; }
}
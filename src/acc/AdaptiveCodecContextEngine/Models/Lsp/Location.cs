namespace AdaptiveCodecContextEngine.Models.Lsp;

public record Location
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = null!;

    [JsonPropertyName("range")]
    public Range Range { get; init; } = null!;
}

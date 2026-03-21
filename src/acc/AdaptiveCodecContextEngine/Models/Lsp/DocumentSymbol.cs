namespace AdaptiveCodecContextEngine.Models.Lsp;

public record DocumentSymbol
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    [JsonPropertyName("kind")]
    public SymbolKind Kind { get; init; }

    [JsonPropertyName("range")]
    public Range Range { get; init; } = null!;

    [JsonPropertyName("selectionRange")]
    public Range SelectionRange { get; init; } = null!;

    [JsonPropertyName("children")]
    public DocumentSymbol[]? Children { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}

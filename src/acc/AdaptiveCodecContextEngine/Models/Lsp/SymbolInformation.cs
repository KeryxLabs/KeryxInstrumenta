namespace AdaptiveCodecContextEngine.Models.Lsp;

public record SymbolInformation
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    [JsonPropertyName("kind")]
    public SymbolKind Kind { get; init; }

    [JsonPropertyName("location")]
    public Location Location { get; init; } = null!;

    [JsonPropertyName("containerName")]
    public string? ContainerName { get; init; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; init; }
}

namespace AdaptiveCodecContextEngine.Models.Lsp;


public record CallHierarchyItem
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;
    
    [JsonPropertyName("kind")]
    public SymbolKind Kind { get; init; }
    
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = null!;
    
    [JsonPropertyName("range")]
    public Range Range { get; init; } = null!;
    
    [JsonPropertyName("selectionRange")]
    public Range SelectionRange { get; init; } = null!;
}
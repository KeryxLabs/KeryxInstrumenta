namespace AdaptiveCodecContextEngine.Models.Lsp;


public record Position
{
    [JsonPropertyName("line")]
    public int Line { get; init; }
    
    [JsonPropertyName("character")]
    public int Character { get; init; }
}

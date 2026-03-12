namespace AdaptiveCodecContextEngine.Models.Lsp;

public record Range
{
    [JsonPropertyName("start")]
    public Position Start { get; init; } = null!;
    
    [JsonPropertyName("end")]
    public Position End { get; init; } = null!;
}
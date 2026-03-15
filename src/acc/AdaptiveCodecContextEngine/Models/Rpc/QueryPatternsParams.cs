namespace AdaptiveCodecContextEngine.Models.Rpc;

public record QueryPatternsParams
{
    [JsonPropertyName("profile")]
    public AvecScores Profile { get; init; } = null!;
    
    [JsonPropertyName("threshold")]
    public double Threshold { get; init; } = 0.8;
}

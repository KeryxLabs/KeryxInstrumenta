
namespace AdaptiveCodecContextEngine.Models.Rpc;

public record GetUnstableParams
{
    [JsonPropertyName("maxStability")]
    public double MaxStability { get; init; } = 0.4;
    
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 20;
}

namespace AdaptiveCodecContextEngine.Models.Rpc;

public record GetHighFrictionParams
{
    [JsonPropertyName("minFriction")]
    public double MinFriction { get; init; } = 0.7;
    
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 20;
}

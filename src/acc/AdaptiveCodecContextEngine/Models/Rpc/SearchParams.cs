
namespace AdaptiveCodecContextEngine.Models.Rpc;

public record SearchParams
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;
    
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 10;
}


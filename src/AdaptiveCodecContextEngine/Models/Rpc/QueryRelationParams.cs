namespace AdaptiveCodecContextEngine.Models.Rpc;

public record QueryRelationsParams
{
    [JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = null!;
    
    [JsonPropertyName("includeScores")]
    public bool IncludeScores { get; init; }
}


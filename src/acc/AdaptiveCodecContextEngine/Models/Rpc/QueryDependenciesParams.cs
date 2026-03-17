namespace AdaptiveCodecContextEngine.Models.Rpc;

public record QueryDependenciesParams
{
    [JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = null!;
    
    [JsonPropertyName("direction")]
    public string Direction { get; init; } = "Both";
    
    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; init; } = -1;
    
    [JsonPropertyName("includeScores")]
    public bool IncludeScores { get; init; }
}


public record ProjectStatsDto
{
    [JsonPropertyName("total_nodes")]
    public int TotalNodes { get; init; }

    [JsonPropertyName("avg_stability")]
    public double AverageStability { get; init; }

    [JsonPropertyName("avg_logic")]
    public double AverageLogic { get; init; }

    [JsonPropertyName("avg_friction")]
    public double AverageFriction { get; init; }

    [JsonPropertyName("avg_autonomy")]
    public double AverageAutonomy { get; init; }
}

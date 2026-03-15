namespace AdaptiveCodecContextEngine.Models.Git;

public record GitHistory
{
    [JsonPropertyName("created")]
    public DateTime Created { get; init; }
    [JsonPropertyName("last_modified")]
    public DateTime LastModified { get; init; }
    [JsonPropertyName("total_commits")]
    public int TotalCommits { get; init; }
    [JsonPropertyName("contributors")]
    public int Contributors { get; init; }
    [JsonPropertyName("avg_days_between_changes")]
    public double AvgDaysBetweenChanges { get; init; }
    [JsonPropertyName("recent_frequency")]
    public string RecentFrequency { get; init; } = "low";
}
namespace AdaptiveCodecContextEngine.Models.Git;

public record GitHistory
{
    public DateTime Created { get; init; }
    public DateTime LastModified { get; init; }
    public int TotalCommits { get; init; }
    public int Contributors { get; init; }
    public double AvgDaysBetweenChanges { get; init; }
    public string RecentFrequency { get; init; } = "low";
}
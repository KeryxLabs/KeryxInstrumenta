namespace SttpMcp.Domain.Models;

public sealed record ConfidenceBandSummary
{
    public int Low { get; init; }
    public int Medium { get; init; }
    public int High { get; init; }
}
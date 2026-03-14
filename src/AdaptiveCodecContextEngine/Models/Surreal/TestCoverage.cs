namespace AdaptiveCodecContextEngine.Models.Surreal;
public record TestCoverage
{
    [JsonPropertyName("covered")]
    public bool Covered { get; init; }
    [JsonPropertyName("line_coverage")]
    public double LineCoverage { get; init; }
    [JsonPropertyName("branch_coverage")]
    public double BranchCoverage { get; init; }
    [JsonPropertyName("test_count")]
    public int TestCount { get; init; }
}
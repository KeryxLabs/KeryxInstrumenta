namespace AdaptiveCodecContextEngine.Models.Surreal;
public record TestCoverage
{
    public bool Covered { get; init; }
    public double LineCoverage { get; init; }
    public double BranchCoverage { get; init; }
    public int TestCount { get; init; }
}
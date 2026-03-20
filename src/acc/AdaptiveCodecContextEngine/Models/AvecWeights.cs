namespace AdaptiveCodecContextEngine.Models;

public record AvecWeights
{
    public StabilityWeights Stability { get; init; } = new();
    public LogicWeights Logic { get; init; } = new();
    public FrictionWeights Friction { get; init; } = new();
    public AutonomyWeights Autonomy { get; set; } = new();

    public record StabilityWeights
    {
        public double ChurnWeight { get; init; } = 0.4;
        public double ContributorWeight { get; init; } = 0.3;
        public double TestWeight { get; init; } = 0.3;
        public double ChurnNormalize { get; init; } = 10;
        public double TestLineCoverageNormalize { get; set; } = 100.0;
        public double TestLineCoverageWeight { get; set; } = 0.5;
        public double TestBranchCoverageNormalize { get; set; } = 100.0;
        public double TestBranchCoverageWeight { get; set; } = 0.5;
        public double TestBaseBias { get; set; } = 0.5;

        public int ContributorCap { get; init; } = 5;
    }

    public record LogicWeights
    {
        public double ComplexityWeight { get; init; } = 0.7;
        public double ParameterWeight { get; init; } = 0.3;
        public double LocDivisor { get; init; } = 10;
        public int ParameterCap { get; init; } = 5;
    }

    public record FrictionWeights
    {
        public double CentralityWeight { get; init; } = 0.4;
        public double DependencyWeight { get; init; } = 0.6;
        public double ChurnWeight { get; init; } = 0.7;
        public double CollaborationNormalize { get; set; } = 0.3;
        public double StructuralFrictionWeight { get; set; } = 0.4;
        public double ProcessFrictionWeight { get; set; } = 0.3;
        public double CognitiveFrictionWeight { get; set; } = 0.3;
        public double CyclomaticComplexityWeight { get; set; } = 20.0;
        public double GitContributorsNormalize { get; set; } = 10.0;
        public double GitTotalCommitsNormalize { get; set; } = 50.0;

        public int IncomingCap { get; init; } = 10;
    }

    public record AutonomyWeights
    {
        public int FileNumberBlastRadius { get; set; } = 30;
        public double DependencyRatio { get; set; } = 0.8;
        public double AbsoluteCount { get; set; } = 0.2;
    }
}

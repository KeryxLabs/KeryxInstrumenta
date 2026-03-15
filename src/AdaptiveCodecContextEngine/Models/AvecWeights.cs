namespace AdaptiveCodecContextEngine.Models;
public record AvecWeights
{
    public StabilityWeights Stability { get; init; } = new();
    public LogicWeights Logic { get; init; } = new();
    public FrictionWeights Friction { get; init; } = new();
    
    public record StabilityWeights
    {
        public double ChurnWeight { get; init; } = 0.4;
        public double ContributorWeight { get; init; } = 0.3;
        public double TestWeight { get; init; } = 0.3;
        public double ChurnNormalize { get; init; } = 10;
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
        public double CentralityWeight { get; init; } = 0.6;
        public double DependencyWeight { get; init; } = 0.4;
        public int IncomingCap { get; init; } = 10;
    }
}
namespace AdaptiveCodecContextEngine.Models;

public record AvecScores
{
    public double Stability { get; init; }
    public double Logic { get; init; }
    public double Friction { get; init; }
    public double Autonomy { get; init; }
}


namespace SttpMcp.Domain.Models;

public record AvecState
{
    public required float Stability { get; init; }
    public required float Friction { get; init; }
    public required float Logic { get; init; }
    public required float Autonomy { get; init; }
    public float Psi => Stability + Friction + Logic + Autonomy;

    public float DriftFrom(AvecState previous) => Psi - previous.Psi;

    public DriftClassification ClassifyDrift(AvecState previous)
    {
        var delta = Math.Abs(DriftFrom(previous));
        return delta > 0.3f
            ? DriftClassification.Uncontrolled
            : DriftClassification.Intentional;
    }
    public static AvecState Zero => new()
    {
        Stability = 0f,
        Friction = 0f,
        Logic = 0f,
        Autonomy = 0f
    };
}



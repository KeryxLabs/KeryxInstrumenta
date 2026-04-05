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

    public static AvecState Focused => new()
    {
        Stability = 0.95f,
        Friction = 0.10f,
        Logic = 0.95f,
        Autonomy = 0.90f
    };

    public static AvecState Creative => new()
    {
        Stability = 0.80f,
        Friction = 0.15f,
        Logic = 0.70f,
        Autonomy = 0.95f
    };

    public static AvecState Analytical => new()
    {
        Stability = 0.90f,
        Friction = 0.20f,
        Logic = 0.98f,
        Autonomy = 0.85f
    };

    public static AvecState Exploratory => new()
    {
        Stability = 0.75f,
        Friction = 0.30f,
        Logic = 0.65f,
        Autonomy = 0.90f
    };

    public static AvecState Collaborative => new()
    {
        Stability = 0.85f,
        Friction = 0.10f,
        Logic = 0.80f,
        Autonomy = 0.70f
    };

    public static AvecState Defensive => new()
    {
        Stability = 0.90f,
        Friction = 0.40f,
        Logic = 0.90f,
        Autonomy = 0.60f
    };

    public static AvecState Passive => new()
    {
        Stability = 0.98f,
        Friction = 0.05f,
        Logic = 0.60f,
        Autonomy = 0.40f
    };
}
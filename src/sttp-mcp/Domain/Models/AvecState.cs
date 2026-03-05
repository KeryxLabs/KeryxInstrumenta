

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

    // Focused: Like being deeply absorbed in a task, calm and clear-minded. High stability and logic means few distractions, while low friction shows little resistance or anxiety. High autonomy reflects self-driven concentration.
    public static AvecState Focused => new()
    {
        Stability = 0.95f,   // Calm, steady attention
        Friction = 0.10f,    // Minimal inner resistance
        Logic = 0.95f,       // Clear, rational thinking
        Autonomy = 0.90f     // Self-motivated focus
    };

    // Creative: Like a brainstorming session, open to new ideas. Moderate stability allows for flexibility, low friction means playful curiosity, logic is less dominant, and high autonomy supports free expression.
    public static AvecState Creative => new()
    {
        Stability = 0.80f,   // Flexible, open-minded
        Friction = 0.15f,    // Playful, little self-doubt
        Logic = 0.70f,       // Imaginative, less rigid
        Autonomy = 0.95f     // Freely expressive
    };

    // Analytical: Like solving a puzzle, methodical and precise. High stability and logic reflect careful reasoning, moderate friction shows some critical self-reflection, and autonomy is strong but not dominant.
    public static AvecState Analytical => new()
    {
        Stability = 0.90f,   // Steady, methodical
        Friction = 0.20f,    // Some critical self-checks
        Logic = 0.98f,       // Highly rational, precise
        Autonomy = 0.85f     // Independent but open to input
    };

    // Exploratory: Like wandering through new territory, curious and adaptive. Lower stability means openness to change, higher friction reflects some uncertainty, logic is moderate, and autonomy is high for self-guided exploration.
    public static AvecState Exploratory => new()
    {
        Stability = 0.75f,   // Open to change
        Friction = 0.30f,    // Some uncertainty or hesitation
        Logic = 0.65f,       // Flexible thinking
        Autonomy = 0.90f     // Self-guided curiosity
    };

    // Collaborative: Like working in a team, open and supportive. High stability means trust and comfort, low friction shows ease in cooperation, logic is strong for shared problem-solving, and moderate autonomy reflects willingness to compromise.
    public static AvecState Collaborative => new()
    {
        Stability = 0.85f,   // Trusting, comfortable
        Friction = 0.10f,    // Easygoing, cooperative
        Logic = 0.80f,       // Thoughtful, solution-oriented
        Autonomy = 0.70f     // Willing to compromise
    };

    // Defensive: Like protecting boundaries, cautious and guarded. High stability means resilience, high friction reflects resistance or stress, logic is strong for self-justification, and lower autonomy shows less openness to outside influence.
    public static AvecState Defensive => new()
    {
        Stability = 0.90f,   // Resilient, steady
        Friction = 0.40f,    // Guarded, resistant
        Logic = 0.90f,       // Self-justifying, rationalizing
        Autonomy = 0.60f     // Less open to influence
    };

    // Passive: Like going with the flow, calm and detached. Very high stability means little disturbance, very low friction shows little resistance, logic is subdued, and low autonomy reflects a tendency to follow rather than lead.
    public static AvecState Passive => new()
    {
        Stability = 0.98f,   // Calm, undisturbed
        Friction = 0.05f,    // Little resistance
        Logic = 0.60f,       // Subdued, less analytical
        Autonomy = 0.40f     // Follower, not a leader
    };
}




namespace AdaptiveCodecContextEngine.Models.Surreal;
public record NodeQueryResult
{
    public string NodeId { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Type { get; init; } = null!;
    public AvecScores? Avec { get; init; }
    public AvecScores? AvecLearned { get; init; }
    public AvecScores? AvecDelta { get; init; }
}

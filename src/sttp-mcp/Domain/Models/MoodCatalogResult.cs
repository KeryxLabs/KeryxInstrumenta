namespace SttpMcp.Domain.Models;

public record MoodCatalogResult
{
    public required IReadOnlyList<MoodPreset> Presets { get; init; }
    public required string ApplyGuide { get; init; }
    public MoodSwapPreview? SwapPreview { get; init; }
}

public record MoodPreset
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required AvecState Avec { get; init; }
}

public record MoodSwapPreview
{
    public required string TargetMood { get; init; }
    public required float Blend { get; init; }
    public required AvecState Current { get; init; }
    public required AvecState Target { get; init; }
    public required AvecState Blended { get; init; }
}
namespace AdaptiveCodecContextEngine.Models.Git;
public record GitEvent
{
    public GitEventType Type { get; init; }
    public string FilePath { get; init; } = null!;
    public string? OldPath { get; init; }
    public DateTime Timestamp { get; init; }
}

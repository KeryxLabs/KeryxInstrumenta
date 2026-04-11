namespace SttpMcp.Domain.Models;

public sealed record SyncPullResult
{
    public int Fetched { get; init; }
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Duplicate { get; init; }
    public int Skipped { get; init; }
    public int Filtered { get; init; }
    public int Batches { get; init; }
    public bool HasMore { get; init; }
    public SyncCursor? LastCursor { get; init; }
    public SyncCheckpoint? Checkpoint { get; init; }
}
namespace SttpMcp.Domain.Models;

public sealed record ChangeQueryResult
{
    public IReadOnlyList<SttpNode> Nodes { get; init; } = [];
    public SyncCursor? NextCursor { get; init; }
    public bool HasMore { get; init; }
}
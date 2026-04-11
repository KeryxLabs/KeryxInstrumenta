namespace SttpMcp.Domain.Models;

public sealed record SyncCursor
{
    public required DateTime UpdatedAt { get; init; }
    public required string SyncKey { get; init; }
}
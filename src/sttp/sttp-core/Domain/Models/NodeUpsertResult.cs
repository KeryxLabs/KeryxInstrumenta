namespace SttpMcp.Domain.Models;

public sealed record NodeUpsertResult
{
    public required string NodeId { get; init; }
    public required string SyncKey { get; init; }
    public required NodeUpsertStatus Status { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
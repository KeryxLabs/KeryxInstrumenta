namespace SttpMcp.Domain.Models;

public sealed record SyncPullRequest
{
    public required string SessionId { get; init; }
    public required string ConnectorId { get; init; }
    public int PageSize { get; init; } = 100;
    public int? MaxBatches { get; init; }
}
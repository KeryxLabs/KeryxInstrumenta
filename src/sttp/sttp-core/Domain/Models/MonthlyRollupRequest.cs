namespace SttpMcp.Domain.Models;

public sealed record MonthlyRollupRequest
{
    public required string SessionId { get; init; }
    public required DateTime StartUtc { get; init; }
    public required DateTime EndUtc { get; init; }
    public string? SourceSessionId { get; init; }
    public string? ParentNodeId { get; init; }
    public int Limit { get; init; } = 5000;
    public bool Persist { get; init; } = true;
}
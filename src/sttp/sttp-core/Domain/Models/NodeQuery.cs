namespace SttpMcp.Domain.Models;

public sealed record NodeQuery
{
    public int Limit { get; init; } = 500;
    public string? SessionId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}
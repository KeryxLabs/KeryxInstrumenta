namespace SttpMcp.Domain.Models;

public record SttpNode
{
    public required string Raw { get; init; }          // full ⏣ text as received
    public required string SessionId { get; init; }
    public required string Tier { get; init; }
    public required DateTime Timestamp { get; init; }
    public required int CompressionDepth { get; init; }
    public string? ParentNodeId { get; init; }
    public required AvecState UserAvec { get; init; }
    public required AvecState ModelAvec { get; init; }
    public AvecState? CompressionAvec { get; init; }
    public required float Rho { get; init; }
    public required float Kappa { get; init; }
    public required float Psi { get; init; }           // ⍉ compiler checksum
}

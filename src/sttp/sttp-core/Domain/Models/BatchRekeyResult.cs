namespace SttpMcp.Domain.Models;

public sealed record BatchRekeyResult
{
    public required bool DryRun { get; init; }
    public required int RequestedNodeIds { get; init; }
    public required int ResolvedNodeIds { get; init; }
    public IReadOnlyList<string> MissingNodeIds { get; init; } = [];
    public IReadOnlyList<ScopeRekeyResult> Scopes { get; init; } = [];
    public required int TemporalNodesUpdated { get; init; }
    public required int CalibrationsUpdated { get; init; }
}
namespace SttpMcp.Storage.SurrealDb.Models;

public sealed class TemporalNodeData
{
    public required string session_id { get; init; }
    public required string raw { get; init; }
    public required string tier { get; init; }
    public required DateTime timestamp { get; init; }
    public required int compression_depth { get; init; }
    public string? parent_node_id { get; init; }
    public required double psi { get; init; }
    public required double rho { get; init; }
    public required double kappa { get; init; }
    public required AvecDataForStore user_avec { get; init; }
    public required AvecDataForStore model_avec { get; init; }
    public required AvecDataForStore compression_avec { get; init; }
}

public sealed class AvecDataForStore
{
    public required double stability { get; init; }
    public required double friction { get; init; }
    public required double logic { get; init; }
    public required double autonomy { get; init; }
    public required double psi { get; init; }
}

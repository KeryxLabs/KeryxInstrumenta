namespace SttpMcp.Domain.Models;

public sealed record ScopeRekeyResult
{
    public required string SourceTenantId { get; init; }
    public required string SourceSessionId { get; init; }
    public required string TargetTenantId { get; init; }
    public required string TargetSessionId { get; init; }
    public required int TemporalNodes { get; init; }
    public required int Calibrations { get; init; }
    public required int TargetTemporalNodes { get; init; }
    public required int TargetCalibrations { get; init; }
    public required bool Applied { get; init; }
    public required bool Conflict { get; init; }
    public string? Message { get; init; }
}
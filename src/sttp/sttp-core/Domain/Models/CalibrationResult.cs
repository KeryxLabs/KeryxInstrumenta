namespace SttpMcp.Domain.Models;

public record CalibrationResult
{
    public required AvecState PreviousAvec { get; init; }
    public required float Delta { get; init; }
    public required DriftClassification DriftClassification { get; init; }
    public required string Trigger { get; init; }
    public required IReadOnlyList<string> TriggerHistory { get; init; }
    public required bool IsFirstCalibration { get; init; }
}
namespace SttpMcp.Domain.Models;

public sealed record MonthlyRollupResult
{
    public bool Success { get; init; }
    public string NodeId { get; init; } = string.Empty;
    public string RawNode { get; init; } = string.Empty;
    public string? Error { get; init; }
    public int SourceNodes { get; init; }
    public string? ParentReference { get; init; }
    public AvecState UserAverage { get; init; } = AvecState.Zero;
    public AvecState ModelAverage { get; init; } = AvecState.Zero;
    public AvecState CompressionAverage { get; init; } = AvecState.Zero;
    public NumericRange RhoRange { get; init; } = new() { Min = 0, Max = 0, Average = 0 };
    public NumericRange KappaRange { get; init; } = new() { Min = 0, Max = 0, Average = 0 };
    public NumericRange PsiRange { get; init; } = new() { Min = 0, Max = 0, Average = 0 };
    public ConfidenceBandSummary RhoBands { get; init; } = new();
    public ConfidenceBandSummary KappaBands { get; init; } = new();
}
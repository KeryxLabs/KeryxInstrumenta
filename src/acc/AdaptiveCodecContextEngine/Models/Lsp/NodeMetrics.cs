namespace AdaptiveCodecContextEngine.Models.Lsp;
public record NodeMetrics
{
    // LSP metrics
    public int LinesOfCode { get; init; }
    public int CyclomaticComplexity { get; init; }
    public int Parameters { get; init; }
    
    // Graph metrics
    public int IncomingEdges { get; init; }
    public int OutgoingEdges { get; init; }
    public int TotalDegree { get; init; }
    
    // Git metrics
    public int GitTotalCommits { get; init; }
    public int GitContributors { get; init; }
    public double GitAvgDaysBetweenChanges { get; init; }
    
    // Test metrics
    public double TestLineCoverage { get; init; }
    public double TestBranchCoverage { get; init; }
}

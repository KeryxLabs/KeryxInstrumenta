using AdaptiveCodecContextEngine.Models.Git;

namespace AdaptiveCodecContextEngine.Models.Surreal;
public record NodeUpdate
{
    public string NodeId { get; init; } = null!;
    public string Type { get; init; } = null!;
    public string Language { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string FilePath { get; init; } = null!;
    public int LineStart { get; init; }
    public int LineEnd { get; init; }
    public string? Namespace { get; init; }
    public string? Signature { get; init; }
    public string? ReturnType { get; init; }
    
    // Metrics (nullable for partial updates)
    public int? LinesOfCode { get; init; }
    public int? CyclomaticComplexity { get; init; }
    public int? Parameters { get; init; }
    
    public GitHistory? GitHistory { get; init; }
    public TestCoverage? TestCoverage { get; init; }
}



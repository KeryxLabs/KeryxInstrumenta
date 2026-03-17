using AdaptiveCodecContextEngine.Models.Git;

namespace AdaptiveCodecContextEngine.Models.Surreal;

public record NodeUpdate
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; init; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; init; } = null!;

    [JsonPropertyName("language")]
    public string Language { get; init; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = null!;

    [JsonPropertyName("line_start")]
    public int LineStart { get; init; }

    [JsonPropertyName("line_end")]
    public int LineEnd { get; init; }

    [JsonPropertyName("namespace")]
    public string? Namespace { get; init; }

    [JsonPropertyName("signature")]
    public string? Signature { get; init; }

    [JsonPropertyName("return_type")]
    public string? ReturnType { get; init; }

    // Metrics (nullable for partial updates)
    [JsonPropertyName("lines_of_code")]
    public int? LinesOfCode { get; init; }

    [JsonPropertyName("cyclomatic_complexity")]
    public int? CyclomaticComplexity { get; init; }

    [JsonPropertyName("parameters")]
    public int? Parameters { get; init; }

    [JsonPropertyName("git_history")]
    public GitHistory? GitHistory { get; init; }

    [JsonPropertyName("test_coverage")]
    public TestCoverage? TestCoverage { get; init; }
}

public record NodeUpdateWithContext(NodeUpdate Update, ActivityContext? Context);

using System.Diagnostics.CodeAnalysis;
using SurrealDb.Net.Models;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public record NodeRecord
{
    [JsonPropertyName("id")] public RecordId? Id {get;init;}
    [JsonPropertyName("node_id")] public string NodeId { get; init; } = null!;
    [JsonPropertyName("type")] public string Type { get; init; } = null!;
    [JsonPropertyName("language")] public string Language { get; init; } = null!;
    [JsonPropertyName("namespace")] public string? Namespace { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = null!;
    [JsonPropertyName("signature")] public string? Signature { get; init; }
    [JsonPropertyName("file_path")] public string FilePath { get; init; } = null!;
    [JsonPropertyName("line_start")] public int LineStart { get; init; }
    [JsonPropertyName("line_end")] public int LineEnd { get; init; }
    [JsonPropertyName("return_type")] public string? ReturnType { get; init; }

    // LSP metrics
    [JsonPropertyName("lines_of_code")] public int LinesOfCode { get; init; }
    [JsonPropertyName("cyclomatic_complexity")] public int CyclomaticComplexity { get; init; }
    [JsonPropertyName("parameters")] public int Parameters { get; init; }

    // Git history
    [JsonPropertyName("git_created")] public DateTime? GitCreated { get; init; }
    [JsonPropertyName("git_last_modified")] public DateTime? GitLastModified { get; init; }
    [JsonPropertyName("git_total_commits")] public int GitTotalCommits { get; init; }
    [JsonPropertyName("git_contributors")] public int GitContributors { get; init; }
    [JsonPropertyName("git_avg_days_between_changes")] public double GitAvgDaysBetweenChanges { get; init; }
    [JsonPropertyName("git_recent_frequency")] public string GitRecentFrequency { get; init; } = "low";

    // Test coverage
    [JsonPropertyName("test_covered")] public bool TestCovered { get; init; }
    [JsonPropertyName("test_line_coverage")] public double TestLineCoverage { get; init; }
    [JsonPropertyName("test_branch_coverage")] public double TestBranchCoverage { get; init; }
    [JsonPropertyName("test_count")] public int TestCount { get; init; }

    // Graph metrics
    [JsonPropertyName("incoming_edges")] public int IncomingEdges { get; init; }
    [JsonPropertyName("outgoing_edges")] public int OutgoingEdges { get; init; }
    [JsonPropertyName("total_degree")] public int TotalDegree { get; init; }

    // Flat AVEC computed
    [JsonPropertyName("avec_stability")] public double? AvecStability { get; init; }
    [JsonPropertyName("avec_logic")] public double? AvecLogic { get; init; }
    [JsonPropertyName("avec_friction")] public double? AvecFriction { get; init; }
    [JsonPropertyName("avec_autonomy")] public double? AvecAutonomy { get; init; }
    [JsonPropertyName("avec_computed_at")] public DateTime? AvecComputedAt { get; init; }
    [JsonPropertyName("avec_needs_recalc")] public bool AvecNeedsRecalc { get; init; }

    // Flat AVEC learned
    [JsonPropertyName("avec_learned_stability")] public double? AvecLearnedStability { get; init; }
    [JsonPropertyName("avec_learned_logic")] public double? AvecLearnedLogic { get; init; }
    [JsonPropertyName("avec_learned_friction")] public double? AvecLearnedFriction { get; init; }
    [JsonPropertyName("avec_learned_autonomy")] public double? AvecLearnedAutonomy { get; init; }

    // Flat AVEC delta
    [JsonPropertyName("avec_delta_stability")] public double? AvecDeltaStability { get; init; }
    [JsonPropertyName("avec_delta_logic")] public double? AvecDeltaLogic { get; init; }
    [JsonPropertyName("avec_delta_friction")] public double? AvecDeltaFriction { get; init; }
    [JsonPropertyName("avec_delta_autonomy")] public double? AvecDeltaAutonomy { get; init; }
}

namespace AdaptiveCodecContextEngine.Models.Lizard;

public record LizardFunction
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    [JsonPropertyName("long_name")]
    public string LongName { get; init; } = null!;

    [JsonPropertyName("cyclomatic_complexity")]
    public int CyclomaticComplexity { get; init; }

    [JsonPropertyName("nloc")]
    public int Nloc { get; init; } // Net lines of code

    [JsonPropertyName("token_count")]
    public int TokenCount { get; init; }

    [JsonPropertyName("parameter_count")]
    public int ParameterCount { get; init; }

    [JsonPropertyName("start_line")]
    public int StartLine { get; init; }

    [JsonPropertyName("end_line")]
    public int EndLine { get; init; }
}

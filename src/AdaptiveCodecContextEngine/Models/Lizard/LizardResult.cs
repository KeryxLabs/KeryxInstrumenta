namespace AdaptiveCodecContextEngine.Models.Lizard;


public record LizardResult
{
    [JsonPropertyName("function_list")]
    public List<LizardFunction> FunctionList { get; init; } = new();
}

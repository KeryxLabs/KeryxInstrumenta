using System.Diagnostics.CodeAnalysis;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public record AvecDto
{
    [JsonPropertyName("stability")]
    public double Stability { get; init; }

    [JsonPropertyName("logic")]
    public double Logic { get; init; }

    [JsonPropertyName("friction")]
    public double Friction { get; init; }

    [JsonPropertyName("autonomy")]
    public double Autonomy { get; init; }

    [JsonPropertyName("computed_at")]
    public DateTime? ComputedAt { get; init; }
}

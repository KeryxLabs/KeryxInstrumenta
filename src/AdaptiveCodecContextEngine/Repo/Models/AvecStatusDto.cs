
using Dahomey.Cbor.Attributes;

public record AvecStatusDto
{
    [JsonPropertyName("needs_recalc")]
    public bool NeedsRecalc { get; init; }
}

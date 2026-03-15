
using Dahomey.Cbor.Attributes;

public record NodeIdDto
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; init; } = null!;
}

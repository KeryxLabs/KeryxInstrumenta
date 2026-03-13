namespace AdaptiveCodecContextEngine.Models.Rpc;
public record UnregisterLspStreamParams
{
    [JsonPropertyName("streamId")]
    public string StreamId { get; init; } = null!;
}
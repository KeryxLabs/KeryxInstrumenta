namespace AdaptiveCodecContextEngine.Models.Rpc;


public record RegisterLspStreamResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    
    [JsonPropertyName("streamId")]
    public string? StreamId { get; init; }
    
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
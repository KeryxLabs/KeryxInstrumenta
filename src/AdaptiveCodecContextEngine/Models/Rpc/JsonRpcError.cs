namespace AdaptiveCodecContextEngine.Models.Rpc;

public record JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = null!;
}


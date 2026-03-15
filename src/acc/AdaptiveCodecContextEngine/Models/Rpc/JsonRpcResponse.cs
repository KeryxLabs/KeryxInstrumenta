
namespace AdaptiveCodecContextEngine.Models.Rpc;

public record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }
    
    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }
    
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }
}


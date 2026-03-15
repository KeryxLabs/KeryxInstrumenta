
namespace AdaptiveCodecContextEngine.Models.Rpc;

public record JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public  JsonElement? Id { get; init; }
    
    [JsonPropertyName("method")]
    public string Method { get; init; } = null!;
    
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}

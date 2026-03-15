namespace AdaptiveCodecContextEngine.Models.Lsp;
public record LspMessage
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; init; }
    
    [JsonPropertyName("method")]
    public string? Method { get; init; }
    
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
    
    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }
}
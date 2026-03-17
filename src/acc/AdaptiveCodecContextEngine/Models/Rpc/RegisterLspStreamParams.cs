namespace AdaptiveCodecContextEngine.Models.Rpc;

public record RegisterLspStreamParams
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = null!;
    
    [JsonPropertyName("language")]
    public string Language { get; init; } = null!;
    
    [JsonPropertyName("path")]
    public string? Path { get; init; }
    
    [JsonPropertyName("port")]
    public int? Port { get; init; }
}


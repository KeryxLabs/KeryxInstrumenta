using System.Text.Json.Serialization;

namespace SttpMcp.Storage.SurrealDb.Models;

public sealed class SurrealNodeRecord
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;
    
    [JsonPropertyName("raw")]
    public string Raw { get; set; } = string.Empty;
    
    [JsonPropertyName("tier")]
    public string Tier { get; set; } = string.Empty;
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("compression_depth")]
    public int CompressionDepth { get; set; }
    
    [JsonPropertyName("parent_node_id")]
    public string? ParentNodeId { get; set; }
    
    [JsonPropertyName("psi")]
    public double Psi { get; set; }
    
    [JsonPropertyName("rho")]
    public double Rho { get; set; }
    
    [JsonPropertyName("kappa")]
    public double Kappa { get; set; }
    
    [JsonPropertyName("user_avec")]
    public AvecData? UserAvec { get; set; }
    
    [JsonPropertyName("model_avec")]
    public AvecData? ModelAvec { get; set; }
    
    [JsonPropertyName("compression_avec")]
    public AvecData? CompressionAvec { get; set; }
    
    [JsonPropertyName("resonance_delta")]
    public double ResonanceDelta { get; set; }
}

public sealed class AvecData
{
    [JsonPropertyName("stability")]
    public double Stability { get; set; }
    
    [JsonPropertyName("friction")]
    public double Friction { get; set; }
    
    [JsonPropertyName("logic")]
    public double Logic { get; set; }
    
    [JsonPropertyName("autonomy")]
    public double Autonomy { get; set; }
    
    [JsonPropertyName("psi")]
    public double Psi { get; set; }
}

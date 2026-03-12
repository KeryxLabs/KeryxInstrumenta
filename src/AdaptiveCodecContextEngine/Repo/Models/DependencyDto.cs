
public record DependencyDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    
    [JsonPropertyName("in")]
    public string In { get; init; } = null!;
    
    [JsonPropertyName("out")]
    public string Out { get; init; } = null!;
    
    [JsonPropertyName("relationship_type")]
    public string RelationshipType { get; init; } = null!;
    
    [JsonPropertyName("weight")]
    public double Weight { get; init; }
}
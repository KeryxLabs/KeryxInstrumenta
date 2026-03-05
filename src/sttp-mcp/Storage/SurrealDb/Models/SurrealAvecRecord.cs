using System.Text.Json.Serialization;

namespace SttpMcp.Storage.SurrealDb.Models;

public sealed record SurrealAvecRecord(
    [property: JsonPropertyName("stability")] float Stability,
    [property: JsonPropertyName("friction")] float Friction,
    [property: JsonPropertyName("logic")] float Logic,
    [property: JsonPropertyName("autonomy")] float Autonomy,
    [property: JsonPropertyName("psi")] float Psi,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);
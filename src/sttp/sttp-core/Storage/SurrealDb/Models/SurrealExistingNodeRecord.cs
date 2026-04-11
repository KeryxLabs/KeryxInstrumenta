using System.Text.Json.Serialization;

namespace SttpMcp.Storage.SurrealDb.Models;

public sealed record SurrealExistingNodeRecord(
    [property: JsonPropertyName("Id")] string Id,
    [property: JsonPropertyName("SourceMetadata")] object? SourceMetadata);
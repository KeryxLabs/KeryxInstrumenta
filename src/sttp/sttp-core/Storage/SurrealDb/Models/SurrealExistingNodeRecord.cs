using System.Text.Json.Serialization;
using SttpMcp.Domain.Models;

namespace SttpMcp.Storage.SurrealDb.Models;

public sealed record SurrealExistingNodeRecord(
    [property: JsonPropertyName("Id")] string Id,
    [property: JsonPropertyName("SourceMetadata")] ConnectorMetadata? SourceMetadata);
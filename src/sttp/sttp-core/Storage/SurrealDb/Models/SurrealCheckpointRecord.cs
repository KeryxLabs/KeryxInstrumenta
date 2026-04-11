using System.Text.Json.Serialization;
using SttpMcp.Domain.Models;

namespace SttpMcp.Storage.SurrealDb.Models;

public sealed record SurrealCheckpointRecord(
    [property: JsonPropertyName("SessionId")] string SessionId,
    [property: JsonPropertyName("ConnectorId")] string ConnectorId,
    [property: JsonPropertyName("CursorUpdatedAt")] DateTime? CursorUpdatedAt,
    [property: JsonPropertyName("CursorSyncKey")] string? CursorSyncKey,
    [property: JsonPropertyName("UpdatedAt")] DateTime UpdatedAt,
    [property: JsonPropertyName("Metadata")] ConnectorMetadata? Metadata);
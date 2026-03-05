using System.Text.Json.Serialization;

namespace SttpMcp.Storage.SurrealDb.Models;

public sealed record SurrealTriggerRecord(
    [property: JsonPropertyName("trigger")] string Trigger);
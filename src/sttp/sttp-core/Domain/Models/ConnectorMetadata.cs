using System.Text.Json;

namespace SttpMcp.Domain.Models;

public sealed record ConnectorMetadata
{
    public required string ConnectorId { get; init; }
    public required string SourceKind { get; init; }
    public required string UpstreamId { get; init; }
    public string? Revision { get; init; }
    public required DateTime ObservedAtUtc { get; init; }
    public JsonElement? Extra { get; init; }
}
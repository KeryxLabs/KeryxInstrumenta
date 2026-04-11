using System.Text.Json;

namespace SttpMcp.Domain.Models;

public sealed record SyncCheckpoint
{
    public required string SessionId { get; init; }
    public required string ConnectorId { get; init; }
    public SyncCursor? Cursor { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public JsonElement? Metadata { get; init; }
}
using SttpMcp.Domain.Models;

namespace SttpMcp.Domain.Contracts;

public interface ISyncChangeSource
{
    Task<ChangeQueryResult> ReadChangesAsync(
        string sessionId,
        string connectorId,
        SyncCursor? cursor,
        int limit,
        CancellationToken ct = default);
}
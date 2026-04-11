using Microsoft.Extensions.Logging;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;

namespace SttpMcp.Application.Services;

public sealed class SyncCoordinatorService(
    INodeStore store,
    ISyncChangeSource source,
    ILogger<SyncCoordinatorService> logger,
    ISyncCoordinatorPolicy? policy = null)
{
    public async Task<SyncPullResult> PullAsync(
        SyncPullRequest request,
        CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var maxBatches = Math.Max(request.MaxBatches ?? int.MaxValue, 1);
        var checkpoint = await store.GetCheckpointAsync(request.SessionId, request.ConnectorId, ct);
        var cursor = checkpoint?.Cursor;
        var fetched = 0;
        var created = 0;
        var updated = 0;
        var duplicate = 0;
        var skipped = 0;
        var filtered = 0;
        var batches = 0;
        var hasMore = false;

        while (batches < maxBatches)
        {
            var page = await source.ReadChangesAsync(
                request.SessionId,
                request.ConnectorId,
                cursor,
                pageSize,
                ct);

            if (page.Nodes.Count == 0)
            {
                hasMore = page.HasMore;
                break;
            }

            batches++;
            fetched += page.Nodes.Count;
            SttpNode? lastAppliedNode = null;

            foreach (var node in page.Nodes)
            {
                if (!(policy?.ShouldAcceptNode(node) ?? true))
                {
                    filtered++;
                    continue;
                }

                var upsert = await store.UpsertNodeAsync(node, ct);
                lastAppliedNode = node;

                switch (upsert.Status)
                {
                    case NodeUpsertStatus.Created:
                        created++;
                        break;
                    case NodeUpsertStatus.Updated:
                        updated++;
                        break;
                    case NodeUpsertStatus.Duplicate:
                        duplicate++;
                        break;
                    case NodeUpsertStatus.Skipped:
                        skipped++;
                        break;
                }
            }

            if (page.NextCursor is not null)
            {
                cursor = page.NextCursor;
                checkpoint = new SyncCheckpoint
                {
                    SessionId = request.SessionId,
                    ConnectorId = request.ConnectorId,
                    Cursor = page.NextCursor,
                    UpdatedAt = DateTime.UtcNow,
                    Metadata = policy?.BuildCheckpointMetadata(
                            request.SessionId,
                            request.ConnectorId,
                            checkpoint,
                            lastAppliedNode,
                            page.NextCursor)
                        ?? checkpoint?.Metadata
                };

                await store.PutCheckpointAsync(checkpoint, ct);
            }

            hasMore = page.HasMore;
            if (!hasMore)
                break;
        }

        logger.LogDebug(
            "Sync pull completed for {SessionId}/{ConnectorId}: fetched={Fetched}, created={Created}, updated={Updated}, duplicate={Duplicate}, skipped={Skipped}, filtered={Filtered}, batches={Batches}, hasMore={HasMore}",
            request.SessionId,
            request.ConnectorId,
            fetched,
            created,
            updated,
            duplicate,
            skipped,
            filtered,
            batches,
            hasMore);

        return new SyncPullResult
        {
            Fetched = fetched,
            Created = created,
            Updated = updated,
            Duplicate = duplicate,
            Skipped = skipped,
            Filtered = filtered,
            Batches = batches,
            HasMore = hasMore,
            LastCursor = cursor,
            Checkpoint = checkpoint
        };
    }
}
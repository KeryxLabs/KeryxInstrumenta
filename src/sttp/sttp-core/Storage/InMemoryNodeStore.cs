using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using System.Text.Json;

namespace SttpMcp.Storage;

public sealed class InMemoryNodeStore : INodeStore, INodeStoreInitializer
{
    private readonly List<SttpNode> _nodes = [];
    private readonly List<(string SessionId, AvecState Avec, string Trigger)> _calibrations = [];
    private readonly List<SyncCheckpoint> _checkpoints = [];

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<SttpNode>> QueryNodesAsync(NodeQuery query, CancellationToken ct = default)
    {
        var cappedLimit = Math.Max(1, query.Limit);

        var result = _nodes
            .Where(n => string.IsNullOrWhiteSpace(query.SessionId) || n.SessionId == query.SessionId)
            .Where(n => query.FromUtc is null || n.Timestamp >= query.FromUtc.Value)
            .Where(n => query.ToUtc is null || n.Timestamp <= query.ToUtc.Value)
            .OrderByDescending(n => n.Timestamp)
            .Take(cappedLimit)
            .ToList();

        return Task.FromResult<IReadOnlyList<SttpNode>>(result);
    }

    public Task<NodeUpsertResult> UpsertNodeAsync(SttpNode node, CancellationToken ct = default)
    {
        var syncKey = string.IsNullOrWhiteSpace(node.SyncKey)
            ? node.CanonicalSyncKey()
            : node.SyncKey.Trim();
        var now = DateTime.UtcNow;
        var candidate = node with
        {
            SyncKey = syncKey,
            UpdatedAt = now
        };

        var existingIndex = _nodes.FindIndex(existing =>
            existing.SessionId == candidate.SessionId &&
            existing.SyncKey == syncKey);

        if (existingIndex >= 0)
        {
            var existing = _nodes[existingIndex];
            if (!AreEquivalent(existing, candidate))
            {
                _nodes[existingIndex] = candidate;
                return Task.FromResult(new NodeUpsertResult
                {
                    NodeId = GetNodeId(existing),
                    SyncKey = syncKey,
                    Status = NodeUpsertStatus.Updated,
                    UpdatedAt = now
                });
            }

            return Task.FromResult(new NodeUpsertResult
            {
                NodeId = GetNodeId(existing),
                SyncKey = syncKey,
                Status = NodeUpsertStatus.Duplicate,
                UpdatedAt = existing.UpdatedAt
            });
        }

        _nodes.Add(candidate);
        return Task.FromResult(new NodeUpsertResult
        {
            NodeId = GetNodeId(candidate),
            SyncKey = syncKey,
            Status = NodeUpsertStatus.Created,
            UpdatedAt = now
        });
    }

    public Task<IReadOnlyList<SttpNode>> GetByResonanceAsync(
        string sessionId, AvecState current, int limit = 5, CancellationToken ct = default)
    {
        var result = _nodes
            .Where(n => n.SessionId == sessionId)
            .OrderBy(n => Math.Abs(n.Psi - current.Psi))
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<SttpNode>>(result);
    }

    public Task<IReadOnlyList<SttpNode>> ListNodesAsync(
        int limit = 50,
        string? sessionId = null,
        CancellationToken ct = default)
        => QueryNodesAsync(new NodeQuery { Limit = Math.Clamp(limit, 1, 200), SessionId = sessionId }, ct);

    public Task<AvecState?> GetLastAvecAsync(
        string sessionId, CancellationToken ct = default)
    {
        var last = _calibrations
            .Where(c => c.SessionId == sessionId)
            .Select(c => c.Avec)
            .LastOrDefault();

        return Task.FromResult<AvecState?>(last);
    }

    public Task<IReadOnlyList<string>> GetTriggerHistoryAsync(
        string sessionId, CancellationToken ct = default)
    {
        var history = _calibrations
            .Where(c => c.SessionId == sessionId)
            .Select(c => c.Trigger)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(history);
    }

    public Task StoreCalibrationAsync(
        string sessionId, AvecState avec, string trigger, CancellationToken ct = default)
    {
        _calibrations.Add((sessionId, avec, trigger));
        return Task.CompletedTask;
    }

    public Task<ChangeQueryResult> QueryChangesSinceAsync(
        string sessionId,
        SyncCursor? cursor,
        int limit,
        CancellationToken ct = default)
    {
        var cappedLimit = Math.Max(1, limit);
        var changes = _nodes
            .Where(node => node.SessionId == sessionId)
            .Where(node => cursor is null ||
                node.UpdatedAt > cursor.UpdatedAt ||
                (node.UpdatedAt == cursor.UpdatedAt && string.CompareOrdinal(node.SyncKey, cursor.SyncKey) > 0))
            .OrderBy(node => node.UpdatedAt)
            .ThenBy(node => node.SyncKey, StringComparer.Ordinal)
            .ToList();

        var hasMore = changes.Count > cappedLimit;
        if (hasMore)
            changes = changes.Take(cappedLimit).ToList();

        return Task.FromResult(new ChangeQueryResult
        {
            Nodes = changes,
            HasMore = hasMore,
            NextCursor = changes.Count == 0
                ? null
                : new SyncCursor
                {
                    UpdatedAt = changes[^1].UpdatedAt,
                    SyncKey = changes[^1].SyncKey
                }
        });
    }

    public Task<SyncCheckpoint?> GetCheckpointAsync(
        string sessionId,
        string connectorId,
        CancellationToken ct = default)
    {
        var checkpoint = _checkpoints.FirstOrDefault(existing =>
            existing.SessionId == sessionId &&
            existing.ConnectorId == connectorId);
        return Task.FromResult(checkpoint);
    }

    public Task PutCheckpointAsync(SyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        var existingIndex = _checkpoints.FindIndex(existing =>
            existing.SessionId == checkpoint.SessionId &&
            existing.ConnectorId == checkpoint.ConnectorId);

        if (existingIndex >= 0)
            _checkpoints[existingIndex] = checkpoint;
        else
            _checkpoints.Add(checkpoint);

        return Task.CompletedTask;
    }

    private static bool AreEquivalent(SttpNode left, SttpNode right)
        => left.Raw == right.Raw
            && left.SessionId == right.SessionId
            && left.Tier == right.Tier
            && left.Timestamp == right.Timestamp
            && left.CompressionDepth == right.CompressionDepth
            && left.ParentNodeId == right.ParentNodeId
            && left.SyncKey == right.SyncKey
            && left.UserAvec == right.UserAvec
            && left.ModelAvec == right.ModelAvec
            && left.CompressionAvec == right.CompressionAvec
            && left.Rho == right.Rho
            && left.Kappa == right.Kappa
            && left.Psi == right.Psi
            && NormalizeMetadata(left.SourceMetadata) == NormalizeMetadata(right.SourceMetadata);

    private static string GetNodeId(SttpNode node)
        => string.IsNullOrWhiteSpace(node.SyncKey)
            ? node.CanonicalSyncKey()
            : node.SyncKey;

    private static string? NormalizeMetadata(ConnectorMetadata? metadata)
        => metadata is null ? null : JsonSerializer.Serialize(metadata);
}
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;

namespace SttpMcp.Storage;

public sealed class InMemoryNodeStore : INodeStore, INodeStoreInitializer
{
    private readonly List<SttpNode> _nodes = [];
    private readonly List<(string SessionId, AvecState Avec, string Trigger)> _calibrations = [];

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

    public Task<string> StoreAsync(SttpNode node, CancellationToken ct = default)
    {
        _nodes.Add(node);
        return Task.FromResult(Guid.NewGuid().ToString());
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
}
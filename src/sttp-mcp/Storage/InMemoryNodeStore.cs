
using SttpMcp.Domain.Models;
using SttpMcp.Domain.Contracts;

namespace SttpMcp.Storage;

// placeholder until SurrealDB wired in Phase 1
public sealed class InMemoryNodeStore : INodeStore
{
    private readonly List<SttpNode> _nodes = [];
    private readonly List<(string SessionId, AvecState Avec, string Trigger)> _calibrations = [];

    public Task<string> StoreAsync(SttpNode node, CancellationToken ct = default)
    {
        _nodes.Add(node);
        return Task.FromResult(Guid.NewGuid().ToString());
    }

    public Task<IReadOnlyList<SttpNode>> GetByResonanceAsync(
        string sessionId, AvecState current, int limit = 5, CancellationToken ct = default)
    {
        // simple psi proximity until SurrealDB HNSW vector index is wired
        var result = _nodes
            .Where(n => n.SessionId == sessionId)
            .OrderBy(n => Math.Abs(n.Psi - current.Psi))
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<SttpNode>>(result);
    }

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
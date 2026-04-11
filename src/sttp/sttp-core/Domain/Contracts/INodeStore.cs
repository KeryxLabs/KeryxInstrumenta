using SttpMcp.Domain.Models;

namespace SttpMcp.Domain.Contracts;

public interface INodeStore
{
    Task<IReadOnlyList<SttpNode>> QueryNodesAsync(
        NodeQuery query,
        CancellationToken ct = default);

    async Task<string> StoreAsync(SttpNode node, CancellationToken ct = default)
        => (await UpsertNodeAsync(node, ct)).NodeId;

    Task<NodeUpsertResult> UpsertNodeAsync(
        SttpNode node,
        CancellationToken ct = default);

    Task<IReadOnlyList<SttpNode>> GetByResonanceAsync(
        string sessionId,
        AvecState currentAvec,
        int limit = 5,
        CancellationToken ct = default);

    Task<IReadOnlyList<SttpNode>> ListNodesAsync(
        int limit = 50,
        string? sessionId = null,
        CancellationToken ct = default);

    Task<AvecState?> GetLastAvecAsync(
        string sessionId,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetTriggerHistoryAsync(
        string sessionId,
        CancellationToken ct = default);

    Task StoreCalibrationAsync(
        string sessionId,
        AvecState avec,
        string trigger,
        CancellationToken ct = default);

    Task<ChangeQueryResult> QueryChangesSinceAsync(
        string sessionId,
        SyncCursor? cursor,
        int limit,
        CancellationToken ct = default);

    Task<SyncCheckpoint?> GetCheckpointAsync(
        string sessionId,
        string connectorId,
        CancellationToken ct = default);

    Task PutCheckpointAsync(
        SyncCheckpoint checkpoint,
        CancellationToken ct = default);

    Task<BatchRekeyResult> BatchRekeyScopesAsync(
        IReadOnlyList<string> nodeIds,
        string targetTenantId,
        string targetSessionId,
        bool dryRun,
        bool allowMerge,
        CancellationToken ct = default);
}
using SttpMcp.Domain.Models;

namespace SttpMcp.Domain.Contracts;
// INodeStore.cs
public interface INodeStore
{
    /// <summary>
    /// Persist a validated STTP node. Returns the assigned node ID.
    /// </summary>
    Task<string> StoreAsync(SttpNode node, CancellationToken ct = default);

    /// <summary>
    /// Retrieve nodes for a session ordered by AVEC resonance proximity.
    /// </summary>
    Task<IReadOnlyList<SttpNode>> GetByResonanceAsync(
        string sessionId,
        AvecState currentAvec,
        int limit = 5,
        CancellationToken ct = default);

    /// <summary>
    /// List stored nodes ordered by newest first.
    /// When sessionId is provided, limits results to that session.
    /// </summary>
    Task<IReadOnlyList<SttpNode>> ListNodesAsync(
        int limit = 50,
        string? sessionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve the most recent AVEC state stored for this session.
    /// Returns null if no prior calibration exists.
    /// </summary>
    Task<AvecState?> GetLastAvecAsync(
        string sessionId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieve calibration trigger history for this session.
    /// </summary>
    Task<IReadOnlyList<string>> GetTriggerHistoryAsync(
        string sessionId,
        CancellationToken ct = default);

    /// <summary>
    /// Store a calibration record — AVEC state + trigger.
    /// </summary>
    Task StoreCalibrationAsync(
        string sessionId,
        AvecState avec,
        string trigger,
        CancellationToken ct = default);
}
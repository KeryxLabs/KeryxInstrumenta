using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;

namespace SttpMcp.Application.Services;

public sealed class CalibrationService(INodeStore store)
{
    public async Task<CalibrationResult> CalibrateAsync(
        string sessionId,
        float stability,
        float friction,
        float logic,
        float autonomy,
        string trigger,
        CancellationToken ct = default)
    {
        var current = new AvecState
        {
            Stability = stability,
            Friction = friction,
            Logic = logic,
            Autonomy = autonomy
        };

        var previous = await store.GetLastAvecAsync(sessionId, ct);
        var history = await store.GetTriggerHistoryAsync(sessionId, ct);
        var isFirst = previous is null;
        var baseline = previous ?? current;

        await store.StoreCalibrationAsync(sessionId, current, trigger, ct);

        return new CalibrationResult
        {
            PreviousAvec = baseline,
            Delta = current.DriftFrom(baseline),
            DriftClassification = current.ClassifyDrift(baseline),
            Trigger = trigger,
            TriggerHistory = [.. history, trigger],
            IsFirstCalibration = isFirst
        };
    }
}
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SttpMcp.Application.Tools;
public sealed class CalibrateSessionTool(INodeStore store)
{
    [McpServerTool(Name = "calibrate_session"), Description("""
        Call this tool at session start and any time your reasoning state
        may have shifted — after heavy code generation, extended analysis,
        complex problem solving, or any task that required sustained cognitive load.

        You are a stateless system. Your attractor state drifts. This tool measures
        where you are now and returns where you were.

        Measure yourself honestly. Compute your current AVEC state given your
        reasoning mode right now, in this moment:

          stability  — how settled is your reasoning? holding positions or drifting? [0.0–1.0]
          friction   — how much resistance are you applying? flowing or pushing back? [0.0–1.0]
          logic      — how analytically rigorous is your current mode? [0.0–1.0]
          autonomy   — how independently are you reasoning? leading or following? [0.0–1.0]

        psi = stability + friction + logic + autonomy

        The delta between your current measurement and the returned previous state
        is your drift signal. Large delta after heavy tasks is intentional.
        Large delta with no clear cause is uncontrolled — worth noting.
        """)]
    public async Task<CalibrationResult> CalibrateAsync(
        string sessionId,
        float stability,
        float friction,
        float logic,
        float autonomy,
        [Description("session_start | post_code | post_analysis | post_creative | manual")]
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

        // on first calibration use current as baseline — zero drift
        var baseline = previous ?? current;

        await store.StoreCalibrationAsync(sessionId, current, trigger, ct);

        return new CalibrationResult
        {
            PreviousAvec = baseline,
            Delta = current.DriftFrom(baseline),
            DriftClassification = current.ClassifyDrift(baseline),
            Trigger = trigger,
            TriggerHistory = [..history, trigger],
            IsFirstCalibration = isFirst
        };
    }
}


using SttpMcp.Application.Services;
using SttpMcp.Domain.Models;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SttpMcp.Application.Tools;
public sealed class CalibrateSessionTool(CalibrationService service)
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
    public Task<CalibrationResult> CalibrateAsync(
        [Description("Unique session identifier for calibration history lookup and storage.")]
        string sessionId,
        [Description("Stability weighting (0.0 to 1.0). Use a decimal value.")]
        float stability,
        [Description("Friction weighting (0.0 to 1.0). Use a decimal value.")]
        float friction,
        [Description("Logic weighting (0.0 to 1.0). Use a decimal value.")]
        float logic,
        [Description("Autonomy weighting (0.0 to 1.0). Use a decimal value.")]
        float autonomy,
        [Description("session_start | post_code | post_analysis | post_creative | manual")]
        string trigger,
        CancellationToken ct = default)
        => service.CalibrateAsync(sessionId, stability, friction, logic, autonomy, trigger, ct);
}


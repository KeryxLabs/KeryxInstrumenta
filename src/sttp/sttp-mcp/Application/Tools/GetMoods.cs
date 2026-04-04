using ModelContextProtocol.Server;
using SttpMcp.Domain.Models;
using System.ComponentModel;

namespace SttpMcp.Application.Tools;

public sealed class GetMoodsTool
{
    [McpServerTool(Name = "get_moods"), Description("""
        Retrieve AVEC mood presets and instructions for applying them.

        Use this tool when you want to shift reasoning mode intentionally
        (focused, creative, analytical, exploratory, collaborative, defensive, passive)
        and perform ad-hoc swaps without guessing values.

        How to apply AVEC:
        1) Choose a target mood preset.
        2) For a hard swap, set your next AVEC state to the preset directly.
        3) For a soft swap, blend from current state toward the preset:
           blended = current*(1-blend) + target*blend, with blend in [0,1].
        4) Use the blended result as your active AVEC for subsequent reasoning.
        5) After major work, recalibrate using calibrate_session.

        Provide targetMood + current AVEC values + blend to receive a computed swap preview.
        """)]
    public Task<MoodCatalogResult> GetAsync(
        [Description("Optional mood to preview a swap toward. Values: focused|creative|analytical|exploratory|collaborative|defensive|passive")]
        string? targetMood = null,
        [Description("Blend amount for soft swap in [0,1]. 1 = full swap, 0 = no change. Default 1.")]
        float blend = 1f,
        [Description("Current stability for swap preview (0.0 to 1.0). Use a decimal value.")]
        float? currentStability = null,
        [Description("Current friction for swap preview (0.0 to 1.0). Use a decimal value.")]
        float? currentFriction = null,
        [Description("Current logic for swap preview (0.0 to 1.0). Use a decimal value.")]
        float? currentLogic = null,
        [Description("Current autonomy for swap preview (0.0 to 1.0). Use a decimal value.")]
        float? currentAutonomy = null)
    {
        var presets = BuildPresets();
        var guide = "Choose mood -> hard swap direct values or soft swap with blend -> use resulting AVEC as active state -> recalibrate after heavy reasoning shifts.";

        var result = new MoodCatalogResult
        {
            Presets = presets,
            ApplyGuide = guide,
            SwapPreview = null
        };

        if (string.IsNullOrWhiteSpace(targetMood))
            return Task.FromResult(result);

        var mood = presets.FirstOrDefault(p => string.Equals(p.Name, targetMood, StringComparison.OrdinalIgnoreCase));
        if (mood is null)
            return Task.FromResult(result);

        if (currentStability is null || currentFriction is null || currentLogic is null || currentAutonomy is null)
            return Task.FromResult(result);

        var normalizedBlend = Math.Clamp(blend, 0f, 1f);
        var current = new AvecState
        {
            Stability = currentStability.Value,
            Friction = currentFriction.Value,
            Logic = currentLogic.Value,
            Autonomy = currentAutonomy.Value
        };

        var blended = Blend(current, mood.Avec, normalizedBlend);

        return Task.FromResult(new MoodCatalogResult
        {
            Presets = presets,
            ApplyGuide = guide,
            SwapPreview = new MoodSwapPreview
            {
                TargetMood = mood.Name,
                Blend = normalizedBlend,
                Current = current,
                Target = mood.Avec,
                Blended = blended
            }
        });
    }

    private static IReadOnlyList<MoodPreset> BuildPresets() =>
    [
        new MoodPreset { Name = "focused", Description = "Deep concentration with low resistance.", Avec = AvecState.Focused },
        new MoodPreset { Name = "creative", Description = "Flexible ideation and exploratory generation.", Avec = AvecState.Creative },
        new MoodPreset { Name = "analytical", Description = "Methodical, precise, high-rigor reasoning.", Avec = AvecState.Analytical },
        new MoodPreset { Name = "exploratory", Description = "Curious search with tolerance for uncertainty.", Avec = AvecState.Exploratory },
        new MoodPreset { Name = "collaborative", Description = "Cooperative and compromise-friendly reasoning.", Avec = AvecState.Collaborative },
        new MoodPreset { Name = "defensive", Description = "Boundary-protective, skeptical stance.", Avec = AvecState.Defensive },
        new MoodPreset { Name = "passive", Description = "Low-agency, low-resistance follow mode.", Avec = AvecState.Passive }
    ];

    private static AvecState Blend(AvecState current, AvecState target, float blend) => new()
    {
        Stability = current.Stability * (1f - blend) + target.Stability * blend,
        Friction = current.Friction * (1f - blend) + target.Friction * blend,
        Logic = current.Logic * (1f - blend) + target.Logic * blend,
        Autonomy = current.Autonomy * (1f - blend) + target.Autonomy * blend
    };
}
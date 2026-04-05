using SttpMcp.Domain.Models;

namespace SttpMcp.Application.Services;

public sealed class MoodCatalogService
{
    public Task<MoodCatalogResult> GetAsync(
        string? targetMood = null,
        float blend = 1f,
        float? currentStability = null,
        float? currentFriction = null,
        float? currentLogic = null,
        float? currentAutonomy = null)
    {
        var presets = BuildPresets();
        const string guide = "Choose mood -> hard swap direct values or soft swap with blend -> use resulting AVEC as active state -> recalibrate after heavy reasoning shifts.";

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

        var blended = new AvecState
        {
            Stability = current.Stability * (1f - normalizedBlend) + mood.Avec.Stability * normalizedBlend,
            Friction = current.Friction * (1f - normalizedBlend) + mood.Avec.Friction * normalizedBlend,
            Logic = current.Logic * (1f - normalizedBlend) + mood.Avec.Logic * normalizedBlend,
            Autonomy = current.Autonomy * (1f - normalizedBlend) + mood.Avec.Autonomy * normalizedBlend
        };

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
}
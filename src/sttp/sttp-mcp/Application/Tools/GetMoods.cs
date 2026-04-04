using ModelContextProtocol.Server;
using SttpMcp.Application.Services;
using SttpMcp.Domain.Models;
using System.ComponentModel;

namespace SttpMcp.Application.Tools;

public sealed class GetMoodsTool(MoodCatalogService service)
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
        => service.GetAsync(targetMood, blend, currentStability, currentFriction, currentLogic, currentAutonomy);
}
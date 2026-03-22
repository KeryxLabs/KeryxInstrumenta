using System.ComponentModel;
using AccCli;
using Cocona;

namespace AccCli;

/// <summary>acccli search — find nodes by name or AVEC profile</summary>
internal sealed class SearchCommands(AccEngineClient client)
{
    [Command("name")]
    [Description("Case-insensitive substring search for nodes by name")]
    public async Task ByName(
        [Argument(Description = "Substring to search for in node names")] string query,
        [Option('l', Description = "Maximum number of results to return")] int limit,
        GlobalOptions globals,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync("acc.search", new { name = query, limit }, globals, ct);
        Console.WriteLine(result ?? "[]");
    }

    [Command("patterns")]
    [Description(
        "Find nodes with a similar AVEC profile using Euclidean distance. "
            + "AVEC: stability (0=high-churn → 1=stable), logic (0=simple → 1=complex), "
            + "friction (0=isolated → 1=chokepoint), autonomy (0=coupled → 1=independent)."
    )]
    public async Task ByPattern(
        [Argument(Description = "Target stability score (0.0–1.0)")] double stability,
        [Argument(Description = "Target logic score (0.0–1.0)")] double logic,
        [Argument(Description = "Target friction score (0.0–1.0)")] double friction,
        [Argument(Description = "Target autonomy score (0.0–1.0)")] double autonomy,
        [Option('t', Description = "Similarity threshold (0.0=any, 1.0=identical)")]
            double threshold,
        GlobalOptions globals,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync(
            "acc.queryPatterns",
            new
            {
                profile = new
                {
                    stability,
                    logic,
                    friction,
                    autonomy,
                },
                threshold,
            },
            globals,
            ct
        );
        Console.WriteLine(result ?? "[]");
    }
}

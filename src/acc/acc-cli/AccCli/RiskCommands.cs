using System.ComponentModel;
using AccCli;
using Cocona;

namespace AccCli;

/// <summary>acccli risk — surface high-friction and unstable nodes</summary>
internal sealed class RiskCommands(AccEngineClient client)
{
    [Command("friction")]
    [Description(
        "Find high-friction nodes — central chokepoints many others depend on. "
            + "These are the riskiest nodes to change. Ordered by friction score descending."
    )]
    public async Task HighFriction(
        [Option('m', Description = "Minimum friction score to include (0.0–1.0)")] double min,
        [Option('l', Description = "Maximum number of results to return")] int limit,
        GlobalOptions globals,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync(
            "acc.getHighFriction",
            new { minFriction = min, limit },
            globals,
            ct
        );
        Console.WriteLine(result ?? "[]");
    }

    [Command("unstable")]
    [Description(
        "Find unstable nodes — high-churn, low-coverage code most likely to introduce bugs. "
            + "Ordered by stability score ascending (least stable first)."
    )]
    public async Task Unstable(
        [Option('m', Description = "Maximum stability score to include (0.0–1.0)")] double max,
        [Option('l', Description = "Maximum number of results to return")] int limit,
        GlobalOptions globals,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync(
            "acc.getUnstable",
            new { maxStability = max, limit },
            globals,
            ct
        );
        Console.WriteLine(result ?? "[]");
    }
}

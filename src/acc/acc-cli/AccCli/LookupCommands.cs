using System.ComponentModel;
using AccCli;
using Cocona;

namespace AccCli;

/// <summary>acccli lookup — retrieve nodes and direct relationships</summary>
internal sealed class LookupCommands(AccEngineClient client)
{
    [Command("get")]
    [Description("Retrieve a single node by its unique ID with full metadata")]
    public async Task Get(
        [Argument(Description = "Node ID — format: '<File>:<Symbol>:<LineStart>'")] string nodeId,
        GlobalOptions globals,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync("acc.getNode", new { nodeId }, globals, ct);
        Console.WriteLine(result ?? "null");
    }

    [Command("relations")]
    [Description("Show one-hop incoming and outgoing edges for a node")]
    public async Task Relations(
        [Argument(Description = "Node ID — format: '<File>:<Symbol>:<LineStart>'")] string nodeId,
        [Option('s', Description = "Include AVEC dimensional scores")] bool scores,
        GlobalOptions globals,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync(
            "acc.queryRelations",
            new { nodeId, includeScores = scores },
            globals,
            ct
        );
        Console.WriteLine(result ?? "null");
    }
}

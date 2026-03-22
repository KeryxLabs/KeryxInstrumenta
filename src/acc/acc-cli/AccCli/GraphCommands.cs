using System.ComponentModel;
using AccCli;
using Cocona;

namespace AccCli;

/// <summary>acccli graph — traverse transitive dependencies</summary>
internal sealed class GraphCommands(AccEngineClient client)
{
    [Command("deps")]
    [Description("Traverse transitive dependencies of a node")]
    public async Task Deps(
        [Argument(Description = "Node ID — format: '<File>:<Symbol>:<LineStart>'")] string nodeId,
        [Option('d', Description = "Traversal direction: Incoming | Outgoing | Both")]
            string direction,
        [Option(Description = "Max traversal depth (-1 = unlimited)")] int depth,
        [Option('s', Description = "Include AVEC dimensional scores in results")] bool scores,
        GlobalOptions globals,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync(
            "acc.queryDependencies",
            new
            {
                nodeId,
                direction,
                maxDepth = depth,
                includeScores = scores,
            },
            globals,
            ct
        );
        Console.WriteLine(result ?? "[]");
    }
}

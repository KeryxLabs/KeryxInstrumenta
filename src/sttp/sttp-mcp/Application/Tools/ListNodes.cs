using ModelContextProtocol.Server;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using System.ComponentModel;

namespace SttpMcp.Application.Tools;

public sealed class ListNodesTool(INodeStore store)
{
    [McpServerTool(Name = "list_nodes"), Description("List stored temporal nodes ordered by newest first. Optionally filter by session ID.")]
    public async Task<ListNodesResult> ListAsync(
        [Description("Maximum nodes to return. Default 50, max 200.")]
        int limit = 50,
        [Description("Optional session ID filter. Omit to list across all sessions.")]
        string? sessionId = null,
        CancellationToken ct = default)
    {
        var cappedLimit = Math.Clamp(limit, 1, 200);
        var nodes = await store.ListNodesAsync(cappedLimit, sessionId, ct);

        return new ListNodesResult
        {
            Nodes = nodes.ToList(),
            Retrieved = nodes.Count
        };
    }
}

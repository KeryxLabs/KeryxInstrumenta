using ModelContextProtocol.Server;
using SttpMcp.Application.Services;
using SttpMcp.Domain.Models;
using System.ComponentModel;

namespace SttpMcp.Application.Tools;

public sealed class ListNodesTool(ContextQueryService service)
{
    [McpServerTool(Name = "list_nodes"), Description("List stored temporal nodes ordered by newest first. Optionally filter by session ID.")]
    public Task<ListNodesResult> ListAsync(
        [Description("Maximum nodes to return. Default 50, max 200.")]
        int limit = 50,
        [Description("Optional session ID filter. Omit to list across all sessions.")]
        string? sessionId = null,
        CancellationToken ct = default)
        => service.ListNodesAsync(limit, sessionId, ct);
}

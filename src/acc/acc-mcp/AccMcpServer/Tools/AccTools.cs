using System.ComponentModel;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for querying the ACC (AdaptiveCodecContext) engine.
/// The engine provides code graph traversal, AVEC dimensional metrics, and search
/// over an indexed codebase. Connect to localhost:9339 by default (configurable via
/// AccEngine:Host and AccEngine:Port in appsettings.json).
/// </summary>
[McpServerToolType]
internal class AccTools(AccEngineClient client)
{
    // -------------------------------------------------------------------------
    // Lookup
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "get_acc_node")]
    [Description(
        "Retrieve a single code node by its unique ID (e.g. 'UserService.cs:AuthenticateAsync:23'). "
            + "Returns full metadata: file location, lines of code, cyclomatic complexity, git history, "
            + "test coverage, graph degree, and AVEC dimensional scores (stability, logic, friction, autonomy). "
            + "Returns null when the node is not found."
    )]
    public async Task<string> GetNode(
        [Description(
            "Unique node identifier — format: '<File>:<Symbol>:<LineStart>' (e.g. 'UserService.cs:AuthenticateAsync:23')"
        )]
            string nodeId,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync("acc.getNode", new { nodeId }, ct);
        return result ?? "null";
    }

    [McpServerTool(Name = "query_relations")]
    [Description(
        "Get a node and its immediate direct relationships (one-hop incoming and outgoing edges). "
            + "Use this to quickly see what a symbol calls and what calls it, without deep traversal. "
            + "Set includeScores=true to include AVEC dimensional scores in the response."
    )]
    public async Task<string> QueryRelations(
        [Description("Unique node identifier — format: '<File>:<Symbol>:<LineStart>'")]
            string nodeId,
        [Description("Include AVEC dimensional scores in the result (default: false)")]
            bool includeScores = false,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync(
            "acc.queryRelations",
            new { nodeId, includeScores },
            ct
        );
        return result ?? "null";
    }

    // -------------------------------------------------------------------------
    // Graph traversal
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "query_dependencies")]
    [Description(
        "Perform a graph traversal to find transitive dependencies of a node. "
            + "direction='Incoming': who depends on this node (impact analysis — 'what breaks if I change this?'). "
            + "direction='Outgoing': what this node depends on (dependency analysis — 'what does this need?'). "
            + "direction='Both': full neighbourhood. "
            + "maxDepth=-1 means unlimited depth. Returns a JSON array of all reachable nodes."
    )]
    public async Task<string> QueryDependencies(
        [Description("Unique node identifier — format: '<File>:<Symbol>:<LineStart>'")]
            string nodeId,
        [Description("Traversal direction: 'Incoming', 'Outgoing', or 'Both' (default: 'Both')")]
            string direction = "Both",
        [Description("Maximum traversal depth. -1 = unlimited (default: -1)")] int maxDepth = -1,
        [Description("Include AVEC dimensional scores in each result node (default: false)")]
            bool includeScores = false,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync(
            "acc.queryDependencies",
            new
            {
                nodeId,
                direction,
                maxDepth,
                includeScores,
            },
            ct
        );
        return result ?? "[]";
    }

    // -------------------------------------------------------------------------
    // Pattern matching
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "query_patterns")]
    [Description(
        "Find code nodes whose AVEC profile is similar to a target profile using Euclidean distance in 4D space. "
            + "AVEC dimensions: stability (0=high churn, 1=stable), logic (0=simple, 1=complex), "
            + "friction (0=isolated, 1=central chokepoint), autonomy (0=highly coupled, 1=independent). "
            + "threshold ranges from 0.0 (any match) to 1.0 (near-identical profile). "
            + "Use this to find architectural patterns — 'show me code structurally similar to X'."
    )]
    public async Task<string> QueryPatterns(
        [Description("Target stability score (0.0–1.0)")] double stability,
        [Description("Target logic score (0.0–1.0)")] double logic,
        [Description("Target friction score (0.0–1.0)")] double friction,
        [Description("Target autonomy score (0.0–1.0)")] double autonomy,
        [Description("Similarity threshold (0.0=any, 1.0=identical). Default: 0.8")]
            double threshold = 0.8,
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
            ct
        );
        return result ?? "[]";
    }

    // -------------------------------------------------------------------------
    // Search
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "search_by_name")]
    [Description(
        "Search for code nodes by name using a case-insensitive substring match. "
            + "Returns up to 'limit' matching nodes with full metadata. "
            + "Use this to locate a symbol when you only know part of its name."
    )]
    public async Task<string> Search(
        [Description("Substring to search for in node names (case-insensitive)")] string name,
        [Description("Maximum number of results to return (default: 10)")] int limit = 10,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync("acc.search", new { name, limit }, ct);
        return result ?? "[]";
    }

    // -------------------------------------------------------------------------
    // Risk queries
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "get_high_friction_nodes")]
    [Description(
        "Find high-friction nodes — central chokepoints that many other nodes depend on. "
            + "These are the riskiest nodes to change: high incoming edges, high impact. "
            + "Results are ordered by friction score descending. "
            + "minFriction filters out nodes below the threshold (0.0–1.0, default: 0.7)."
    )]
    public async Task<string> GetHighFriction(
        [Description("Minimum friction score to include (0.0–1.0, default: 0.7)")]
            double minFriction = 0.7,
        [Description("Maximum number of results to return (default: 20)")] int limit = 20,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync("acc.getHighFriction", new { minFriction, limit }, ct);
        return result ?? "[]";
    }

    [McpServerTool(Name = "get_unstable_nodes")]
    [Description(
        "Find unstable nodes — high-churn, low-coverage code most likely to introduce bugs. "
            + "Low stability means: frequent commits, many contributors, and/or low test coverage. "
            + "Results are ordered by stability score ascending (least stable first). "
            + "maxStability filters to only nodes below the threshold (0.0–1.0, default: 0.4)."
    )]
    public async Task<string> GetUnstable(
        [Description("Maximum stability score to include (0.0–1.0, default: 0.4)")]
            double maxStability = 0.4,
        [Description("Maximum number of results to return (default: 20)")] int limit = 20,
        CancellationToken ct = default
    )
    {
        var result = await client.CallAsync("acc.getUnstable", new { maxStability, limit }, ct);
        return result ?? "[]";
    }

    // -------------------------------------------------------------------------
    // Stats
    // -------------------------------------------------------------------------

    [McpServerTool(Name = "get_project_stats")]
    [Description(
        "Retrieve aggregate statistics for the entire indexed codebase: "
            + "total node count and average AVEC scores (stability, logic, friction, autonomy) across all nodes. "
            + "Use this to get a high-level health overview before diving into specific nodes."
    )]
    public async Task<string> GetStats(CancellationToken ct = default)
    {
        var result = await client.CallAsync("acc.getStats", ct: ct);
        return result ?? "null";
    }
}

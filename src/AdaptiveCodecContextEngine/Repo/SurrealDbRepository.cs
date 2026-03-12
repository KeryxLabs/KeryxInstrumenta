using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models.Surreal;
using SurrealDb.Net;
using System.Threading.Channels;

public class SurrealDbRepository
{
    private readonly SurrealDbClient _db;
    private readonly AvecCalculator _avecCalculator;

    public SurrealDbRepository(string connectionString, AvecWeights weights)
    {
        _db = new SurrealDbClient(connectionString);
        _avecCalculator = new AvecCalculator(weights);
    }

    public async Task InitializeAsync()
    {
        await _db.Use("acc", "nodes");
        await CreateSchema();
    }

    private async Task CreateSchema()
    {
        // Define node table
        await _db.RawQuery(@"
            DEFINE TABLE IF NOT EXISTS node SCHEMAFULL;
            DEFINE FIELD node_id ON node TYPE string;
            DEFINE FIELD type ON node TYPE string;
            DEFINE FIELD language ON node TYPE string;
            DEFINE FIELD namespace ON node TYPE option<string>;
            DEFINE FIELD name ON node TYPE string;
            DEFINE FIELD signature ON node TYPE option<string>;
            DEFINE FIELD file_path ON node TYPE string;
            DEFINE FIELD line_start ON node TYPE int;
            DEFINE FIELD line_end ON node TYPE int;
            
            -- LSP metrics
            DEFINE FIELD lines_of_code ON node TYPE int DEFAULT 0;
            DEFINE FIELD cyclomatic_complexity ON node TYPE int DEFAULT 0;
            DEFINE FIELD parameters ON node TYPE int DEFAULT 0;
            DEFINE FIELD return_type ON node TYPE option<string>;
            
            -- Git history
            DEFINE FIELD git_created ON node TYPE option<datetime>;
            DEFINE FIELD git_last_modified ON node TYPE option<datetime>;
            DEFINE FIELD git_total_commits ON node TYPE int DEFAULT 0;
            DEFINE FIELD git_contributors ON node TYPE int DEFAULT 0;
            DEFINE FIELD git_avg_days_between_changes ON node TYPE float DEFAULT 0;
            DEFINE FIELD git_recent_frequency ON node TYPE string DEFAULT 'low';
            
            -- Test coverage
            DEFINE FIELD test_covered ON node TYPE bool DEFAULT false;
            DEFINE FIELD test_line_coverage ON node TYPE float DEFAULT 0;
            DEFINE FIELD test_branch_coverage ON node TYPE float DEFAULT 0;
            DEFINE FIELD test_count ON node TYPE int DEFAULT 0;
            
            -- Graph metrics (we'll calculate these separately)
            DEFINE FIELD incoming_edges ON node TYPE int DEFAULT 0;
            DEFINE FIELD outgoing_edges ON node TYPE int DEFAULT 0;
            DEFINE FIELD total_degree ON node TYPE int DEFAULT 0;
            
            -- AVEC scores
            DEFINE FIELD avec ON node TYPE option<object>;
            DEFINE FIELD avec_learned ON node TYPE option<object>;
            DEFINE FIELD avec_delta ON node TYPE option<object>;
            
            DEFINE INDEX unique_node_id ON node FIELDS node_id UNIQUE;
        ");

        // Define dependency edges
        await _db.RawQuery(@"
            DEFINE TABLE IF NOT EXISTS depends SCHEMAFULL TYPE RELATION IN node OUT node;
            DEFINE FIELD relationship_type ON depends TYPE string;
            DEFINE FIELD weight ON depends TYPE float DEFAULT 0.5;
            
            DEFINE INDEX depends_in_out ON depends FIELDS in, out;
        ");
    }

    public async Task UpsertNodeAsync(NodeUpdate update)
    {
        // Build the node record
        var nodeRecord = new Dictionary<string, object>
        {
            ["node_id"] = update.NodeId,
            ["type"] = update.Type,
            ["language"] = update.Language,
            ["name"] = update.Name,
            ["file_path"] = update.FilePath,
            ["line_start"] = update.LineStart,
            ["line_end"] = update.LineEnd
        };

        // Add optional fields
        if (update.Namespace != null) nodeRecord["namespace"] = update.Namespace;
        if (update.Signature != null) nodeRecord["signature"] = update.Signature;
        if (update.ReturnType != null) nodeRecord["return_type"] = update.ReturnType;

        // LSP metrics
        if (update.LinesOfCode.HasValue) nodeRecord["lines_of_code"] = update.LinesOfCode.Value;
        if (update.CyclomaticComplexity.HasValue) nodeRecord["cyclomatic_complexity"] = update.CyclomaticComplexity.Value;
        if (update.Parameters.HasValue) nodeRecord["parameters"] = update.Parameters.Value;

        // Git history
        if (update.GitHistory != null)
        {
            nodeRecord["git_created"] = update.GitHistory.Created;
            nodeRecord["git_last_modified"] = update.GitHistory.LastModified;
            nodeRecord["git_total_commits"] = update.GitHistory.TotalCommits;
            nodeRecord["git_contributors"] = update.GitHistory.Contributors;
            nodeRecord["git_avg_days_between_changes"] = update.GitHistory.AvgDaysBetweenChanges;
            nodeRecord["git_recent_frequency"] = update.GitHistory.RecentFrequency;
        }

        // Test coverage
        if (update.TestCoverage != null)
        {
            nodeRecord["test_covered"] = update.TestCoverage.Covered;
            nodeRecord["test_line_coverage"] = update.TestCoverage.LineCoverage;
            nodeRecord["test_branch_coverage"] = update.TestCoverage.BranchCoverage;
            nodeRecord["test_count"] = update.TestCoverage.TestCount;
        }

        // Upsert: insert if not exists, merge if exists
        var query = @"
            UPSERT node:⟨$node_id⟩ 
            CONTENT $data
            RETURN AFTER;
        ";

        var result = await _db.RawQuery(query, new
        {
            node_id = update.NodeId,
            data = nodeRecord
        });

        // After upsert, recalculate AVEC if we have enough data
        await RecalculateAvecAsync(update.NodeId);
    }

    private async Task RecalculateAvecAsync(string nodeId)
    {
        // Fetch the node with all metrics
        var query = "SELECT * FROM node:⟨$node_id⟩";
        var result = await _db.RawQuery(query, new { node_id = nodeId });

        var node = result.FirstOrDefault();
        if (node == null) return;

        // Extract metrics
        var metrics = new NodeMetrics
        {
            LinesOfCode = node.GetValueOrDefault<int>("lines_of_code"),
            CyclomaticComplexity = node.GetValueOrDefault<int>("cyclomatic_complexity"),
            Parameters = node.GetValueOrDefault<int>("parameters"),
            IncomingEdges = node.GetValueOrDefault<int>("incoming_edges"),
            OutgoingEdges = node.GetValueOrDefault<int>("outgoing_edges"),
            TotalDegree = node.GetValueOrDefault<int>("total_degree"),
            GitTotalCommits = node.GetValueOrDefault<int>("git_total_commits"),
            GitContributors = node.GetValueOrDefault<int>("git_contributors"),
            GitAvgDaysBetweenChanges = node.GetValueOrDefault<double>("git_avg_days_between_changes"),
            TestLineCoverage = node.GetValueOrDefault<double>("test_line_coverage"),
            TestBranchCoverage = node.GetValueOrDefault<double>("test_branch_coverage")
        };

        // Calculate AVEC
        var avec = _avecCalculator.Calculate(metrics);

        // Update AVEC field
        var updateQuery = @"
            UPDATE node:⟨$node_id⟩ 
            SET avec = $avec,
                avec.computed_at = time::now()
            RETURN AFTER;
        ";

        await _db.RawQuery(updateQuery, new
        {
            node_id = nodeId,
            avec = new
            {
                stability = avec.Stability,
                logic = avec.Logic,
                friction = avec.Friction,
                autonomy = avec.Autonomy
            }
        });

        // Calculate delta if avec_learned exists
        await CalculateDeltaAsync(nodeId);
    }

    private async Task CalculateDeltaAsync(string nodeId)
    {
        var query = @"
            UPDATE node:⟨$node_id⟩
            SET avec_delta = {
                stability: avec_learned.stability - avec.stability,
                logic: avec_learned.logic - avec.logic,
                friction: avec_learned.friction - avec.friction,
                autonomy: avec_learned.autonomy - avec.autonomy
            }
            WHERE avec_learned IS NOT NONE
            RETURN AFTER;
        ";

        await _db.RawQuery(query, new { node_id = nodeId });
    }

    public async Task UpsertDependencyAsync(string fromNodeId, string toNodeId, string relationshipType)
    {
        var weight = relationshipType switch
        {
            "inherits" => 1.0,
            "calls" => 0.7,
            "imports" => 0.5,
            "references" => 0.3,
            _ => 0.5
        };

        var query = @"
            RELATE node:⟨$from⟩->depends->node:⟨$to⟩
            SET relationship_type = $type,
                weight = $weight;
        ";

        await _db.RawQuery(query, new
        {
            from = fromNodeId,
            to = toNodeId,
            type = relationshipType,
            weight
        });

        // Update edge counts for both nodes
        await UpdateEdgeCountsAsync(fromNodeId);
        await UpdateEdgeCountsAsync(toNodeId);

        // Recalculate AVEC for both (friction/autonomy depend on edges)
        await RecalculateAvecAsync(fromNodeId);
        await RecalculateAvecAsync(toNodeId);
    }

    private async Task UpdateEdgeCountsAsync(string nodeId)
    {
        var query = @"
            UPDATE node:⟨$node_id⟩
            SET incoming_edges = (SELECT count() FROM depends WHERE out = $parent.id)[0].count,
                outgoing_edges = (SELECT count() FROM depends WHERE in = $parent.id)[0].count,
                total_degree = incoming_edges + outgoing_edges;
        ";

        await _db.RawQuery(query, new { node_id = nodeId });
    }

    public async Task<NodeQueryResult?> QueryRelationsAsync(string nodeId, bool includeScores = false)
    {
        var query = includeScores
            ? "SELECT *, ->depends.* as outgoing, <-depends.* as incoming FROM node:⟨$node_id⟩"
            : "SELECT node_id, name, type, ->depends.out as outgoing, <-depends.in as incoming FROM node:⟨$node_id⟩";

        var result = await _db.RawQuery(query, new { node_id = nodeId });
        return result.FirstOrDefault() as NodeQueryResult;
    }

    public async Task<List<NodeQueryResult>> QueryDependenciesAsync(
        string nodeId,
        DependencyDirection direction = DependencyDirection.Both,
        int maxDepth = -1,
        bool includeScores = false)
    {
        var traversal = direction switch
        {
            DependencyDirection.Incoming => "<-depends<-",
            DependencyDirection.Outgoing => "->depends->",
            DependencyDirection.Both => "<->depends<->",
            _ => "<->depends<->"
        };

        var depthClause = maxDepth > 0 ? $"..{maxDepth}" : "..";

        var query = $@"
            SELECT * FROM node:⟨$node_id⟩{traversal}node{depthClause}
            {(includeScores ? "" : "OMIT avec, avec_learned")}
        ";

        var result = await _db.RawQuery(query, new { node_id = nodeId });
        return result.Cast<NodeQueryResult>().ToList();
    }

    public async Task<List<NodeQueryResult>> QueryPatternsAsync(
        AvecScores targetProfile,
        double threshold = 0.8,
        bool includeScores = true)
    {
        // Calculate distance in 4D space using Euclidean distance
        var query = @"
            SELECT *,
                   math::sqrt(
                       math::pow(avec.stability - $stability, 2) +
                       math::pow(avec.logic - $logic, 2) +
                       math::pow(avec.friction - $friction, 2) +
                       math::pow(avec.autonomy - $autonomy, 2)
                   ) AS distance
            FROM node
            WHERE avec IS NOT NONE
              AND distance <= $threshold
            ORDER BY distance ASC
            LIMIT 50;
        ";

        var result = await _db.RawQuery(query, new
        {
            stability = targetProfile.Stability,
            logic = targetProfile.Logic,
            friction = targetProfile.Friction,
            autonomy = targetProfile.Autonomy,
            threshold = 1 - threshold // Convert similarity to distance
        });

        return result.Cast<NodeQueryResult>().ToList();
    }
    public async Task<string?> FindNodeAtLocationAsync(string filePath, int line)
    {
        var query = @"
        SELECT node_id FROM node 
        WHERE file_path = $path 
          AND line_start <= $line 
          AND line_end >= $line
        LIMIT 1;
    ";

        var result = await _db.RawQuery(query, new { path = filePath, line });
        return result.FirstOrDefault()?.GetValue<string>("node_id");
    }

    public async Task<string?> FindNodeByNameAsync(string name)
    {
        var query = "SELECT node_id FROM node WHERE name = $name LIMIT 1;";
        var result = await _db.RawQuery(query, new { name });
        return result.FirstOrDefault()?.GetValue<string>("node_id");
    }
}

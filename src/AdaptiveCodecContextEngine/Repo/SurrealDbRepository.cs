using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models.Surreal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SurrealDb.Net;

public class SurrealDbRepository
{
    private readonly ISurrealDbClient _db;
    private readonly AvecCalculator _avecCalculator;
    private readonly ILogger<SurrealDbRepository> _logger;
    private bool _schemaInitialized = false;
    
    public SurrealDbRepository(ISurrealDbClient client, IConfiguration configuration, ILogger<SurrealDbRepository> logger)
    {
        _db = client;
        _avecCalculator = new AvecCalculator(configuration.Get<AvecWeights>()!);
        _logger = logger;
    }
    
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing SurrealDB schema...");
        await _db.Use("acc", "nodes");
        
        if (!_schemaInitialized)
        {
            await CreateSchema();
            await CreateEvents();
            _schemaInitialized = true;
            _logger.LogInformation("SurrealDB schema initialized.");
        }
    }
    
    private async Task CreateSchema()
    {
        await _db.RawQuery(@"
            DEFINE TABLE IF NOT EXISTS node SCHEMAFULL;
            
            -- Core fields
            DEFINE FIELD IF NOT EXISTS node_id ON node TYPE string ASSERT $value != NONE;
            DEFINE FIELD IF NOT EXISTS type ON node TYPE string ASSERT $value != NONE;
            DEFINE FIELD IF NOT EXISTS language ON node TYPE string ASSERT $value != NONE;
            DEFINE FIELD IF NOT EXISTS namespace ON node TYPE option<string>;
            DEFINE FIELD IF NOT EXISTS name ON node TYPE string ASSERT $value != NONE;
            DEFINE FIELD IF NOT EXISTS signature ON node TYPE option<string>;
            DEFINE FIELD IF NOT EXISTS file_path ON node TYPE string ASSERT $value != NONE;
            DEFINE FIELD IF NOT EXISTS line_start ON node TYPE int ASSERT $value != NONE;
            DEFINE FIELD IF NOT EXISTS line_end ON node TYPE int ASSERT $value != NONE;
            DEFINE FIELD IF NOT EXISTS return_type ON node TYPE option<string>;
            
            -- LSP metrics (defaults)
            DEFINE FIELD IF NOT EXISTS lines_of_code ON node TYPE int DEFAULT 0;
            DEFINE FIELD IF NOT EXISTS cyclomatic_complexity ON node TYPE int DEFAULT 0;
            DEFINE FIELD IF NOT EXISTS parameters ON node TYPE int DEFAULT 0;
            
            -- Git history
            DEFINE FIELD IF NOT EXISTS git_created ON node TYPE option<datetime>;
            DEFINE FIELD IF NOT EXISTS git_last_modified ON node TYPE option<datetime>;
            DEFINE FIELD IF NOT EXISTS git_total_commits ON node TYPE int DEFAULT 0;
            DEFINE FIELD IF NOT EXISTS git_contributors ON node TYPE int DEFAULT 0;
            DEFINE FIELD IF NOT EXISTS git_avg_days_between_changes ON node TYPE float DEFAULT 0.0;
            DEFINE FIELD IF NOT EXISTS git_recent_frequency ON node TYPE string DEFAULT 'low';
            
            -- Test coverage
            DEFINE FIELD IF NOT EXISTS test_covered ON node TYPE bool DEFAULT false;
            DEFINE FIELD IF NOT EXISTS test_line_coverage ON node TYPE float DEFAULT 0.0;
            DEFINE FIELD IF NOT EXISTS test_branch_coverage ON node TYPE float DEFAULT 0.0;
            DEFINE FIELD IF NOT EXISTS test_count ON node TYPE int DEFAULT 0;
            
            -- Graph metrics
            DEFINE FIELD IF NOT EXISTS incoming_edges ON node TYPE int DEFAULT 0;
            DEFINE FIELD IF NOT EXISTS outgoing_edges ON node TYPE int DEFAULT 0;
            DEFINE FIELD IF NOT EXISTS total_degree ON node TYPE int DEFAULT 0;
            
            -- AVEC
            DEFINE FIELD IF NOT EXISTS avec ON node TYPE option<object>;
            DEFINE FIELD IF NOT EXISTS avec_learned ON node TYPE option<object>;
            DEFINE FIELD IF NOT EXISTS avec_delta ON node TYPE option<object>;
            
            -- Timestamps for idempotency
            DEFINE FIELD IF NOT EXISTS created_at ON node TYPE datetime DEFAULT time::now();
            DEFINE FIELD IF NOT EXISTS updated_at ON node TYPE datetime DEFAULT time::now();
            
            DEFINE INDEX IF NOT EXISTS unique_node_id ON node FIELDS node_id UNIQUE;
            DEFINE INDEX IF NOT EXISTS node_file_path ON node FIELDS file_path;
            DEFINE INDEX IF NOT EXISTS node_type ON node FIELDS type;
        ");
        
        await _db.RawQuery(@"
            DEFINE TABLE IF NOT EXISTS depends SCHEMAFULL TYPE RELATION IN node OUT node;
            DEFINE FIELD IF NOT EXISTS relationship_type ON depends TYPE string ASSERT $value != NONE;
            DEFINE FIELD IF NOT EXISTS weight ON depends TYPE float DEFAULT 0.5;
            DEFINE FIELD IF NOT EXISTS created_at ON depends TYPE datetime DEFAULT time::now();
            
            DEFINE INDEX IF NOT EXISTS depends_in_out ON depends FIELDS in, out, relationship_type UNIQUE;
        ");
    }
    
    private async Task CreateEvents()
    {
        // Event: Auto-update timestamp on node update
        await _db.RawQuery(@"
            DEFINE EVENT update_timestamp ON TABLE node WHEN $event = 'UPDATE' THEN {
                UPDATE $after.id SET updated_at = time::now()
            };
        ");
        
        // Event: Recalculate AVEC when metrics change
        await _db.RawQuery(@"
            DEFINE EVENT recalculate_avec ON TABLE node 
            WHEN $event IN ['CREATE', 'UPDATE'] 
            AND (
                $before.lines_of_code != $after.lines_of_code OR
                $before.cyclomatic_complexity != $after.cyclomatic_complexity OR
                $before.parameters != $after.parameters OR
                $before.incoming_edges != $after.incoming_edges OR
                $before.outgoing_edges != $after.outgoing_edges OR
                $before.git_total_commits != $after.git_total_commits OR
                $before.test_line_coverage != $after.test_line_coverage
            )
            THEN {
                -- Trigger external recalculation
                -- SurrealDB events can't call functions, so we mark for recalc
                UPDATE $after.id SET avec.needs_recalc = true
            };
        ");
        
        // Event: Update edge counts when dependency created/deleted
        await _db.RawQuery(@"
            DEFINE EVENT update_edge_counts_on_create ON TABLE depends 
            WHEN $event = 'CREATE' THEN {
                UPDATE $after.in SET 
                    outgoing_edges = (SELECT count() FROM depends WHERE in = $parent.id)[0].count ?? 0,
                    total_degree = incoming_edges + outgoing_edges;
                    
                UPDATE $after.out SET 
                    incoming_edges = (SELECT count() FROM depends WHERE out = $parent.id)[0].count ?? 0,
                    total_degree = incoming_edges + outgoing_edges;
            };
        ");
        
        await _db.RawQuery(@"
            DEFINE EVENT update_edge_counts_on_delete ON TABLE depends 
            WHEN $event = 'DELETE' THEN {
                UPDATE $before.in SET 
                    outgoing_edges = (SELECT count() FROM depends WHERE in = $parent.id)[0].count ?? 0,
                    total_degree = incoming_edges + outgoing_edges;
                    
                UPDATE $before.out SET 
                    incoming_edges = (SELECT count() FROM depends WHERE out = $parent.id)[0].count ?? 0,
                    total_degree = incoming_edges + outgoing_edges;
            };
        ");
        
        // Event: Calculate delta when avec_learned changes
        await _db.RawQuery(@"
            DEFINE EVENT calculate_delta ON TABLE node 
            WHEN $event = 'UPDATE' AND $after.avec_learned IS NOT NONE AND $after.avec IS NOT NONE
            THEN {
                UPDATE $after.id SET avec_delta = {
                    stability: $after.avec_learned.stability - $after.avec.stability,
                    logic: $after.avec_learned.logic - $after.avec.logic,
                    friction: $after.avec_learned.friction - $after.avec.friction,
                    autonomy: $after.avec_learned.autonomy - $after.avec.autonomy
                }
            };
        ");
    }
    
    public async Task<NodeDto?> UpsertNodeAsync(NodeUpdate update)
    {
        // Build DTO from update
        var dto = new NodeDto
        {
            NodeId = update.NodeId,
            Type = update.Type,
            Language = update.Language,
            Namespace = update.Namespace,
            Name = update.Name,
            Signature = update.Signature,
            FilePath = update.FilePath,
            LineStart = update.LineStart,
            LineEnd = update.LineEnd,
            ReturnType = update.ReturnType,
            
            LinesOfCode = update.LinesOfCode ?? 0,
            CyclomaticComplexity = update.CyclomaticComplexity ?? 0,
            Parameters = update.Parameters ?? 0,
            
            GitCreated = update.GitHistory?.Created,
            GitLastModified = update.GitHistory?.LastModified,
            GitTotalCommits = update.GitHistory?.TotalCommits ?? 0,
            GitContributors = update.GitHistory?.Contributors ?? 0,
            GitAvgDaysBetweenChanges = update.GitHistory?.AvgDaysBetweenChanges ?? 0,
            GitRecentFrequency = update.GitHistory?.RecentFrequency ?? "low",
            
            TestCovered = update.TestCoverage?.Covered ?? false,
            TestLineCoverage = update.TestCoverage?.LineCoverage ?? 0,
            TestBranchCoverage = update.TestCoverage?.BranchCoverage ?? 0,
            TestCount = update.TestCoverage?.TestCount ?? 0
        };
        
        // Upsert with MERGE to preserve existing data
        var query = @"
            UPSERT node:⟨$node_id⟩
            MERGE $data
            RETURN AFTER;
        ";
        
        var upsertParams = new Dictionary<string, object?>
        {
            ["node_id"] = update.NodeId,
            ["data"] = dto
        };
        var result = await _db.RawQuery(query, upsertParams);
        var records = result.GetValue<List<NodeDto>>(0);
        var node = records?.FirstOrDefault();
        
        // Check if AVEC needs recalculation
        if (node != null)
        {
            _logger.LogDebug("Upserted node {NodeId}", node.NodeId);
            await RecalculateAvecIfNeededAsync(node.NodeId);
        }
        
        return node;
    }
    
    private async Task RecalculateAvecIfNeededAsync(string nodeId)
    {
        // Check if recalc needed
        var checkQuery = "SELECT avec.needs_recalc as needs_recalc FROM node:⟨$node_id⟩";
        var checkParams = new Dictionary<string, object?>
        {
            ["node_id"] = nodeId
        };
        var checkResult = await _db.RawQuery(checkQuery, checkParams);
        var checkRecords = checkResult.GetValue<List<AvecStatusDto>>(0);
        var needsRecalc = checkRecords?.FirstOrDefault()?.NeedsRecalc ?? false;
        
        if (!needsRecalc) return;
        
        _logger.LogDebug("Recalculating AVEC for node {NodeId}", nodeId);
        
        // Fetch full node
        var nodeQuery = "SELECT * FROM node:⟨$node_id⟩";
        var nodeParams = new Dictionary<string, object?>
        {
            ["node_id"] = nodeId
        };
        var nodeResult = await _db.RawQuery(nodeQuery, nodeParams);
        var nodeRecords = nodeResult.GetValue<List<NodeDto>>(0);
        var node = nodeRecords?.FirstOrDefault();
        
        if (node == null) return;
        
        // Calculate AVEC
        var metrics = new NodeMetrics
        {
            LinesOfCode = node.LinesOfCode,
            CyclomaticComplexity = node.CyclomaticComplexity,
            Parameters = node.Parameters,
            IncomingEdges = node.IncomingEdges,
            OutgoingEdges = node.OutgoingEdges,
            TotalDegree = node.TotalDegree,
            GitTotalCommits = node.GitTotalCommits,
            GitContributors = node.GitContributors,
            GitAvgDaysBetweenChanges = node.GitAvgDaysBetweenChanges,
            TestLineCoverage = node.TestLineCoverage,
            TestBranchCoverage = node.TestBranchCoverage
        };
        
        var avec = _avecCalculator.Calculate(metrics);
        
        // Update AVEC
        var updateQuery = @"
            UPDATE node:⟨$node_id⟩
            SET avec = $avec
            RETURN AFTER;
        ";
        
        var avecDto = new AvecDto
        {
            Stability = avec.Stability,
            Logic = avec.Logic,
            Friction = avec.Friction,
            Autonomy = avec.Autonomy,
            ComputedAt = DateTime.UtcNow
        };
        
        var updateParams = new Dictionary<string, object?>
        {
            ["node_id"] = nodeId,
            ["avec"] = avecDto
        };
        await _db.RawQuery(updateQuery, updateParams);
    }
    
    public async Task<DependencyDto?> UpsertDependencyAsync(string fromNodeId, string toNodeId, string relationshipType)
    {
        var weight = relationshipType switch
        {
            "inherits" => 1.0,
            "implements" => 1.0,
            "calls" => 0.7,
            "imports" => 0.5,
            "references" => 0.3,
            _ => 0.5
        };
        
        // Idempotent: use UNIQUE index on (in, out, relationship_type)
        var query = @"
            RELATE node:⟨$from⟩->depends->node:⟨$to⟩
            SET relationship_type = $type,
                weight = $weight
            RETURN AFTER;
        ";
        
        var depParams = new Dictionary<string, object?>
        {
            ["from"] = fromNodeId,
            ["to"] = toNodeId,
            ["type"] = relationshipType,
            ["weight"] = weight
        };
        var result = await _db.RawQuery(query, depParams);
        
        var records = result.GetValue<List<DependencyDto>>(0);
        return records?.FirstOrDefault();
    }
    
    public async Task<NodeDto?> QueryRelationsAsync(string nodeId, bool includeScores = false)
    {
        var fields = includeScores ? "*" : "node_id, name, type, file_path";
        
        var query = $@"
            SELECT {fields},
                   (SELECT out.* FROM ->depends) as outgoing,
                   (SELECT in.* FROM <-depends) as incoming
            FROM node:⟨$node_id⟩;
        ";
        
        var relParams = new Dictionary<string, object?>
        {
            ["node_id"] = nodeId
        };
        var result = await _db.RawQuery(query, relParams);
        var records = result.GetValue<List<NodeDto>>(0);
        return records?.FirstOrDefault();
    }
    
    public async Task<List<NodeDto>> QueryDependenciesAsync(
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
        var fields = includeScores ? "*" : "node_id, name, type, file_path";
        
        var query = $@"
            SELECT {fields}
            FROM node:⟨$node_id⟩{traversal}node{depthClause};
        ";
        
        var depQueryParams = new Dictionary<string, object?>
        {
            ["node_id"] = nodeId
        };
        var result = await _db.RawQuery(query, depQueryParams);
        var records = result.GetValue<List<NodeDto>>(0);
        return records ?? new List<NodeDto>();
    }
    
    public async Task<List<NodeDto>> QueryPatternsAsync(
        AvecScores targetProfile,
        double threshold = 0.8)
    {
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
              AND distance <= $max_distance
            ORDER BY distance ASC
            LIMIT 50;
        ";
        
        var maxDistance = 1 - threshold;
        var patternParams = new Dictionary<string, object?>
        {
            ["stability"] = targetProfile.Stability,
            ["logic"] = targetProfile.Logic,
            ["friction"] = targetProfile.Friction,
            ["autonomy"] = targetProfile.Autonomy,
            ["max_distance"] = maxDistance
        };
        var result = await _db.RawQuery(query, patternParams);
        
        var records = result.GetValue<List<NodeDto>>(0);
        return records ?? new List<NodeDto>();
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
        
        var locParams = new Dictionary<string, object?>
        {
            ["path"] = filePath,
            ["line"] = line
        };
        var result = await _db.RawQuery(query, locParams);
        var records = result.GetValue<List<NodeIdDto>>(0);
        return records?.FirstOrDefault()?.NodeId;
    }
    
    public async Task<string?> FindNodeByNameAsync(string name)
    {
        var query = "SELECT node_id FROM node WHERE name = $name LIMIT 1;";
        var nameParams = new Dictionary<string, object?>
        {
            ["name"] = name
        };
        var result = await _db.RawQuery(query, nameParams);
        var records = result.GetValue<List<NodeIdDto>>(0);
        return records?.FirstOrDefault()?.NodeId;
    }
    public async Task<List<NodeDto>> SearchByNameAsync(string name, int limit = 10)
{
    var query = @"
        SELECT * FROM node
        WHERE name CONTAINS $name
        LIMIT $limit;
    ";
    
    var result = await _db.RawQuery(query, new Dictionary<string, object?> { ["name"] = name, ["limit"] = limit });
    var records = result.GetValue<List<NodeDto>>(0);
    return records ?? new List<NodeDto>();
}

public async Task<List<NodeDto>> GetNodesWithHighFrictionAsync(double minFriction = 0.7, int limit = 20)
{
    var query = @"
        SELECT * FROM node
        WHERE avec IS NOT NONE
          AND avec.friction >= $min_friction
        ORDER BY avec.friction DESC
        LIMIT $limit;
    ";
    
    var result = await _db.RawQuery(query, new Dictionary<string, object?> { ["min_friction"] = minFriction, ["limit"] = limit });
    var records = result.GetValue<List<NodeDto>>(0);
    return records ?? new List<NodeDto>();
}

public async Task<List<NodeDto>> GetUnstableNodesAsync(double maxStability = 0.4, int limit = 20)
{
    var query = @"
        SELECT * FROM node
        WHERE avec IS NOT NONE
          AND avec.stability <= $max_stability
        ORDER BY avec.stability ASC
        LIMIT $limit;
    ";
    
    var result = await _db.RawQuery(query, new Dictionary<string, object?> { ["max_stability"] = maxStability, ["limit"] = limit });
    var records = result.GetValue<List<NodeDto>>(0);
    return records ?? new List<NodeDto>();
}

public async Task<ProjectStatsDto> GetProjectStatsAsync()
{
    var query = @"
        SELECT 
            count() as total_nodes,
            math::mean(avec.stability) as avg_stability,
            math::mean(avec.logic) as avg_logic,
            math::mean(avec.friction) as avg_friction,
            math::mean(avec.autonomy) as avg_autonomy
        FROM node
        WHERE avec IS NOT NONE
        GROUP ALL;
    ";
    
    var result = await _db.RawQuery(query, null);
    var records = result.GetValue<List<ProjectStatsDto>>(0);
    return records?.FirstOrDefault() ?? new ProjectStatsDto();
}
}
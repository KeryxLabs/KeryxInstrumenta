using AdaptiveCodecContextEngine.Diagnostics;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models.Surreal;
using Dahomey.Cbor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SurrealDb.Net;
using SurrealDb.Net.Models.Response;

public class SurrealDbRepository
{
    private readonly ISurrealDbClient _db;
    private readonly AvecCalculator _avecCalculator;
    private readonly ILogger<SurrealDbRepository> _logger;
    private bool _schemaInitialized = false;
    private SurrealDbSettings _settings;

    public SurrealDbRepository(
        ISurrealDbClient client,
        IConfiguration configuration,
        ILogger<SurrealDbRepository> logger
    )
    {
        _db = client;
        _avecCalculator = new AvecCalculator(
            configuration.Get<AvecWeights>()
                ?? throw new InvalidOperationException("Avec configuration missing")
        );
        _logger = logger;
        _settings =
            configuration.GetSection("SurrealDb").Get<SurrealDbSettings>()
            ?? throw new InvalidOperationException("SurrealDb configuration missing");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing SurrealDB schema...");
        await _db.Use(_settings.Namespace, _settings.Database, cancellationToken);

        if (!_schemaInitialized)
        {
            await CreateSchema(cancellationToken);
            await CreateEvents(cancellationToken);
            _schemaInitialized = true;
            _logger.LogInformation("SurrealDB schema initialized.");
        }
    }

    private async Task CreateSchema(CancellationToken cancellationToken = default)
    {
        var response = await _db.RawQuery(
            @"
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
            
            -- AVEC (flat scalar fields)
            DEFINE FIELD IF NOT EXISTS avec_stability ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_logic ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_friction ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_autonomy ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_computed_at ON node TYPE option<datetime>;
            DEFINE FIELD IF NOT EXISTS avec_needs_recalc ON node TYPE bool DEFAULT false;

            DEFINE FIELD IF NOT EXISTS avec_learned_stability ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_learned_logic ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_learned_friction ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_learned_autonomy ON node TYPE option<float>;

            DEFINE FIELD IF NOT EXISTS avec_delta_stability ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_delta_logic ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_delta_friction ON node TYPE option<float>;
            DEFINE FIELD IF NOT EXISTS avec_delta_autonomy ON node TYPE option<float>;

            -- Timestamps for idempotency
            DEFINE FIELD IF NOT EXISTS created_at ON node TYPE datetime DEFAULT time::now();
            DEFINE FIELD IF NOT EXISTS updated_at ON node TYPE datetime DEFAULT time::now();
            
            
            DEFINE INDEX IF NOT EXISTS node_file_path ON node FIELDS file_path;
            DEFINE INDEX IF NOT EXISTS node_type ON node FIELDS type;
        "
        );

        _logger.LogPossibleDbWriteError(response);

        response = await _db.RawQuery(
            @"
            DEFINE TABLE IF NOT EXISTS depends SCHEMAFULL TYPE RELATION IN node OUT node;
            DEFINE FIELD IF NOT EXISTS relationship_type ON depends TYPE string ASSERT $value != NONE;
            DEFINE FIELD IF NOT EXISTS weight ON depends TYPE float DEFAULT 0.5;
            DEFINE FIELD IF NOT EXISTS created_at ON depends TYPE datetime DEFAULT time::now();
            
            DEFINE INDEX IF NOT EXISTS depends_in_out ON depends FIELDS in, out, relationship_type UNIQUE;
        ",
            cancellationToken: cancellationToken
        );

        _logger.LogPossibleDbWriteError(response);
    }

    private async Task CreateEvents(CancellationToken cancellationToken = default)
    {
        // Event: Auto-update timestamp on node update
        var response = await _db.RawQuery(
            @"
            DEFINE EVENT IF NOT EXISTS update_timestamp ON TABLE node WHEN $event = 'UPDATE' THEN {
                    IF $before.updated_at == $after.updated_at THEN
                        UPDATE $after SET updated_at = time::now()
                    END
            };
        ",
            cancellationToken: cancellationToken
        );

        _logger.LogPossibleDbWriteError(response);
        // Event: Recalculate AVEC when metrics change
        response = await _db.RawQuery(
            @"
            DEFINE EVENT IF NOT EXISTS recalculate_avec ON TABLE node 
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
                UPDATE $after.id SET avec_needs_recalc = true
            };
        ",
            cancellationToken: cancellationToken
        );

        _logger.LogPossibleDbWriteError(response);
        // Event: Update edge counts when dependency created/deleted
        response = await _db.RawQuery(
            @"
            DEFINE EVENT IF NOT EXISTS update_edge_counts_on_create ON TABLE depends 
            WHEN $event = 'CREATE' THEN {
                UPDATE $after.in SET 
                    outgoing_edges = (SELECT count() FROM depends WHERE in = $parent.id)[0].count ?? 0,
                    total_degree = incoming_edges + outgoing_edges;
                    
                UPDATE $after.out SET 
                    incoming_edges = (SELECT count() FROM depends WHERE out = $parent.id)[0].count ?? 0,
                    total_degree = incoming_edges + outgoing_edges;
            };
        ",
            cancellationToken: cancellationToken
        );

        _logger.LogPossibleDbWriteError(response);
        response = await _db.RawQuery(
            @"
            DEFINE EVENT IF NOT EXISTS update_edge_counts_on_delete ON TABLE depends 
            WHEN $event = 'DELETE' THEN {
                UPDATE $before.in SET 
                    outgoing_edges = (SELECT count() FROM depends WHERE in = $parent.id)[0].count ?? 0,
                    total_degree = incoming_edges + outgoing_edges;
                    
                UPDATE $before.out SET 
                    incoming_edges = (SELECT count() FROM depends WHERE out = $parent.id)[0].count ?? 0,
                    total_degree = incoming_edges + outgoing_edges;
            };
        ",
            cancellationToken: cancellationToken
        );

        _logger.LogPossibleDbWriteError(response);
        // Event: Calculate delta when avec_learned changes
        response = await _db.RawQuery(
            @"
            DEFINE EVENT IF NOT EXISTS calculate_delta ON TABLE node 
            WHEN $event = 'UPDATE' AND $after.avec_learned_stability IS NOT NONE AND $after.avec_stability IS NOT NONE
            THEN {
                UPDATE $after.id SET
                    avec_delta_stability = $after.avec_learned_stability - $after.avec_stability,
                    avec_delta_logic = $after.avec_learned_logic - $after.avec_logic,
                    avec_delta_friction = $after.avec_learned_friction - $after.avec_friction,
                    avec_delta_autonomy = $after.avec_learned_autonomy - $after.avec_autonomy
            };
        ",
            cancellationToken: cancellationToken
        );

        _logger.LogPossibleDbWriteError(response);
    }

    public async Task<List<NodeDto>> UpsertNodeListAsync(
        IEnumerable<NodeUpdate> updates,
        CancellationToken cancellationToken = default
    )
    {
        var data = updates.AsInsertable();

        var parameters = new Dictionary<string, object?> { { "data", data } };

        var result = await _db.RawQuery(
            "INSERT INTO node $data ON DUPLICATE KEY UPDATE updated_at = time::now() RETURN AFTER;",
            parameters,
            cancellationToken: cancellationToken
        );

        _logger.LogPossibleDbWriteError(result);

        var records = result.GetValue<List<NodeRecord>>(0);
        var rawRecords = result.GetValue<List<object?>>(0);
        _logger.LogInformation("Raw JSON: {Json}", JsonSerializer.Serialize(rawRecords));
        if (records?.Count > 0)
        {
            await BatchRecalculateAsync(
                updates.Select(u => u.NodeId),
                cancellationToken: cancellationToken
            );
        }

        return records is not null ? [.. records!.Select(MapToDto)] : [];
    }

    public async Task BatchRecalculateAsync(
        IEnumerable<string> nodeIds,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Batching calculation for {nodeIds}", string.Join(',', nodeIds));
        // Use 'IN' to get every node in one round trip
        var query =
            @"SELECT id as Id,
                    node_id as NodeId,
                    type as Type,
                    language as Language,
                    namespace as Namespace,
                    name as Name,
                    signature as Signature,
                    file_path as FilePath,
                    line_start as LineStart,
                    line_end as LineEnd,
                    return_type as ReturnType,
                    lines_of_code as LinesOfCode,
                    cyclomatic_complexity as CyclomaticComplexity,
                    parameters as Parameters,
                    git_created as GitCreated,
                    git_last_modified as GitLastModified,
                    git_total_commits as GitTotalCommits,
                    git_contributors as GitContributors,
                    git_avg_days_between_changes as GitAvgDaysBetweenChanges,
                    git_recent_frequency as GitRecentFrequency,
                    test_covered as TestCovered,
                    test_line_coverage as TestLineCoverage,
                    test_branch_coverage as TestBranchCoverage,
                    test_count as TestCount,
                    incoming_edges as IncomingEdges,
                    outgoing_edges as OutgoingEdges,
                    total_degree as TotalDegree,
                    avec_stability as AvecStability,
                    avec_logic as AvecLogic,
                    avec_friction as AvecFriction,
                    avec_autonomy as AvecAutonomy,
                    avec_computed_at as AvecComputedAt,
                    avec_needs_recalc as AvecNeedsRecalc,
                    avec_learned_stability as AvecLearnedStability,
                    avec_learned_logic as AvecLearnedLogic,
                    avec_learned_friction as AvecLearnedFriction,
                    avec_learned_autonomy as AvecLearnedAutonomy,
                    avec_delta_stability as AvecDeltaStability,
                    avec_delta_logic as AvecDeltaLogic,
                    avec_delta_friction as AvecDeltaFriction,
                    avec_delta_autonomy as AvecDeltaAutonomy
                    FROM node WHERE node_id IN $ids AND avec_needs_recalc = true;";
        var results = await _db.RawQuery(
            query,
            new Dictionary<string, object?> { { "ids", nodeIds.Select(id => $"{id}").ToList() } },
            cancellationToken: cancellationToken
        );

        List<NodeRecord>? nodes = null;

        try
        {
            nodes = results.GetValue<List<NodeRecord>>(0) ?? new List<NodeRecord>();
        }
        catch (CborException ex)
        {
            _logger.LogError(ex, "Unable to parse dawg");
        }

        _logger.LogPossibleDbReadError(results);

        if (nodes is null)
        {
            _logger.LogInformation("Nothing returned from query.");
            return;
        }

        if (!nodes.Any())
        {
            _logger.LogInformation("No nodes returned from query.");

            return;
        }

        var updates = new List<object>();
        _logger.LogInformation("We got something...");
        foreach (var node in nodes)
        {
            // 2. Perform the C# calculation in memory (Super Fast)
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
                TestBranchCoverage = node.TestBranchCoverage,
            };
            var avec = _avecCalculator.Calculate(metrics);

            _logger.LogAvecCalculation(
                node.Name,
                avec.Stability,
                avec.Logic,
                avec.Friction,
                avec.Autonomy
            );

            updates.Add(
                new
                {
                    id = node.Id, // Ensure this is the record ID (node:hash)
                    avec_stability = avec.Stability,
                    avec_logic = avec.Logic,
                    avec_friction = avec.Friction,
                    avec_autonomy = avec.Autonomy,
                    avec_computed_at = DateTime.UtcNow,
                    avec_needs_recalc = false,
                }
            );
        }

        // Use INSERT with the array, which SurrealDB optimizes as a bulk-patch
        var avecQuery =
            @"
                        FOR $u IN $updates {
                            UPDATE $u.id MERGE {
                                avec_stability: $u.avec_stability,
                                avec_logic: $u.avec_logic,
                                avec_friction: $u.avec_friction,
                                avec_autonomy: $u.avec_autonomy,
                                avec_computed_at: $u.avec_computed_at,
                                avec_needs_recalc: false
                            };
                        };";

        results = await _db.RawQuery(
            avecQuery,
            new Dictionary<string, object?> { { "updates", updates } },
            cancellationToken: cancellationToken
        );

        _logger.LogPossibleDbWriteError(results);
    }

    public async Task<DependencyDto?> UpsertDependencyAsync(
        string fromNodeId,
        string toNodeId,
        string relationshipType,
        CancellationToken cancellationToken = default
    )
    {
        var weight = relationshipType switch
        {
            "inherits" => 1.0,
            "implements" => 1.0,
            "calls" => 0.7,
            "imports" => 0.5,
            "references" => 0.3,
            _ => 0.5,
        };

        // Idempotent: use UNIQUE index on (in, out, relationship_type)
        var query =
            @"
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
            ["weight"] = weight,
        };
        var result = await _db.RawQuery(query, depParams, cancellationToken: cancellationToken);
        _logger.LogPossibleDbWriteError(result);

        var records = result.GetValue<List<DependencyDto>>(0);
        return records?.FirstOrDefault();
    }

    public async Task<NodeDto?> QueryRelationsAsync(
        string nodeId,
        bool includeScores = false,
        CancellationToken cancellationToken = default
    )
    {
        var fields = includeScores ? "*" : "node_id, name, type, file_path";

        var query =
            $@"
            SELECT {fields},
                   (SELECT out.* FROM ->depends) as outgoing,
                   (SELECT in.* FROM <-depends) as incoming
            FROM node:⟨$node_id⟩;
        ";

        var relParams = new Dictionary<string, object?> { ["node_id"] = nodeId };
        var result = await _db.RawQuery(query, relParams, cancellationToken: cancellationToken);
        _logger.LogPossibleDbReadError(result);

        var records = result.GetValue<List<NodeRecord>>(0);
        return records?.Select(MapToDto).FirstOrDefault();
    }

    public async Task<List<NodeDto>> QueryDependenciesAsync(
        string nodeId,
        DependencyDirection direction = DependencyDirection.Both,
        int maxDepth = -1,
        bool includeScores = false,
        CancellationToken cancellationToken = default
    )
    {
        var traversal = direction switch
        {
            DependencyDirection.Incoming => "<-depends<-",
            DependencyDirection.Outgoing => "->depends->",
            DependencyDirection.Both => "<->depends<->",
            _ => "<->depends<->",
        };

        var depthClause = maxDepth > 0 ? $"..{maxDepth}" : "..";
        var fields = includeScores ? "*" : "node_id, name, type, file_path";

        var query =
            $@"
            SELECT {fields}
            FROM node:⟨$node_id⟩{traversal}node{depthClause};
        ";

        var depQueryParams = new Dictionary<string, object?> { ["node_id"] = nodeId };
        var result = await _db.RawQuery(
            query,
            depQueryParams,
            cancellationToken: cancellationToken
        );
        var records = result.GetValue<List<NodeRecord>>(0);
        return records?.Select(MapToDto).ToList() ?? new List<NodeDto>();
    }

    public async Task<List<NodeDto>> QueryPatternsAsync(
        AvecScores targetProfile,
        double threshold = 0.8,
        CancellationToken cancellationToken = default
    )
    {
        var query =
            @"
            SELECT id as Id,
                    node_id as NodeId,
                    type as Type,
                    language as Language,
                    namespace as Namespace,
                    name as Name,
                    signature as Signature,
                    file_path as FilePath,
                    line_start as LineStart,
                    line_end as LineEnd,
                    return_type as ReturnType,
                    lines_of_code as LinesOfCode,
                    cyclomatic_complexity as CyclomaticComplexity,
                    parameters as Parameters,
                    git_created as GitCreated,
                    git_last_modified as GitLastModified,
                    git_total_commits as GitTotalCommits,
                    git_contributors as GitContributors,
                    git_avg_days_between_changes as GitAvgDaysBetweenChanges,
                    git_recent_frequency as GitRecentFrequency,
                    test_covered as TestCovered,
                    test_line_coverage as TestLineCoverage,
                    test_branch_coverage as TestBranchCoverage,
                    test_count as TestCount,
                    incoming_edges as IncomingEdges,
                    outgoing_edges as OutgoingEdges,
                    total_degree as TotalDegree,
                    avec_stability as AvecStability,
                    avec_logic as AvecLogic,
                    avec_friction as AvecFriction,
                    avec_autonomy as AvecAutonomy,
                    avec_computed_at as AvecComputedAt,
                    avec_needs_recalc as AvecNeedsRecalc,
                    avec_learned_stability as AvecLearnedStability,
                    avec_learned_logic as AvecLearnedLogic,
                    avec_learned_friction as AvecLearnedFriction,
                    avec_learned_autonomy as AvecLearnedAutonomy,
                    avec_delta_stability as AvecDeltaStability,
                    avec_delta_logic as AvecDeltaLogic,
                    avec_delta_friction as AvecDeltaFriction,
                    avec_delta_autonomy as AvecDeltaAutonomy,
                   math::sqrt(
                       math::pow(avec_stability - $stability, 2) +
                       math::pow(avec_logic - $logic, 2) +
                       math::pow(avec_friction - $friction, 2) +
                       math::pow(avec_autonomy - $autonomy, 2)
                   ) AS distance
            FROM node
            WHERE avec_stability IS NOT NONE
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
            ["max_distance"] = maxDistance,
        };
        var result = await _db.RawQuery(query, patternParams, cancellationToken: cancellationToken);
        _logger.LogPossibleDbReadError(result);

        var records = result.GetValue<List<NodeRecord>>(0);
        return records?.Select(MapToDto).ToList() ?? new List<NodeDto>();
    }

    public async Task<string?> FindNodeAtLocationAsync(
        string filePath,
        int line,
        CancellationToken cancellationToken = default
    )
    {
        var query =
            @"
            SELECT node_id FROM node
            WHERE file_path = $path
              AND line_start <= $line
              AND line_end >= $line
            LIMIT 1;
        ";

        var locParams = new Dictionary<string, object?> { ["path"] = filePath, ["line"] = line };
        var result = await _db.RawQuery(query, locParams);
        _logger.LogPossibleDbReadError(result);

        var records = result.GetValue<List<NodeIdDto>>(0);
        return records?.FirstOrDefault()?.NodeId;
    }

    public async Task<string?> FindNodeByNameAsync(
        string name,
        CancellationToken cancellationToken = default
    )
    {
        var query = "SELECT node_id FROM node WHERE name = $name LIMIT 1;";
        var nameParams = new Dictionary<string, object?> { ["name"] = name };
        var result = await _db.RawQuery(query, nameParams, cancellationToken: cancellationToken);
        _logger.LogPossibleDbReadError(result);

        var records = result.GetValue<List<NodeIdDto>>(0);
        return records?.FirstOrDefault()?.NodeId;
    }

    public async Task<List<NodeDto>> SearchByNameAsync(
        string name,
        int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        var query =
            @"
        SELECT id as Id,
                    node_id as NodeId,
                    type as Type,
                    language as Language,
                    namespace as Namespace,
                    name as Name,
                    signature as Signature,
                    file_path as FilePath,
                    line_start as LineStart,
                    line_end as LineEnd,
                    return_type as ReturnType,
                    lines_of_code as LinesOfCode,
                    cyclomatic_complexity as CyclomaticComplexity,
                    parameters as Parameters,
                    git_created as GitCreated,
                    git_last_modified as GitLastModified,
                    git_total_commits as GitTotalCommits,
                    git_contributors as GitContributors,
                    git_avg_days_between_changes as GitAvgDaysBetweenChanges,
                    git_recent_frequency as GitRecentFrequency,
                    test_covered as TestCovered,
                    test_line_coverage as TestLineCoverage,
                    test_branch_coverage as TestBranchCoverage,
                    test_count as TestCount,
                    incoming_edges as IncomingEdges,
                    outgoing_edges as OutgoingEdges,
                    total_degree as TotalDegree,
                    avec_stability as AvecStability,
                    avec_logic as AvecLogic,
                    avec_friction as AvecFriction,
                    avec_autonomy as AvecAutonomy,
                    avec_computed_at as AvecComputedAt,
                    avec_needs_recalc as AvecNeedsRecalc,
                    avec_learned_stability as AvecLearnedStability,
                    avec_learned_logic as AvecLearnedLogic,
                    avec_learned_friction as AvecLearnedFriction,
                    avec_learned_autonomy as AvecLearnedAutonomy,
                    avec_delta_stability as AvecDeltaStability,
                    avec_delta_logic as AvecDeltaLogic,
                    avec_delta_friction as AvecDeltaFriction,
                    avec_delta_autonomy as AvecDeltaAutonomy FROM node
        WHERE name CONTAINS $name
        LIMIT $limit;
    ";

        var result = await _db.RawQuery(
            query,
            new Dictionary<string, object?> { ["name"] = name, ["limit"] = limit },
            cancellationToken: cancellationToken
        );
        _logger.LogPossibleDbReadError(result);

        var records = result.GetValue<List<NodeRecord>>(0);
        return records?.Select(MapToDto).ToList() ?? new List<NodeDto>();
    }

    public async Task<List<NodeDto>> GetNodesWithHighFrictionAsync(
        double minFriction = 0.7,
        int limit = 20,
        CancellationToken cancellationToken = default
    )
    {
        var query =
            @"
        SELECT id as Id,
                    node_id as NodeId,
                    type as Type,
                    language as Language,
                    namespace as Namespace,
                    name as Name,
                    signature as Signature,
                    file_path as FilePath,
                    line_start as LineStart,
                    line_end as LineEnd,
                    return_type as ReturnType,
                    lines_of_code as LinesOfCode,
                    cyclomatic_complexity as CyclomaticComplexity,
                    parameters as Parameters,
                    git_created as GitCreated,
                    git_last_modified as GitLastModified,
                    git_total_commits as GitTotalCommits,
                    git_contributors as GitContributors,
                    git_avg_days_between_changes as GitAvgDaysBetweenChanges,
                    git_recent_frequency as GitRecentFrequency,
                    test_covered as TestCovered,
                    test_line_coverage as TestLineCoverage,
                    test_branch_coverage as TestBranchCoverage,
                    test_count as TestCount,
                    incoming_edges as IncomingEdges,
                    outgoing_edges as OutgoingEdges,
                    total_degree as TotalDegree,
                    avec_stability as AvecStability,
                    avec_logic as AvecLogic,
                    avec_friction as AvecFriction,
                    avec_autonomy as AvecAutonomy,
                    avec_computed_at as AvecComputedAt,
                    avec_needs_recalc as AvecNeedsRecalc,
                    avec_learned_stability as AvecLearnedStability,
                    avec_learned_logic as AvecLearnedLogic,
                    avec_learned_friction as AvecLearnedFriction,
                    avec_learned_autonomy as AvecLearnedAutonomy,
                    avec_delta_stability as AvecDeltaStability,
                    avec_delta_logic as AvecDeltaLogic,
                    avec_delta_friction as AvecDeltaFriction,
                    avec_delta_autonomy as AvecDeltaAutonomy FROM node
        WHERE avec_friction >= $min_friction
        ORDER BY avec_friction DESC
        LIMIT $limit;
    ";

        var result = await _db.RawQuery(
            query,
            new Dictionary<string, object?> { ["min_friction"] = minFriction, ["limit"] = limit },
            cancellationToken: cancellationToken
        );
        _logger.LogPossibleDbReadError(result);

        var records = result.GetValue<List<NodeRecord>>(0);
        return records?.Select(MapToDto).ToList() ?? new List<NodeDto>();
    }

    public async Task<List<NodeDto>> GetUnstableNodesAsync(
        double maxStability = 0.4,
        int limit = 20,
        CancellationToken cancellationToken = default
    )
    {
        var query =
            @"
        SELECT id as Id,
                    node_id as NodeId,
                    type as Type,
                    language as Language,
                    namespace as Namespace,
                    name as Name,
                    signature as Signature,
                    file_path as FilePath,
                    line_start as LineStart,
                    line_end as LineEnd,
                    return_type as ReturnType,
                    lines_of_code as LinesOfCode,
                    cyclomatic_complexity as CyclomaticComplexity,
                    parameters as Parameters,
                    git_created as GitCreated,
                    git_last_modified as GitLastModified,
                    git_total_commits as GitTotalCommits,
                    git_contributors as GitContributors,
                    git_avg_days_between_changes as GitAvgDaysBetweenChanges,
                    git_recent_frequency as GitRecentFrequency,
                    test_covered as TestCovered,
                    test_line_coverage as TestLineCoverage,
                    test_branch_coverage as TestBranchCoverage,
                    test_count as TestCount,
                    incoming_edges as IncomingEdges,
                    outgoing_edges as OutgoingEdges,
                    total_degree as TotalDegree,
                    avec_stability as AvecStability,
                    avec_logic as AvecLogic,
                    avec_friction as AvecFriction,
                    avec_autonomy as AvecAutonomy,
                    avec_computed_at as AvecComputedAt,
                    avec_needs_recalc as AvecNeedsRecalc,
                    avec_learned_stability as AvecLearnedStability,
                    avec_learned_logic as AvecLearnedLogic,
                    avec_learned_friction as AvecLearnedFriction,
                    avec_learned_autonomy as AvecLearnedAutonomy,
                    avec_delta_stability as AvecDeltaStability,
                    avec_delta_logic as AvecDeltaLogic,
                    avec_delta_friction as AvecDeltaFriction,
                    avec_delta_autonomy as AvecDeltaAutonomy FROM node
        WHERE avec_stability IS NOT NONE
          AND avec_stability <= $max_stability
        ORDER BY avec_stability ASC
        LIMIT $limit;
    ";

        var result = await _db.RawQuery(
            query,
            new Dictionary<string, object?> { ["max_stability"] = maxStability, ["limit"] = limit },
            cancellationToken: cancellationToken
        );
        _logger.LogPossibleDbReadError(result);

        var records = result.GetValue<List<NodeRecord>>(0);
        return records?.Select(MapToDto).ToList() ?? new List<NodeDto>();
    }

    public async Task<ProjectStatsDto> GetProjectStatsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var query =
            @"
        SELECT 
            count() as total_nodes,
            math::mean(avec_stability) as avg_stability,
            math::mean(avec_logic) as avg_logic,
            math::mean(avec_friction) as avg_friction,
            math::mean(avec_autonomy) as avg_autonomy
        FROM node
        WHERE avec_stability IS NOT NONE
        GROUP ALL;
    ";

        var result = await _db.RawQuery(query, null, cancellationToken: cancellationToken);
        _logger.LogPossibleDbReadError(result);

        var records = result.GetValue<List<ProjectStatsDto>>(0);
        return records?.FirstOrDefault() ?? new ProjectStatsDto();
    }

    private static NodeDto MapToDto(NodeRecord r) =>
        new()
        {
            NodeId = r.NodeId,
            Type = r.Type,
            Language = r.Language,
            Namespace = r.Namespace,
            Name = r.Name,
            Signature = r.Signature,
            FilePath = r.FilePath,
            LineStart = r.LineStart,
            LineEnd = r.LineEnd,
            ReturnType = r.ReturnType,
            LinesOfCode = r.LinesOfCode,
            CyclomaticComplexity = r.CyclomaticComplexity,
            Parameters = r.Parameters,
            GitCreated = r.GitCreated,
            GitLastModified = r.GitLastModified,
            GitTotalCommits = r.GitTotalCommits,
            GitContributors = r.GitContributors,
            GitAvgDaysBetweenChanges = r.GitAvgDaysBetweenChanges,
            GitRecentFrequency = r.GitRecentFrequency,
            TestCovered = r.TestCovered,
            TestLineCoverage = r.TestLineCoverage,
            TestBranchCoverage = r.TestBranchCoverage,
            TestCount = r.TestCount,
            IncomingEdges = r.IncomingEdges,
            OutgoingEdges = r.OutgoingEdges,
            TotalDegree = r.TotalDegree,
            Avec = r.AvecStability.HasValue
                ? new AvecDto
                {
                    Stability = r.AvecStability.Value,
                    Logic = r.AvecLogic ?? 0,
                    Friction = r.AvecFriction ?? 0,
                    Autonomy = r.AvecAutonomy ?? 0,
                    ComputedAt = r.AvecComputedAt,
                }
                : null,
            AvecLearned = r.AvecLearnedStability.HasValue
                ? new AvecDto
                {
                    Stability = r.AvecLearnedStability.Value,
                    Logic = r.AvecLearnedLogic ?? 0,
                    Friction = r.AvecLearnedFriction ?? 0,
                    Autonomy = r.AvecLearnedAutonomy ?? 0,
                }
                : null,
            AvecDelta = r.AvecDeltaStability.HasValue
                ? new AvecDto
                {
                    Stability = r.AvecDeltaStability.Value,
                    Logic = r.AvecDeltaLogic ?? 0,
                    Friction = r.AvecDeltaFriction ?? 0,
                    Autonomy = r.AvecDeltaAutonomy ?? 0,
                }
                : null,
        };
}

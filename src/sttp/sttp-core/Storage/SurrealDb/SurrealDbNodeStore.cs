using Microsoft.Extensions.Logging;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using SttpMcp.Storage.SurrealDb.Models;
using SurrealDb.Net;
using SurrealDb.Net.Models.Response;

namespace SttpMcp.Storage.SurrealDb;

public sealed class SurrealDbNodeStore : INodeStore, INodeStoreInitializer, IAsyncDisposable
{
    private const string DefaultTenantId = "default";

    private readonly ISurrealDbClient _db;
    private readonly ILogger<SurrealDbNodeStore> _logger;

    public SurrealDbNodeStore(ISurrealDbClient db, ILogger<SurrealDbNodeStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var schema = @"
            DEFINE TABLE IF NOT EXISTS temporal_node SCHEMAFULL;
            DEFINE FIELD IF NOT EXISTS tenant_id         ON temporal_node TYPE string;
            DEFINE FIELD IF NOT EXISTS session_id        ON temporal_node TYPE string;
            DEFINE FIELD IF NOT EXISTS raw               ON temporal_node TYPE string;
            DEFINE FIELD IF NOT EXISTS tier              ON temporal_node TYPE string;
            DEFINE FIELD IF NOT EXISTS timestamp         ON temporal_node TYPE datetime;
            DEFINE FIELD IF NOT EXISTS compression_depth ON temporal_node TYPE int;
            DEFINE FIELD IF NOT EXISTS parent_node_id    ON temporal_node TYPE option<string>;
            DEFINE FIELD IF NOT EXISTS psi               ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS rho               ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS kappa             ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS user_stability    ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS user_friction     ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS user_logic        ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS user_autonomy     ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS user_psi          ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS model_stability   ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS model_friction    ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS model_logic       ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS model_autonomy    ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS model_psi         ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS comp_stability    ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS comp_friction     ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS comp_logic        ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS comp_autonomy     ON temporal_node TYPE float;
            DEFINE FIELD IF NOT EXISTS comp_psi          ON temporal_node TYPE float;

            DEFINE TABLE IF NOT EXISTS calibration SCHEMAFULL;
            DEFINE FIELD IF NOT EXISTS tenant_id   ON calibration TYPE string;
            DEFINE FIELD IF NOT EXISTS session_id  ON calibration TYPE string;
            DEFINE FIELD IF NOT EXISTS stability   ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS friction    ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS logic       ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS autonomy    ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS psi         ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS trigger     ON calibration TYPE string;
            DEFINE FIELD IF NOT EXISTS created_at  ON calibration TYPE datetime;

            DEFINE INDEX IF NOT EXISTS idx_node_session ON temporal_node FIELDS session_id;
            DEFINE INDEX IF NOT EXISTS idx_node_tenant_session ON temporal_node FIELDS tenant_id, session_id;
            DEFINE INDEX IF NOT EXISTS idx_cal_session ON calibration FIELDS session_id;
            DEFINE INDEX IF NOT EXISTS idx_cal_tenant_session ON calibration FIELDS tenant_id, session_id;
            SELECT * FROM calibration LIMIT 0;
            ";

        try
        {
            await _db.RawQuery(schema, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unable to initialize SurrealDB schema");
        }
    }

    public async Task<IReadOnlyList<SttpNode>> QueryNodesAsync(NodeQuery query, CancellationToken ct = default)
    {
        var cappedLimit = Math.Max(1, query.Limit);
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object?>
        {
            ["tenant_id"] = DefaultTenantId
        };

        clauses.Add("(tenant_id = $tenant_id OR tenant_id = NONE OR tenant_id = '')");

        if (!string.IsNullOrWhiteSpace(query.SessionId))
        {
            clauses.Add("session_id = $session_id");
            parameters["session_id"] = query.SessionId;
        }

        if (query.FromUtc is not null)
        {
            clauses.Add("timestamp >= <datetime>$from_utc");
            parameters["from_utc"] = query.FromUtc.Value;
        }

        if (query.ToUtc is not null)
        {
            clauses.Add("timestamp <= <datetime>$to_utc");
            parameters["to_utc"] = query.ToUtc.Value;
        }

        var whereClause = clauses.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", clauses)}";

        var queryText = $"""
            SELECT
                session_id AS SessionId,
                raw AS Raw,
                tier AS Tier,
                timestamp AS Timestamp,
                compression_depth AS CompressionDepth,
                parent_node_id AS ParentNodeId,
                psi AS Psi,
                rho AS Rho,
                kappa AS Kappa,
                user_stability AS UserStability,
                user_friction AS UserFriction,
                user_logic AS UserLogic,
                user_autonomy AS UserAutonomy,
                user_psi AS UserPsi,
                model_stability AS ModelStability,
                model_friction AS ModelFriction,
                model_logic AS ModelLogic,
                model_autonomy AS ModelAutonomy,
                model_psi AS ModelPsi,
                comp_stability AS CompStability,
                comp_friction AS CompFriction,
                comp_logic AS CompLogic,
                comp_autonomy AS CompAutonomy,
                comp_psi AS CompPsi,
                0 AS ResonanceDelta
            FROM temporal_node
            {whereClause}
            ORDER BY Timestamp DESC
            LIMIT {cappedLimit};
            """;

        var results = await _db.RawQuery(queryText, parameters.Count == 0 ? null : parameters, ct);
        var records = results.GetValue<List<SurrealNodeRecord>>(0);
        return records?.Select(MapToNode).ToList() ?? [];
    }

    public async Task<string> StoreAsync(SttpNode node, CancellationToken ct = default)
    {
        var compressionAvecToUse = node.CompressionAvec is null || node.CompressionAvec.Psi == 0
            ? node.ModelAvec
            : node.CompressionAvec;

        var parentAssignment = node.ParentNodeId is null
            ? string.Empty
            : "\n                parent_node_id = $parent_node_id,";

        var parameters = new Dictionary<string, object?>
        {
            ["tenant_id"] = DefaultTenantId,
            ["session_id"] = node.SessionId,
            ["raw"] = node.Raw,
            ["tier"] = node.Tier,
            ["timestamp"] = node.Timestamp,
            ["compression_depth"] = node.CompressionDepth,
            ["psi"] = node.Psi,
            ["rho"] = node.Rho,
            ["kappa"] = node.Kappa,
            ["user_stability"] = node.UserAvec.Stability,
            ["user_friction"] = node.UserAvec.Friction,
            ["user_logic"] = node.UserAvec.Logic,
            ["user_autonomy"] = node.UserAvec.Autonomy,
            ["user_psi"] = node.UserAvec.Psi,
            ["model_stability"] = node.ModelAvec.Stability,
            ["model_friction"] = node.ModelAvec.Friction,
            ["model_logic"] = node.ModelAvec.Logic,
            ["model_autonomy"] = node.ModelAvec.Autonomy,
            ["model_psi"] = node.ModelAvec.Psi,
            ["comp_stability"] = compressionAvecToUse.Stability,
            ["comp_friction"] = compressionAvecToUse.Friction,
            ["comp_logic"] = compressionAvecToUse.Logic,
            ["comp_autonomy"] = compressionAvecToUse.Autonomy,
            ["comp_psi"] = compressionAvecToUse.Psi
        };

        if (node.ParentNodeId is not null)
            parameters["parent_node_id"] = node.ParentNodeId;

        var recordId = Guid.NewGuid().ToString("N");

        await _db.RawQuery(
            $"""
            CREATE temporal_node:`{recordId}` SET
                tenant_id = $tenant_id,
                session_id = $session_id,
                raw = $raw,
                tier = $tier,
                timestamp = <datetime>$timestamp,
                compression_depth = $compression_depth,{parentAssignment}
                psi = $psi,
                rho = $rho,
                kappa = $kappa,
                user_stability = $user_stability,
                user_friction = $user_friction,
                user_logic = $user_logic,
                user_autonomy = $user_autonomy,
                user_psi = $user_psi,
                model_stability = $model_stability,
                model_friction = $model_friction,
                model_logic = $model_logic,
                model_autonomy = $model_autonomy,
                model_psi = $model_psi,
                comp_stability = $comp_stability,
                comp_friction = $comp_friction,
                comp_logic = $comp_logic,
                comp_autonomy = $comp_autonomy,
                comp_psi = $comp_psi;
            """,
            parameters,
            cancellationToken: ct);

        return recordId;
    }

    public async Task<IReadOnlyList<SttpNode>> GetByResonanceAsync(
        string sessionId, AvecState current,
        int limit = 5, CancellationToken ct = default)
    {
        var queryText = $"""
            SELECT 
                session_id AS SessionId,
                raw AS Raw,
                tier AS Tier,
                timestamp AS Timestamp,
                compression_depth AS CompressionDepth,
                parent_node_id AS ParentNodeId,
                psi AS Psi,
                rho AS Rho,
                kappa AS Kappa,
                user_stability AS UserStability,
                user_friction AS UserFriction,
                user_logic AS UserLogic,
                user_autonomy AS UserAutonomy,
                user_psi AS UserPsi,
                model_stability AS ModelStability,
                model_friction AS ModelFriction,
                model_logic AS ModelLogic,
                model_autonomy AS ModelAutonomy,
                model_psi AS ModelPsi,
                comp_stability AS CompStability,
                comp_friction AS CompFriction,
                comp_logic AS CompLogic,
                comp_autonomy AS CompAutonomy,
                comp_psi AS CompPsi,
                math::abs(psi - {current.Psi.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}) AS ResonanceDelta
            FROM temporal_node
                        WHERE session_id = $session_id
                            AND (tenant_id = $tenant_id OR tenant_id = NONE OR tenant_id = '')
            ORDER BY ResonanceDelta ASC
            LIMIT {limit};
            """;

        var results = await _db.RawQuery(
            queryText,
                        new Dictionary<string, object?>
                        {
                                ["session_id"] = sessionId,
                                ["tenant_id"] = DefaultTenantId
                        },
            ct);

        var records = results.GetValue<List<SurrealNodeRecord>>(0);
        return records?.Select(MapToNode).ToList() ?? [];
    }

    public Task<IReadOnlyList<SttpNode>> ListNodesAsync(int limit = 50, string? sessionId = null, CancellationToken ct = default)
        => QueryNodesAsync(new NodeQuery { Limit = Math.Clamp(limit, 1, 200), SessionId = sessionId }, ct);

    public async Task<AvecState?> GetLastAvecAsync(string sessionId, CancellationToken ct = default)
    {
        var results = await _db.RawQuery(
            """
            SELECT stability, friction, logic, autonomy, psi, created_at
            FROM calibration
            WHERE session_id = $session_id
                            AND (tenant_id = $tenant_id OR tenant_id = NONE OR tenant_id = '')
            ORDER BY created_at DESC
            LIMIT 1;
            """,
                        new Dictionary<string, object?>
                        {
                                ["session_id"] = sessionId,
                                ["tenant_id"] = DefaultTenantId
                        },
            ct);

        var records = results.GetValue<List<SurrealAvecRecord>>(0);
        var last = records?.FirstOrDefault();
        if (last is null) return null;

        return new AvecState
        {
            Stability = last.Stability,
            Friction = last.Friction,
            Logic = last.Logic,
            Autonomy = last.Autonomy
        };
    }

    public async Task<IReadOnlyList<string>> GetTriggerHistoryAsync(string sessionId, CancellationToken ct = default)
    {
        var results = await _db.RawQuery(
            """
            SELECT trigger, created_at FROM calibration
            WHERE session_id = $session_id
                            AND (tenant_id = $tenant_id OR tenant_id = NONE OR tenant_id = '')
            ORDER BY created_at ASC;
            """,
                        new Dictionary<string, object?>
                        {
                                ["session_id"] = sessionId,
                                ["tenant_id"] = DefaultTenantId
                        },
            ct);

        var records = results.GetValue<List<SurrealTriggerRecord>>(0);
        return records?.Select(r => r.Trigger).ToList() ?? [];
    }

    public async Task StoreCalibrationAsync(string sessionId, AvecState avec, string trigger, CancellationToken ct = default)
    {
        await _db.Create("calibration", new
        {
            tenant_id = DefaultTenantId,
            session_id = sessionId,
            stability = avec.Stability,
            friction = avec.Friction,
            logic = avec.Logic,
            autonomy = avec.Autonomy,
            psi = avec.Psi,
            trigger,
            created_at = DateTime.UtcNow
        }, ct);
    }

    private static SttpNode MapToNode(SurrealNodeRecord record) => new()
    {
        Raw = record.Raw,
        SessionId = record.SessionId,
        Tier = record.Tier,
        Timestamp = record.Timestamp,
        CompressionDepth = record.CompressionDepth,
        ParentNodeId = record.ParentNodeId,
        Psi = (float)record.Psi,
        Rho = (float)record.Rho,
        Kappa = (float)record.Kappa,
        UserAvec = new AvecState
        {
            Stability = (float)record.UserStability,
            Friction = (float)record.UserFriction,
            Logic = (float)record.UserLogic,
            Autonomy = (float)record.UserAutonomy
        },
        ModelAvec = new AvecState
        {
            Stability = (float)record.ModelStability,
            Friction = (float)record.ModelFriction,
            Logic = (float)record.ModelLogic,
            Autonomy = (float)record.ModelAutonomy
        },
        CompressionAvec = new AvecState
        {
            Stability = (float)record.CompStability,
            Friction = (float)record.CompFriction,
            Logic = (float)record.CompLogic,
            Autonomy = (float)record.CompAutonomy
        }
    };

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();
}
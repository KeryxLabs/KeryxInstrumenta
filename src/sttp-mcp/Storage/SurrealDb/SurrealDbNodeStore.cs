// appsettings.json addition:


// ─────────────────────────────────────────
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using SttpMcp.Storage.SurrealDb.Models;
using SurrealDb.Net;
using Microsoft.Extensions.Logging;

namespace SttpMcp.Storage.SurrealDb;

public sealed class SurrealDbNodeStore : INodeStore, IAsyncDisposable
{
    private readonly ISurrealDbClient _db;
    private readonly ILogger<SurrealDbNodeStore> _logger;

    public SurrealDbNodeStore(ISurrealDbClient db, ILogger<SurrealDbNodeStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Schema bootstrap ─────────────────
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _db.RawQuery("""
            DEFINE TABLE IF NOT EXISTS temporal_node SCHEMAFULL;
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
            DEFINE FIELD IF NOT EXISTS session_id  ON calibration TYPE string;
            DEFINE FIELD IF NOT EXISTS stability   ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS friction    ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS logic       ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS autonomy    ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS psi         ON calibration TYPE float;
            DEFINE FIELD IF NOT EXISTS trigger     ON calibration TYPE string;
            DEFINE FIELD IF NOT EXISTS created_at  ON calibration TYPE datetime;

            DEFINE INDEX IF NOT EXISTS idx_node_session
                ON temporal_node FIELDS session_id;
            DEFINE INDEX IF NOT EXISTS idx_cal_session
                ON calibration FIELDS session_id;
            """, cancellationToken: ct);
    }

    // ── INodeStore ────────────────────────

    public async Task<string> StoreAsync(
        SttpNode node, CancellationToken ct = default)
    {
        var compressionAvecToUse = node.CompressionAvec is null || node.CompressionAvec.Psi == 0
            ? node.ModelAvec
            : node.CompressionAvec;
        
        _logger.LogInformation("StoreAsync - SessionId: {SessionId}, Psi: {Psi}", node.SessionId, node.Psi);
        
        var parentAssignment = node.ParentNodeId is null
            ? string.Empty
            : "\n                parent_node_id = $parent_node_id,";

        var parameters = new Dictionary<string, object?>
        {
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

        var result = await _db.RawQuery(
            $"""
            CREATE temporal_node SET
                session_id = $session_id,
                raw = $raw,
                tier = $tier,
                timestamp = $timestamp,
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

        _logger.LogInformation(
            "CREATE result count: {Count}, IsOk: {IsOk}, IsError: {IsError}",
            result.Count,
            result.Count > 0 ? result[0].IsOk : false,
            result.Count > 0 ? result[0].IsError : false);

        try
        {
            var verifyResult = await _db.RawQuery(
                "SELECT VALUE count() FROM temporal_node WHERE session_id = $session_id GROUP ALL;",
                new Dictionary<string, object?> { ["session_id"] = node.SessionId },
                cancellationToken: ct);

            var verifyCounts = verifyResult.GetValue<List<double>>(0) ?? [];
            var persistedCount = verifyCounts.FirstOrDefault();
            var persisted = persistedCount > 0;

            _logger.LogInformation(
                "CREATE verification for {SessionId}: {Persisted} (count={Count})",
                node.SessionId,
                persisted,
                persistedCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "CREATE verification skipped for {SessionId} due to decode mismatch",
                node.SessionId);
        }

        return node.SessionId;
    }

    public async Task<IReadOnlyList<SttpNode>> GetByResonanceAsync(
        string sessionId, AvecState current,
        int limit = 5, CancellationToken ct = default)
    {
        _logger.LogInformation("GetByResonanceAsync - sessionId: {SessionId}, psi: {Psi}, limit: {Limit}",
            sessionId, current.Psi, limit);
        
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
                math::abs(psi - {current.Psi.ToString("F4",
                    System.Globalization.CultureInfo.InvariantCulture)}) AS ResonanceDelta
            FROM temporal_node
            WHERE session_id = $session_id
            ORDER BY ResonanceDelta ASC
            LIMIT {limit};
            """;
        
        var results = await _db.RawQuery(
            queryText,
            new Dictionary<string, object?> { ["session_id"] = sessionId },
            ct);

        var records = results.GetValue<List<SurrealNodeRecord>>(0);
        _logger.LogInformation("Retrieved {Count} records for session {SessionId}", records?.Count ?? 0, sessionId);
        
        return records?.Select(MapToNode).ToList() ?? [];
    }

    public async Task<IReadOnlyList<SttpNode>> ListNodesAsync(
        int limit = 50,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        var cappedLimit = Math.Clamp(limit, 1, 200);

        string queryText;
        Dictionary<string, object?> parameters;

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            queryText = $"""
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
                ORDER BY Timestamp DESC
                LIMIT {cappedLimit};
                """;
            parameters = [];
        }
        else
        {
            queryText = $"""
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
                WHERE session_id = $session_id
                ORDER BY Timestamp DESC
                LIMIT {cappedLimit};
                """;
            parameters = new Dictionary<string, object?> { ["session_id"] = sessionId };
        }

        var results = await _db.RawQuery(queryText, parameters, ct);
        var records = results.GetValue<List<SurrealNodeRecord>>(0);
        return records?.Select(MapToNode).ToList() ?? [];
    }

    public async Task<AvecState?> GetLastAvecAsync(
        string sessionId, CancellationToken ct = default)
    {
        var results = await _db.RawQuery(
            """
            SELECT stability, friction, logic, autonomy, psi, created_at
            FROM calibration
            WHERE session_id = $session_id
            ORDER BY created_at DESC
            LIMIT 1;
            """,
            new Dictionary<string, object?> { ["session_id"] = sessionId },
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

    public async Task<IReadOnlyList<string>> GetTriggerHistoryAsync(
        string sessionId, CancellationToken ct = default)
    {
        var results = await _db.RawQuery(
            """
            SELECT trigger, created_at FROM calibration
            WHERE session_id = $session_id
            ORDER BY created_at ASC;
            """,
            new Dictionary<string, object?> { ["session_id"] = sessionId },
            ct);

        var records = results.GetValue<List<SurrealTriggerRecord>>(0);
        return records?.Select(r => r.Trigger).ToList()
               ?? [];
    }

    public async Task StoreCalibrationAsync(
        string sessionId, AvecState avec,
        string trigger, CancellationToken ct = default)
    {
        await _db.Create("calibration", new
        {
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

    // ── Mapping ───────────────────────────

    private static SttpNode MapToNode(SurrealNodeRecord r) => new()
    {
        Raw = r.Raw,
        SessionId = r.SessionId,
        Tier = r.Tier,
        Timestamp = r.Timestamp,
        CompressionDepth = r.CompressionDepth,
        ParentNodeId = r.ParentNodeId,
        Psi = (float)r.Psi,
        Rho = (float)r.Rho,
        Kappa = (float)r.Kappa,
        UserAvec = new AvecState
        {
            Stability = (float)r.UserStability,
            Friction = (float)r.UserFriction,
            Logic = (float)r.UserLogic,
            Autonomy = (float)r.UserAutonomy
        },
        ModelAvec = new AvecState
        {
            Stability = (float)r.ModelStability,
            Friction = (float)r.ModelFriction,
            Logic = (float)r.ModelLogic,
            Autonomy = (float)r.ModelAutonomy
        },
        CompressionAvec = new AvecState
        {
            Stability = (float)r.CompStability,
            Friction = (float)r.CompFriction,
            Logic = (float)r.CompLogic,
            Autonomy = (float)r.CompAutonomy
        }
    };

    public async ValueTask DisposeAsync()
        => await _db.DisposeAsync();
}

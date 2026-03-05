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
            DEFINE FIELD IF NOT EXISTS user_avec         ON temporal_node TYPE object;
            DEFINE FIELD IF NOT EXISTS model_avec        ON temporal_node TYPE object;
            DEFINE FIELD IF NOT EXISTS compression_avec  ON temporal_node TYPE object;

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
        
        _logger.LogInformation("StoreAsync - SessionId: {SessionId}, ParentNodeId: {ParentNodeId}",
            node.SessionId, node.ParentNodeId ?? "null");

        // Build SQL manually to handle NONE for null parent_node_id
        var parentNodeIdValue = node.ParentNodeId == null ? "NONE" : $"'{node.ParentNodeId}'";
        var rawEscaped = node.Raw.Replace("'", "\\'");
        
        await _db.RawQuery(
            $$"""
            CREATE temporal_node CONTENT {
                session_id: '{{node.SessionId}}',
                raw: '{{rawEscaped}}',
                tier: '{{node.Tier}}',
                timestamp: '{{node.Timestamp:yyyy-MM-ddTHH:mm:ssZ}}',
                compression_depth: {{node.CompressionDepth}},
                parent_node_id: {{parentNodeIdValue}},
                psi: {{node.Psi.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                rho: {{node.Rho.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                kappa: {{node.Kappa.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                user_avec: {
                    stability: {{node.UserAvec.Stability.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    friction: {{node.UserAvec.Friction.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    logic: {{node.UserAvec.Logic.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    autonomy: {{node.UserAvec.Autonomy.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    psi: {{node.UserAvec.Psi.ToString(System.Globalization.CultureInfo.InvariantCulture)}}
                },
                model_avec: {
                    stability: {{node.ModelAvec.Stability.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    friction: {{node.ModelAvec.Friction.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    logic: {{node.ModelAvec.Logic.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    autonomy: {{node.ModelAvec.Autonomy.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    psi: {{node.ModelAvec.Psi.ToString(System.Globalization.CultureInfo.InvariantCulture)}}
                },
                compression_avec: {
                    stability: {{compressionAvecToUse.Stability.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    friction: {{compressionAvecToUse.Friction.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    logic: {{compressionAvecToUse.Logic.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    autonomy: {{compressionAvecToUse.Autonomy.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                    psi: {{compressionAvecToUse.Psi.ToString(System.Globalization.CultureInfo.InvariantCulture)}}
                }
            };
            """,
            cancellationToken: ct);

        return node.SessionId;
    }

    public async Task<IReadOnlyList<SttpNode>> GetByResonanceAsync(
        string sessionId, AvecState current,
        int limit = 5, CancellationToken ct = default)
    {
        _logger.LogInformation("=== NEW CODE EXECUTING === GetByResonanceAsync - sessionId: {SessionId}, psi: {Psi}, limit: {Limit}",
            sessionId, current.Psi, limit);
        
        // First, check what's actually in the database
        try
        {
            var allResults = await _db.RawQuery("SELECT session_id, psi, user_avec FROM temporal_node WHERE session_id = $sid LIMIT 1;", 
                new Dictionary<string, object?> { ["sid"] = sessionId }, 
                cancellationToken: ct);
            var debugRecords = allResults.GetValue<List<Dictionary<string, object>>>(0);
            _logger.LogInformation("Debug query found {Count} records for session {SessionId}", debugRecords?.Count ?? 0, sessionId);
            if (debugRecords?.Count > 0)
            {
                var firstDict = debugRecords[0];
                _logger.LogInformation("Keys in record: {Keys}", string.Join(", ", firstDict.Keys));
                foreach (var kvp in firstDict.Take(3))
                {
                    _logger.LogInformation("  {Key}: {Value} ({Type})", kvp.Key, kvp.Value, kvp.Value?.GetType().Name ?? "null");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run debug query");
        }
        
        var results = await _db.RawQuery(
            $"""
            SELECT 
                session_id, raw, tier, timestamp, compression_depth, parent_node_id,
                psi, rho, kappa, user_avec, model_avec, compression_avec,
                math::abs(psi - {current.Psi.ToString("F4",
                    System.Globalization.CultureInfo.InvariantCulture)}) AS resonance_delta
            FROM temporal_node
            WHERE session_id = $session_id
            ORDER BY resonance_delta ASC
            LIMIT {limit};
            """,
            new Dictionary<string, object?> { ["session_id"] = sessionId },
            ct);

        _logger.LogInformation("Query executed, attempting to deserialize results");
        
        try
        {
            var records = results.GetValue<List<SurrealNodeRecord>>(0);
            _logger.LogInformation("Retrieved {Count} records", records?.Count ?? 0);
            if (records?.Count > 0)
            {
                var first = records[0];
                _logger.LogInformation("First record - SessionId: '{SessionId}', Psi: {Psi}, UserAvec null: {UserNull}",
                    first.SessionId ?? "NULL",
                    first.Psi,
                    first.UserAvec == null);
            }
            return records?.Select(MapToNode).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize SurrealNodeRecord");
            return [];
        }
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
        UserAvec = AvecDataToAvec(r.UserAvec),
        ModelAvec = AvecDataToAvec(r.ModelAvec),
        CompressionAvec = AvecDataToAvec(r.CompressionAvec)
    };

    private static AvecState AvecDataToAvec(AvecData? d)
    {
        if (d == null)
            return AvecState.Zero;
            
        return new()
        {
            Stability = (float)d.Stability,
            Friction = (float)d.Friction,
            Logic = (float)d.Logic,
            Autonomy = (float)d.Autonomy
        };
    }

    public async ValueTask DisposeAsync()
        => await _db.DisposeAsync();
}

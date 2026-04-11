using Microsoft.Extensions.Logging;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;
using SttpMcp.Storage.SurrealDb.Models;
using SurrealDb.Net;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SttpMcp.Storage.SurrealDb;

public sealed class SurrealDbNodeStore : INodeStore, INodeStoreInitializer, IAsyncDisposable
{
    private const string DefaultTenantId = "default";
    private const string TenantScopePrefix = "tenant:";
    private const string TenantScopeSeparator = "::session:";

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
            DEFINE FIELD IF NOT EXISTS sync_key          ON temporal_node TYPE string;
            DEFINE FIELD IF NOT EXISTS updated_at        ON temporal_node TYPE datetime;
            DEFINE FIELD IF NOT EXISTS source_metadata   ON temporal_node TYPE option<object>;
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

            DEFINE TABLE IF NOT EXISTS sync_checkpoint SCHEMAFULL;
            DEFINE FIELD IF NOT EXISTS tenant_id         ON sync_checkpoint TYPE string;
            DEFINE FIELD IF NOT EXISTS session_id        ON sync_checkpoint TYPE string;
            DEFINE FIELD IF NOT EXISTS connector_id      ON sync_checkpoint TYPE string;
            DEFINE FIELD IF NOT EXISTS cursor_updated_at ON sync_checkpoint TYPE option<datetime>;
            DEFINE FIELD IF NOT EXISTS cursor_sync_key   ON sync_checkpoint TYPE option<string>;
            DEFINE FIELD IF NOT EXISTS metadata          ON sync_checkpoint TYPE option<object>;
            DEFINE FIELD IF NOT EXISTS updated_at        ON sync_checkpoint TYPE datetime;

            DEFINE INDEX IF NOT EXISTS idx_node_session ON temporal_node FIELDS session_id;
            DEFINE INDEX IF NOT EXISTS idx_node_tenant_session ON temporal_node FIELDS tenant_id, session_id;
            DEFINE INDEX IF NOT EXISTS idx_node_change_cursor ON temporal_node FIELDS tenant_id, session_id, updated_at, sync_key;
            DEFINE INDEX IF NOT EXISTS idx_node_sync_identity ON temporal_node FIELDS tenant_id, session_id, sync_key UNIQUE;
            DEFINE INDEX IF NOT EXISTS idx_cal_session ON calibration FIELDS session_id;
            DEFINE INDEX IF NOT EXISTS idx_cal_tenant_session ON calibration FIELDS tenant_id, session_id;
            DEFINE INDEX IF NOT EXISTS idx_checkpoint_scope ON sync_checkpoint FIELDS tenant_id, session_id, connector_id UNIQUE;
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
        var tenantId = string.IsNullOrWhiteSpace(query.SessionId)
            ? DefaultTenantId
            : DeriveTenantIdFromSession(query.SessionId);
        var parameters = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId
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
                sync_key AS SyncKey,
                updated_at AS UpdatedAt,
                source_metadata AS SourceMetadata,
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
        => (await UpsertNodeAsync(node, ct)).NodeId;

    public async Task<NodeUpsertResult> UpsertNodeAsync(SttpNode node, CancellationToken ct = default)
    {
        var syncKey = string.IsNullOrWhiteSpace(node.SyncKey)
            ? node.CanonicalSyncKey()
            : node.SyncKey.Trim();
        var updatedAt = DateTime.UtcNow;
        var candidate = node with
        {
            SyncKey = syncKey,
            UpdatedAt = updatedAt
        };
        var tenantId = DeriveTenantIdFromSession(candidate.SessionId);
        var compressionAvecToUse = candidate.CompressionAvec is null || candidate.CompressionAvec.Psi == 0
            ? candidate.ModelAvec
            : candidate.CompressionAvec;

        var existingResults = await _db.RawQuery(
            """
            SELECT
                id AS Id,
                source_metadata AS SourceMetadata
            FROM temporal_node
            WHERE session_id = $session_id
                AND sync_key = $sync_key
                AND (tenant_id = $tenant_id OR tenant_id = NONE OR tenant_id = '')
            LIMIT 1;
            """,
            new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["session_id"] = candidate.SessionId,
                ["sync_key"] = syncKey
            },
            ct);

        var existing = existingResults.GetValue<List<SurrealExistingNodeRecord>>(0)?.FirstOrDefault();
        if (existing is not null)
        {
            var existingId = NormalizeTemporalNodeId(existing.Id);
            if (NormalizeMetadata(existing.SourceMetadata) != NormalizeMetadata(candidate.SourceMetadata))
            {
                await _db.RawQuery(
                    $"""
                    UPDATE temporal_node:`{existingId}` SET
                        source_metadata = $source_metadata,
                        updated_at = <datetime>$updated_at;
                    """,
                    new Dictionary<string, object?>
                    {
                        ["source_metadata"] = ToSurrealValue(candidate.SourceMetadata),
                        ["updated_at"] = updatedAt
                    },
                    ct);

                return new NodeUpsertResult
                {
                    NodeId = existingId,
                    SyncKey = syncKey,
                    Status = NodeUpsertStatus.Updated,
                    UpdatedAt = updatedAt
                };
            }

            return new NodeUpsertResult
            {
                NodeId = existingId,
                SyncKey = syncKey,
                Status = NodeUpsertStatus.Duplicate,
                UpdatedAt = updatedAt
            };
        }

        var parentAssignment = candidate.ParentNodeId is null
            ? string.Empty
            : "\n                parent_node_id = $parent_node_id,";

        var parameters = new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId,
            ["session_id"] = candidate.SessionId,
            ["raw"] = candidate.Raw,
            ["tier"] = candidate.Tier,
            ["timestamp"] = candidate.Timestamp,
            ["compression_depth"] = candidate.CompressionDepth,
            ["sync_key"] = syncKey,
            ["updated_at"] = updatedAt,
            ["source_metadata"] = ToSurrealValue(candidate.SourceMetadata),
            ["psi"] = candidate.Psi,
            ["rho"] = candidate.Rho,
            ["kappa"] = candidate.Kappa,
            ["user_stability"] = candidate.UserAvec.Stability,
            ["user_friction"] = candidate.UserAvec.Friction,
            ["user_logic"] = candidate.UserAvec.Logic,
            ["user_autonomy"] = candidate.UserAvec.Autonomy,
            ["user_psi"] = candidate.UserAvec.Psi,
            ["model_stability"] = candidate.ModelAvec.Stability,
            ["model_friction"] = candidate.ModelAvec.Friction,
            ["model_logic"] = candidate.ModelAvec.Logic,
            ["model_autonomy"] = candidate.ModelAvec.Autonomy,
            ["model_psi"] = candidate.ModelAvec.Psi,
            ["comp_stability"] = compressionAvecToUse.Stability,
            ["comp_friction"] = compressionAvecToUse.Friction,
            ["comp_logic"] = compressionAvecToUse.Logic,
            ["comp_autonomy"] = compressionAvecToUse.Autonomy,
            ["comp_psi"] = compressionAvecToUse.Psi
        };

        if (candidate.ParentNodeId is not null)
            parameters["parent_node_id"] = candidate.ParentNodeId;

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
                sync_key = $sync_key,
                updated_at = <datetime>$updated_at,
                source_metadata = $source_metadata,
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

        return new NodeUpsertResult
        {
            NodeId = recordId,
            SyncKey = syncKey,
            Status = NodeUpsertStatus.Created,
            UpdatedAt = updatedAt
        };
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
                sync_key AS SyncKey,
                updated_at AS UpdatedAt,
                source_metadata AS SourceMetadata,
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
                            ["tenant_id"] = DeriveTenantIdFromSession(sessionId)
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
                            ["tenant_id"] = DeriveTenantIdFromSession(sessionId)
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
                            ["tenant_id"] = DeriveTenantIdFromSession(sessionId)
                        },
            ct);

        var records = results.GetValue<List<SurrealTriggerRecord>>(0);
        return records?.Select(r => r.Trigger).ToList() ?? [];
    }

    public async Task StoreCalibrationAsync(string sessionId, AvecState avec, string trigger, CancellationToken ct = default)
    {
        await _db.Create("calibration", new
        {
            tenant_id = DeriveTenantIdFromSession(sessionId),
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

    public async Task<ChangeQueryResult> QueryChangesSinceAsync(
        string sessionId,
        SyncCursor? cursor,
        int limit,
        CancellationToken ct = default)
    {
        var cappedLimit = Math.Max(1, limit);
        var results = await _db.RawQuery(
            $"""
            SELECT
                session_id AS SessionId,
                raw AS Raw,
                tier AS Tier,
                timestamp AS Timestamp,
                compression_depth AS CompressionDepth,
                parent_node_id AS ParentNodeId,
                sync_key AS SyncKey,
                updated_at AS UpdatedAt,
                source_metadata AS SourceMetadata,
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
                AND (tenant_id = $tenant_id OR tenant_id = NONE OR tenant_id = '')
                AND (
                    NOT $include_cursor
                    OR updated_at > <datetime>$cursor_updated_at
                    OR (
                        updated_at = <datetime>$cursor_updated_at
                        AND sync_key > $cursor_sync_key
                    )
                )
            ORDER BY updated_at ASC, sync_key ASC
            LIMIT {cappedLimit + 1};
            """,
            new Dictionary<string, object?>
            {
                ["session_id"] = sessionId,
                ["tenant_id"] = DeriveTenantIdFromSession(sessionId),
                ["include_cursor"] = cursor is not null,
                ["cursor_updated_at"] = cursor?.UpdatedAt,
                ["cursor_sync_key"] = cursor?.SyncKey
            },
            ct);

        var records = results.GetValue<List<SurrealNodeRecord>>(0) ?? [];
        var hasMore = records.Count > cappedLimit;
        if (hasMore)
            records = records.Take(cappedLimit).ToList();

        var nodes = records.Select(MapToNode).ToList();
        return new ChangeQueryResult
        {
            Nodes = nodes,
            HasMore = hasMore,
            NextCursor = nodes.Count == 0
                ? null
                : new SyncCursor
                {
                    UpdatedAt = nodes[^1].UpdatedAt,
                    SyncKey = nodes[^1].SyncKey
                }
        };
    }

    public async Task<SyncCheckpoint?> GetCheckpointAsync(
        string sessionId,
        string connectorId,
        CancellationToken ct = default)
    {
        var results = await _db.RawQuery(
            """
            SELECT
                session_id AS SessionId,
                connector_id AS ConnectorId,
                cursor_updated_at AS CursorUpdatedAt,
                cursor_sync_key AS CursorSyncKey,
                updated_at AS UpdatedAt,
                metadata AS Metadata
            FROM sync_checkpoint
            WHERE session_id = $session_id
                AND connector_id = $connector_id
                AND (tenant_id = $tenant_id OR tenant_id = NONE OR tenant_id = '')
            LIMIT 1;
            """,
            new Dictionary<string, object?>
            {
                ["session_id"] = sessionId,
                ["connector_id"] = connectorId,
                ["tenant_id"] = DeriveTenantIdFromSession(sessionId)
            },
            ct);

        var record = results.GetValue<List<SurrealCheckpointRecord>>(0)?.FirstOrDefault();
        if (record is null)
            return null;

        return new SyncCheckpoint
        {
            SessionId = record.SessionId,
            ConnectorId = record.ConnectorId,
            Cursor = record.CursorUpdatedAt is null || string.IsNullOrWhiteSpace(record.CursorSyncKey)
                ? null
                : new SyncCursor
                {
                    UpdatedAt = record.CursorUpdatedAt.Value,
                    SyncKey = record.CursorSyncKey
                },
            UpdatedAt = record.UpdatedAt,
            Metadata = record.Metadata
        };
    }

    public async Task PutCheckpointAsync(SyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        var tenantId = DeriveTenantIdFromSession(checkpoint.SessionId);
        var recordId = BuildCheckpointRecordId(tenantId, checkpoint.SessionId, checkpoint.ConnectorId);

        await _db.RawQuery(
            $"""
            UPSERT sync_checkpoint:`{recordId}` SET
                tenant_id = $tenant_id,
                session_id = $session_id,
                connector_id = $connector_id,
                cursor_updated_at = <datetime>$cursor_updated_at,
                cursor_sync_key = $cursor_sync_key,
                metadata = $metadata,
                updated_at = <datetime>$updated_at;
            """,
            new Dictionary<string, object?>
            {
                ["tenant_id"] = tenantId,
                ["session_id"] = checkpoint.SessionId,
                ["connector_id"] = checkpoint.ConnectorId,
                ["cursor_updated_at"] = checkpoint.Cursor?.UpdatedAt,
                ["cursor_sync_key"] = checkpoint.Cursor?.SyncKey,
                ["metadata"] = ToSurrealValue(checkpoint.Metadata),
                ["updated_at"] = checkpoint.UpdatedAt
            },
            ct);
    }

    private static SttpNode MapToNode(SurrealNodeRecord record)
    {
        var node = new SttpNode
        {
            Raw = record.Raw,
            SessionId = record.SessionId,
            Tier = record.Tier,
            Timestamp = record.Timestamp,
            CompressionDepth = record.CompressionDepth,
            ParentNodeId = record.ParentNodeId,
            SyncKey = string.IsNullOrWhiteSpace(record.SyncKey) ? string.Empty : record.SyncKey,
            UpdatedAt = record.UpdatedAt ?? record.Timestamp,
            SourceMetadata = record.SourceMetadata,
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

        if (string.IsNullOrWhiteSpace(node.SyncKey))
            node = node with { SyncKey = node.CanonicalSyncKey() };

        return node;
    }

    private static string DeriveTenantIdFromSession(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return DefaultTenantId;

        if (!sessionId.StartsWith(TenantScopePrefix, StringComparison.Ordinal))
            return DefaultTenantId;

        var remainder = sessionId[TenantScopePrefix.Length..];
        var separatorIndex = remainder.IndexOf(TenantScopeSeparator, StringComparison.Ordinal);
        if (separatorIndex <= 0)
            return DefaultTenantId;

        var tenantId = remainder[..separatorIndex].Trim();
        return string.IsNullOrWhiteSpace(tenantId) ? DefaultTenantId : tenantId;
    }

    private static string NormalizeTemporalNodeId(string value)
        => value.StartsWith("temporal_node:", StringComparison.Ordinal)
            ? value["temporal_node:".Length..]
            : value;

    private static object? ToSurrealValue(JsonElement? value)
        => value is null ? null : JsonSerializer.Deserialize<object>(value.Value.GetRawText());

    private static string? NormalizeMetadata(JsonElement? metadata)
        => metadata is null ? null : JsonSerializer.Serialize(metadata.Value);

    private static string? NormalizeMetadata(object? metadata)
    {
        if (metadata is null)
            return null;

        if (metadata is JsonElement jsonElement)
            return JsonSerializer.Serialize(jsonElement);

        return JsonSerializer.Serialize(metadata);
    }

    private static string BuildCheckpointRecordId(string tenantId, string sessionId, string connectorId)
    {
        var payload = string.Join("\0", tenantId, sessionId, connectorId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();
}
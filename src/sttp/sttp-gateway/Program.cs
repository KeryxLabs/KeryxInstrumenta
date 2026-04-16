using Microsoft.AspNetCore.Server.Kestrel.Core;
using SttpGateway.Contracts;
using SttpGateway.Services;
using SttpMcp.Application.Services;
using SttpMcp.Application.Validation;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;

const string TenantHeader = "x-tenant-id";
const string DefaultTenant = "default";
const string TenantScopePrefix = "tenant:";
const string TenantScopeSeparator = "::session:";
const int TenantScanLimit = 200;

var builder = WebApplication.CreateBuilder(args);

// Optional user-local config: ~/.sttp-gateway/appsettings.json
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var rootDir = Path.Combine(home, ".sttp-gateway");
var appSettingsPath = Path.Combine(rootDir, "appsettings.json");
builder.Configuration.AddJsonFile(appSettingsPath, optional: true, reloadOnChange: true);

var switchMappings = new Dictionary<string, string>
{
    ["--remote-endpoint"] = "SurrealDb:Endpoints:Remote",
    ["--embedded-endpoint"] = "SurrealDb:Endpoints:Embedded",
    ["--namespace"] = "SurrealDb:Namespace",
    ["--database"] = "SurrealDb:Database",
    ["--username"] = "SurrealDb:User",
    ["--password"] = "SurrealDb:Password",
    ["--port"] = "Gateway:HttpPort",
    ["--http-port"] = "Gateway:HttpPort",
    ["--grpc-port"] = "Gateway:GrpcPort",
    ["--cors-enabled"] = "Gateway:Cors:Enabled",
    ["--cors-allowed-origins"] = "Gateway:Cors:AllowedOrigins"
};

var configArgs = args.Where(a => !string.Equals(a, "--remote", StringComparison.OrdinalIgnoreCase)).ToArray();
builder.Configuration.AddCommandLine(configArgs, switchMappings);

var httpPort = builder.Configuration.GetValue<int?>("Gateway:HttpPort") ?? 8080;
var grpcPort = builder.Configuration.GetValue<int?>("Gateway:GrpcPort") ?? 8081;
var corsEnabled =
    builder.Configuration.GetValue<bool?>("Gateway:Cors:Enabled")
    ?? builder.Configuration.GetValue<bool?>("STTP_GATEWAY_CORS_ENABLED")
    ?? true;
var corsAllowedOriginsRaw =
    builder.Configuration.GetValue<string>("Gateway:Cors:AllowedOrigins")
    ?? builder.Configuration.GetValue<string>("STTP_GATEWAY_CORS_ALLOWED_ORIGINS")
    ?? "*";
var corsAllowedOrigins = ParseCorsAllowedOrigins(corsAllowedOriginsRaw);

if (grpcPort == httpPort)
    throw new InvalidOperationException("Gateway:GrpcPort must be different from Gateway:HttpPort for non-TLS dual mode.");

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(httpPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });

    options.ListenAnyIP(grpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
if (corsEnabled)
{
    builder.Services.AddCors(options =>
        options.AddPolicy("ByoGateway", policy =>
        {
            policy
                .AllowAnyMethod()
                .AllowAnyHeader();

            if (corsAllowedOrigins.AllowAny)
                policy.AllowAnyOrigin();
            else
                policy.WithOrigins(corsAllowedOrigins.Origins);
        }));
}

var storageRuntime = builder.Services.AddSttpSurrealDbStorage(builder.Configuration, args, ".sttp-gateway");

builder.Services
    .AddSingleton<INodeValidator, TreeSitterValidator>()
    .AddSttpCore();

var app = builder.Build();
if (corsEnabled)
    app.UseCors("ByoGateway");

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "STTP gateway startup | PID={Pid} | HttpPort={HttpPort} | GrpcPort={GrpcPort} | CorsEnabled={CorsEnabled} | CorsAllowedOrigins={CorsAllowedOrigins} | RootDir={RootDir} | Mode={Mode} | Endpoint={Endpoint} | Namespace={Namespace} | Database={Database}",
    Environment.ProcessId,
    httpPort,
    grpcPort,
    corsEnabled,
    corsAllowedOriginsRaw,
    storageRuntime.RootDir,
    storageRuntime.UseRemote ? "remote" : "embedded",
    storageRuntime.Endpoint,
    storageRuntime.Namespace,
    storageRuntime.Database);

var storeInitializer = app.Services.GetService<INodeStoreInitializer>();
if (storeInitializer is not null)
    await storeInitializer.InitializeAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok", transport = "http+grpc" }));

app.MapPost("/api/v1/calibrate", async (HttpRequest httpRequest, CalibrateSessionHttpRequest request, CalibrationService service, CancellationToken ct) =>
{
    var tenant = ResolveHttpTenant(request.TenantId, httpRequest.Headers);
    var trigger = string.IsNullOrWhiteSpace(request.Trigger) ? "manual" : request.Trigger;
    var result = await service.CalibrateAsync(
        ScopeSessionId(tenant, request.SessionId),
        request.Stability,
        request.Friction,
        request.Logic,
        request.Autonomy,
        trigger,
        ct);

    return Results.Ok(result);
});

app.MapPost("/api/v1/store", StoreContextEndpoint);
app.MapPost("/api/store", StoreContextEndpoint);
app.MapPost("/store", StoreContextEndpoint);
app.MapPost("/api/v1/session/rename", RenameSessionEndpoint);
app.MapPost("/api/session/rename", RenameSessionEndpoint);
app.MapPost("/session/rename", RenameSessionEndpoint);

app.MapPost("/api/v1/context", async (HttpRequest httpRequest, GetContextHttpRequest request, ContextQueryService service, CancellationToken ct) =>
{
    var tenant = ResolveHttpTenant(request.TenantId, httpRequest.Headers);
    var result = await service.GetContextAsync(
        ScopeSessionId(tenant, request.SessionId),
        request.Stability,
        request.Friction,
        request.Logic,
        request.Autonomy,
        request.Limit,
        ct);

    return Results.Ok(result with
    {
        Nodes = result.Nodes.Select(node => NormalizeNodeForTenant(node, tenant)).Where(node => node is not null).Select(node => node!).ToList(),
        Retrieved = result.Nodes.Count
    });
});

app.MapGet("/api/v1/nodes", ListNodesEndpoint);
app.MapGet("/api/nodes", ListNodesEndpoint);
app.MapGet("/nodes", ListNodesEndpoint);

app.MapGet("/api/v1/graph", GraphEndpoint);
app.MapGet("/api/graph", GraphEndpoint);
app.MapGet("/graph", GraphEndpoint);

app.MapGet("/api/v1/moods", async (
    string? targetMood,
    float? blend,
    float? currentStability,
    float? currentFriction,
    float? currentLogic,
    float? currentAutonomy,
    MoodCatalogService service) =>
{
    var result = await service.GetAsync(
        targetMood,
        blend ?? 1f,
        currentStability,
        currentFriction,
        currentLogic,
        currentAutonomy);

    return Results.Ok(result);
});

app.MapPost("/api/v1/rollups/monthly", async (HttpRequest httpRequest, CreateMonthlyRollupHttpRequest request, MonthlyRollupService service, CancellationToken ct) =>
{
    var tenant = ResolveHttpTenant(request.TenantId, httpRequest.Headers);
    var rollupRequest = new MonthlyRollupRequest
    {
        SessionId = ScopeSessionId(tenant, request.SessionId),
        StartUtc = request.StartDateUtc,
        EndUtc = request.EndDateUtc,
        SourceSessionId = string.IsNullOrWhiteSpace(request.SourceSessionId) ? null : ScopeSessionId(tenant, request.SourceSessionId),
        ParentNodeId = request.ParentNodeId,
        Persist = request.Persist,
        Limit = request.Limit
    };

    var result = await service.CreateAsync(rollupRequest, ct);
    return Results.Ok(result);
});

app.MapPost("/api/v1/rekey", async (HttpRequest httpRequest, BatchRekeyHttpRequest request, RekeyScopeService service, CancellationToken ct) =>
{
    if (request.NodeIds.Count == 0)
        return Results.BadRequest(new { error = "nodeIds must contain at least one value" });

    if (string.IsNullOrWhiteSpace(request.TargetSessionId))
        return Results.BadRequest(new { error = "targetSessionId cannot be empty" });

    var targetTenant = ResolveHttpTenant(request.TargetTenantId, httpRequest.Headers);
    var scopedTargetSession = ScopeSessionId(targetTenant, request.TargetSessionId.Trim());
    var result = await service.RekeyAsync(
        request.NodeIds,
        targetTenant,
        scopedTargetSession,
        request.DryRun,
        request.AllowMerge,
        ct);

    return Results.Ok(new BatchRekeyResult
    {
        DryRun = result.DryRun,
        RequestedNodeIds = result.RequestedNodeIds,
        ResolvedNodeIds = result.ResolvedNodeIds,
        MissingNodeIds = result.MissingNodeIds,
        Scopes = result.Scopes.Select(scope => scope with
        {
            SourceSessionId = DisplaySessionId(scope.SourceSessionId),
            TargetSessionId = DisplaySessionId(scope.TargetSessionId)
        }).ToList(),
        TemporalNodesUpdated = result.TemporalNodesUpdated,
        CalibrationsUpdated = result.CalibrationsUpdated
    });
});

app.MapGrpcService<SttpGrpcService>();
app.MapGrpcReflectionService();

app.Run();

static async Task<IResult> StoreContextEndpoint(HttpRequest httpRequest, StoreContextHttpRequest request, StoreContextService service, CancellationToken ct)
{
    var tenant = ResolveHttpTenant(request.TenantId, httpRequest.Headers);
    var result = await service.StoreAsync(request.Node, ScopeSessionId(tenant, request.SessionId), ct);
    return Results.Ok(new
    {
        nodeId = result.NodeId,
        psi = result.Psi,
        valid = result.Valid,
        validationError = result.ValidationError,
        duplicateSkipped = false,
        upsertStatus = result.Valid ? "created" : "skipped"
    });
}

static async Task<IResult> RenameSessionEndpoint(HttpRequest httpRequest, RenameSessionHttpRequest request, INodeStore store, RekeyScopeService service, CancellationToken ct)
{
    var tenant = ResolveHttpTenant(request.TenantId, httpRequest.Headers);
    var sourceSessionId = request.SourceSessionId?.Trim() ?? string.Empty;
    var targetSessionId = request.TargetSessionId?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(sourceSessionId) || string.IsNullOrWhiteSpace(targetSessionId))
        return Results.BadRequest(new { error = "sourceSessionId and targetSessionId are required" });

    if (string.Equals(sourceSessionId, targetSessionId, StringComparison.Ordinal))
    {
        return Results.Ok(new
        {
            sourceSessionId,
            targetSessionId,
            movedNodes = 0,
            movedCalibrations = 0,
            scopesApplied = 0
        });
    }

    var scopedSourceSessionId = ScopeSessionId(tenant, sourceSessionId);
    var scopedTargetSessionId = ScopeSessionId(tenant, targetSessionId);

    var sourceNodes = await store.QueryNodesAsync(
        new NodeQuery
        {
            Limit = 10_000,
            SessionId = scopedSourceSessionId
        },
        ct);

    if (sourceNodes.Count == 0)
        return Results.BadRequest(new { error = $"source session not found: {sourceSessionId}" });

    var anchorNodeIds = new List<string>(sourceNodes.Count);
    foreach (var node in sourceNodes)
    {
        var upsert = await store.UpsertNodeAsync(node, ct);
        anchorNodeIds.Add(upsert.NodeId);
    }
    anchorNodeIds = anchorNodeIds
        .Distinct(StringComparer.Ordinal)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToList();

    var result = await service.RekeyAsync(
        anchorNodeIds,
        tenant,
        scopedTargetSessionId,
        dryRun: false,
        allowMerge: request.AllowMerge,
        ct);

    var conflict = result.Scopes.FirstOrDefault(scope => scope.Conflict);
    if (conflict is not null)
        return Results.BadRequest(new { error = conflict.Message ?? "target session already exists" });

    return Results.Ok(new
    {
        sourceSessionId,
        targetSessionId,
        movedNodes = result.TemporalNodesUpdated,
        movedCalibrations = result.CalibrationsUpdated,
        scopesApplied = result.Scopes.Count(scope => scope.Applied)
    });
}

static async Task<IResult> ListNodesEndpoint(HttpRequest httpRequest, int? limit, string? sessionId, string? tenantId, ContextQueryService service, CancellationToken ct)
{
    var tenant = ResolveHttpTenant(tenantId, httpRequest.Headers);
    var requestedLimit = Math.Clamp(limit ?? 50, 1, TenantScanLimit);
    var scopedSessionId = string.IsNullOrWhiteSpace(sessionId) ? null : ScopeSessionId(tenant, sessionId);
    var backendLimit = scopedSessionId is null ? TenantScanLimit : requestedLimit;
    var result = await service.ListNodesAsync(backendLimit, scopedSessionId, ct);
    var nodes = result.Nodes
        .Select(node => NormalizeNodeForTenant(node, tenant))
        .Where(node => node is not null)
        .Take(requestedLimit)
        .Select(node => ToNodeHttpDto(node!))
        .ToList();

    return Results.Ok(new
    {
        nodes,
        retrieved = nodes.Count
    });
}

static async Task<IResult> GraphEndpoint(HttpRequest httpRequest, int? limit, string? sessionId, string? tenantId, ContextQueryService service, CancellationToken ct)
{
    var tenant = ResolveHttpTenant(tenantId, httpRequest.Headers);
    var cappedLimit = Math.Clamp(limit ?? 1000, 1, 5000);
    var scopedSessionId = string.IsNullOrWhiteSpace(sessionId) ? null : ScopeSessionId(tenant, sessionId);
    var backendLimit = scopedSessionId is null ? TenantScanLimit : cappedLimit;
    var result = await service.ListNodesAsync(backendLimit, scopedSessionId, ct);

    var orderedNodes = result.Nodes
        .Select(node => NormalizeNodeForTenant(node, tenant))
        .Where(node => node is not null)
        .Select(node => node!)
        .OrderByDescending(n => n.Timestamp)
        .Take(cappedLimit)
        .ToList();

    var grouped = orderedNodes
        .GroupBy(n => n.SessionId)
        .Select(g =>
        {
            var sessionNodes = g.OrderByDescending(n => n.Timestamp).ToList();
            return new
            {
                Id = g.Key,
                Label = g.Key,
                Nodes = sessionNodes,
                NodeCount = sessionNodes.Count,
                AvgPsi = sessionNodes.Average(n => n.Psi),
                LastModified = sessionNodes[0].Timestamp,
                Size = 16 + Math.Min(28, sessionNodes.Count * 2)
            };
        })
        .OrderByDescending(s => s.LastModified)
        .ToList();

    var nodeById = orderedNodes
        .Select(n => new { Id = GraphNodeId(n), Node = n })
        .GroupBy(x => x.Id)
        .ToDictionary(g => g.Key, g => g.First().Node);

    var sessions = grouped.Select(s => new
    {
        id = $"s:{s.Id}",
        label = s.Label,
        nodeCount = s.NodeCount,
        avgPsi = s.AvgPsi,
        lastModified = s.LastModified,
        size = s.Size
    }).ToList();

    var nodes = orderedNodes.Select(n => new
    {
        id = GraphNodeId(n),
        sessionId = n.SessionId,
        label = $"{n.Tier} {n.Timestamp:MM-dd HH:mm}",
        tier = n.Tier,
        timestamp = n.Timestamp,
        psi = n.Psi,
        parentNodeId = n.ParentNodeId,
        size = 9
    }).ToList();

    var edges = new List<object>();

    for (var i = 0; i < grouped.Count - 1; i++)
    {
        edges.Add(new
        {
            id = $"t-{i}",
            source = $"s:{grouped[i].Id}",
            target = $"s:{grouped[i + 1].Id}",
            kind = "timeline"
        });
    }

    for (var i = 0; i < grouped.Count; i++)
    {
        var from = grouped[i];
        var nearest = -1;
        var nearestDistance = float.MaxValue;
        for (var j = 0; j < grouped.Count; j++)
        {
            if (i == j) continue;
            var distance = MathF.Abs(from.AvgPsi - grouped[j].AvgPsi);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = j;
            }
        }

        if (nearest >= 0 && i < nearest)
        {
            edges.Add(new
            {
                id = $"s-{i}-{nearest}",
                source = $"s:{from.Id}",
                target = $"s:{grouped[nearest].Id}",
                kind = "similarity"
            });
        }
    }

    foreach (var session in grouped)
    {
        for (var i = 0; i < session.Nodes.Count; i++)
        {
            var current = session.Nodes[i];
            var currentId = GraphNodeId(current);

            edges.Add(new
            {
                id = $"m-{session.Id}-{i}",
                source = $"s:{session.Id}",
                target = currentId,
                kind = "membership"
            });

            if (i < session.Nodes.Count - 1)
            {
                var older = session.Nodes[i + 1];
                edges.Add(new
                {
                    id = $"nt-{session.Id}-{i}",
                    source = currentId,
                    target = GraphNodeId(older),
                    kind = "node_timeline"
                });
            }

            if (!string.IsNullOrWhiteSpace(current.ParentNodeId) && nodeById.ContainsKey(current.ParentNodeId))
            {
                edges.Add(new
                {
                    id = $"l-{session.Id}-{i}",
                    source = currentId,
                    target = current.ParentNodeId,
                    kind = "lineage"
                });
            }
        }
    }

    return Results.Ok(new
    {
        sessions,
        nodes,
        edges,
        retrieved = orderedNodes.Count
    });
}

static string? NormalizeTenantValue(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return null;

    var normalized = value.Trim().ToLowerInvariant();
    return normalized.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-' || ch == '_')
        ? normalized
        : null;
}

static (bool AllowAny, string[] Origins) ParseCorsAllowedOrigins(string? value)
{
    if (string.IsNullOrWhiteSpace(value) || value.Trim() == "*")
        return (true, []);

    var origins = value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (origins.Length == 0)
        throw new InvalidOperationException("CORS allowed origins must include at least one origin or '*'.");

    foreach (var origin in origins)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Invalid CORS origin '{origin}'. Use absolute http/https origins.");
        }
    }

    return (false, origins);
}

static string ResolveHttpTenant(string? explicitTenant, IHeaderDictionary? headers)
    => NormalizeTenantValue(explicitTenant)
        ?? ResolveTenantHeader(headers)
        ?? DefaultTenant;

static string? ResolveTenantHeader(IHeaderDictionary? headers)
{
    if (headers is null)
        return null;

    foreach (var header in new[] { "x-resonantia-tenant", TenantHeader, "x-tenant" })
    {
        var resolved = NormalizeTenantValue(headers[header].FirstOrDefault());
        if (resolved is not null)
            return resolved;
    }

    return null;
}

static string GraphNodeId(SttpNode node)
    => $"n:{node.SessionId}|{node.Timestamp:O}|{node.CompressionDepth}|{node.Psi:0.0000}";

static object ToNodeHttpDto(SttpNode node) => new
{
    raw = node.Raw,
    sessionId = node.SessionId,
    tier = node.Tier,
    timestamp = node.Timestamp,
    compressionDepth = node.CompressionDepth,
    parentNodeId = node.ParentNodeId,
    userAvec = node.UserAvec,
    modelAvec = node.ModelAvec,
    compressionAvec = node.CompressionAvec,
    rho = node.Rho,
    kappa = node.Kappa,
    psi = node.Psi,
    syncKey = node.SyncKey,
    syntheticId = GraphNodeId(node)
};

static string ScopeSessionId(string tenant, string sessionId)
    => string.Equals(tenant, DefaultTenant, StringComparison.Ordinal)
        ? sessionId
        : $"{TenantScopePrefix}{tenant}{TenantScopeSeparator}{sessionId}";

static string DisplaySessionId(string sessionId)
{
    if (!sessionId.StartsWith(TenantScopePrefix, StringComparison.Ordinal))
        return sessionId;

    var remainder = sessionId[TenantScopePrefix.Length..];
    var parts = remainder.Split(TenantScopeSeparator, 2, StringSplitOptions.None);
    return parts.Length == 2 ? parts[1] : sessionId;
}

static bool SessionBelongsToTenant(string sessionId, string tenant)
{
    if (!sessionId.StartsWith(TenantScopePrefix, StringComparison.Ordinal))
        return string.Equals(tenant, DefaultTenant, StringComparison.Ordinal);

    var remainder = sessionId[TenantScopePrefix.Length..];
    var parts = remainder.Split(TenantScopeSeparator, 2, StringSplitOptions.None);
    return parts.Length == 2 && string.Equals(parts[0], tenant, StringComparison.Ordinal);
}

static SttpNode? NormalizeNodeForTenant(SttpNode node, string tenant)
{
    if (!SessionBelongsToTenant(node.SessionId, tenant))
        return null;

    return node with { SessionId = DisplaySessionId(node.SessionId) };
}

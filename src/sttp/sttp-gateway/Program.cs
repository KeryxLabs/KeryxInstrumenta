using Microsoft.AspNetCore.Server.Kestrel.Core;
using SttpGateway.Contracts;
using SttpGateway.Services;
using SttpMcp.Application.Services;
using SttpMcp.Application.Validation;
using SttpMcp.Domain.Contracts;
using SttpMcp.Domain.Models;

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
    ["--grpc-port"] = "Gateway:GrpcPort"
};

var configArgs = args.Where(a => !string.Equals(a, "--remote", StringComparison.OrdinalIgnoreCase)).ToArray();
builder.Configuration.AddCommandLine(configArgs, switchMappings);

var httpPort = builder.Configuration.GetValue<int?>("Gateway:HttpPort") ?? 8080;
var grpcPort = builder.Configuration.GetValue<int?>("Gateway:GrpcPort") ?? 8081;

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

var storageRuntime = builder.Services.AddSttpSurrealDbStorage(builder.Configuration, args, ".sttp-gateway");

builder.Services
    .AddSingleton<INodeValidator, TreeSitterValidator>()
    .AddSttpCore();

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "STTP gateway startup | PID={Pid} | HttpPort={HttpPort} | GrpcPort={GrpcPort} | RootDir={RootDir} | Mode={Mode} | Endpoint={Endpoint} | Namespace={Namespace} | Database={Database}",
    Environment.ProcessId,
    httpPort,
    grpcPort,
    storageRuntime.RootDir,
    storageRuntime.UseRemote ? "remote" : "embedded",
    storageRuntime.Endpoint,
    storageRuntime.Namespace,
    storageRuntime.Database);

var storeInitializer = app.Services.GetService<INodeStoreInitializer>();
if (storeInitializer is not null)
    await storeInitializer.InitializeAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok", transport = "http+grpc" }));

app.MapPost("/api/v1/calibrate", async (CalibrateSessionHttpRequest request, CalibrationService service, CancellationToken ct) =>
{
    var trigger = string.IsNullOrWhiteSpace(request.Trigger) ? "manual" : request.Trigger;
    var result = await service.CalibrateAsync(
        request.SessionId,
        request.Stability,
        request.Friction,
        request.Logic,
        request.Autonomy,
        trigger,
        ct);

    return Results.Ok(result);
});

app.MapPost("/api/v1/store", async (StoreContextHttpRequest request, StoreContextService service, CancellationToken ct) =>
{
    var result = await service.StoreAsync(request.Node, request.SessionId, ct);
    return Results.Ok(result);
});

app.MapPost("/api/v1/context", async (GetContextHttpRequest request, ContextQueryService service, CancellationToken ct) =>
{
    var result = await service.GetContextAsync(
        request.SessionId,
        request.Stability,
        request.Friction,
        request.Logic,
        request.Autonomy,
        request.Limit,
        ct);

    return Results.Ok(result);
});

app.MapGet("/api/v1/nodes", async (int? limit, string? sessionId, ContextQueryService service, CancellationToken ct) =>
{
    var result = await service.ListNodesAsync(limit ?? 50, sessionId, ct);
    return Results.Ok(result);
});

app.MapGet("/api/v1/graph", async (int? limit, string? sessionId, ContextQueryService service, CancellationToken ct) =>
{
    var cappedLimit = Math.Clamp(limit ?? 1000, 1, 5000);
    var result = await service.ListNodesAsync(cappedLimit, sessionId, ct);

    var orderedNodes = result.Nodes
        .OrderByDescending(n => n.Timestamp)
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

    static string GetNodeId(SttpNode node)
        => $"n:{node.SessionId}|{node.Timestamp:O}|{node.CompressionDepth}|{node.Psi:0.0000}";

    var nodeById = orderedNodes
        .Select(n => new { Id = GetNodeId(n), Node = n })
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
        id = GetNodeId(n),
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
            var currentId = GetNodeId(current);

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
                    target = GetNodeId(older),
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
});

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

app.MapPost("/api/v1/rollups/monthly", async (CreateMonthlyRollupHttpRequest request, MonthlyRollupService service, CancellationToken ct) =>
{
    var rollupRequest = new MonthlyRollupRequest
    {
        SessionId = request.SessionId,
        StartUtc = request.StartDateUtc,
        EndUtc = request.EndDateUtc,
        SourceSessionId = request.SourceSessionId,
        ParentNodeId = request.ParentNodeId,
        Persist = request.Persist,
        Limit = request.Limit
    };

    var result = await service.CreateAsync(rollupRequest, ct);
    return Results.Ok(result);
});

app.MapGrpcService<SttpGrpcService>();
app.MapGrpcReflectionService();

app.Run();

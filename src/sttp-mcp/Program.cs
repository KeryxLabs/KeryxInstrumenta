using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SttpMcp.Application.Tools;
using SttpMcp.Domain.Contracts;
using SttpMcp.Storage;
using SttpMcp.Storage.SurrealDb;
using SttpMcp.Storage.SurrealDb.Models;
using SurrealDb.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = Host.CreateApplicationBuilder(args);
var baseDirectory = AppContext.BaseDirectory;
var appSettingsPath = Path.Combine(baseDirectory, "appsettings.json");
builder.Configuration.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true);


// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);


var surrealSettings = builder.Configuration.GetSection("SurrealDB").Get<SurrealDbSettings>() ?? throw new Exception("SurrealDb settings not passed");

if (surrealSettings.Endpoint.StartsWith("surrealkv://", StringComparison.OrdinalIgnoreCase))
{
    const string scheme = "surrealkv://";
    var endpointPath = surrealSettings.Endpoint[scheme.Length..];

    if (!Path.IsPathRooted(endpointPath))
    {
        var dataRoot = Environment.GetEnvironmentVariable("STTP_MCP_DATA_ROOT");
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            dataRoot = Path.Combine(home, ".sttp-mcp");
        }

        endpointPath = Path.GetFullPath(Path.Combine(dataRoot, endpointPath));
        surrealSettings.Endpoint = $"{scheme}{endpointPath}";
    }

    var dataDirectory = Path.GetDirectoryName(endpointPath);
    if (!string.IsNullOrWhiteSpace(dataDirectory))
        Directory.CreateDirectory(dataDirectory);
}

var options = SurrealDbOptions.Create()
    .WithEndpoint(surrealSettings.Endpoint)
    .WithNamespace(surrealSettings.Namespace)
    .WithDatabase(surrealSettings.Database)
    .Build();

builder.Services
    .AddSurreal(options)
    .AddSurrealKvProvider();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services   
    .AddSingleton<INodeStore, SurrealDbNodeStore>()
    .AddSingleton<INodeValidator, TreeSitterValidator>()
    .AddSingleton<CalibrateSessionTool>()
    .AddSingleton<StoreContextTool>()
    .AddSingleton<GetContextTool>()
    .AddSingleton<ListNodesTool>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<CalibrateSessionTool>()
    .WithTools<StoreContextTool>()
    .WithTools<GetContextTool>()
    .WithTools<ListNodesTool>();


var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "STTP startup path resolution | PID={Pid} | BaseDirectory={BaseDirectory} | CWD={Cwd} | Endpoint={Endpoint} | Namespace={Namespace} | Database={Database}",
    Environment.ProcessId,
    baseDirectory,
    Environment.CurrentDirectory,
    surrealSettings.Endpoint,
    surrealSettings.Namespace,
    surrealSettings.Database);

// bootstrap schema on startup
var store = app.Services.GetRequiredService<INodeStore>();
if (store is SurrealDbNodeStore surrealStore)
    await surrealStore.InitializeAsync();

await app.RunAsync();

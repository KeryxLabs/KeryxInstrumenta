using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using SttpMcp.Application.Tools;
using SttpMcp.Domain.Contracts;
using SttpMcp.Storage.SurrealDb;
using SttpMcp.Storage.SurrealDb.Models;


var builder = Host.CreateApplicationBuilder(args);
var useRemote = Array.Exists(args, a => string.Equals(a, "--remote", StringComparison.OrdinalIgnoreCase));
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var rootDir = Path.Combine(home, ".sttp-mcp");

var appSettingsPath = Path.Combine(rootDir, "appsettings.json");
builder.Configuration.AddJsonFile(appSettingsPath, optional: true, reloadOnChange: true);


// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);


var surrealSettings = builder.Configuration.GetSection("SurrealDB").Get<SurrealDbSettings>() ?? throw new Exception("SurrealDb settings not passed");

var dbEndpoint = surrealSettings.Endpoint(useRemote);

if (!useRemote)
{
    const string scheme = "surrealkv://";
    var endpointPath = dbEndpoint[scheme.Length..];

    if (!Path.IsPathRooted(endpointPath))
    {
        endpointPath = Path.GetFullPath(Path.Combine(rootDir, endpointPath));
    }

    var dataDirectory = Path.GetDirectoryName(endpointPath);
    if (!string.IsNullOrWhiteSpace(dataDirectory))
        Directory.CreateDirectory(dataDirectory);
}

var options = SurrealDbOptions.Create()
    .WithEndpoint(dbEndpoint)
    .WithNamespace(surrealSettings.Namespace)
    .WithDatabase(surrealSettings.Database);
    

if (useRemote){
    options
    .WithUsername(surrealSettings.User)
    .WithPassword(surrealSettings.Password);
}
    

var surrealServices = builder.Services.AddSurreal(options.Build());
//allow provider when not remote
if (!useRemote)
    surrealServices.AddSurrealKvProvider();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services   
    .AddSingleton<INodeStore, SurrealDbNodeStore>()
    .AddSingleton<INodeValidator, TreeSitterValidator>()
    .AddSingleton<CalibrateSessionTool>()
    .AddSingleton<StoreContextTool>()
    .AddSingleton<GetContextTool>()
    .AddSingleton<ListNodesTool>()
    .AddSingleton<GetMoodsTool>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<CalibrateSessionTool>()
    .WithTools<StoreContextTool>()
    .WithTools<GetContextTool>()
    .WithTools<ListNodesTool>()
    .WithTools<GetMoodsTool>();


var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "STTP startup path resolution | PID={Pid} | BaseDirectory={BaseDirectory} | CWD={Cwd} | Mode={Mode} | Endpoint={Endpoint} | Namespace={Namespace} | Database={Database}",
    Environment.ProcessId,
    rootDir,
    Environment.CurrentDirectory,
    useRemote ? "remote" : "embedded",
    dbEndpoint,
    surrealSettings.Namespace,
    surrealSettings.Database);

// bootstrap schema on startup
var store = app.Services.GetRequiredService<INodeStore>();
if (store is SurrealDbNodeStore surrealStore)
    await surrealStore.InitializeAsync();

await app.RunAsync();

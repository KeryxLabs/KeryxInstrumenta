using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SttpMcp.Application.Validation;
using SttpMcp.Application.Tools;
using SttpMcp.Domain.Contracts;


var builder = Host.CreateApplicationBuilder(args);

// Load appsettings from ~/.sttp-mcp/ (optional — falls back to built-in defaults).
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var rootDir = Path.Combine(home, ".sttp-mcp");
var appSettingsPath = Path.Combine(rootDir, "appsettings.json");
builder.Configuration.AddJsonFile(appSettingsPath, optional: true, reloadOnChange: true);

// CLI flags override everything. Supported flags:
//   --remote                                  use remote WebSocket endpoint
//   --endpoint <url>                          override the active endpoint URL
//   --remote-endpoint <url>                   set SurrealDb:Endpoints:Remote
//   --embedded-endpoint <path>                set SurrealDb:Endpoints:Embedded
//   --namespace <ns>                          set SurrealDb:Namespace
//   --database <db>                           set SurrealDb:Database
//   --username <user>                         set SurrealDb:User
//   --password <pass>                         set SurrealDb:Password
var switchMappings = new Dictionary<string, string>
{
    ["--remote-endpoint"] = "SurrealDb:Endpoints:Remote",
    ["--embedded-endpoint"] = "SurrealDb:Endpoints:Embedded",
    ["--namespace"] = "SurrealDb:Namespace",
    ["--database"] = "SurrealDb:Database",
    ["--username"] = "SurrealDb:User",
    ["--password"] = "SurrealDb:Password",
};
// Strip bare --remote flag before passing to AddCommandLine (it has no value).
var configArgs = args.Where(a => !string.Equals(a, "--remote", StringComparison.OrdinalIgnoreCase)).ToArray();
builder.Configuration.AddCommandLine(configArgs, switchMappings);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var storageRuntime = builder.Services.AddSttpSurrealDbStorage(builder.Configuration, args);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services   
    .AddSingleton<INodeValidator, TreeSitterValidator>()
    .AddSttpCore()
    .AddSingleton<CalibrateSessionTool>()
    .AddSingleton<StoreContextTool>()
    .AddSingleton<GetContextTool>()
    .AddSingleton<ListNodesTool>()
    .AddSingleton<GetMoodsTool>()
    .AddSingleton<CreateMonthlyRollupTool>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<CalibrateSessionTool>()
    .WithTools<StoreContextTool>()
    .WithTools<GetContextTool>()
    .WithTools<ListNodesTool>()
    .WithTools<GetMoodsTool>()
    .WithTools<CreateMonthlyRollupTool>();


var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
startupLogger.LogInformation(
    "STTP startup path resolution | PID={Pid} | BaseDirectory={BaseDirectory} | CWD={Cwd} | Mode={Mode} | Endpoint={Endpoint} | Namespace={Namespace} | Database={Database}",
    Environment.ProcessId,
    storageRuntime.RootDir,
    Environment.CurrentDirectory,
    storageRuntime.UseRemote ? "remote" : "embedded",
    storageRuntime.Endpoint,
    storageRuntime.Namespace,
    storageRuntime.Database);

// bootstrap schema on startup
var storeInitializer = app.Services.GetService<INodeStoreInitializer>();
if (storeInitializer is not null)
    await storeInitializer.InitializeAsync();

await app.RunAsync();

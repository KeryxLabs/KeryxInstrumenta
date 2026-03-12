using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using SurrealDb.Net;
using AdaptiveCodecContextEngine.Models.Git;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Surreal;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// --- 1. Setup Paths & Files ---
var rootDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".acc-engine");
var appSettingsPath = Path.Combine(rootDir, "appsettings.json");

builder.Configuration
    .AddJsonFile(appSettingsPath, optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// --- 2. Bind Configuration (AOT Safe) ---
builder.Services.Configure<SurrealDbSettings>(builder.Configuration.GetSection("SurrealDB"));
builder.Services.Configure<AccOptions>(builder.Configuration.GetSection("Acc"));
builder.Services.Configure<AvecWeights>(builder.Configuration.GetSection("AvecWeights"));

// Extract values needed for DB setup immediately
var surrealSettings = new SurrealDbSettings();
builder.Configuration.GetSection("SurrealDB").Bind(surrealSettings);

if (string.IsNullOrEmpty(surrealSettings.Endpoint())) 
    throw new Exception("SurrealDb settings missing or invalid.");

// --- 3. Database & Directory Setup ---
var dbEndpoint = surrealSettings.Endpoint();
if (dbEndpoint.StartsWith("surrealkv://"))
{
    var path = dbEndpoint["surrealkv://".Length..];
    var fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(rootDir, path));
    var dir = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
}

builder.Services.AddSurreal(dbEndpoint)
    .AddSurrealKvProvider();

// --- 4. Register Services ---
// Channels
builder.Services.AddSingleton(_ => Channel.CreateUnbounded<LspMessage>());
builder.Services.AddSingleton(_ => Channel.CreateUnbounded<GitEvent>());
builder.Services.AddSingleton(_ => Channel.CreateUnbounded<NodeUpdate>());
builder.Services.AddSingleton(_ => Channel.CreateUnbounded<DependencyEdge>());

// Logic Services (Using IOptions for AOT safety)
builder.Services.AddSingleton<AvecCalculator>(); 
builder.Services.AddSingleton<SurrealDbRepository>();
builder.Services.AddSingleton<LspReferenceTracker>();
builder.Services.AddSingleton<LizardAnalyzer>();
builder.Services.AddSingleton<MetricsCollector>();

builder.Services.AddSingleton<LspClient>(_ => new LspClient(Console.OpenStandardInput()));

builder.Services.AddSingleton<GitWatcher>(sp => {
    var acc = sp.GetRequiredService<IOptions<AccOptions>>().Value;
    var gitChannel = sp.GetRequiredService<Channel<GitEvent>>();
    return new GitWatcher(acc.RepositoryPath, gitChannel);
});

builder.Services.AddHostedService<AccHostedService>();

// --- 5. Build and Initialize ---
var host = builder.Build();

// Scope to ensure DB is ready before hosted services kick in
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ISurrealDbClient>();
    // Use settings from config
    await db.Use(surrealSettings.Namespace, surrealSettings.Database);
}

await host.RunAsync();


public class AccOptions
{
    public string RepositoryPath { get; set; } = null!;
    public string SurrealDbConnection { get; set; } = "ws://localhost:8000";
    public string Language { get; set; } = "csharp";
    public AvecTarget? Target { get; set; }
}

public class AvecTarget
{
    public double Stability { get; set; }
    public double Logic { get; set; }
    public double Friction { get; set; }
    public double Autonomy { get; set; }
}

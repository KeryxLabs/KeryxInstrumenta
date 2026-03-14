using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using SurrealDb.Net;
using AdaptiveCodecContextEngine.Models.Git;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Surreal;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;


using System.Diagnostics.CodeAnalysis;
using SurrealDb.Net.Models.Response;
using Dahomey.Cbor.Serialization.Converters;

// This forces the compiler to see the link between the List and the Interface
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MemberConverter<ProjectStatsDto, int>))]
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MemberConverter<ProjectStatsDto, string>))] // Add one for each property type in your DTO
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ObjectConverter<ProjectStatsDto>))]
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<ISurrealDbResult>))]
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ISurrealDbResult))]
static void PreserveSurrealInternalTypes() {}

PreserveSurrealInternalTypes();

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



var options = SurrealDbOptions.Create()
    .WithEndpoint(dbEndpoint)
    .WithNamespace(surrealSettings.Namespace)
    .WithDatabase(surrealSettings.Database)
    .WithUsername(surrealSettings.User)
    .WithPassword(surrealSettings.Password)
    .Build();
    
builder.Services.AddSurreal(options);
   //.AddSurrealKvProvider();

// --- 4. Register Services ---
// Channels
builder.Services
.AddSingleton(_ => Channel.CreateUnbounded<LspMessageWithContext>())
.AddSingleton(_ => Channel.CreateUnbounded<GitEvent>())
.AddSingleton(_ => Channel.CreateUnbounded<NodeUpdate>())
.AddSingleton(_ => Channel.CreateUnbounded<DependencyEdge>());


// Logic Services (Using IOptions for AOT safety)
builder.Services
.AddSingleton<AvecCalculator>()
.AddSingleton<SurrealDbRepository>()
.AddSingleton<LspReferenceTracker>()
.AddSingleton<LizardAnalyzer>()
.AddSingleton<MetricsCollector>()
.AddSingleton<IAccQueryService,AccQueryService>()
.AddHostedService<JsonRpcServer>();

builder.Services
.AddSingleton<LspStreamManager>()
.AddSingleton<GitWatcher>()
.AddHostedService<AccHostedService>();


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
    public AvecTarget? Target { get; set; }
    public string[] FileExtensions {get;set;}= [];
    public List<LspStreamConfig> LspStreams { get; set; } = [];
}

public class AvecTarget
{
    public double Stability { get; set; }
    public double Logic { get; set; }
    public double Friction { get; set; }
    public double Autonomy { get; set; }
}

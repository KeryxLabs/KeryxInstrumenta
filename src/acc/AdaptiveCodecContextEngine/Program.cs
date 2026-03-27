using System.Diagnostics.CodeAnalysis;
using System.Text;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Git;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models.Surreal;
using Dahomey.Cbor.Serialization.Converters;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SurrealDb.Net;
using SurrealDb.Net.Models.Response;

// This forces the compiler to see the link between the List and the Interface
[DynamicDependency(
    DynamicallyAccessedMemberTypes.All,
    typeof(MemberConverter<ProjectStatsDto, int>)
)]
[DynamicDependency(
    DynamicallyAccessedMemberTypes.All,
    typeof(MemberConverter<ProjectStatsDto, string>)
)] // Add one for each property type in your DTO
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ObjectConverter<ProjectStatsDto>))]
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<ISurrealDbResult>))]
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ISurrealDbResult))]
static void PreserveSurrealInternalTypes() { }

PreserveSurrealInternalTypes();

var builder = Host.CreateApplicationBuilder(args);

// --- 1. Setup Paths & Files ---
var rootDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".acc-engine"
);
var appSettingsPath = Path.Combine(rootDir, "appsettings.json");

builder
    .Configuration.AddJsonFile(appSettingsPath, optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Extract values needed for DB setup immediately
var surrealSettings = new SurrealDbSettings();
builder.Configuration.GetSection("SurrealDb").Bind(surrealSettings);

// --- 2. Bind Configuration (AOT Safe) ---

builder.Services.Configure<AccOptions>(builder.Configuration.GetSection("Acc"));
builder.Services.Configure<AvecWeights>(builder.Configuration.GetSection("AvecWeights"));

if (string.IsNullOrEmpty(surrealSettings.Endpoint()))
    throw new Exception("SurrealDb settings missing or invalid.");

// --- 3. Database & Directory Setup ---
var dbEndpoint = surrealSettings.Endpoint(surrealSettings.Remote);
if (dbEndpoint.StartsWith("surrealkv://"))
{
    var path = dbEndpoint["surrealkv://".Length..];
    var fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(rootDir, path));
    var dir = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);
}

// var repoPathHash =builder.Configuration.GetSection("Acc").Get<AccOptions>()?.RepositoryPath.ComputeStableHash();
// var dbName = $"{surrealSettings.Database}_{repoPathHash}";

var accOptions =
    builder.Configuration.GetSection("Acc").Get<AccOptions>()
    ?? throw new InvalidOperationException("ACC configuration missing");

if (accOptions.UseGitBranchNaming)
{
    using var repo = new Repository(accOptions.RepositoryPath);
    // Get the current branch name using the Head.FriendlyName property
    string branchName = repo.Head.FriendlyName.Replace("/", "_");
    DirectoryInfo info = new(accOptions.RepositoryPath);

    surrealSettings.Namespace = $"{surrealSettings.Namespace}_{info.Name}";
    surrealSettings.Database = $"{surrealSettings.Database}_{branchName}";
}

var options = SurrealDbOptions
    .Create()
    .WithEndpoint(dbEndpoint)
    .WithNamespace(surrealSettings.Namespace)
    .WithDatabase(surrealSettings.Database)
    .WithUsername(surrealSettings.User)
    .WithPassword(surrealSettings.Password)
    .Build();

if (surrealSettings.Remote)
    builder.Services.AddSurreal(options);
else
    builder.Services.AddSurreal(options).AddSurrealKvProvider();
Console.WriteLine($"Database: {surrealSettings.Database} NameSpace: {surrealSettings.Namespace}");
builder.Services.Configure<SurrealDbSettings>(builder.Configuration.GetSection("SurrealDb"));

// --- 4. Register Services ---
// Channels
builder
    .Services.AddSingleton(_ =>
        Channel.CreateBounded<LspMessageWithContext>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait }
        )
    )
    .AddSingleton(_ =>
        Channel.CreateBounded<GitEventWithContext>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait }
        )
    )
    .AddSingleton(_ =>
        Channel.CreateBounded<NodeUpdateWithContext>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait }
        )
    )
    .AddSingleton(_ =>
        Channel.CreateBounded<DependencyEdgeWithContext>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait }
        )
    )
    .AddSingleton(_ =>
        Channel.CreateBounded<InitialIndexingMessageWithContext>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait }
        )
    );
;

// Logic Services (Using IOptions for AOT safety)
builder
    .Services.AddSingleton<AvecCalculator>()
    .AddSingleton<SurrealDbRepository>()
    .AddSingleton<LspReferenceTracker>()
    .AddSingleton<LizardAnalyzer>()
    .AddSingleton<MetricsCollector>()
    .AddSingleton<IAccQueryService, AccQueryService>()
    .AddKeyedTransient<GitClient>(GitClient.ServiceName)
    .AddHostedService<JsonRpcServer>();

builder
    .Services.AddSingleton<LspStreamManager>()
    .AddSingleton<GitWatcher>()
    .AddHostedService<AccHostedService>();

builder.Services.AddTelemetry(builder.Configuration);

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
    public string[] FileExtensions { get; set; } = [];
    public List<LspStreamConfig> LspStreams { get; set; } = [];
    public bool UseGitBranchNaming { get; set; } = true;
}

public class AvecTarget
{
    public double Stability { get; set; }
    public double Logic { get; set; }
    public double Friction { get; set; }
    public double Autonomy { get; set; }
}

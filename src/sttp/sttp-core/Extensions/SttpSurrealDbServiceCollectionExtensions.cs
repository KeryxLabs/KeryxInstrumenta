using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SttpMcp.Domain.Contracts;
using SttpMcp.Storage.SurrealDb;
using SttpMcp.Storage.SurrealDb.Models;
using SurrealDb.Net;

namespace Microsoft.Extensions.DependencyInjection;

public static class SttpSurrealDbServiceCollectionExtensions
{
    public static SttpSurrealDbRuntimeOptions AddSttpSurrealDbStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        string[] args,
        string rootDirectoryName = ".sttp-mcp")
    {
        var useRemote = Array.Exists(args, a => string.Equals(a, "--remote", StringComparison.OrdinalIgnoreCase));
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configuredRoot = string.IsNullOrWhiteSpace(rootDirectoryName) ? ".sttp-mcp" : rootDirectoryName;
        var rootDir = Path.Combine(home, configuredRoot);

        var surrealSettings = configuration.GetSection("SurrealDb").Get<SurrealDbSettings>()
            ?? throw new Exception("SurrealDb settings not passed");

        var dbEndpoint = surrealSettings.Endpoint(useRemote);

        if (!useRemote)
        {
            const string scheme = "surrealkv://";
            var endpointPath = dbEndpoint[scheme.Length..];

            if (!Path.IsPathRooted(endpointPath))
                endpointPath = Path.GetFullPath(Path.Combine(rootDir, endpointPath));

            var dataDirectory = Path.GetDirectoryName(endpointPath);
            if (!string.IsNullOrWhiteSpace(dataDirectory))
                Directory.CreateDirectory(dataDirectory);
        }

        var options = SurrealDbOptions.Create()
            .WithEndpoint(dbEndpoint)
            .WithNamespace(surrealSettings.Namespace)
            .WithDatabase(surrealSettings.Database);

        if (useRemote)
            options.WithUsername(surrealSettings.User).WithPassword(surrealSettings.Password);

        var surrealServices = services.AddSurreal(options.Build());
        if (!useRemote)
            surrealServices.AddSurrealKvProvider();

        services.AddSingleton<SurrealDbNodeStore>();
        services.AddSingleton<INodeStore>(sp => sp.GetRequiredService<SurrealDbNodeStore>());
        services.AddSingleton<INodeStoreInitializer>(sp => sp.GetRequiredService<SurrealDbNodeStore>());

        var runtime = new SttpSurrealDbRuntimeOptions
        {
            RootDir = rootDir,
            UseRemote = useRemote,
            Endpoint = dbEndpoint,
            Namespace = surrealSettings.Namespace,
            Database = surrealSettings.Database
        };

        services.AddSingleton(runtime);
        return runtime;
    }
}

public sealed record SttpSurrealDbRuntimeOptions
{
    public required string RootDir { get; init; }
    public required bool UseRemote { get; init; }
    public required string Endpoint { get; init; }
    public required string Namespace { get; init; }
    public required string Database { get; init; }
}
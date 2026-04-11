using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SttpMcp.Application.Services;
using SttpMcp.Domain.Contracts;

namespace Microsoft.Extensions.DependencyInjection;

public static class SttpServiceCollectionExtensions
{
    public static IServiceCollection AddSttpCore(this IServiceCollection services)
    {
        services.AddSingleton<CalibrationService>();
        services.AddSingleton<ContextQueryService>();
        services.AddSingleton<MoodCatalogService>();
        services.AddSingleton<StoreContextService>();
        services.AddSingleton<MonthlyRollupService>();
        services.AddSingleton<RekeyScopeService>();
        return services;
    }

    public static IServiceCollection AddSttpSyncCoordinator<TChangeSource>(this IServiceCollection services)
        where TChangeSource : class, ISyncChangeSource
    {
        services.AddSingleton<ISyncChangeSource, TChangeSource>();
        services.AddSingleton<SyncCoordinatorService>(sp => new SyncCoordinatorService(
            sp.GetRequiredService<INodeStore>(),
            sp.GetRequiredService<ISyncChangeSource>(),
            sp.GetRequiredService<ILogger<SyncCoordinatorService>>(),
            sp.GetService<ISyncCoordinatorPolicy>()));
        return services;
    }

    public static IServiceCollection AddSttpSyncCoordinator<TChangeSource, TPolicy>(this IServiceCollection services)
        where TChangeSource : class, ISyncChangeSource
        where TPolicy : class, ISyncCoordinatorPolicy
    {
        services.AddSingleton<ISyncCoordinatorPolicy, TPolicy>();
        return services.AddSttpSyncCoordinator<TChangeSource>();
    }
}
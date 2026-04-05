using Microsoft.Extensions.DependencyInjection;
using SttpMcp.Application.Services;

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
        return services;
    }
}
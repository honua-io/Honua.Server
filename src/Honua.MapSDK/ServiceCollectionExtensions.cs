using Honua.MapSDK.Core;
using Honua.MapSDK.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.MapSDK;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Honua MapSDK services
    /// </summary>
    public static IServiceCollection AddHonuaMapSDK(this IServiceCollection services)
    {
        // Register ComponentBus as scoped (one per Blazor circuit)
        services.AddScoped<ComponentBus>();

        // Register configuration service
        services.AddScoped<IMapConfigurationService, MapConfigurationService>();

        return services;
    }
}

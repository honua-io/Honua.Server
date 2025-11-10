// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Honua.Server.Core.Elevation;

/// <summary>
/// Extension methods for registering elevation services.
/// </summary>
public static class ElevationServiceExtensions
{
    /// <summary>
    /// Adds elevation services to the service collection.
    /// Registers a composite elevation service with attribute and default providers.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddElevationServices(this IServiceCollection services)
    {
        Guard.NotNull(services);

        // Register elevation providers
        services.TryAddSingleton<AttributeElevationService>();
        services.TryAddSingleton<DefaultElevationService>();

        // Register composite elevation service
        services.TryAddSingleton<IElevationService>(sp =>
        {
            var providers = new List<IElevationService>
            {
                // Attribute provider first (highest priority)
                sp.GetRequiredService<AttributeElevationService>(),
                // Default provider last (fallback)
                sp.GetRequiredService<DefaultElevationService>()
            };

            return new CompositeElevationService(providers);
        });

        return services;
    }

    /// <summary>
    /// Adds a custom elevation service provider to the service collection.
    /// </summary>
    /// <typeparam name="TProvider">The elevation service provider type</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddElevationProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IElevationService
    {
        Guard.NotNull(services);

        services.AddSingleton<TProvider>();
        return services;
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Data;
using Honua.Server.Core.HealthChecks;
using Honua.Server.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Honua.Server.Core.DependencyInjection;

/// <summary>
/// Extension methods for configuring cold start optimizations.
/// </summary>
/// <remarks>
/// These extensions add services that improve startup performance in serverless deployments
/// by deferring non-critical initialization to background tasks and implementing lazy loading.
/// </remarks>
public static class ColdStartOptimizationExtensions
{
    /// <summary>
    /// Adds cold start optimization services including connection pool warmup,
    /// lazy Redis initialization, and startup profiling.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method configures:
    /// - Connection pool warmup (background initialization)
    /// - Lazy Redis connection establishment
    /// - Warmup health checks for Kubernetes readiness probes
    /// - Metadata cache warmup service
    ///
    /// Example usage in Program.cs:
    /// <code>
    /// builder.Services.AddColdStartOptimizations(builder.Configuration);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddColdStartOptimizations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure connection pool warmup options
        services.Configure<ConnectionPoolWarmupOptions>(
            configuration.GetSection(ConnectionPoolWarmupOptions.SectionName));

        // Register connection pool warmup service
        services.AddHostedService<ConnectionPoolWarmupService>();

        // Register lazy Redis initializer
        // This defers Redis connection establishment to background, improving startup time
        services.AddSingleton<LazyRedisInitializer>();
        services.AddHostedService(sp => sp.GetRequiredService<LazyRedisInitializer>());

        // Register warmup services for health check integration
        services.AddSingleton<IWarmupService, MetadataCacheWarmupService>();

        // Register warmup health check
        // This triggers lazy service initialization on first health check
        services.AddHealthChecks()
            .AddCheck<WarmupHealthCheck>(
                "warmup",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "ready" });

        return services;
    }

    /// <summary>
    /// Adds only connection pool warmup without other cold start optimizations.
    /// Useful if you want fine-grained control over which optimizations to enable.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConnectionPoolWarmup(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ConnectionPoolWarmupOptions>(
            configuration.GetSection(ConnectionPoolWarmupOptions.SectionName));

        services.AddHostedService<ConnectionPoolWarmupService>();

        return services;
    }

    /// <summary>
    /// Adds only lazy Redis initialization without other cold start optimizations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLazyRedisInitialization(
        this IServiceCollection services)
    {
        services.AddSingleton<LazyRedisInitializer>();
        services.AddHostedService(sp => sp.GetRequiredService<LazyRedisInitializer>());

        return services;
    }

    /// <summary>
    /// Registers a custom warmup service that will be invoked during health check warmup.
    /// </summary>
    /// <typeparam name="TService">The warmup service type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Example:
    /// <code>
    /// services.AddWarmupService&lt;MyCustomWarmupService&gt;();
    ///
    /// public class MyCustomWarmupService : IWarmupService
    /// {
    ///     public async Task WarmupAsync(CancellationToken cancellationToken)
    ///     {
    ///         // Pre-load data, establish connections, etc.
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddWarmupService<TService>(
        this IServiceCollection services)
        where TService : class, IWarmupService
    {
        services.AddSingleton<IWarmupService, TService>();
        return services;
    }
}

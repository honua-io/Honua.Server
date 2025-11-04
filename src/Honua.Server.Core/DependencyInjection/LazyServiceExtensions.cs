// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering services with lazy initialization to improve cold start performance.
/// </summary>
/// <remarks>
/// Lazy service registration defers expensive service initialization until first use,
/// significantly reducing cold start times in serverless deployments.
///
/// Example:
/// Instead of:
///   services.AddSingleton&lt;IHeavyService, HeavyService&gt;(); // Instantiated at startup
///
/// Use:
///   services.AddLazySingleton&lt;IHeavyService, HeavyService&gt;(); // Instantiated on first use
/// </remarks>
public static class LazyServiceExtensions
{
    /// <summary>
    /// Registers a singleton service with lazy initialization.
    /// The service is not instantiated until the first time it's requested.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <typeparam name="TImplementation">The concrete implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLazySingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        // Register the actual implementation as singleton
        services.AddSingleton<TImplementation>();

        // Register a lazy wrapper that resolves the implementation on first access
        services.AddSingleton<TService>(sp =>
        {
            var lazy = new Lazy<TImplementation>(() => sp.GetRequiredService<TImplementation>());
            return lazy.Value;
        });

        return services;
    }

    /// <summary>
    /// Registers a singleton service with lazy initialization using a factory.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">The factory function to create the service.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLazySingleton<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService> factory)
        where TService : class
    {
        services.AddSingleton<TService>(sp =>
        {
            var lazy = new Lazy<TService>(() => factory(sp));
            return lazy.Value;
        });

        return services;
    }

    /// <summary>
    /// Registers a Lazy&lt;T&gt; wrapper for a service, allowing explicit lazy loading.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This allows consuming code to explicitly control when initialization happens:
    /// <code>
    /// public class MyController
    /// {
    ///     private readonly Lazy&lt;IHeavyService&gt; _service;
    ///
    ///     public MyController(Lazy&lt;IHeavyService&gt; service)
    ///     {
    ///         _service = service;
    ///     }
    ///
    ///     public void DoWork()
    ///     {
    ///         // Service is only initialized when .Value is accessed
    ///         _service.Value.DoSomething();
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddLazyWrapper<TService>(this IServiceCollection services)
        where TService : class
    {
        services.AddSingleton(sp => new Lazy<TService>(() => sp.GetRequiredService<TService>()));
        return services;
    }
}

/// <summary>
/// Generic lazy service wrapper for dependency injection.
/// </summary>
/// <typeparam name="T">The service type to lazily load.</typeparam>
/// <remarks>
/// This wrapper provides thread-safe lazy initialization for any service.
/// Useful for heavy services that should only be instantiated when actually needed.
///
/// Example usage:
/// <code>
/// services.AddSingleton(typeof(LazyService&lt;&gt;), typeof(LazyService&lt;&gt;));
///
/// public class MyController
/// {
///     private readonly LazyService&lt;IHeavyService&gt; _lazyService;
///
///     public MyController(LazyService&lt;IHeavyService&gt; lazyService)
///     {
///         _lazyService = lazyService;
///     }
///
///     public void DoWork()
///     {
///         // Service is initialized on first access to Value
///         _lazyService.Value.DoSomething();
///     }
/// }
/// </code>
/// </remarks>
public sealed class LazyService<T> where T : class
{
    private readonly Lazy<T> _lazy;

    /// <summary>
    /// Initializes a new instance of the LazyService wrapper.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve the service from.</param>
    public LazyService(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _lazy = new Lazy<T>(() => serviceProvider.GetRequiredService<T>());
    }

    /// <summary>
    /// Gets the lazily-initialized service instance.
    /// The service is instantiated on first access to this property.
    /// </summary>
    public T Value => _lazy.Value;

    /// <summary>
    /// Gets whether the service has been instantiated yet.
    /// </summary>
    public bool IsValueCreated => _lazy.IsValueCreated;
}

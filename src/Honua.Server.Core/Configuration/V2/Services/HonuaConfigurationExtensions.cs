// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Configuration.V2.Services;

/// <summary>
/// Extension methods for configuring Honua from declarative configuration.
/// </summary>
public static class HonuaConfigurationExtensions
{
    /// <summary>
    /// Configure services from Honua configuration.
    /// Automatically discovers and registers all enabled services.
    /// </summary>
    public static IServiceCollection AddHonuaFromConfiguration(
        this IServiceCollection services,
        HonuaConfig config,
        Action<HonuaConfigurationOptions>? configure = null)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var options = new HonuaConfigurationOptions();
        configure?.Invoke(options);

        // Store configuration in DI for later access
        services.AddSingleton(config);

        // Discover service registrations
        var discovery = new ServiceRegistrationDiscovery();
        if (options.AssembliesToScan.Count > 0)
        {
            discovery.DiscoverServices(options.AssembliesToScan.ToArray());
        }
        else
        {
            discovery.DiscoverAllServices();
        }

        // Store discovery for endpoint mapping
        services.AddSingleton(discovery);

        // Register enabled services
        var registeredServices = new List<string>();
        var validationErrors = new List<string>();

        foreach (var (serviceId, serviceConfig) in config.Services)
        {
            if (!serviceConfig.Enabled)
            {
                continue; // Skip disabled services
            }

            var registration = discovery.GetService(serviceConfig.Type ?? serviceId);
            if (registration == null)
            {
                var warning = $"Service '{serviceId}' (type: '{serviceConfig.Type}') is enabled in configuration but no registration found. Available services: {string.Join(", ", discovery.GetAllServices().Keys)}";
                validationErrors.Add(warning);
                continue;
            }

            // Validate service configuration
            var validationResult = registration.ValidateConfiguration(serviceConfig);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    validationErrors.Add($"Service '{serviceId}': {error}");
                }
                continue;
            }

            // Register service
            try
            {
                registration.ConfigureServices(services, serviceConfig);
                registeredServices.Add(serviceId);
            }
            catch (Exception ex)
            {
                validationErrors.Add($"Failed to register service '{serviceId}': {ex.Message}");
            }
        }

        // Store metadata about registered services
        services.AddSingleton(new HonuaServiceMetadata(registeredServices, validationErrors));

        if (validationErrors.Count > 0 && options.ThrowOnValidationErrors)
        {
            throw new InvalidOperationException(
                $"Service configuration validation failed:\n{string.Join("\n", validationErrors)}");
        }

        return services;
    }

    /// <summary>
    /// Map Honua service endpoints from configuration.
    /// Automatically maps all enabled service endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapHonuaEndpoints(
        this IEndpointRouteBuilder endpoints,
        HonuaConfig? config = null)
    {
        // Get config from DI if not provided
        config ??= endpoints.ServiceProvider.GetRequiredService<HonuaConfig>();

        // Get service discovery
        var discovery = endpoints.ServiceProvider.GetRequiredService<ServiceRegistrationDiscovery>();

        // Get logger
        var loggerFactory = endpoints.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Honua.Configuration");

        // Map endpoints for enabled services
        foreach (var (serviceId, serviceConfig) in config.Services)
        {
            if (!serviceConfig.Enabled)
            {
                continue;
            }

            var registration = discovery.GetService(serviceConfig.Type ?? serviceId);
            if (registration == null)
            {
                logger?.LogWarning("Service '{ServiceId}' is enabled but no registration found", serviceId);
                continue;
            }

            try
            {
                registration.MapEndpoints(endpoints, serviceConfig);
                logger?.LogInformation("Mapped endpoints for service '{ServiceId}' ({DisplayName})",
                    serviceId, registration.DisplayName);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to map endpoints for service '{ServiceId}'", serviceId);
                throw;
            }
        }

        return endpoints;
    }
}

/// <summary>
/// Options for Honua configuration.
/// </summary>
public sealed class HonuaConfigurationOptions
{
    /// <summary>
    /// Assemblies to scan for service registrations.
    /// If empty, all Honua.Server.* assemblies will be scanned.
    /// </summary>
    public List<System.Reflection.Assembly> AssembliesToScan { get; } = new();

    /// <summary>
    /// Whether to throw an exception if service validation fails.
    /// Default: true (fail fast).
    /// </summary>
    public bool ThrowOnValidationErrors { get; set; } = true;
}

/// <summary>
/// Metadata about registered Honua services.
/// </summary>
public sealed class HonuaServiceMetadata
{
    public IReadOnlyList<string> RegisteredServices { get; }
    public IReadOnlyList<string> ValidationErrors { get; }

    public HonuaServiceMetadata(IEnumerable<string> registeredServices, IEnumerable<string> validationErrors)
    {
        RegisteredServices = registeredServices.ToList();
        ValidationErrors = validationErrors.ToList();
    }
}

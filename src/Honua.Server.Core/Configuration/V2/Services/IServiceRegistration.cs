// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Configuration.V2.Services;

/// <summary>
/// Interface for service registration from configuration.
/// Each service (OData, OGC API, WFS, etc.) implements this to enable
/// automatic registration and endpoint mapping from configuration.
/// </summary>
public interface IServiceRegistration
{
    /// <summary>
    /// Unique service identifier (matches service type in config).
    /// Examples: "odata", "ogc_api", "wfs", "wms", "wmts"
    /// </summary>
    string ServiceId { get; }

    /// <summary>
    /// Human-readable service name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Service description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Register service dependencies into the DI container.
    /// Called during application startup (builder.Services).
    /// </summary>
    /// <param name="services">Service collection for DI registration</param>
    /// <param name="serviceConfig">Service-specific configuration from .honua file</param>
    void ConfigureServices(IServiceCollection services, ServiceBlock serviceConfig);

    /// <summary>
    /// Map service endpoints to the application.
    /// Called during application configuration (app.MapXxx()).
    /// </summary>
    /// <param name="endpoints">Endpoint route builder</param>
    /// <param name="serviceConfig">Service-specific configuration from .honua file</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints, ServiceBlock serviceConfig);

    /// <summary>
    /// Validate service-specific configuration.
    /// Called before registration to ensure config is valid.
    /// </summary>
    /// <param name="serviceConfig">Service-specific configuration</param>
    /// <returns>Validation result with any errors or warnings</returns>
    ServiceValidationResult ValidateConfiguration(ServiceBlock serviceConfig);
}

/// <summary>
/// Result of service configuration validation.
/// </summary>
public sealed class ServiceValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public static ServiceValidationResult Success() => new();

    public static ServiceValidationResult Error(string error)
    {
        var result = new ServiceValidationResult();
        result.Errors.Add(error);
        return result;
    }

    public void AddError(string error) => Errors.Add(error);
    public void AddWarning(string warning) => Warnings.Add(warning);
}

/// <summary>
/// Metadata attribute for service registration discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ServiceRegistrationAttribute : Attribute
{
    /// <summary>
    /// Service identifier (matches config service type).
    /// </summary>
    public string ServiceId { get; }

    /// <summary>
    /// Priority for registration order (lower = earlier).
    /// Services with dependencies should have higher priority.
    /// </summary>
    public int Priority { get; init; } = 100;

    public ServiceRegistrationAttribute(string serviceId)
    {
        ServiceId = serviceId ?? throw new ArgumentNullException(nameof(serviceId));
    }
}

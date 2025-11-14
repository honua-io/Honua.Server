// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Plugins;

/// <summary>
/// Interface for service plugins (WFS, WMS, OData, etc.).
/// Service plugins provide HTTP endpoints and integrate with ASP.NET Core.
/// </summary>
public interface IServicePlugin : IHonuaPlugin
{
    /// <summary>
    /// Service identifier used in configuration (e.g., "wfs", "wms").
    /// Must match the service ID in Configuration V2.
    /// </summary>
    string ServiceId { get; }

    /// <summary>
    /// Type of service this plugin provides.
    /// </summary>
    ServiceType ServiceType { get; }

    /// <summary>
    /// Configure dependency injection services.
    /// Called during application startup, before the service provider is built.
    /// </summary>
    /// <param name="services">Service collection to register services.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="context">Plugin context.</param>
    void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context);

    /// <summary>
    /// Map HTTP endpoints for this service.
    /// Called after the service provider is built, during endpoint configuration.
    /// </summary>
    /// <param name="endpoints">Endpoint route builder.</param>
    /// <param name="context">Plugin context (includes ServiceProvider).</param>
    void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context);

    /// <summary>
    /// Validate plugin configuration.
    /// Called during startup to ensure configuration is valid before services are registered.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    PluginValidationResult ValidateConfiguration(IConfiguration configuration);

    /// <summary>
    /// Optional: Configure middleware pipeline.
    /// Called during middleware configuration.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <param name="context">Plugin context.</param>
    void ConfigureMiddleware(IApplicationBuilder app, PluginContext context)
    {
        // Default: no middleware configuration
    }
}

/// <summary>
/// Type of service provided by the plugin.
/// </summary>
public enum ServiceType
{
    /// <summary>
    /// Unknown or unspecified service type (default value).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// OGC standard service (WFS, WMS, WMTS, CSW, WCS).
    /// </summary>
    OGC = 1,

    /// <summary>
    /// Modern REST API (OGC API Features, STAC, Carto).
    /// </summary>
    API = 2,

    /// <summary>
    /// Proprietary/vendor-specific API (Esri GeoServices REST).
    /// </summary>
    Proprietary = 3,

    /// <summary>
    /// Specialized service (Zarr, Print).
    /// </summary>
    Specialized = 4,

    /// <summary>
    /// Custom service type.
    /// </summary>
    Custom = 5
}

/// <summary>
/// Result of plugin configuration validation.
/// </summary>
public sealed class PluginValidationResult
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    /// <summary>
    /// True if validation passed (no errors).
    /// Warnings are allowed.
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// Validation errors (must be fixed).
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Validation warnings (should be addressed).
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Add a validation error.
    /// </summary>
    public void AddError(string error)
    {
        _errors.Add(error);
    }

    /// <summary>
    /// Add a validation warning.
    /// </summary>
    public void AddWarning(string warning)
    {
        _warnings.Add(warning);
    }

    /// <summary>
    /// Get a summary of validation results.
    /// </summary>
    public string GetSummary()
    {
        if (IsValid && _warnings.Count == 0)
        {
            return "Validation passed";
        }

        if (IsValid)
        {
            return $"Validation passed with {_warnings.Count} warning(s)";
        }

        return $"Validation failed with {_errors.Count} error(s) and {_warnings.Count} warning(s)";
    }
}

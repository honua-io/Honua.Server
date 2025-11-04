// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Host.Middleware;

/// <summary>
/// Configuration for API versioning to support multiple API versions and graceful deprecation.
/// Note: API versioning can be added by installing Asp.Versioning.Mvc package.
/// Implements URL path versioning (e.g., /v1/collections, /v2/collections).
/// </summary>
public static class ApiVersioningConfiguration
{
    /// <summary>
    /// Configuration for API versioning to support multiple API versions and graceful deprecation.
    /// Implements URL path versioning (e.g., /v1/collections, /v2/collections).
    /// </summary>
    public static IServiceCollection AddHonuaApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            // Default version if not specified
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);

            // Assume default version when not specified
            options.AssumeDefaultVersionWhenUnspecified = true;

            // Report API versions in response headers
            options.ReportApiVersions = true;

            // Read version from URL path
            options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            // Format version as 'v{major}.{minor}' (e.g., v1.0)
            options.GroupNameFormat = "'v'VVV";

            // Substitute version in URL path
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}

/// <summary>
/// API version constants for use in controllers.
/// </summary>
public static class ApiVersions
{
    public const string V1 = "1.0";
    public const string V2 = "2.0";
}

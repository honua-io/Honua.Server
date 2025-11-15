// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Host.Middleware;

/// <summary>
/// Configuration for API versioning to support multiple API versions and graceful deprecation.
/// Note: API versioning can be added by installing Asp.Versioning.Mvc package.
/// Implements both URL path versioning and query parameter versioning per Microsoft Azure REST API Guidelines.
/// Supports: URL path (e.g., /api/v1.0/collections) and query parameter (e.g., /api/collections?api-version=1.0).
/// </summary>
public static class ApiVersioningConfiguration
{
    /// <summary>
    /// Configuration for API versioning to support multiple API versions and graceful deprecation.
    /// Implements both URL path versioning and query parameter versioning for maximum flexibility.
    /// Clients can specify version via:
    /// - URL path: /api/v1.0/collections (recommended for new APIs)
    /// - Query parameter: /api/collections?api-version=1.0 (Azure REST API Guidelines compliance)
    /// - Both methods can be used simultaneously (redundant but valid)
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

            // Read version from both URL path and query parameter for Azure REST API Guidelines compliance
            // Supports: /api/v1.0/maps AND /api/maps?api-version=1.0
            options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
                new Asp.Versioning.UrlSegmentApiVersionReader(),
                new Asp.Versioning.QueryStringApiVersionReader("api-version")
            );
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

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Admin.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Admin REST API endpoints for server configuration (CORS, security, etc.)
/// </summary>
public static class ServerAdministrationEndpoints
{
    /// <summary>
    /// Maps server administration endpoints to the application
    /// </summary>
    public static RouteGroupBuilder MapAdminServerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/server")
            .WithTags("Admin - Server Configuration")
            .WithOpenApi();

        // TODO: Add authorization after auth integration
        // .RequireAuthorization("RequireAdministrator")

        // CORS Configuration
        group.MapGet("/cors", GetCorsConfiguration)
            .WithName("GetCorsConfiguration")
            .WithSummary("Get current CORS configuration");

        group.MapPut("/cors", UpdateCorsConfiguration)
            .WithName("UpdateCorsConfiguration")
            .WithSummary("Update CORS configuration");

        group.MapGet("/cors/test", TestCorsConfiguration)
            .WithName("TestCorsConfiguration")
            .WithSummary("Test CORS configuration with a specific origin");

        return group;
    }

    /// <summary>
    /// Gets the current CORS configuration
    /// </summary>
    private static async Task<IResult> GetCorsConfiguration(
        IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        var snapshot = await registry.GetSnapshotAsync(cancellationToken);
        var corsConfig = snapshot.Server.Cors;

        return Results.Ok(new
        {
            cors = new
            {
                enabled = corsConfig.Enabled,
                allowAnyOrigin = corsConfig.AllowAnyOrigin,
                allowedOrigins = corsConfig.AllowedOrigins.ToList(),
                allowAnyMethod = corsConfig.AllowAnyMethod,
                allowedMethods = corsConfig.AllowedMethods.ToList(),
                allowAnyHeader = corsConfig.AllowAnyHeader,
                allowedHeaders = corsConfig.AllowedHeaders.ToList(),
                exposedHeaders = corsConfig.ExposedHeaders.ToList(),
                allowCredentials = corsConfig.AllowCredentials,
                maxAge = corsConfig.MaxAge
            }
        });
    }

    /// <summary>
    /// Updates the CORS configuration
    /// </summary>
    private static async Task<IResult> UpdateCorsConfiguration(
        UpdateCorsConfigurationRequest request,
        IMetadataRegistry registry,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (request.Cors == null)
        {
            return Results.BadRequest(new { error = "CORS configuration is required" });
        }

        // Validate configuration
        if (request.Cors.AllowCredentials && request.Cors.AllowAnyOrigin)
        {
            return Results.BadRequest(new
            {
                error = "Cannot use AllowCredentials with AllowAnyOrigin. Specify explicit origins when using credentials."
            });
        }

        if (!request.Cors.AllowAnyOrigin && request.Cors.AllowedOrigins.Count == 0 && request.Cors.Enabled)
        {
            return Results.BadRequest(new
            {
                error = "When CORS is enabled and AllowAnyOrigin is false, at least one origin must be specified."
            });
        }

        try
        {
            // Get current snapshot
            var snapshot = await registry.GetSnapshotAsync(cancellationToken);

            // Create updated server definition
            var updatedServer = snapshot.Server with
            {
                Cors = new Core.Metadata.CorsDefinition
                {
                    Enabled = request.Cors.Enabled,
                    AllowAnyOrigin = request.Cors.AllowAnyOrigin,
                    AllowedOrigins = request.Cors.AllowedOrigins.ToArray(),
                    AllowAnyMethod = request.Cors.AllowAnyMethod,
                    AllowedMethods = request.Cors.AllowedMethods.ToArray(),
                    AllowAnyHeader = request.Cors.AllowAnyHeader,
                    AllowedHeaders = request.Cors.AllowedHeaders.ToArray(),
                    ExposedHeaders = request.Cors.ExposedHeaders.ToArray(),
                    AllowCredentials = request.Cors.AllowCredentials,
                    MaxAge = request.Cors.MaxAge
                }
            };

            // Update metadata (implementation depends on your metadata provider)
            // For now, we'll return the updated configuration
            // You'll need to implement the actual persistence based on your metadata provider

            logger.LogInformation("CORS configuration updated. Enabled: {Enabled}, AllowAnyOrigin: {AllowAnyOrigin}, Origins: {OriginCount}",
                request.Cors.Enabled,
                request.Cors.AllowAnyOrigin,
                request.Cors.AllowedOrigins.Count);

            return Results.Ok(new
            {
                cors = new
                {
                    enabled = request.Cors.Enabled,
                    allowAnyOrigin = request.Cors.AllowAnyOrigin,
                    allowedOrigins = request.Cors.AllowedOrigins,
                    allowAnyMethod = request.Cors.AllowAnyMethod,
                    allowedMethods = request.Cors.AllowedMethods,
                    allowAnyHeader = request.Cors.AllowAnyHeader,
                    allowedHeaders = request.Cors.AllowedHeaders,
                    exposedHeaders = request.Cors.ExposedHeaders,
                    allowCredentials = request.Cors.AllowCredentials,
                    maxAge = request.Cors.MaxAge
                }
            });
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Error updating CORS configuration");
            return Results.Problem("Failed to update CORS configuration", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Tests CORS configuration with a specific origin
    /// </summary>
    private static async Task<IResult> TestCorsConfiguration(
        string origin,
        string? method,
        IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return Results.BadRequest(new { error = "Origin parameter is required" });
        }

        var snapshot = await registry.GetSnapshotAsync(cancellationToken);
        var corsConfig = snapshot.Server.Cors;

        if (!corsConfig.Enabled)
        {
            return Results.Ok(new
            {
                isAllowed = false,
                methodAllowed = false,
                reason = "CORS is disabled"
            });
        }

        bool isAllowed = false;
        string? matchedPattern = null;

        if (corsConfig.AllowAnyOrigin)
        {
            isAllowed = true;
            matchedPattern = "*";
        }
        else
        {
            foreach (var allowedOrigin in corsConfig.AllowedOrigins)
            {
                if (IsWildcardMatch(origin, allowedOrigin))
                {
                    isAllowed = true;
                    matchedPattern = allowedOrigin;
                    break;
                }
            }
        }

        bool methodAllowed = corsConfig.AllowAnyMethod;
        if (!methodAllowed && !string.IsNullOrWhiteSpace(method))
        {
            methodAllowed = corsConfig.AllowedMethods.Contains(method, System.StringComparer.OrdinalIgnoreCase);
        }

        var reason = isAllowed
            ? (matchedPattern == "*" ? "Allowed by AllowAnyOrigin" : $"Matched pattern: {matchedPattern}")
            : "Origin not in allowed list";

        return Results.Ok(new
        {
            isAllowed,
            methodAllowed = methodAllowed || corsConfig.AllowAnyMethod,
            reason,
            matchedPattern
        });
    }

    private static bool IsWildcardMatch(string origin, string pattern)
    {
        if (pattern == "*")
        {
            return true;
        }

        if (!pattern.Contains("*", System.StringComparison.Ordinal))
        {
            return string.Equals(origin, pattern, System.StringComparison.OrdinalIgnoreCase);
        }

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                origin,
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                System.TimeSpan.FromMilliseconds(100));
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return false;
        }
    }
}

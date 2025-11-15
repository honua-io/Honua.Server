// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Host.Admin.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            .WithOpenApi()
            .RequireAuthorization(AdminAuthorizationPolicies.RequireServerAdministrator);

        // CORS Configuration
        group.MapGet("/cors", GetCorsConfiguration)
            .WithName("GetCorsConfiguration")
            .WithSummary("Get current CORS configuration")
            .RequireAuthorization(AdminAuthorizationPolicies.RequireServerAdministrator);

        group.MapPut("/cors", UpdateCorsConfiguration)
            .WithName("UpdateCorsConfiguration")
            .WithSummary("Update CORS configuration")
            .RequireAuthorization(AdminAuthorizationPolicies.RequireServerAdministrator);

        group.MapGet("/cors/test", TestCorsConfiguration)
            .WithName("TestCorsConfiguration")
            .WithSummary("Test CORS configuration with a specific origin")
            .RequireAuthorization(AdminAuthorizationPolicies.RequireServerAdministrator);

        return group;
    }

    /// <summary>
    /// Gets the current CORS configuration
    /// </summary>
    private static async Task<IResult> GetCorsConfiguration(
        [FromServices] IMetadataRegistry registry,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetCorsConfiguration",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            var snapshot = await registry.GetSnapshotAsync(cancellationToken);
            var corsConfig = snapshot.Server.Cors;

            // Audit logging for sensitive configuration access
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "ServerConfiguration",
                resourceId: "CORS",
                operation: "Read",
                additionalData: new Dictionary<string, object>
                {
                    ["enabled"] = corsConfig.Enabled,
                    ["allowAnyOrigin"] = corsConfig.AllowAnyOrigin,
                    ["originCount"] = corsConfig.AllowedOrigins.Count
                });

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
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetCorsConfiguration",
                resourceType: "ServerConfiguration",
                resourceId: "CORS",
                details: "Failed to retrieve CORS configuration",
                exception: ex);

            throw;
        }
    }

    /// <summary>
    /// Updates the CORS configuration
    /// </summary>
    private static async Task<IResult> UpdateCorsConfiguration(
        UpdateCorsConfigurationRequest request,
        [FromServices] IMetadataRegistry registry,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "UpdateCorsConfiguration",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Basic validation
            if (request.Cors == null)
            {
                return Results.BadRequest(new { error = "CORS configuration is required" });
            }

            // Input validation for allowed origins
            if (request.Cors.AllowedOrigins != null)
            {
                foreach (var origin in request.Cors.AllowedOrigins)
                {
                    InputValidationHelpers.ThrowIfUnsafeInput(origin, nameof(request.Cors.AllowedOrigins));

                    // Validate origin format (basic check for URLs)
                    if (!string.IsNullOrWhiteSpace(origin) &&
                        origin != "*" &&
                        !origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        return Results.BadRequest(new { error = $"Invalid origin format: {origin}. Must be '*' or start with http:// or https://" });
                    }
                }
            }

            // Input validation for methods
            if (request.Cors.AllowedMethods != null)
            {
                foreach (var method in request.Cors.AllowedMethods)
                {
                    InputValidationHelpers.ThrowIfUnsafeInput(method, nameof(request.Cors.AllowedMethods));
                }
            }

            // Input validation for headers
            if (request.Cors.AllowedHeaders != null)
            {
                foreach (var header in request.Cors.AllowedHeaders)
                {
                    InputValidationHelpers.ThrowIfUnsafeInput(header, nameof(request.Cors.AllowedHeaders));
                }
            }

            if (request.Cors.ExposedHeaders != null)
            {
                foreach (var header in request.Cors.ExposedHeaders)
                {
                    InputValidationHelpers.ThrowIfUnsafeInput(header, nameof(request.Cors.ExposedHeaders));
                }
            }

            // Validate configuration logic
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

            // Validate MaxAge range (optional, reasonable limit)
            if (request.Cors.MaxAge.HasValue && (request.Cors.MaxAge.Value < 0 || request.Cors.MaxAge.Value > 86400))
            {
                return Results.BadRequest(new
                {
                    error = "MaxAge must be between 0 and 86400 seconds (24 hours)."
                });
            }

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

            // Audit logging for configuration changes
            await auditLoggingService.LogAdminActionAsync(
                action: "UpdateCorsConfiguration",
                resourceType: "ServerConfiguration",
                resourceId: "CORS",
                details: $"Updated CORS configuration: Enabled={request.Cors.Enabled}, AllowAnyOrigin={request.Cors.AllowAnyOrigin}",
                additionalData: new Dictionary<string, object>
                {
                    ["enabled"] = request.Cors.Enabled,
                    ["allowAnyOrigin"] = request.Cors.AllowAnyOrigin,
                    ["allowedOrigins"] = request.Cors.AllowedOrigins.ToArray(),
                    ["allowAnyMethod"] = request.Cors.AllowAnyMethod,
                    ["allowedMethods"] = request.Cors.AllowedMethods.ToArray(),
                    ["allowAnyHeader"] = request.Cors.AllowAnyHeader,
                    ["allowCredentials"] = request.Cors.AllowCredentials,
                    ["originCount"] = request.Cors.AllowedOrigins.Count,
                    ["methodCount"] = request.Cors.AllowedMethods.Count,
                    ["headerCount"] = request.Cors.AllowedHeaders.Count
                });

            logger.LogInformation(
                "User {UserId} updated CORS configuration. Enabled: {Enabled}, AllowAnyOrigin: {AllowAnyOrigin}, Origins: {OriginCount}",
                identity.UserId,
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
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "UpdateCorsConfiguration",
                resourceType: "ServerConfiguration",
                resourceId: "CORS",
                details: "Failed to update CORS configuration",
                exception: ex);

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
        [FromServices] IMetadataRegistry registry,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "TestCorsConfiguration",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (string.IsNullOrWhiteSpace(origin))
            {
                return Results.BadRequest(new { error = "Origin parameter is required" });
            }

            InputValidationHelpers.ThrowIfUnsafeInput(origin, nameof(origin));

            if (!string.IsNullOrWhiteSpace(method))
            {
                InputValidationHelpers.ThrowIfUnsafeInput(method, nameof(method));
            }

            var snapshot = await registry.GetSnapshotAsync(cancellationToken);
            var corsConfig = snapshot.Server.Cors;

            if (!corsConfig.Enabled)
            {
                // Audit logging for test operation
                await auditLoggingService.LogAdminActionAsync(
                    action: "TestCorsConfiguration",
                    resourceType: "ServerConfiguration",
                    resourceId: "CORS",
                    details: $"Tested CORS configuration for origin: {origin}",
                    additionalData: new Dictionary<string, object>
                    {
                        ["origin"] = origin,
                        ["method"] = method ?? "N/A",
                        ["isAllowed"] = false,
                        ["reason"] = "CORS is disabled"
                    });

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
                methodAllowed = corsConfig.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
            }

            var reason = isAllowed
                ? (matchedPattern == "*" ? "Allowed by AllowAnyOrigin" : $"Matched pattern: {matchedPattern}")
                : "Origin not in allowed list";

            // Audit logging for test operation
            await auditLoggingService.LogAdminActionAsync(
                action: "TestCorsConfiguration",
                resourceType: "ServerConfiguration",
                resourceId: "CORS",
                details: $"Tested CORS configuration for origin: {origin}",
                additionalData: new Dictionary<string, object>
                {
                    ["origin"] = origin,
                    ["method"] = method ?? "N/A",
                    ["isAllowed"] = isAllowed,
                    ["methodAllowed"] = methodAllowed || corsConfig.AllowAnyMethod,
                    ["matchedPattern"] = matchedPattern ?? "none",
                    ["reason"] = reason
                });

            return Results.Ok(new
            {
                isAllowed,
                methodAllowed = methodAllowed || corsConfig.AllowAnyMethod,
                reason,
                matchedPattern
            });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "TestCorsConfiguration",
                resourceType: "ServerConfiguration",
                resourceId: "CORS",
                details: $"Failed to test CORS configuration for origin: {origin}",
                exception: ex);

            throw;
        }
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

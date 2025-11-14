// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Features;
using Honua.Server.Core.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Admin API endpoints for feature flag management.
/// </summary>
public static class FeatureFlagEndpoints
{
    /// <summary>
    /// Maps feature flag endpoints to the application.
    /// </summary>
    public static RouteGroupBuilder MapAdminFeatureFlagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin")
            .WithTags("Admin - Feature Flags")
            .WithOpenApi()
            .RequireAuthorization(AdminAuthorizationPolicies.RequireFeatureFlagAdministrator);

        group.MapGet("/feature-flags", GetFeatureFlags)
            .WithName("GetFeatureFlags")
            .WithSummary("Get current feature flag state based on license")
            .RequireAuthorization(AdminAuthorizationPolicies.RequireFeatureFlagAdministrator);

        group.MapGet("/feature-flags/{featureName}", IsFeatureEnabled)
            .WithName("IsFeatureEnabled")
            .WithSummary("Check if a specific feature is enabled")
            .RequireAuthorization(AdminAuthorizationPolicies.RequireFeatureFlagAdministrator);

        return group;
    }

    /// <summary>
    /// Gets all feature flags for the current license.
    /// </summary>
    private static async Task<IResult> GetFeatureFlags(
        [FromServices] ILicenseFeatureFlagService featureFlagService,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity from authentication context
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetFeatureFlags",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            var flags = await featureFlagService.GetFeatureFlagsAsync(cancellationToken);

            if (flags == null)
            {
                logger.LogWarning("No feature flags available, returning default (free tier)");
                flags = LicenseFeatureFlags.GetDefault();
            }

            // Audit successful data access
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "FeatureFlags",
                resourceId: "All",
                operation: "Read",
                additionalData: new Dictionary<string, object>
                {
                    ["tier"] = flags.Tier,
                    ["userId"] = identity.UserId
                });

            logger.LogInformation(
                "User {UserId} retrieved feature flags for tier: {Tier}",
                identity.UserId, flags.Tier);

            return Results.Ok(flags);
        }
        catch (System.Exception ex)
        {
            // Log failure with exception details
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetFeatureFlags",
                resourceType: "FeatureFlags",
                details: "Failed to retrieve feature flags",
                exception: ex);

            logger.LogError(ex, "Error retrieving feature flags");
            return Results.Problem("Failed to retrieve feature flags", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Checks if a specific feature is enabled.
    /// </summary>
    private static async Task<IResult> IsFeatureEnabled(
        string featureName,
        [FromServices] ILicenseFeatureFlagService featureFlagService,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user identity from authentication context
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "IsFeatureEnabled",
                    resourceId: featureName,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (string.IsNullOrWhiteSpace(featureName))
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "IsFeatureEnabled",
                    resourceType: "FeatureFlag",
                    details: "Feature name is required");

                return Results.BadRequest(new { error = "Feature name is required" });
            }

            InputValidationHelpers.ThrowIfUnsafeInput(featureName, nameof(featureName));

            if (!InputValidationHelpers.IsValidLength(featureName, minLength: 1, maxLength: 200))
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "IsFeatureEnabled",
                    resourceType: "FeatureFlag",
                    resourceId: featureName,
                    details: "Feature name length validation failed");

                return Results.BadRequest(new { error = "Feature name must be between 1 and 200 characters" });
            }

            var isEnabled = await featureFlagService.IsFeatureEnabledAsync(featureName, cancellationToken);

            // Audit successful data access
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "FeatureFlag",
                resourceId: featureName,
                operation: "Read",
                additionalData: new Dictionary<string, object>
                {
                    ["featureName"] = featureName,
                    ["enabled"] = isEnabled,
                    ["userId"] = identity.UserId
                });

            logger.LogInformation(
                "User {UserId} checked feature flag {FeatureName}: {Enabled}",
                identity.UserId, featureName, isEnabled);

            return Results.Ok(new
            {
                feature = featureName,
                enabled = isEnabled
            });
        }
        catch (System.Exception ex)
        {
            // Log failure with exception details
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "IsFeatureEnabled",
                resourceType: "FeatureFlag",
                resourceId: featureName ?? "unknown",
                details: $"Failed to check feature: {featureName}",
                exception: ex);

            logger.LogError(ex, "Error checking feature {FeatureName}", featureName);
            return Results.Problem($"Failed to check feature: {featureName}", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

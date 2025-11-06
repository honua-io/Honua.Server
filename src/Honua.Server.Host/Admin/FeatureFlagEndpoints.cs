// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Features;
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
            .WithOpenApi();

        // TODO: Add authorization after auth integration
        // .RequireAuthorization()

        group.MapGet("/feature-flags", GetFeatureFlags)
            .WithName("GetFeatureFlags")
            .WithSummary("Get current feature flag state based on license");

        group.MapGet("/feature-flags/{featureName}", IsFeatureEnabled)
            .WithName("IsFeatureEnabled")
            .WithSummary("Check if a specific feature is enabled");

        return group;
    }

    /// <summary>
    /// Gets all feature flags for the current license.
    /// </summary>
    private static async Task<IResult> GetFeatureFlags(
        [FromServices] ILicenseFeatureFlagService featureFlagService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var flags = await featureFlagService.GetFeatureFlagsAsync(cancellationToken);

            if (flags == null)
            {
                logger.LogWarning("No feature flags available, returning default (free tier)");
                return Results.Ok(LicenseFeatureFlags.GetDefault());
            }

            logger.LogInformation("Returning feature flags for tier: {Tier}", flags.Tier);
            return Results.Ok(flags);
        }
        catch (System.Exception ex)
        {
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
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return Results.BadRequest(new { error = "Feature name is required" });
        }

        try
        {
            var isEnabled = await featureFlagService.IsFeatureEnabledAsync(featureName, cancellationToken);

            return Results.Ok(new
            {
                feature = featureName,
                enabled = isEnabled
            });
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Error checking feature {FeatureName}", featureName);
            return Results.Problem($"Failed to check feature: {featureName}", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

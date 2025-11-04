// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Features;
using Honua.Server.Host.GeoservicesREST;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Endpoints for viewing and managing feature degradation status.
/// </summary>
public static class DegradationStatusEndpoints
{
    /// <summary>
    /// Maps degradation status endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapDegradationStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/features")
            .WithTags("Feature Management")
            .RequireAuthorization("RequireAdministrator");

        group.MapGet("/status", GetAllFeatureStatus)
            .WithName("GetAllFeatureStatus")
            .WithSummary("Get status of all features")
            .Produces<FeatureStatusResponse>();

        group.MapGet("/status/{featureName}", GetFeatureStatus)
            .WithName("GetFeatureStatus")
            .WithSummary("Get status of a specific feature")
            .Produces<FeatureStatusResponse>()
            .Produces(404);

        group.MapPost("/status/{featureName}/disable", DisableFeature)
            .WithName("DisableFeature")
            .WithSummary("Manually disable a feature")
            .Produces(200)
            .Produces(400);

        group.MapPost("/status/{featureName}/enable", EnableFeature)
            .WithName("EnableFeature")
            .WithSummary("Manually enable a feature")
            .Produces(200)
            .Produces(400);

        group.MapPost("/status/{featureName}/check-health", CheckFeatureHealth)
            .WithName("CheckFeatureHealth")
            .WithSummary("Force a health check for a feature")
            .Produces<FeatureStatusResponse>();

        group.MapGet("/degradations", GetActiveDegradations)
            .WithName("GetActiveDegradations")
            .WithSummary("Get all currently degraded features")
            .Produces<DegradationSummaryResponse>();

        return endpoints;
    }

    private static async Task<IResult> GetAllFeatureStatus(
        [FromServices] IFeatureManagementService featureManagement,
        CancellationToken cancellationToken)
    {
        var statuses = await featureManagement.GetAllFeatureStatusesAsync(cancellationToken);

        var response = new FeatureStatusResponse
        {
            Timestamp = DateTimeOffset.UtcNow,
            Features = statuses.Select(MapToDto).ToList()
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> GetFeatureStatus(
        string featureName,
        [FromServices] IFeatureManagementService featureManagement,
        CancellationToken cancellationToken)
    {
        var status = await featureManagement.GetFeatureStatusAsync(featureName, cancellationToken);

        if (status.State == FeatureDegradationState.Unavailable &&
            status.DegradationReason == "Feature not registered")
        {
            return GeoservicesRESTErrorHelper.NotFound("Feature", featureName);
        }

        var response = new FeatureStatusResponse
        {
            Timestamp = DateTimeOffset.UtcNow,
            Features = new List<FeatureStatusDto> { MapToDto(new(featureName, status)) }
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> DisableFeature(
        string featureName,
        DisableFeatureRequest request,
        [FromServices] IFeatureManagementService featureManagement,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason ?? "Manually disabled via API";

        await featureManagement.DisableFeatureAsync(featureName, reason, cancellationToken);

        var status = await featureManagement.GetFeatureStatusAsync(featureName, cancellationToken);

        return Results.Ok(new
        {
            featureName,
            status = MapToDto(new(featureName, status))
        });
    }

    private static async Task<IResult> EnableFeature(
        string featureName,
        [FromServices] IFeatureManagementService featureManagement,
        CancellationToken cancellationToken)
    {
        await featureManagement.EnableFeatureAsync(featureName, cancellationToken);

        var status = await featureManagement.GetFeatureStatusAsync(featureName, cancellationToken);

        return Results.Ok(new
        {
            featureName,
            status = MapToDto(new(featureName, status))
        });
    }

    private static async Task<IResult> CheckFeatureHealth(
        string featureName,
        [FromServices] IFeatureManagementService featureManagement,
        CancellationToken cancellationToken)
    {
        var status = await featureManagement.CheckFeatureHealthAsync(featureName, cancellationToken);

        var response = new FeatureStatusResponse
        {
            Timestamp = DateTimeOffset.UtcNow,
            Features = new List<FeatureStatusDto> { MapToDto(new(featureName, status)) }
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> GetActiveDegradations(
        [FromServices] IFeatureManagementService featureManagement,
        CancellationToken cancellationToken)
    {
        var statuses = await featureManagement.GetAllFeatureStatusesAsync(cancellationToken);

        var degradedFeatures = statuses
            .Where(kvp => kvp.Value.IsDegraded || !kvp.Value.IsAvailable)
            .Select(MapToDto)
            .ToList();

        var healthyCount = statuses.Count(kvp => kvp.Value.State == FeatureDegradationState.Healthy);
        var degradedCount = statuses.Count(kvp => kvp.Value.IsDegraded);
        var unavailableCount = statuses.Count(kvp => !kvp.Value.IsAvailable);

        var response = new DegradationSummaryResponse
        {
            Timestamp = DateTimeOffset.UtcNow,
            TotalFeatures = statuses.Count,
            HealthyFeatures = healthyCount,
            DegradedFeatures = degradedCount,
            UnavailableFeatures = unavailableCount,
            ActiveDegradations = degradedFeatures
        };

        return Results.Ok(response);
    }

    private static FeatureStatusDto MapToDto(KeyValuePair<string, FeatureStatus> kvp)
    {
        var status = kvp.Value;
        return new FeatureStatusDto
        {
            Name = kvp.Key,
            IsAvailable = status.IsAvailable,
            IsDegraded = status.IsDegraded,
            State = status.State.ToString(),
            HealthScore = status.HealthScore,
            DegradationType = status.ActiveDegradation?.ToString(),
            DegradationReason = status.DegradationReason,
            StateChangedAt = status.StateChangedAt,
            NextRecoveryCheck = status.NextRecoveryCheck
        };
    }
}

public sealed record FeatureStatusResponse
{
    public required DateTimeOffset Timestamp { get; init; }
    public required List<FeatureStatusDto> Features { get; init; }
}

public sealed record FeatureStatusDto
{
    public required string Name { get; init; }
    public required bool IsAvailable { get; init; }
    public required bool IsDegraded { get; init; }
    public required string State { get; init; }
    public required int HealthScore { get; init; }
    public string? DegradationType { get; init; }
    public string? DegradationReason { get; init; }
    public DateTimeOffset StateChangedAt { get; init; }
    public DateTimeOffset? NextRecoveryCheck { get; init; }
}

public sealed record DegradationSummaryResponse
{
    public required DateTimeOffset Timestamp { get; init; }
    public required int TotalFeatures { get; init; }
    public required int HealthyFeatures { get; init; }
    public required int DegradedFeatures { get; init; }
    public required int UnavailableFeatures { get; init; }
    public required List<FeatureStatusDto> ActiveDegradations { get; init; }
}

public sealed record DisableFeatureRequest
{
    public string? Reason { get; init; }
}

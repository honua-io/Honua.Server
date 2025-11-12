// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Models;
using Honua.Server.Core.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Admin API endpoints for geofence alert configuration and monitoring.
/// </summary>
public static class GeofenceAlertAdministrationEndpoints
{
    public static void MapGeofenceAlertAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/geofence-alerts")
            .WithTags("Geofence Alerts Admin")
            .RequireAuthorization("RequireAdministrator");

        // === Alert Rules ===

        group.MapGet("/rules", GetAlertRules)
            .WithName("GetGeofenceAlertRules")
            .WithDescription("Get all geofence alert rules");

        group.MapGet("/rules/{id:guid}", GetAlertRule)
            .WithName("GetGeofenceAlertRule")
            .WithDescription("Get a specific geofence alert rule");

        group.MapPost("/rules", CreateAlertRule)
            .WithName("CreateGeofenceAlertRule")
            .WithDescription("Create a new geofence alert rule");

        group.MapPut("/rules/{id:guid}", UpdateAlertRule)
            .WithName("UpdateGeofenceAlertRule")
            .WithDescription("Update an existing geofence alert rule");

        group.MapDelete("/rules/{id:guid}", DeleteAlertRule)
            .WithName("DeleteGeofenceAlertRule")
            .WithDescription("Delete a geofence alert rule");

        // === Silencing Rules ===

        group.MapGet("/silencing", GetSilencingRules)
            .WithName("GetGeofenceSilencingRules")
            .WithDescription("Get all geofence silencing rules");

        group.MapGet("/silencing/{id:guid}", GetSilencingRule)
            .WithName("GetGeofenceSilencingRule")
            .WithDescription("Get a specific silencing rule");

        group.MapPost("/silencing", CreateSilencingRule)
            .WithName("CreateGeofenceSilencingRule")
            .WithDescription("Create a new silencing rule");

        group.MapPut("/silencing/{id:guid}", UpdateSilencingRule)
            .WithName("UpdateGeofenceSilencingRule")
            .WithDescription("Update an existing silencing rule");

        group.MapDelete("/silencing/{id:guid}", DeleteSilencingRule)
            .WithName("DeleteGeofenceSilencingRule")
            .WithDescription("Delete a silencing rule");

        // === Active Alerts & Correlations ===

        group.MapGet("/active", GetActiveAlerts)
            .WithName("GetActiveGeofenceAlerts")
            .WithDescription("Get all currently active geofence alerts");

        group.MapGet("/correlation/{geofenceEventId:guid}", GetCorrelation)
            .WithName("GetGeofenceAlertCorrelation")
            .WithDescription("Get alert correlation for a specific geofence event");

        group.MapGet("/geofence/{geofenceId:guid}/alerts", GetAlertsByGeofence)
            .WithName("GetAlertsByGeofence")
            .WithDescription("Get all alerts triggered by a specific geofence");

        group.MapGet("/entity/{entityId}/alerts", GetAlertsByEntity)
            .WithName("GetAlertsByEntity")
            .WithDescription("Get all alerts triggered by a specific entity");
    }

    // === Alert Rule Handlers ===

    private static async Task<IResult> GetAlertRules(
        [FromServices] IGeofenceAlertRepository repository,
        [FromQuery] string? tenantId,
        [FromQuery] bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        var rules = await repository.GetAlertRulesAsync(tenantId, enabledOnly, cancellationToken);
        return Results.Ok(rules);
    }

    private static async Task<IResult> GetAlertRule(
        Guid id,
        [FromServices] IGeofenceAlertRepository repository,
        CancellationToken cancellationToken = default)
    {
        var rule = await repository.GetAlertRuleAsync(id, cancellationToken);
        return rule != null ? Results.Ok(rule) : Results.NotFound();
    }

    private static async Task<IResult> CreateAlertRule(
        [FromBody] CreateGeofenceAlertRuleRequest request,
        [FromServices] IGeofenceAlertRepository repository,
        [FromServices] ILogger<GeofenceAlertRepository> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = new GeofenceAlertRule
            {
                Name = request.Name,
                Description = request.Description,
                Enabled = request.Enabled,
                GeofenceId = request.GeofenceId,
                GeofenceNamePattern = request.GeofenceNamePattern,
                EventTypes = request.EventTypes,
                EntityIdPattern = request.EntityIdPattern,
                EntityType = request.EntityType,
                MinDwellTimeSeconds = request.MinDwellTimeSeconds,
                MaxDwellTimeSeconds = request.MaxDwellTimeSeconds,
                AlertSeverity = request.AlertSeverity,
                AlertNameTemplate = request.AlertNameTemplate,
                AlertDescriptionTemplate = request.AlertDescriptionTemplate,
                AlertLabels = request.AlertLabels,
                NotificationChannelIds = request.NotificationChannelIds,
                SilenceDurationMinutes = request.SilenceDurationMinutes,
                DeduplicationWindowMinutes = request.DeduplicationWindowMinutes
            };

            var id = await repository.CreateAlertRuleAsync(rule, cancellationToken);
            logger.LogInformation("Created geofence alert rule {RuleId}: {RuleName}", id, rule.Name);

            return Results.Created($"/admin/geofence-alerts/rules/{id}", new { id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating geofence alert rule");
            return Results.Problem("Failed to create alert rule", statusCode: 500);
        }
    }

    private static async Task<IResult> UpdateAlertRule(
        Guid id,
        [FromBody] CreateGeofenceAlertRuleRequest request,
        [FromServices] IGeofenceAlertRepository repository,
        [FromServices] ILogger<GeofenceAlertRepository> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await repository.GetAlertRuleAsync(id, cancellationToken);
            if (existing == null)
            {
                return Results.NotFound();
            }

            var rule = new GeofenceAlertRule
            {
                Id = id,
                Name = request.Name,
                Description = request.Description,
                Enabled = request.Enabled,
                GeofenceId = request.GeofenceId,
                GeofenceNamePattern = request.GeofenceNamePattern,
                EventTypes = request.EventTypes,
                EntityIdPattern = request.EntityIdPattern,
                EntityType = request.EntityType,
                MinDwellTimeSeconds = request.MinDwellTimeSeconds,
                MaxDwellTimeSeconds = request.MaxDwellTimeSeconds,
                AlertSeverity = request.AlertSeverity,
                AlertNameTemplate = request.AlertNameTemplate,
                AlertDescriptionTemplate = request.AlertDescriptionTemplate,
                AlertLabels = request.AlertLabels,
                NotificationChannelIds = request.NotificationChannelIds,
                SilenceDurationMinutes = request.SilenceDurationMinutes,
                DeduplicationWindowMinutes = request.DeduplicationWindowMinutes
            };

            await repository.UpdateAlertRuleAsync(id, rule, cancellationToken);
            logger.LogInformation("Updated geofence alert rule {RuleId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating geofence alert rule {RuleId}", id);
            return Results.Problem("Failed to update alert rule", statusCode: 500);
        }
    }

    private static async Task<IResult> DeleteAlertRule(
        Guid id,
        [FromServices] IGeofenceAlertRepository repository,
        [FromServices] ILogger<GeofenceAlertRepository> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.DeleteAlertRuleAsync(id, cancellationToken);
            logger.LogInformation("Deleted geofence alert rule {RuleId}", id);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting geofence alert rule {RuleId}", id);
            return Results.Problem("Failed to delete alert rule", statusCode: 500);
        }
    }

    // === Silencing Rule Handlers ===

    private static async Task<IResult> GetSilencingRules(
        [FromServices] IGeofenceAlertRepository repository,
        [FromQuery] string? tenantId,
        [FromQuery] bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        var rules = await repository.GetSilencingRulesAsync(tenantId, enabledOnly, cancellationToken);
        return Results.Ok(rules);
    }

    private static async Task<IResult> GetSilencingRule(
        Guid id,
        [FromServices] IGeofenceAlertRepository repository,
        CancellationToken cancellationToken = default)
    {
        var rule = await repository.GetSilencingRuleAsync(id, cancellationToken);
        return rule != null ? Results.Ok(rule) : Results.NotFound();
    }

    private static async Task<IResult> CreateSilencingRule(
        [FromBody] GeofenceAlertSilencingRule request,
        [FromServices] IGeofenceAlertRepository repository,
        [FromServices] ILogger<GeofenceAlertRepository> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = await repository.CreateSilencingRuleAsync(request, cancellationToken);
            logger.LogInformation("Created geofence silencing rule {RuleId}: {RuleName}", id, request.Name);

            return Results.Created($"/admin/geofence-alerts/silencing/{id}", new { id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating geofence silencing rule");
            return Results.Problem("Failed to create silencing rule", statusCode: 500);
        }
    }

    private static async Task<IResult> UpdateSilencingRule(
        Guid id,
        [FromBody] GeofenceAlertSilencingRule request,
        [FromServices] IGeofenceAlertRepository repository,
        [FromServices] ILogger<GeofenceAlertRepository> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await repository.GetSilencingRuleAsync(id, cancellationToken);
            if (existing == null)
            {
                return Results.NotFound();
            }

            await repository.UpdateSilencingRuleAsync(id, request, cancellationToken);
            logger.LogInformation("Updated geofence silencing rule {RuleId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating geofence silencing rule {RuleId}", id);
            return Results.Problem("Failed to update silencing rule", statusCode: 500);
        }
    }

    private static async Task<IResult> DeleteSilencingRule(
        Guid id,
        [FromServices] IGeofenceAlertRepository repository,
        [FromServices] ILogger<GeofenceAlertRepository> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.DeleteSilencingRuleAsync(id, cancellationToken);
            logger.LogInformation("Deleted geofence silencing rule {RuleId}", id);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting geofence silencing rule {RuleId}", id);
            return Results.Problem("Failed to delete silencing rule", statusCode: 500);
        }
    }

    // === Alert Query Handlers ===

    private static async Task<IResult> GetActiveAlerts(
        [FromServices] IGeofenceAlertRepository repository,
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var alerts = await repository.GetActiveAlertsAsync(tenantId, cancellationToken);
        return Results.Ok(alerts);
    }

    private static async Task<IResult> GetCorrelation(
        Guid geofenceEventId,
        [FromServices] IGeofenceAlertRepository repository,
        CancellationToken cancellationToken = default)
    {
        var correlation = await repository.GetCorrelationAsync(geofenceEventId, cancellationToken);
        return correlation != null ? Results.Ok(correlation) : Results.NotFound();
    }

    private static Task<IResult> GetAlertsByGeofence(
        Guid geofenceId,
        [FromServices] IGeofenceAlertRepository repository,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        // This would require additional repository method - for now return placeholder
        return Task.FromResult<IResult>(Results.Ok(new List<ActiveGeofenceAlert>()));
    }

    private static Task<IResult> GetAlertsByEntity(
        string entityId,
        [FromServices] IGeofenceAlertRepository repository,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        // This would require additional repository method - for now return placeholder
        return Task.FromResult<IResult>(Results.Ok(new List<ActiveGeofenceAlert>()));
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Scheduling;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Admin;

/// <summary>
/// REST API endpoints for GeoETL workflow scheduling
/// </summary>
public static class GeoEtlScheduleEndpoints
{
    public static IEndpointRouteBuilder MapGeoEtlScheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/geoetl/schedules")
            .WithTags("GeoETL Schedules")
            .WithOpenApi();

        // List all schedules for a tenant
        group.MapGet("/", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromQuery] Guid? tenantId,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0) =>
        {
            if (!tenantId.HasValue)
            {
                return Results.BadRequest(new { error = "tenantId query parameter is required" });
            }

            var schedules = await store.ListSchedulesAsync(tenantId.Value, limit, offset);
            return Results.Ok(schedules);
        })
        .WithName("ListSchedules")
        .WithSummary("List all schedules for a tenant");

        // Get a specific schedule by ID
        group.MapGet("/{scheduleId:guid}", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromRoute] Guid scheduleId) =>
        {
            var schedule = await store.GetScheduleAsync(scheduleId);
            if (schedule == null)
            {
                return Results.NotFound(new { error = "Schedule not found" });
            }

            return Results.Ok(schedule);
        })
        .WithName("GetSchedule")
        .WithSummary("Get a schedule by ID");

        // Create a new schedule
        group.MapPost("/", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromServices] IWorkflowStore workflowStore,
            [FromBody] CreateScheduleRequest request) =>
        {
            if (request.TenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "TenantId is required" });
            }

            if (request.WorkflowId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "WorkflowId is required" });
            }

            if (request.CreatedBy == Guid.Empty)
            {
                return Results.BadRequest(new { error = "CreatedBy is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Schedule name is required" });
            }

            if (string.IsNullOrWhiteSpace(request.CronExpression))
            {
                return Results.BadRequest(new { error = "Cron expression is required" });
            }

            // Verify workflow exists
            var workflow = await workflowStore.GetWorkflowAsync(request.WorkflowId);
            if (workflow == null)
            {
                return Results.BadRequest(new { error = "Workflow not found" });
            }

            var schedule = new WorkflowSchedule
            {
                WorkflowId = request.WorkflowId,
                TenantId = request.TenantId,
                Name = request.Name,
                Description = request.Description,
                CronExpression = request.CronExpression,
                Timezone = request.Timezone ?? "UTC",
                Enabled = request.Enabled ?? true,
                ParameterValues = request.ParameterValues,
                MaxConcurrentExecutions = request.MaxConcurrentExecutions ?? 1,
                RetryAttempts = request.RetryAttempts ?? 0,
                RetryDelayMinutes = request.RetryDelayMinutes ?? 5,
                ExpiresAt = request.ExpiresAt,
                NotificationConfig = request.NotificationConfig,
                Tags = request.Tags ?? new List<string>(),
                CreatedBy = request.CreatedBy
            };

            // Validate cron expression
            if (!schedule.IsValidCronExpression())
            {
                return Results.BadRequest(new { error = "Invalid cron expression" });
            }

            var created = await store.CreateScheduleAsync(schedule);
            return Results.Created($"/admin/api/geoetl/schedules/{created.Id}", created);
        })
        .WithName("CreateSchedule")
        .WithSummary("Create a new workflow schedule");

        // Update an existing schedule
        group.MapPut("/{scheduleId:guid}", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromRoute] Guid scheduleId,
            [FromBody] UpdateScheduleRequest request) =>
        {
            if (request.UpdatedBy == Guid.Empty)
            {
                return Results.BadRequest(new { error = "UpdatedBy is required" });
            }

            var existing = await store.GetScheduleAsync(scheduleId);
            if (existing == null)
            {
                return Results.NotFound(new { error = "Schedule not found" });
            }

            // Update fields
            if (request.Name != null) existing.Name = request.Name;
            if (request.Description != null) existing.Description = request.Description;
            if (request.CronExpression != null)
            {
                existing.CronExpression = request.CronExpression;
                // Recalculate next run time
                existing.NextRunAt = existing.CalculateNextRun();
            }
            if (request.Timezone != null) existing.Timezone = request.Timezone;
            if (request.Enabled.HasValue) existing.Enabled = request.Enabled.Value;
            if (request.ParameterValues != null) existing.ParameterValues = request.ParameterValues;
            if (request.MaxConcurrentExecutions.HasValue) existing.MaxConcurrentExecutions = request.MaxConcurrentExecutions.Value;
            if (request.RetryAttempts.HasValue) existing.RetryAttempts = request.RetryAttempts.Value;
            if (request.RetryDelayMinutes.HasValue) existing.RetryDelayMinutes = request.RetryDelayMinutes.Value;
            if (request.ExpiresAt.HasValue) existing.ExpiresAt = request.ExpiresAt;
            if (request.NotificationConfig != null) existing.NotificationConfig = request.NotificationConfig;
            if (request.Tags != null) existing.Tags = request.Tags;

            existing.UpdatedBy = request.UpdatedBy;

            // Validate cron expression
            if (!existing.IsValidCronExpression())
            {
                return Results.BadRequest(new { error = "Invalid cron expression" });
            }

            var updated = await store.UpdateScheduleAsync(existing);
            return Results.Ok(updated);
        })
        .WithName("UpdateSchedule")
        .WithSummary("Update an existing schedule");

        // Delete a schedule
        group.MapDelete("/{scheduleId:guid}", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromRoute] Guid scheduleId) =>
        {
            var schedule = await store.GetScheduleAsync(scheduleId);
            if (schedule == null)
            {
                return Results.NotFound(new { error = "Schedule not found" });
            }

            await store.DeleteScheduleAsync(scheduleId);
            return Results.NoContent();
        })
        .WithName("DeleteSchedule")
        .WithSummary("Delete a schedule");

        // Pause a schedule
        group.MapPost("/{scheduleId:guid}/pause", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromRoute] Guid scheduleId) =>
        {
            var schedule = await store.GetScheduleAsync(scheduleId);
            if (schedule == null)
            {
                return Results.NotFound(new { error = "Schedule not found" });
            }

            schedule.Enabled = false;
            schedule.Status = ScheduleStatus.Paused;
            await store.UpdateScheduleAsync(schedule);

            return Results.Ok(schedule);
        })
        .WithName("PauseSchedule")
        .WithSummary("Pause a schedule");

        // Resume a schedule
        group.MapPost("/{scheduleId:guid}/resume", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromRoute] Guid scheduleId) =>
        {
            var schedule = await store.GetScheduleAsync(scheduleId);
            if (schedule == null)
            {
                return Results.NotFound(new { error = "Schedule not found" });
            }

            schedule.Enabled = true;
            schedule.Status = ScheduleStatus.Active;
            schedule.NextRunAt = schedule.CalculateNextRun();
            await store.UpdateScheduleAsync(schedule);

            return Results.Ok(schedule);
        })
        .WithName("ResumeSchedule")
        .WithSummary("Resume a paused schedule");

        // Trigger immediate run
        group.MapPost("/{scheduleId:guid}/run-now", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromServices] IWorkflowStore workflowStore,
            [FromServices] IWorkflowEngine engine,
            [FromRoute] Guid scheduleId,
            [FromQuery] Guid? userId) =>
        {
            if (!userId.HasValue)
            {
                return Results.BadRequest(new { error = "userId query parameter is required" });
            }

            var schedule = await store.GetScheduleAsync(scheduleId);
            if (schedule == null)
            {
                return Results.NotFound(new { error = "Schedule not found" });
            }

            var workflow = await workflowStore.GetWorkflowAsync(schedule.WorkflowId);
            if (workflow == null)
            {
                return Results.BadRequest(new { error = "Workflow not found" });
            }

            // Execute workflow immediately
            var executionOptions = new WorkflowExecutionOptions
            {
                TenantId = schedule.TenantId,
                UserId = userId.Value,
                TriggerType = WorkflowTriggerType.Manual,
                ParameterValues = schedule.ParameterValues
            };

            var run = await engine.ExecuteAsync(workflow, executionOptions);

            // Update schedule last run
            await store.UpdateLastRunAsync(
                schedule.Id,
                run.Id,
                run.Status.ToString(),
                DateTimeOffset.UtcNow);

            return Results.Ok(new { scheduleId = schedule.Id, runId = run.Id, status = run.Status });
        })
        .WithName("RunScheduleNow")
        .WithSummary("Trigger an immediate execution of a schedule");

        // Get execution history
        group.MapGet("/{scheduleId:guid}/history", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromRoute] Guid scheduleId,
            [FromQuery] int limit = 50) =>
        {
            var schedule = await store.GetScheduleAsync(scheduleId);
            if (schedule == null)
            {
                return Results.NotFound(new { error = "Schedule not found" });
            }

            var history = await store.GetExecutionHistoryAsync(scheduleId, limit);
            return Results.Ok(history);
        })
        .WithName("GetScheduleHistory")
        .WithSummary("Get execution history for a schedule");

        // Get next execution times
        group.MapGet("/{scheduleId:guid}/next-runs", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromRoute] Guid scheduleId,
            [FromQuery] int count = 5) =>
        {
            var schedule = await store.GetScheduleAsync(scheduleId);
            if (schedule == null)
            {
                return Results.NotFound(new { error = "Schedule not found" });
            }

            var nextRuns = schedule.GetNextExecutions(count);
            return Results.Ok(new { scheduleId = schedule.Id, nextRuns });
        })
        .WithName("GetNextScheduleRuns")
        .WithSummary("Get the next N execution times for a schedule");

        // List schedules for a workflow
        group.MapGet("/workflow/{workflowId:guid}", async (
            [FromServices] IWorkflowScheduleStore store,
            [FromRoute] Guid workflowId) =>
        {
            var schedules = await store.ListSchedulesByWorkflowAsync(workflowId);
            return Results.Ok(schedules);
        })
        .WithName("ListWorkflowSchedules")
        .WithSummary("List all schedules for a specific workflow");

        return endpoints;
    }
}

/// <summary>
/// Request model for creating a schedule
/// </summary>
public record CreateScheduleRequest
{
    public required Guid TenantId { get; init; }
    public required Guid WorkflowId { get; init; }
    public required Guid CreatedBy { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string CronExpression { get; init; }
    public string? Timezone { get; init; }
    public bool? Enabled { get; init; }
    public Dictionary<string, object>? ParameterValues { get; init; }
    public int? MaxConcurrentExecutions { get; init; }
    public int? RetryAttempts { get; init; }
    public int? RetryDelayMinutes { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public ScheduleNotificationConfig? NotificationConfig { get; init; }
    public List<string>? Tags { get; init; }
}

/// <summary>
/// Request model for updating a schedule
/// </summary>
public record UpdateScheduleRequest
{
    public required Guid UpdatedBy { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? CronExpression { get; init; }
    public string? Timezone { get; init; }
    public bool? Enabled { get; init; }
    public Dictionary<string, object>? ParameterValues { get; init; }
    public int? MaxConcurrentExecutions { get; init; }
    public int? RetryAttempts { get; init; }
    public int? RetryDelayMinutes { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public ScheduleNotificationConfig? NotificationConfig { get; init; }
    public List<string>? Tags { get; init; }
}

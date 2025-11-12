// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Resilience;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Admin;

/// <summary>
/// API endpoints for GeoETL resilience and error handling
/// </summary>
public static class GeoEtlResilienceEndpoints
{
    public static IEndpointRouteBuilder MapGeoEtlResilienceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/geoetl")
            .WithTags("GeoETL Resilience")
            .WithOpenApi()
            .RequireAuthorization("RequireAdministrator");

        // Failed Workflows
        group.MapGet("/failed-workflows", GetFailedWorkflows)
            .WithName("GetFailedWorkflows")
            .WithSummary("List failed workflows with filtering");

        group.MapGet("/failed-workflows/{id:guid}", GetFailedWorkflow)
            .WithName("GetFailedWorkflow")
            .WithSummary("Get detailed information about a failed workflow");

        group.MapPost("/failed-workflows/{id:guid}/retry", RetryFailedWorkflow)
            .WithName("RetryFailedWorkflow")
            .WithSummary("Retry a single failed workflow");

        group.MapPost("/failed-workflows/bulk-retry", BulkRetryFailedWorkflows)
            .WithName("BulkRetryFailedWorkflows")
            .WithSummary("Retry multiple failed workflows");

        group.MapPost("/failed-workflows/{id:guid}/abandon", AbandonFailedWorkflow)
            .WithName("AbandonFailedWorkflow")
            .WithSummary("Mark a failed workflow as abandoned");

        group.MapPost("/failed-workflows/{id:guid}/assign", AssignFailedWorkflow)
            .WithName("AssignFailedWorkflow")
            .WithSummary("Assign a failed workflow to a user");

        group.MapGet("/failed-workflows/{id:guid}/related", GetRelatedFailures)
            .WithName("GetRelatedFailures")
            .WithSummary("Find related failures with similar error patterns");

        // Circuit Breakers
        group.MapGet("/circuit-breakers", GetCircuitBreakers)
            .WithName("GetCircuitBreakers")
            .WithSummary("Get status of all circuit breakers");

        group.MapGet("/circuit-breakers/{nodeType}", GetCircuitBreakerState)
            .WithName("GetCircuitBreakerState")
            .WithSummary("Get state of a specific circuit breaker");

        group.MapPost("/circuit-breakers/{nodeType}/reset", ResetCircuitBreaker)
            .WithName("ResetCircuitBreaker")
            .WithSummary("Manually reset a circuit breaker");

        // Statistics
        group.MapGet("/error-stats", GetErrorStatistics)
            .WithName("GetErrorStatistics")
            .WithSummary("Get error statistics and analytics");

        group.MapPost("/error-stats/cleanup", CleanupOldFailures)
            .WithName("CleanupOldFailures")
            .WithSummary("Clean up old resolved/abandoned failures");

        return endpoints;
    }

    private static async Task<IResult> GetFailedWorkflows(
        [FromServices] IDeadLetterQueueService dlqService,
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? workflowId,
        [FromQuery] string? status,
        [FromQuery] string? errorCategory,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] Guid? assignedTo,
        [FromQuery] string? searchText,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = true)
    {
        var filter = new FailedWorkflowFilter
        {
            TenantId = tenantId,
            WorkflowId = workflowId,
            Status = status != null ? Enum.Parse<FailedWorkflowStatus>(status, true) : null,
            ErrorCategory = errorCategory != null ? Enum.Parse<ErrorCategory>(errorCategory, true) : null,
            FromDate = fromDate,
            ToDate = toDate,
            AssignedTo = assignedTo,
            SearchText = searchText,
            Skip = skip,
            Take = Math.Min(take, 100), // Cap at 100
            SortBy = sortBy,
            SortDescending = sortDescending
        };

        var result = await dlqService.ListAsync(filter);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetFailedWorkflow(
        [FromServices] IDeadLetterQueueService dlqService,
        [FromRoute] Guid id)
    {
        var failedWorkflow = await dlqService.GetAsync(id);
        if (failedWorkflow == null)
        {
            return Results.NotFound(new { error = "Failed workflow not found" });
        }

        return Results.Ok(failedWorkflow);
    }

    private static async Task<IResult> RetryFailedWorkflow(
        [FromServices] IDeadLetterQueueService dlqService,
        [FromRoute] Guid id,
        [FromBody] RetryOptions options)
    {
        try
        {
            var newRun = await dlqService.RetryAsync(id, options);
            return Results.Ok(new
            {
                success = true,
                newRunId = newRun.Id,
                message = "Workflow retry initiated successfully"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static async Task<IResult> BulkRetryFailedWorkflows(
        [FromServices] IDeadLetterQueueService dlqService,
        [FromBody] BulkRetryRequest request)
    {
        try
        {
            var result = await dlqService.BulkRetryAsync(request.FailedWorkflowIds, request.Options);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static async Task<IResult> AbandonFailedWorkflow(
        [FromServices] IDeadLetterQueueService dlqService,
        [FromRoute] Guid id,
        [FromBody] AbandonRequest request)
    {
        await dlqService.AbandonAsync(id, request.Reason);
        return Results.Ok(new
        {
            success = true,
            message = "Failed workflow marked as abandoned"
        });
    }

    private static async Task<IResult> AssignFailedWorkflow(
        [FromServices] IDeadLetterQueueService dlqService,
        [FromRoute] Guid id,
        [FromBody] AssignRequest request)
    {
        await dlqService.AssignAsync(id, request.UserId);
        return Results.Ok(new
        {
            success = true,
            message = "Failed workflow assigned successfully"
        });
    }

    private static async Task<IResult> GetRelatedFailures(
        [FromServices] IDeadLetterQueueService dlqService,
        [FromRoute] Guid id)
    {
        var relatedFailures = await dlqService.FindRelatedFailuresAsync(id);
        return Results.Ok(relatedFailures);
    }

    private static async Task<IResult> GetCircuitBreakers(
        [FromServices] ICircuitBreakerService? circuitBreakerService)
    {
        if (circuitBreakerService == null)
        {
            return Results.Ok(new
            {
                enabled = false,
                message = "Circuit breaker service is not enabled"
            });
        }

        var stats = await circuitBreakerService.GetStatsAsync();
        return Results.Ok(new
        {
            enabled = true,
            circuitBreakers = stats.NodeTypeStats.Select(kvp => new
            {
                nodeType = kvp.Key,
                state = kvp.Value.State.ToString(),
                consecutiveFailures = kvp.Value.ConsecutiveFailures,
                totalFailures = kvp.Value.TotalFailures,
                totalSuccesses = kvp.Value.TotalSuccesses,
                failureRate = kvp.Value.FailureRate,
                lastFailureAt = kvp.Value.LastFailureAt,
                openedAt = kvp.Value.OpenedAt,
                halfOpenAt = kvp.Value.HalfOpenAt
            })
        });
    }

    private static async Task<IResult> GetCircuitBreakerState(
        [FromServices] ICircuitBreakerService? circuitBreakerService,
        [FromRoute] string nodeType)
    {
        if (circuitBreakerService == null)
        {
            return Results.Ok(new { enabled = false });
        }

        var state = await circuitBreakerService.GetStateAsync(nodeType);
        return Results.Ok(new
        {
            nodeType,
            state = state.ToString()
        });
    }

    private static async Task<IResult> ResetCircuitBreaker(
        [FromServices] ICircuitBreakerService? circuitBreakerService,
        [FromRoute] string nodeType)
    {
        if (circuitBreakerService == null)
        {
            return Results.BadRequest(new { error = "Circuit breaker service is not enabled" });
        }

        await circuitBreakerService.ResetAsync(nodeType);
        return Results.Ok(new
        {
            success = true,
            message = $"Circuit breaker for {nodeType} has been reset"
        });
    }

    private static async Task<IResult> GetErrorStatistics(
        [FromServices] IDeadLetterQueueService dlqService,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var stats = await dlqService.GetStatisticsAsync(from, to);
        return Results.Ok(stats);
    }

    private static async Task<IResult> CleanupOldFailures(
        [FromServices] IDeadLetterQueueService dlqService,
        [FromBody] CleanupRequest request)
    {
        var deletedCount = await dlqService.CleanupOldFailuresAsync(request.RetentionDays);
        return Results.Ok(new
        {
            success = true,
            deletedCount,
            message = $"Cleaned up {deletedCount} old failed workflows"
        });
    }

    // Request models
    public record BulkRetryRequest(List<Guid> FailedWorkflowIds, RetryOptions Options);
    public record AbandonRequest(string? Reason);
    public record AssignRequest(Guid UserId);
    public record CleanupRequest(int RetentionDays = 30);
}

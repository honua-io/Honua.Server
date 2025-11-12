// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Admin;

/// <summary>
/// REST API endpoints for GeoETL workflow execution and monitoring
/// </summary>
public static class GeoEtlExecutionEndpoints
{
    public static IEndpointRouteBuilder MapGeoEtlExecutionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/geoetl/executions")
            .WithTags("GeoETL Execution")
            .WithOpenApi()
            .RequireAuthorization("RequireAdministrator");

        // Execute a workflow
        group.MapPost("/", async (
            [FromServices] IWorkflowStore store,
            [FromServices] IWorkflowEngine engine,
            [FromBody] ExecuteWorkflowRequest request,
            CancellationToken cancellationToken) =>
        {
            if (request.WorkflowId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "WorkflowId is required" });
            }

            if (request.TenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "TenantId is required" });
            }

            if (request.UserId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "UserId is required" });
            }

            var workflow = await store.GetWorkflowAsync(request.WorkflowId, cancellationToken);
            if (workflow == null)
            {
                return Results.NotFound(new { error = "Workflow not found" });
            }

            // Validate before execution
            var validation = await engine.ValidateAsync(workflow, request.ParameterValues, cancellationToken);
            if (!validation.IsValid)
            {
                return Results.BadRequest(new
                {
                    error = "Workflow validation failed",
                    validationErrors = validation.Errors
                });
            }

            // Execute workflow
            var options = new WorkflowExecutionOptions
            {
                TenantId = request.TenantId,
                UserId = request.UserId,
                ParameterValues = request.ParameterValues ?? new Dictionary<string, object>()
            };

            try
            {
                var run = await engine.ExecuteAsync(workflow, options, cancellationToken);
                return Results.Created($"/admin/api/geoetl/executions/{run.Id}", run);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Workflow execution failed",
                    detail: ex.Message,
                    statusCode: 500);
            }
        })
        .WithName("ExecuteWorkflow")
        .WithSummary("Execute a workflow");

        // Get execution status
        group.MapGet("/{runId:guid}", async (
            [FromServices] IWorkflowStore store,
            [FromRoute] Guid runId,
            CancellationToken cancellationToken = default) =>
        {
            var run = await store.GetRunAsync(runId, cancellationToken);
            if (run == null)
            {
                return Results.NotFound(new { error = "Workflow run not found" });
            }

            return Results.Ok(run);
        })
        .WithName("GetWorkflowRun")
        .WithSummary("Get workflow execution status");

        // List workflow runs for a specific workflow
        group.MapGet("/workflow/{workflowId:guid}", async (
            [FromServices] IWorkflowStore store,
            [FromRoute] Guid workflowId,
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0,
            CancellationToken cancellationToken = default) =>
        {
            var runs = await store.ListRunsAsync(workflowId, cancellationToken);
            return Results.Ok(runs);
        })
        .WithName("ListWorkflowRuns")
        .WithSummary("List all runs for a specific workflow");

        // List all workflow runs for a tenant
        group.MapGet("/tenant/{tenantId:guid}", async (
            [FromServices] IWorkflowStore store,
            [FromRoute] Guid tenantId,
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0,
            CancellationToken cancellationToken = default) =>
        {
            var runs = await store.ListRunsByTenantAsync(tenantId, limit, cancellationToken);
            return Results.Ok(runs);
        })
        .WithName("ListTenantWorkflowRuns")
        .WithSummary("List all workflow runs for a tenant");

        // Get execution statistics
        group.MapGet("/stats/{workflowId:guid}", async (
            [FromServices] IWorkflowStore store,
            [FromRoute] Guid workflowId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            CancellationToken cancellationToken = default) =>
        {
            // Get all runs for the workflow
            var runs = await store.ListRunsAsync(workflowId, cancellationToken);

            // Filter by date range if provided
            if (startDate.HasValue)
            {
                runs = runs.Where(r => r.StartedAt >= startDate.Value).ToList();
            }
            if (endDate.HasValue)
            {
                runs = runs.Where(r => r.StartedAt <= endDate.Value).ToList();
            }

            // Calculate statistics
            var stats = new
            {
                totalRuns = runs.Count,
                completedRuns = runs.Count(r => r.Status == WorkflowRunStatus.Completed),
                failedRuns = runs.Count(r => r.Status == WorkflowRunStatus.Failed),
                runningRuns = runs.Count(r => r.Status == WorkflowRunStatus.Running),
                totalFeatures = runs.Where(r => r.Status == WorkflowRunStatus.Completed).Sum(r => r.FeaturesProcessed),
                avgDurationSeconds = runs
                    .Where(r => r.Status == WorkflowRunStatus.Completed && r.CompletedAt.HasValue && r.StartedAt.HasValue)
                    .Select(r => (r.CompletedAt!.Value - r.StartedAt!.Value).TotalSeconds)
                    .DefaultIfEmpty(0)
                    .Average(),
                totalComputeCost = 0, // TODO: Add cost calculation when available
                recentRuns = runs.OrderByDescending(r => r.StartedAt).Take(10)
            };

            return Results.Ok(stats);
        })
        .WithName("GetWorkflowStats")
        .WithSummary("Get execution statistics for a workflow");

        // Get node-level execution details
        group.MapGet("/{runId:guid}/nodes", async (
            [FromServices] IWorkflowStore store,
            [FromRoute] Guid runId,
            CancellationToken cancellationToken = default) =>
        {
            var run = await store.GetRunAsync(runId, cancellationToken);
            if (run == null)
            {
                return Results.NotFound(new { error = "Workflow run not found" });
            }

            return Results.Ok(run.NodeRuns);
        })
        .WithName("GetWorkflowRunNodes")
        .WithSummary("Get node-level execution details for a workflow run");

        // Cancel a running workflow
        group.MapPost("/{runId:guid}/cancel", async (
            [FromServices] IWorkflowStore store,
            [FromRoute] Guid runId,
            CancellationToken cancellationToken = default) =>
        {
            var run = await store.GetRunAsync(runId, cancellationToken);
            if (run == null)
            {
                return Results.NotFound(new { error = "Workflow run not found" });
            }

            if (run.Status != WorkflowRunStatus.Running)
            {
                return Results.BadRequest(new { error = "Workflow is not currently running" });
            }

            // Update status to cancelled
            run.Status = WorkflowRunStatus.Cancelled;
            run.CompletedAt = DateTime.UtcNow;
            run.ErrorMessage = "Cancelled by user";

            await store.UpdateRunAsync(run, cancellationToken);

            return Results.Ok(run);
        })
        .WithName("CancelWorkflowRun")
        .WithSummary("Cancel a running workflow");

        return endpoints;
    }
}

/// <summary>
/// Request model for executing a workflow
/// </summary>
public record ExecuteWorkflowRequest
{
    public required Guid WorkflowId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public Dictionary<string, object>? ParameterValues { get; init; }
}

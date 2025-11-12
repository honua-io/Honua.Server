// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Admin;

/// <summary>
/// REST API endpoints for GeoETL workflow management
/// </summary>
public static class GeoEtlWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapGeoEtlWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/geoetl/workflows")
            .WithTags("GeoETL Workflows")
            .WithOpenApi()
            .RequireAuthorization("RequireAdministrator");

        // List all workflows for a tenant
        group.MapGet("/", async (
            [FromServices] IWorkflowStore store,
            [FromQuery] Guid? tenantId,
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0,
            CancellationToken cancellationToken = default) =>
        {
            if (!tenantId.HasValue)
            {
                return Results.BadRequest(new { error = "tenantId query parameter is required" });
            }

            var workflows = await store.ListWorkflowsAsync(tenantId.Value, cancellationToken);
            return Results.Ok(workflows);
        })
        .WithName("ListWorkflows")
        .WithSummary("List all workflows for a tenant");

        // Get a specific workflow by ID
        group.MapGet("/{workflowId:guid}", async (
            [FromServices] IWorkflowStore store,
            [FromRoute] Guid workflowId,
            [FromQuery] Guid? tenantId,
            CancellationToken cancellationToken = default) =>
        {
            if (!tenantId.HasValue)
            {
                return Results.BadRequest(new { error = "tenantId query parameter is required" });
            }

            var workflow = await store.GetWorkflowAsync(workflowId, cancellationToken);
            if (workflow == null)
            {
                return Results.NotFound(new { error = "Workflow not found" });
            }

            return Results.Ok(workflow);
        })
        .WithName("GetWorkflow")
        .WithSummary("Get a workflow by ID");

        // Create a new workflow
        group.MapPost("/", async (
            [FromServices] IWorkflowStore store,
            [FromBody] CreateWorkflowRequest request,
            CancellationToken cancellationToken = default) =>
        {
            if (request.TenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "TenantId is required" });
            }

            if (request.CreatedBy == Guid.Empty)
            {
                return Results.BadRequest(new { error = "CreatedBy is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Workflow name is required" });
            }

            var workflow = new WorkflowDefinition
            {
                TenantId = request.TenantId,
                CreatedBy = request.CreatedBy,
                Metadata = new WorkflowMetadata
                {
                    Name = request.Name,
                    Description = request.Description,
                    Author = request.Author,
                    Tags = request.Tags?.ToList() ?? new List<string>(),
                    Category = request.Category
                },
                Nodes = request.Nodes ?? new List<WorkflowNode>(),
                Edges = request.Edges ?? new List<WorkflowEdge>(),
                Parameters = request.Parameters?.ToDictionary(kvp => kvp.Key, kvp => new WorkflowParameter { Name = kvp.Key, DefaultValue = kvp.Value }),
                Version = 1
            };

            var created = await store.CreateWorkflowAsync(workflow, cancellationToken);
            return Results.Created($"/admin/api/geoetl/workflows/{created.Id}", created);
        })
        .WithName("CreateWorkflow")
        .WithSummary("Create a new workflow");

        // Update an existing workflow
        group.MapPut("/{workflowId:guid}", async (
            [FromServices] IWorkflowStore store,
            [FromRoute] Guid workflowId,
            [FromBody] UpdateWorkflowRequest request,
            CancellationToken cancellationToken = default) =>
        {
            if (request.TenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "TenantId is required" });
            }

            if (request.UpdatedBy == Guid.Empty)
            {
                return Results.BadRequest(new { error = "UpdatedBy is required" });
            }

            var existing = await store.GetWorkflowAsync(workflowId, cancellationToken);
            if (existing == null)
            {
                return Results.NotFound(new { error = "Workflow not found" });
            }

            // Update fields
            existing.Metadata = new WorkflowMetadata
            {
                Name = request.Name ?? existing.Metadata.Name,
                Description = request.Description ?? existing.Metadata.Description,
                Author = request.Author ?? existing.Metadata.Author,
                Tags = request.Tags?.ToList() ?? existing.Metadata.Tags,
                Category = request.Category ?? existing.Metadata.Category
            };

            if (request.Nodes != null)
            {
                existing.Nodes = request.Nodes;
            }

            if (request.Edges != null)
            {
                existing.Edges = request.Edges;
            }

            if (request.Parameters != null)
            {
                existing.Parameters = request.Parameters.ToDictionary(kvp => kvp.Key, kvp => new WorkflowParameter { Name = kvp.Key, DefaultValue = kvp.Value });
            }

            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = request.UpdatedBy;

            var updated = await store.UpdateWorkflowAsync(existing, cancellationToken);
            return Results.Ok(updated);
        })
        .WithName("UpdateWorkflow")
        .WithSummary("Update an existing workflow");

        // Delete a workflow (soft delete)
        group.MapDelete("/{workflowId:guid}", async (
            [FromServices] IWorkflowStore store,
            [FromRoute] Guid workflowId,
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid? deletedBy,
            CancellationToken cancellationToken = default) =>
        {
            if (!tenantId.HasValue)
            {
                return Results.BadRequest(new { error = "tenantId query parameter is required" });
            }

            if (!deletedBy.HasValue)
            {
                return Results.BadRequest(new { error = "deletedBy query parameter is required" });
            }

            var workflow = await store.GetWorkflowAsync(workflowId, cancellationToken);
            if (workflow == null)
            {
                return Results.NotFound(new { error = "Workflow not found" });
            }

            await store.DeleteWorkflowAsync(workflowId, cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteWorkflow")
        .WithSummary("Delete a workflow (soft delete)");

        // Validate a workflow
        group.MapPost("/{workflowId:guid}/validate", async (
            [FromServices] IWorkflowStore store,
            [FromServices] IWorkflowEngine engine,
            [FromRoute] Guid workflowId,
            [FromQuery] Guid? tenantId,
            CancellationToken cancellationToken = default) =>
        {
            if (!tenantId.HasValue)
            {
                return Results.BadRequest(new { error = "tenantId query parameter is required" });
            }

            var workflow = await store.GetWorkflowAsync(workflowId, cancellationToken);
            if (workflow == null)
            {
                return Results.NotFound(new { error = "Workflow not found" });
            }

            var validation = await engine.ValidateAsync(workflow, new Dictionary<string, object>(), cancellationToken);
            return Results.Ok(validation);
        })
        .WithName("ValidateWorkflow")
        .WithSummary("Validate a workflow");

        // Estimate workflow resources
        group.MapPost("/{workflowId:guid}/estimate", async (
            [FromServices] IWorkflowStore store,
            [FromServices] IWorkflowEngine engine,
            [FromRoute] Guid workflowId,
            [FromQuery] Guid? tenantId,
            CancellationToken cancellationToken = default) =>
        {
            if (!tenantId.HasValue)
            {
                return Results.BadRequest(new { error = "tenantId query parameter is required" });
            }

            var workflow = await store.GetWorkflowAsync(workflowId, cancellationToken);
            if (workflow == null)
            {
                return Results.NotFound(new { error = "Workflow not found" });
            }

            var estimate = await engine.EstimateAsync(workflow, new Dictionary<string, object>(), cancellationToken);
            return Results.Ok(estimate);
        })
        .WithName("EstimateWorkflow")
        .WithSummary("Estimate workflow resource usage");

        return endpoints;
    }
}

/// <summary>
/// Request model for creating a workflow
/// </summary>
public record CreateWorkflowRequest
{
    public required Guid TenantId { get; init; }
    public required Guid CreatedBy { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string[]? Tags { get; init; }
    public string? Category { get; init; }
    public List<WorkflowNode>? Nodes { get; init; }
    public List<WorkflowEdge>? Edges { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// Request model for updating a workflow
/// </summary>
public record UpdateWorkflowRequest
{
    public required Guid TenantId { get; init; }
    public required Guid UpdatedBy { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string[]? Tags { get; init; }
    public string? Category { get; init; }
    public List<WorkflowNode>? Nodes { get; init; }
    public List<WorkflowEdge>? Edges { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
}

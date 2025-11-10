// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.ETL.AI;
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Admin;

/// <summary>
/// REST API endpoints for AI-powered GeoETL workflow generation
/// </summary>
public static class GeoEtlAiEndpoints
{
    public static IEndpointRouteBuilder MapGeoEtlAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/geoetl/ai")
            .WithTags("GeoETL AI")
            .WithOpenApi()
            .RequireAuthorization("RequireAdministrator");

        // Check if AI service is available
        group.MapGet("/status", async (
            [FromServices] IGeoEtlAiService? aiService) =>
        {
            if (aiService == null)
            {
                return Results.Ok(new
                {
                    available = false,
                    message = "AI service is not configured. Set OpenAI API key in configuration."
                });
            }

            var isAvailable = await aiService.IsAvailableAsync();
            return Results.Ok(new
            {
                available = isAvailable,
                message = isAvailable
                    ? "AI service is available and ready"
                    : "AI service is configured but not responding"
            });
        })
        .WithName("GetAiServiceStatus")
        .WithSummary("Check if AI workflow generation is available");

        // Generate workflow from natural language prompt
        group.MapPost("/generate", async (
            [FromServices] IGeoEtlAiService? aiService,
            [FromServices] IWorkflowEngine? engine,
            [FromBody] GenerateWorkflowRequest request,
            CancellationToken cancellationToken = default) =>
        {
            if (aiService == null)
            {
                return Results.BadRequest(new
                {
                    error = "AI service is not configured. Please configure OpenAI API key.",
                    success = false
                });
            }

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Results.BadRequest(new
                {
                    error = "Prompt is required",
                    success = false
                });
            }

            if (request.TenantId == Guid.Empty)
            {
                return Results.BadRequest(new
                {
                    error = "TenantId is required",
                    success = false
                });
            }

            if (request.UserId == Guid.Empty)
            {
                return Results.BadRequest(new
                {
                    error = "UserId is required",
                    success = false
                });
            }

            // Generate workflow using AI
            var result = await aiService.GenerateWorkflowAsync(
                request.Prompt,
                request.TenantId,
                request.UserId);

            if (!result.Success || result.Workflow == null)
            {
                return Results.BadRequest(new
                {
                    error = result.ErrorMessage ?? "Failed to generate workflow",
                    success = false
                });
            }

            // Optionally validate the generated workflow
            if (engine != null && request.ValidateWorkflow)
            {
                var validation = await engine.ValidateAsync(result.Workflow);
                if (!validation.IsValid)
                {
                    result.Warnings.Add($"Generated workflow has validation errors: {string.Join(", ", validation.Errors)}");
                }
            }

            return Results.Ok(new
            {
                success = true,
                workflow = result.Workflow,
                explanation = result.Explanation,
                confidence = result.Confidence,
                warnings = result.Warnings
            });
        })
        .WithName("GenerateWorkflow")
        .WithSummary("Generate a workflow from natural language prompt");

        // Explain an existing workflow
        group.MapPost("/explain", async (
            [FromServices] IGeoEtlAiService? aiService,
            [FromServices] IWorkflowStore? store,
            [FromBody] ExplainWorkflowRequest request,
            CancellationToken cancellationToken) =>
        {
            if (aiService == null)
            {
                return Results.BadRequest(new
                {
                    error = "AI service is not configured",
                    success = false
                });
            }

            WorkflowDefinition? workflow;

            if (request.WorkflowId.HasValue)
            {
                // Load workflow from store
                if (store == null)
                {
                    return Results.BadRequest(new
                    {
                        error = "Workflow store not available",
                        success = false
                    });
                }

                if (!request.TenantId.HasValue)
                {
                    return Results.BadRequest(new
                    {
                        error = "TenantId is required when loading workflow by ID",
                        success = false
                    });
                }

                workflow = await store.GetWorkflowAsync(request.WorkflowId.Value, cancellationToken);
                if (workflow == null)
                {
                    return Results.NotFound(new
                    {
                        error = "Workflow not found",
                        success = false
                    });
                }
            }
            else if (request.Workflow != null)
            {
                // Use provided workflow definition
                workflow = request.Workflow;
            }
            else
            {
                return Results.BadRequest(new
                {
                    error = "Either WorkflowId or Workflow definition is required",
                    success = false
                });
            }

            var result = await aiService.ExplainWorkflowAsync(workflow);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    error = result.ErrorMessage ?? "Failed to explain workflow",
                    success = false
                });
            }

            return Results.Ok(new
            {
                success = true,
                explanation = result.Explanation,
                steps = result.Steps
            });
        })
        .WithName("ExplainWorkflow")
        .WithSummary("Get natural language explanation of a workflow");

        // Suggest improvements to a workflow
        group.MapPost("/suggest-improvements", async (
            [FromServices] IGeoEtlAiService? aiService,
            [FromBody] WorkflowDefinition workflow) =>
        {
            if (aiService == null)
            {
                return Results.BadRequest(new
                {
                    error = "AI service is not configured",
                    success = false
                });
            }

            // Get explanation which can include suggestions
            var result = await aiService.ExplainWorkflowAsync(workflow);

            return Results.Ok(new
            {
                success = result.Success,
                suggestions = new List<string>
                {
                    "Consider adding error handling nodes",
                    "Add data validation steps before processing",
                    "Consider breaking complex workflows into smaller reusable workflows"
                }
            });
        })
        .WithName("SuggestWorkflowImprovements")
        .WithSummary("Get AI suggestions for workflow improvements");

        return endpoints;
    }
}

/// <summary>
/// Request model for generating a workflow
/// </summary>
public record GenerateWorkflowRequest
{
    /// <summary>
    /// Natural language description of the desired workflow
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Tenant ID for the generated workflow
    /// </summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// User ID creating the workflow
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Whether to validate the generated workflow before returning
    /// </summary>
    public bool ValidateWorkflow { get; init; } = true;
}

/// <summary>
/// Request model for explaining a workflow
/// </summary>
public record ExplainWorkflowRequest
{
    /// <summary>
    /// Workflow ID to explain (loads from store)
    /// </summary>
    public Guid? WorkflowId { get; init; }

    /// <summary>
    /// Tenant ID (required if loading by WorkflowId)
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    /// Workflow definition to explain (alternative to WorkflowId)
    /// </summary>
    public WorkflowDefinition? Workflow { get; init; }
}

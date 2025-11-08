// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Stores;
using Honua.Server.Enterprise.ETL.Templates;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Admin;

/// <summary>
/// REST API endpoints for GeoETL workflow templates
/// </summary>
public static class GeoEtlTemplateEndpoints
{
    public static IEndpointRouteBuilder MapGeoEtlTemplateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/geoetl/templates")
            .WithTags("GeoETL Templates")
            .WithOpenApi();

        // Get all templates
        group.MapGet("/", async (
            [FromServices] IWorkflowTemplateRepository repository,
            [FromQuery] string? category = null,
            [FromQuery] string? tag = null,
            [FromQuery] string? search = null,
            [FromQuery] bool? featured = null,
            CancellationToken cancellationToken = default) =>
        {
            List<WorkflowTemplate> templates;

            if (!string.IsNullOrWhiteSpace(search))
            {
                templates = await repository.SearchTemplatesAsync(search, cancellationToken);
            }
            else if (featured == true)
            {
                templates = await repository.GetFeaturedTemplatesAsync(cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(category))
            {
                templates = await repository.GetTemplatesByCategoryAsync(category, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(tag))
            {
                templates = await repository.GetTemplatesByTagAsync(tag, cancellationToken);
            }
            else
            {
                templates = await repository.GetAllTemplatesAsync(cancellationToken);
            }

            return Results.Ok(new
            {
                templates,
                total = templates.Count
            });
        })
        .WithName("GetTemplates")
        .WithSummary("Get workflow templates with optional filtering");

        // Get a specific template by ID
        group.MapGet("/{templateId}", async (
            [FromServices] IWorkflowTemplateRepository repository,
            [FromRoute] string templateId,
            CancellationToken cancellationToken = default) =>
        {
            var template = await repository.GetTemplateByIdAsync(templateId, cancellationToken);
            if (template == null)
            {
                return Results.NotFound(new { error = "Template not found" });
            }

            return Results.Ok(template);
        })
        .WithName("GetTemplate")
        .WithSummary("Get a specific template by ID");

        // Get all categories
        group.MapGet("/categories", async (
            [FromServices] IWorkflowTemplateRepository repository,
            CancellationToken cancellationToken = default) =>
        {
            var categories = await repository.GetCategoriesAsync(cancellationToken);
            return Results.Ok(new { categories });
        })
        .WithName("GetTemplateCategories")
        .WithSummary("Get all template categories");

        // Get all tags
        group.MapGet("/tags", async (
            [FromServices] IWorkflowTemplateRepository repository,
            CancellationToken cancellationToken = default) =>
        {
            var tags = await repository.GetTagsAsync(cancellationToken);
            return Results.Ok(new { tags });
        })
        .WithName("GetTemplateTags")
        .WithSummary("Get all template tags");

        // Instantiate a template (create workflow from template)
        group.MapPost("/{templateId}/instantiate", async (
            [FromServices] IWorkflowTemplateRepository templateRepository,
            [FromServices] IWorkflowStore workflowStore,
            [FromRoute] string templateId,
            [FromBody] InstantiateTemplateRequest request,
            CancellationToken cancellationToken = default) =>
        {
            // Validate request
            if (request.TenantId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "TenantId is required" });
            }

            if (request.CreatedBy == Guid.Empty)
            {
                return Results.BadRequest(new { error = "CreatedBy is required" });
            }

            // Get template
            var template = await templateRepository.GetTemplateByIdAsync(templateId, cancellationToken);
            if (template == null)
            {
                return Results.NotFound(new { error = "Template not found" });
            }

            // Convert template to workflow definition
            var workflow = template.ToWorkflowDefinition(
                request.TenantId,
                request.CreatedBy,
                request.WorkflowName);

            // Apply parameter overrides if provided
            if (request.ParameterOverrides != null)
            {
                foreach (var (nodeId, overrides) in request.ParameterOverrides)
                {
                    var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
                    if (node != null && overrides is Dictionary<string, object> nodeOverrides)
                    {
                        foreach (var (key, value) in nodeOverrides)
                        {
                            node.Parameters[key] = value;
                        }
                    }
                }
            }

            // Create workflow
            var created = await workflowStore.CreateWorkflowAsync(workflow, cancellationToken);

            // Increment usage count
            await templateRepository.IncrementUsageCountAsync(templateId, cancellationToken);

            return Results.Created(
                $"/admin/api/geoetl/workflows/{created.Id}",
                new
                {
                    workflowId = created.Id,
                    templateId,
                    message = "Workflow created successfully from template"
                });
        })
        .WithName("InstantiateTemplate")
        .WithSummary("Create a new workflow from a template");

        // Get template statistics
        group.MapGet("/statistics", async (
            [FromServices] IWorkflowTemplateRepository repository,
            CancellationToken cancellationToken = default) =>
        {
            var templates = await repository.GetAllTemplatesAsync(cancellationToken);
            var categories = await repository.GetCategoriesAsync(cancellationToken);
            var tags = await repository.GetTagsAsync(cancellationToken);

            var statistics = new
            {
                totalTemplates = templates.Count,
                totalCategories = categories.Count,
                totalTags = tags.Count,
                featuredTemplates = templates.Count(t => t.IsFeatured),
                categoryBreakdown = templates
                    .GroupBy(t => t.Category)
                    .Select(g => new
                    {
                        category = g.Key,
                        count = g.Count(),
                        totalUsage = g.Sum(t => t.UsageCount)
                    })
                    .OrderByDescending(x => x.count)
                    .ToList(),
                difficultyBreakdown = templates
                    .GroupBy(t => t.Difficulty)
                    .Select(g => new
                    {
                        difficulty = g.Key.ToString(),
                        count = g.Count()
                    })
                    .OrderBy(x => x.difficulty)
                    .ToList(),
                mostUsed = templates
                    .OrderByDescending(t => t.UsageCount)
                    .Take(10)
                    .Select(t => new
                    {
                        t.Id,
                        t.Name,
                        t.Category,
                        t.UsageCount
                    })
                    .ToList()
            };

            return Results.Ok(statistics);
        })
        .WithName("GetTemplateStatistics")
        .WithSummary("Get template library statistics");

        return endpoints;
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.ETL.Models;

namespace Honua.Server.Enterprise.ETL.Templates;

/// <summary>
/// Represents a pre-built workflow template that users can instantiate
/// </summary>
public class WorkflowTemplate
{
    /// <summary>
    /// Unique template identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Template display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Template description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Template category (e.g., "Data Import/Export", "Buffer Operations")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Icon identifier for UI display (e.g., "upload", "buffer", "intersection")
    /// </summary>
    public string Icon { get; set; } = "workflow";

    /// <summary>
    /// Difficulty level: Easy, Medium, Hard
    /// </summary>
    public TemplateDifficulty Difficulty { get; set; } = TemplateDifficulty.Easy;

    /// <summary>
    /// Estimated execution time in minutes
    /// </summary>
    public int EstimatedMinutes { get; set; } = 5;

    /// <summary>
    /// Tags for searching and filtering
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Template author
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Template version
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Node definitions for the workflow
    /// </summary>
    public List<WorkflowNode> Nodes { get; set; } = new();

    /// <summary>
    /// Edge definitions connecting the nodes
    /// </summary>
    public List<WorkflowEdge> Edges { get; set; } = new();

    /// <summary>
    /// Parameter definitions with defaults
    /// </summary>
    public Dictionary<string, WorkflowParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Default parameter values that users can customize during instantiation
    /// </summary>
    public Dictionary<string, object>? ParameterDefaults { get; set; }

    /// <summary>
    /// Input requirements (data sources, formats, etc.)
    /// </summary>
    public List<string> InputRequirements { get; set; } = new();

    /// <summary>
    /// Expected outputs
    /// </summary>
    public List<string> ExpectedOutputs { get; set; } = new();

    /// <summary>
    /// Use case examples
    /// </summary>
    public List<string> UseCases { get; set; } = new();

    /// <summary>
    /// Whether this template is featured/recommended
    /// </summary>
    public bool IsFeatured { get; set; } = false;

    /// <summary>
    /// Number of times this template has been used
    /// </summary>
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// Preview image URL (optional)
    /// </summary>
    public string? PreviewImageUrl { get; set; }

    /// <summary>
    /// Documentation URL (optional)
    /// </summary>
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// Convert this template to a WorkflowDefinition
    /// </summary>
    public WorkflowDefinition ToWorkflowDefinition(Guid tenantId, Guid createdBy, string? customName = null)
    {
        return new WorkflowDefinition
        {
            TenantId = tenantId,
            CreatedBy = createdBy,
            Metadata = new WorkflowMetadata
            {
                Name = customName ?? Name,
                Description = Description,
                Author = Author,
                Tags = Tags,
                Category = Category,
                Custom = new Dictionary<string, object>
                {
                    { "templateId", Id },
                    { "templateVersion", Version }
                }
            },
            Nodes = Nodes.Select(n => new WorkflowNode
            {
                Id = n.Id,
                Type = n.Type,
                Name = n.Name,
                Description = n.Description,
                Parameters = new Dictionary<string, object>(n.Parameters),
                Execution = n.Execution,
                Position = n.Position
            }).ToList(),
            Edges = Edges.Select(e => new WorkflowEdge
            {
                From = e.From,
                To = e.To,
                FromPort = e.FromPort,
                ToPort = e.ToPort,
                Label = e.Label
            }).ToList(),
            Parameters = new Dictionary<string, WorkflowParameter>(Parameters)
        };
    }
}

/// <summary>
/// Template difficulty levels
/// </summary>
public enum TemplateDifficulty
{
    Easy,
    Medium,
    Hard
}

/// <summary>
/// Response model for template instantiation
/// </summary>
public record InstantiateTemplateRequest
{
    /// <summary>
    /// Tenant ID for the new workflow
    /// </summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// User creating the workflow
    /// </summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>
    /// Custom name for the workflow (optional, defaults to template name)
    /// </summary>
    public string? WorkflowName { get; init; }

    /// <summary>
    /// Custom parameter values to override template defaults
    /// </summary>
    public Dictionary<string, object>? ParameterOverrides { get; init; }
}

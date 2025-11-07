// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;

namespace Honua.Server.Enterprise.ETL.AI;

/// <summary>
/// AI service for generating and explaining GeoETL workflows
/// </summary>
public interface IGeoEtlAiService
{
    /// <summary>
    /// Generates a workflow from a natural language prompt
    /// </summary>
    /// <param name="prompt">Natural language description of desired workflow</param>
    /// <param name="tenantId">Tenant ID for the generated workflow</param>
    /// <param name="userId">User ID creating the workflow</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated workflow definition</returns>
    Task<WorkflowGenerationResult> GenerateWorkflowAsync(
        string prompt,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explains a workflow in natural language
    /// </summary>
    /// <param name="workflow">Workflow to explain</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Natural language explanation of the workflow</returns>
    Task<WorkflowExplanationResult> ExplainWorkflowAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if the AI service is properly configured and available
    /// </summary>
    /// <returns>True if service is available</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of workflow generation
/// </summary>
public class WorkflowGenerationResult
{
    /// <summary>
    /// Generated workflow definition
    /// </summary>
    public WorkflowDefinition? Workflow { get; set; }

    /// <summary>
    /// Whether generation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if generation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Explanation of what the workflow does
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Warnings or suggestions about the generated workflow
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    public static WorkflowGenerationResult Succeed(WorkflowDefinition workflow, string explanation, double confidence = 1.0)
    {
        return new WorkflowGenerationResult
        {
            Success = true,
            Workflow = workflow,
            Explanation = explanation,
            Confidence = confidence
        };
    }

    public static WorkflowGenerationResult Fail(string errorMessage)
    {
        return new WorkflowGenerationResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Result of workflow explanation
/// </summary>
public class WorkflowExplanationResult
{
    /// <summary>
    /// Natural language explanation of the workflow
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Step-by-step breakdown of the workflow
    /// </summary>
    public List<string> Steps { get; set; } = new();

    /// <summary>
    /// Whether explanation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if explanation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    public static WorkflowExplanationResult Succeed(string explanation, List<string> steps)
    {
        return new WorkflowExplanationResult
        {
            Success = true,
            Explanation = explanation,
            Steps = steps
        };
    }

    public static WorkflowExplanationResult Fail(string errorMessage)
    {
        return new WorkflowExplanationResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

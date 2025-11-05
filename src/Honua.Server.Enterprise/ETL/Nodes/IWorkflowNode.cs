// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;

namespace Honua.Server.Enterprise.ETL.Nodes;

/// <summary>
/// Interface for all workflow nodes
/// </summary>
public interface IWorkflowNode
{
    /// <summary>
    /// Node type identifier (e.g., "geoprocessing.buffer", "data_source.postgis")
    /// </summary>
    string NodeType { get; }

    /// <summary>
    /// Node display name
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Node description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Validates node parameters before execution
    /// </summary>
    Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates resource usage for this node
    /// </summary>
    Task<NodeEstimate> EstimateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the node
    /// </summary>
    Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context provided to node during execution
/// </summary>
public class NodeExecutionContext
{
    /// <summary>
    /// Workflow run ID
    /// </summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>
    /// Node run ID
    /// </summary>
    public Guid NodeRunId { get; set; }

    /// <summary>
    /// Node definition from workflow
    /// </summary>
    public required WorkflowNode NodeDefinition { get; set; }

    /// <summary>
    /// Runtime parameter values (workflow parameters + inputs from previous nodes)
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Inputs from upstream nodes (keyed by node ID)
    /// </summary>
    public Dictionary<string, NodeExecutionResult> Inputs { get; set; } = new();

    /// <summary>
    /// Progress callback
    /// </summary>
    public IProgress<NodeProgress>? ProgressCallback { get; set; }

    /// <summary>
    /// Workflow run state (for storing/retrieving intermediate data)
    /// </summary>
    public Dictionary<string, object> State { get; set; } = new();

    /// <summary>
    /// Tenant ID
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }
}

/// <summary>
/// Result of node validation
/// </summary>
public class NodeValidationResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Helper to create success result
    /// </summary>
    public static NodeValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Helper to create failure result
    /// </summary>
    public static NodeValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = new List<string>(errors)
    };
}

/// <summary>
/// Resource estimate for a node
/// </summary>
public class NodeEstimate
{
    /// <summary>
    /// Estimated duration in seconds
    /// </summary>
    public long EstimatedDurationSeconds { get; set; }

    /// <summary>
    /// Estimated memory usage in MB
    /// </summary>
    public long EstimatedMemoryMB { get; set; }

    /// <summary>
    /// Estimated CPU cores needed
    /// </summary>
    public int EstimatedCpuCores { get; set; } = 1;

    /// <summary>
    /// Estimated features to process
    /// </summary>
    public long? EstimatedFeatures { get; set; }

    /// <summary>
    /// Estimated cost in USD
    /// </summary>
    public decimal? EstimatedCostUsd { get; set; }
}

/// <summary>
/// Progress information from node execution
/// </summary>
public class NodeProgress
{
    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Percentage { get; set; }

    /// <summary>
    /// Progress message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Features processed so far
    /// </summary>
    public long? FeaturesProcessed { get; set; }

    /// <summary>
    /// Total features to process
    /// </summary>
    public long? TotalFeatures { get; set; }
}

/// <summary>
/// Result of node execution
/// </summary>
public class NodeExecutionResult
{
    /// <summary>
    /// Whether execution was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Output data to pass to downstream nodes
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error details/stack trace
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Features processed
    /// </summary>
    public long? FeaturesProcessed { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Helper to create success result
    /// </summary>
    public static NodeExecutionResult Succeed(Dictionary<string, object>? data = null) => new()
    {
        Success = true,
        Data = data ?? new()
    };

    /// <summary>
    /// Helper to create failure result
    /// </summary>
    public static NodeExecutionResult Fail(string errorMessage, string? errorDetails = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorDetails = errorDetails
    };
}

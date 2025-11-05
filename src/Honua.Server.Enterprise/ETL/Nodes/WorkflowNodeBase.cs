// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.Nodes;

/// <summary>
/// Base class for workflow nodes with common functionality
/// </summary>
public abstract class WorkflowNodeBase : IWorkflowNode
{
    protected readonly ILogger Logger;

    protected WorkflowNodeBase(ILogger logger)
    {
        Logger = logger;
    }

    public abstract string NodeType { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }

    public virtual Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var result = new NodeValidationResult { IsValid = true };

        // Basic validation - override in derived classes for specific validation
        if (string.IsNullOrWhiteSpace(nodeDefinition.Id))
        {
            result.IsValid = false;
            result.Errors.Add("Node ID is required");
        }

        return Task.FromResult(result);
    }

    public virtual Task<NodeEstimate> EstimateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        // Default estimate - override in derived classes for accurate estimates
        return Task.FromResult(new NodeEstimate
        {
            EstimatedDurationSeconds = 10,
            EstimatedMemoryMB = 256,
            EstimatedCpuCores = 1
        });
    }

    public abstract Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Helper to report progress
    /// </summary>
    protected void ReportProgress(
        NodeExecutionContext context,
        int percentage,
        string? message = null,
        long? featuresProcessed = null,
        long? totalFeatures = null)
    {
        context.ProgressCallback?.Report(new NodeProgress
        {
            Percentage = percentage,
            Message = message,
            FeaturesProcessed = featuresProcessed,
            TotalFeatures = totalFeatures
        });
    }

    /// <summary>
    /// Helper to get required parameter
    /// </summary>
    protected T GetRequiredParameter<T>(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            throw new ArgumentException($"Required parameter '{key}' not found");
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Parameter '{key}' has invalid type. Expected {typeof(T).Name}", ex);
        }
    }

    /// <summary>
    /// Helper to get optional parameter
    /// </summary>
    protected T? GetOptionalParameter<T>(Dictionary<string, object> parameters, string key, T? defaultValue = default)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Helper to resolve parameter values (supports templates like {{parameters.foo}})
    /// </summary>
    protected object? ResolveParameterValue(string value, NodeExecutionContext context)
    {
        // Simple template resolution - supports {{parameters.key}} and {{nodes.nodeId.outputKey}}
        if (value.StartsWith("{{") && value.EndsWith("}}"))
        {
            var expression = value[2..^2].Trim();
            var parts = expression.Split('.');

            if (parts.Length >= 2)
            {
                if (parts[0] == "parameters" && context.Parameters.TryGetValue(parts[1], out var paramValue))
                {
                    return paramValue;
                }
                else if (parts[0] == "nodes" && parts.Length >= 3)
                {
                    var nodeId = parts[1];
                    var outputKey = parts[2];

                    if (context.Inputs.TryGetValue(nodeId, out var nodeResult) &&
                        nodeResult.Data.TryGetValue(outputKey, out var outputValue))
                    {
                        return outputValue;
                    }
                }
            }
        }

        return value;
    }
}

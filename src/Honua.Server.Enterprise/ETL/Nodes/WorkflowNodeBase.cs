// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Resilience;
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

    /// <summary>
    /// Override to provide custom retry policy for this node type
    /// </summary>
    protected virtual RetryPolicy GetRetryPolicy() => RetryPolicy.Default;

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

    /// <summary>
    /// Execute with automatic retry logic
    /// </summary>
    public async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // Check if node has custom retry configuration
        var retryPolicy = context.NodeDefinition.Execution?.MaxRetries.HasValue == true
            ? new RetryPolicy { MaxAttempts = context.NodeDefinition.Execution.MaxRetries.Value }
            : GetRetryPolicy();

        // If no retries configured, execute directly
        if (retryPolicy.MaxAttempts == 0)
        {
            return await ExecuteInternalAsync(context, cancellationToken);
        }

        // Execute with retry logic
        return await ExecuteWithRetryAsync(context, retryPolicy, cancellationToken);
    }

    /// <summary>
    /// Execute with retry logic
    /// </summary>
    private async Task<NodeExecutionResult> ExecuteWithRetryAsync(
        NodeExecutionContext context,
        RetryPolicy retryPolicy,
        CancellationToken cancellationToken)
    {
        var attemptNumber = 0;
        Exception? lastException = null;

        while (attemptNumber <= retryPolicy.MaxAttempts)
        {
            try
            {
                if (attemptNumber > 0)
                {
                    Logger.LogInformation(
                        "Retrying node {NodeId} ({NodeType}), attempt {Attempt}/{MaxAttempts}",
                        context.NodeDefinition.Id,
                        NodeType,
                        attemptNumber,
                        retryPolicy.MaxAttempts);
                }

                var result = await ExecuteInternalAsync(context, cancellationToken);

                // Success!
                if (result.Success)
                {
                    if (attemptNumber > 0)
                    {
                        Logger.LogInformation(
                            "Node {NodeId} succeeded after {Attempts} retries",
                            context.NodeDefinition.Id,
                            attemptNumber);
                    }
                    return result;
                }

                // Failed - check if we should retry
                lastException = new InvalidOperationException(result.ErrorMessage ?? "Node execution failed");
                var errorCategory = ErrorCategorizer.CategorizeByMessage(result.ErrorMessage ?? "");

                if (!retryPolicy.ShouldRetry(errorCategory, attemptNumber))
                {
                    Logger.LogWarning(
                        "Node {NodeId} failed with non-retryable error category {Category}: {Error}",
                        context.NodeDefinition.Id,
                        errorCategory,
                        result.ErrorMessage);
                    return result;
                }

                attemptNumber++;

                // Calculate delay before retry
                if (attemptNumber <= retryPolicy.MaxAttempts)
                {
                    var delay = retryPolicy.GetDelay(attemptNumber);
                    Logger.LogInformation(
                        "Waiting {Delay}ms before retry attempt {Attempt}/{MaxAttempts}",
                        delay.TotalMilliseconds,
                        attemptNumber,
                        retryPolicy.MaxAttempts);

                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Don't retry cancellations
            }
            catch (Exception ex)
            {
                lastException = ex;
                var errorCategory = ErrorCategorizer.Categorize(ex);

                Logger.LogWarning(
                    ex,
                    "Node {NodeId} attempt {Attempt} failed with {Category} error",
                    context.NodeDefinition.Id,
                    attemptNumber,
                    errorCategory);

                // Check if we should retry this exception
                if (!retryPolicy.ShouldRetry(errorCategory, attemptNumber))
                {
                    return NodeExecutionResult.Fail(
                        ex.Message,
                        ex.StackTrace);
                }

                attemptNumber++;

                // Calculate delay before retry
                if (attemptNumber <= retryPolicy.MaxAttempts)
                {
                    var delay = retryPolicy.GetDelay(attemptNumber);
                    Logger.LogInformation(
                        "Waiting {Delay}ms before retry attempt {Attempt}/{MaxAttempts}",
                        delay.TotalMilliseconds,
                        attemptNumber,
                        retryPolicy.MaxAttempts);

                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        // All retries exhausted
        Logger.LogError(
            lastException,
            "Node {NodeId} failed after {Attempts} attempts",
            context.NodeDefinition.Id,
            attemptNumber);

        return NodeExecutionResult.Fail(
            lastException?.Message ?? "Unknown error after retries",
            lastException?.StackTrace);
    }

    /// <summary>
    /// Internal execution method to be implemented by derived classes
    /// </summary>
    protected abstract Task<NodeExecutionResult> ExecuteInternalAsync(
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

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Orchestrates rollback of failed processes by executing step rollbacks in reverse order.
/// </summary>
public class ProcessRollbackOrchestrator
{
    private readonly ILogger<ProcessRollbackOrchestrator> _logger;
    private readonly IProcessStateStore _stateStore;
    private readonly IServiceProvider _serviceProvider;

    public ProcessRollbackOrchestrator(
        ILogger<ProcessRollbackOrchestrator> logger,
        IProcessStateStore stateStore,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Rollback a process by undoing completed steps in reverse order.
    /// </summary>
    /// <param name="processId">The process to rollback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rollback result with details of each step</returns>
    public async Task<RollbackResult> RollbackProcessAsync(
        string processId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting rollback for process {ProcessId}", processId);

        // Get process info
        var processInfo = await _stateStore.GetProcessAsync(processId, cancellationToken);
        if (processInfo == null)
        {
            _logger.LogWarning("Process {ProcessId} not found", processId);
            return RollbackResult.NotFound(processId);
        }

        // Check if process is in a rollback-able state
        if (processInfo.Status != "Failed" && processInfo.Status != "Running")
        {
            _logger.LogWarning("Process {ProcessId} is not in a failed state (Status: {Status})",
                processId, processInfo.Status);
            return RollbackResult.InvalidState(processId, processInfo.Status);
        }

        // For now, we'll work with a simplified approach since we don't have
        // execution log in ProcessInfo. In a real implementation, you'd track
        // completed steps in the state store.

        var rollbackResults = new List<StepRollbackResult>();

        // Get rollback-capable steps for this workflow type
        var steps = GetRollbackStepsForWorkflow(processInfo.WorkflowType);

        _logger.LogInformation("Rolling back {Count} steps for process {ProcessId}",
            steps.Count, processId);

        // Execute rollbacks in reverse order
        foreach (var step in steps.AsEnumerable().Reverse())
        {
            if (!step.SupportsRollback)
            {
                _logger.LogWarning("Step {StepName} does not support rollback, skipping",
                    step.GetType().Name);
                rollbackResults.Add(StepRollbackResult.NotSupported(step.GetType().Name));
                continue;
            }

            try
            {
                _logger.LogInformation("Rolling back step: {StepName} - {Description}",
                    step.GetType().Name, step.RollbackDescription);

                // Get the process state (would be retrieved from state store in real implementation)
                object state = GetStateForWorkflow(processInfo.WorkflowType, processId);

                var stepResult = await step.RollbackAsync(state, cancellationToken);

                if (stepResult.IsSuccess)
                {
                    _logger.LogInformation("Successfully rolled back step: {StepName}",
                        step.GetType().Name);
                    rollbackResults.Add(StepRollbackResult.Success(
                        step.GetType().Name,
                        stepResult.Details));
                }
                else
                {
                    _logger.LogError("Failed to rollback step: {StepName} - {Error}",
                        step.GetType().Name, stepResult.Error);
                    rollbackResults.Add(StepRollbackResult.Failed(
                        step.GetType().Name,
                        stepResult.Error ?? "Unknown error",
                        stepResult.Details));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during rollback of step: {StepName}",
                    step.GetType().Name);
                rollbackResults.Add(StepRollbackResult.Failed(
                    step.GetType().Name,
                    ex.Message,
                    ex.StackTrace));
            }
        }

        // Update process state to rolled back
        await _stateStore.UpdateProcessStatusAsync(
            processId,
            "RolledBack",
            100,
            $"Process rolled back: {rollbackResults.Count(r => r.IsSuccess)}/{rollbackResults.Count} steps succeeded",
            cancellationToken);

        var result = new RollbackResult(
            ProcessId: processId,
            WorkflowType: processInfo.WorkflowType,
            TotalSteps: rollbackResults.Count,
            SuccessfulRollbacks: rollbackResults.Count(r => r.IsSuccess),
            FailedRollbacks: rollbackResults.Count(r => !r.IsSuccess),
            Steps: rollbackResults);

        _logger.LogInformation(
            "Rollback completed for process {ProcessId}: {Successful}/{Total} steps succeeded",
            processId, result.SuccessfulRollbacks, result.TotalSteps);

        return result;
    }

    private List<IProcessStepRollback> GetRollbackStepsForWorkflow(string workflowType)
    {
        // In a real implementation, this would get the actual steps that were executed
        // For now, we'll return empty list as a placeholder
        // The actual steps would be retrieved from DI container based on workflow type
        return new List<IProcessStepRollback>();
    }

    private object GetStateForWorkflow(string workflowType, string processId)
    {
        // In a real implementation, this would retrieve the actual state from state store
        // For now, return a placeholder
        return workflowType switch
        {
            "HonuaDeployment" => new State.DeploymentState { DeploymentId = processId },
            "HonuaUpgrade" => new State.UpgradeState { UpgradeId = processId },
            _ => new object()
        };
    }
}

/// <summary>
/// Result of a process rollback operation.
/// </summary>
public record RollbackResult(
    string ProcessId,
    string WorkflowType,
    int TotalSteps,
    int SuccessfulRollbacks,
    int FailedRollbacks,
    List<StepRollbackResult> Steps)
{
    /// <summary>
    /// Whether all rollbacks succeeded.
    /// </summary>
    public bool IsFullySuccessful => FailedRollbacks == 0;

    /// <summary>
    /// Whether at least some rollbacks succeeded.
    /// </summary>
    public bool IsPartiallySuccessful => SuccessfulRollbacks > 0 && FailedRollbacks > 0;

    /// <summary>
    /// Create a not found result.
    /// </summary>
    public static RollbackResult NotFound(string processId) =>
        new(
            ProcessId: processId,
            WorkflowType: "Unknown",
            TotalSteps: 0,
            SuccessfulRollbacks: 0,
            FailedRollbacks: 0,
            Steps: new List<StepRollbackResult>
            {
                StepRollbackResult.Failed("ProcessLookup", $"Process {processId} not found")
            });

    /// <summary>
    /// Create an invalid state result.
    /// </summary>
    public static RollbackResult InvalidState(string processId, string currentStatus) =>
        new(
            ProcessId: processId,
            WorkflowType: "Unknown",
            TotalSteps: 0,
            SuccessfulRollbacks: 0,
            FailedRollbacks: 0,
            Steps: new List<StepRollbackResult>
            {
                StepRollbackResult.Failed(
                    "StateValidation",
                    $"Process cannot be rolled back from state: {currentStatus}")
            });
}

/// <summary>
/// Result of rolling back a single step.
/// </summary>
public record StepRollbackResult(
    string StepName,
    bool IsSuccess,
    string? Error = null,
    string? Details = null)
{
    /// <summary>
    /// Create a success result.
    /// </summary>
    public static StepRollbackResult Success(string stepName, string? details = null) =>
        new(stepName, true, null, details);

    /// <summary>
    /// Create a failure result.
    /// </summary>
    public static StepRollbackResult Failed(string stepName, string error, string? details = null) =>
        new(stepName, false, error, details);

    /// <summary>
    /// Create a not supported result.
    /// </summary>
    public static StepRollbackResult NotSupported(string stepName) =>
        new(stepName, false, "Rollback not supported");
}

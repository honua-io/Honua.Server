// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Planning;

namespace Honua.Cli.AI.Services.Execution;

/// <summary>
/// Executes approved execution plans with rollback support.
/// </summary>
public interface IPlanExecutor
{
    /// <summary>
    /// Executes an approved plan step-by-step.
    /// </summary>
    Task<ExecutionResult> ExecuteAsync(
        ExecutionPlan plan,
        IExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a partially executed or failed plan.
    /// </summary>
    /// <param name="plan">The execution plan to roll back</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the rollback operation</returns>
    /// <remarks>
    /// This method requires an execution context. Use the overload that accepts IExecutionContext.
    /// This signature is maintained for backward compatibility but will throw InvalidOperationException.
    /// </remarks>
    [Obsolete("Use RollbackAsync(ExecutionPlan plan, IExecutionContext context, CancellationToken cancellationToken) instead")]
    Task<RollbackResult> RollbackAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a partially executed or failed plan with execution context.
    /// </summary>
    /// <param name="plan">The execution plan to roll back</param>
    /// <param name="context">Execution context providing credentials and workspace access</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the rollback operation</returns>
    Task<RollbackResult> RollbackAsync(
        ExecutionPlan plan,
        IExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs dry-run simulation without making actual changes.
    /// </summary>
    Task<SimulationResult> SimulateAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for plan execution with access to services and credentials.
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// Gets a scoped token for the specified credential requirement.
    /// </summary>
    Task<string> GetCredentialAsync(
        CredentialRequirement requirement,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs execution events for audit trail.
    /// </summary>
    void LogEvent(ExecutionEvent executionEvent);

    /// <summary>
    /// Gets the workspace path being operated on.
    /// </summary>
    string WorkspacePath { get; }

    /// <summary>
    /// Whether to continue execution if non-critical steps fail.
    /// </summary>
    bool ContinueOnError { get; }
}

/// <summary>
/// Result of plan execution.
/// </summary>
public sealed class ExecutionResult
{
    /// <summary>
    /// The plan that was executed.
    /// </summary>
    public required ExecutionPlan Plan { get; init; }

    /// <summary>
    /// Whether execution was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Results of individual step executions.
    /// </summary>
    public required List<StepResult> StepResults { get; init; }

    /// <summary>
    /// Overall error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// When execution started.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// When execution completed.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Total duration of execution.
    /// </summary>
    public TimeSpan Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : TimeSpan.Zero;

    /// <summary>
    /// Snapshot ID for rollback (if created).
    /// </summary>
    public string? SnapshotId { get; init; }
}

/// <summary>
/// Result of executing a single plan step.
/// </summary>
public sealed class StepResult
{
    /// <summary>
    /// The step that was executed.
    /// </summary>
    public required PlanStep Step { get; init; }

    /// <summary>
    /// Whether the step succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Output from the step execution.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Error message if step failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// When the step started.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// When the step completed.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Duration of step execution.
    /// </summary>
    public TimeSpan Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : TimeSpan.Zero;
}

/// <summary>
/// Result of rolling back a plan.
/// </summary>
public sealed class RollbackResult
{
    /// <summary>
    /// Whether rollback was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Steps that were rolled back.
    /// </summary>
    public required List<RollbackStepResult> StepResults { get; init; }

    /// <summary>
    /// Error message if rollback failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// When rollback started.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// When rollback completed.
    /// </summary>
    public DateTime? CompletedAt { get; init; }
}

public sealed class RollbackStepResult
{
    public required RollbackStep Step { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

/// <summary>
/// Result of simulating a plan without actual execution.
/// </summary>
public sealed class SimulationResult
{
    /// <summary>
    /// Whether the simulation succeeded (all steps are executable).
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Simulation results for each step.
    /// </summary>
    public required List<StepSimulation> StepSimulations { get; init; }

    /// <summary>
    /// Warnings discovered during simulation.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Errors that would prevent execution.
    /// </summary>
    public List<string> Errors { get; init; } = new();
}

public sealed class StepSimulation
{
    public required PlanStep Step { get; init; }
    public required bool CanExecute { get; init; }
    public string? Reason { get; init; }
    public List<string> Prerequisites { get; init; } = new();
}

/// <summary>
/// Audit event for plan execution.
/// </summary>
public sealed class ExecutionEvent
{
    public required DateTime Timestamp { get; init; }
    public required ExecutionEventType Type { get; init; }
    public required string Message { get; init; }
    public required string PlanId { get; init; }
    public int? StepNumber { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public enum ExecutionEventType
{
    PlanStarted,
    PlanCompleted,
    PlanFailed,
    StepStarted,
    StepCompleted,
    StepFailed,
    StepSkipped,
    SnapshotCreated,
    RollbackStarted,
    RollbackCompleted,
    CredentialRequested,
    CredentialGranted,
    ValidationWarning,
    ValidationError
}

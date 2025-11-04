// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Defines rollback capability for process steps.
/// Steps implementing this interface can undo their changes when a process fails.
/// </summary>
public interface IProcessStepRollback
{
    /// <summary>
    /// Rollback this step's changes.
    /// Called in reverse order when a process needs to be rolled back.
    /// </summary>
    /// <param name="state">The process state containing step execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure of rollback</returns>
    Task<ProcessStepRollbackResult> RollbackAsync(
        object state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this step supports rollback.
    /// Some steps may not be reversible (e.g., sending notifications).
    /// </summary>
    bool SupportsRollback { get; }

    /// <summary>
    /// Description of what the rollback will do.
    /// Used for logging and user confirmation.
    /// </summary>
    string RollbackDescription { get; }
}

/// <summary>
/// Result of a rollback operation for a single step.
/// </summary>
public record ProcessStepRollbackResult
{
    /// <summary>
    /// Whether the rollback succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if rollback failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Additional details about the rollback operation.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Create a success result.
    /// </summary>
    public static ProcessStepRollbackResult Success(string? details = null) =>
        new() { IsSuccess = true, Details = details };

    /// <summary>
    /// Create a failure result.
    /// </summary>
    public static ProcessStepRollbackResult Failure(string error, string? details = null) =>
        new() { IsSuccess = false, Error = error, Details = details };

    /// <summary>
    /// Create a not supported result.
    /// </summary>
    public static ProcessStepRollbackResult NotSupported() =>
        new() { IsSuccess = false, Error = "Rollback not supported for this step" };
}

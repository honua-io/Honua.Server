// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Interface for storing and retrieving process execution state.
/// Supports multiple storage backends (Redis, in-memory, database).
/// </summary>
public interface IProcessStateStore
{
    /// <summary>
    /// Gets a process by its unique identifier.
    /// </summary>
    /// <param name="processId">The unique process identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The process info if found, null otherwise</returns>
    Task<ProcessInfo?> GetProcessAsync(string processId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a process to the store (creates or updates).
    /// </summary>
    /// <param name="processInfo">The process information to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveProcessAsync(ProcessInfo processInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a running process.
    /// </summary>
    /// <param name="processId">The unique process identifier</param>
    /// <param name="status">The new status (Running, Completed, Failed, etc.)</param>
    /// <param name="completionPercentage">Optional completion percentage (0-100)</param>
    /// <param name="errorMessage">Optional error message if status is Failed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateProcessStatusAsync(
        string processId,
        string status,
        int? completionPercentage = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active (running or pending) processes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active processes</returns>
    Task<IReadOnlyList<ProcessInfo>> GetActiveProcessesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a process from the store.
    /// </summary>
    /// <param name="processId">The unique process identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the process was deleted, false if not found</returns>
    Task<bool> DeleteProcessAsync(string processId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a process as cancelled.
    /// </summary>
    /// <param name="processId">The unique process identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the process was cancelled, false if not found</returns>
    Task<bool> CancelProcessAsync(string processId, CancellationToken cancellationToken = default);
}

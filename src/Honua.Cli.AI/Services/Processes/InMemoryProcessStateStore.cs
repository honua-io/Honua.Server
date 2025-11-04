// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// In-memory implementation of IProcessStateStore.
/// Used as a fallback when Redis is not configured or available.
/// Note: This is not suitable for multi-instance deployments as state is not shared.
/// </summary>
public class InMemoryProcessStateStore : InMemoryStoreBase<ProcessInfo>, IProcessStateStore
{
    private readonly ILogger<InMemoryProcessStateStore> _logger;

    public InMemoryProcessStateStore(ILogger<InMemoryProcessStateStore> logger)
    {
        Guard.NotNull(logger);
        _logger = logger;

        _logger.LogWarning(
            "Using in-memory process state store. " +
            "This is not suitable for production or multi-instance deployments. " +
            "Configure Redis for persistent process state storage.");
    }

    /// <summary>
    /// Extracts the ProcessId from a ProcessInfo entity.
    /// </summary>
    protected override string GetKey(ProcessInfo entity) => entity.ProcessId;

    /// <inheritdoc/>
    public async Task<ProcessInfo?> GetProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        if (processId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Process ID cannot be null or empty", nameof(processId));
        }

        var processInfo = await GetAsync(processId, cancellationToken);

        if (processInfo != null)
        {
            _logger.LogDebug("Retrieved process {ProcessId} from in-memory store", processId);
        }
        else
        {
            _logger.LogDebug("Process {ProcessId} not found in in-memory store", processId);
        }

        return processInfo;
    }

    /// <inheritdoc/>
    public async Task SaveProcessAsync(ProcessInfo processInfo, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(processInfo);

        if (processInfo.ProcessId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("ProcessInfo.ProcessId cannot be null or empty", nameof(processInfo));
        }

        await PutAsync(processInfo, cancellationToken);
        _logger.LogDebug("Saved process {ProcessId} to in-memory store", processInfo.ProcessId);
    }

    /// <inheritdoc/>
    public async Task UpdateProcessStatusAsync(
        string processId,
        string status,
        int? completionPercentage = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (processId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Process ID cannot be null or empty", nameof(processId));
        }

        if (status.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
        }

        var updated = await UpdateAsync(
            processId,
            processInfo =>
            {
                processInfo.Status = status;
                if (completionPercentage.HasValue)
                {
                    processInfo.CompletionPercentage = completionPercentage.Value;
                }
                if (errorMessage != null)
                {
                    processInfo.ErrorMessage = errorMessage;
                }

                // Set end time if completed or failed
                if (status == "Completed" || status == "Failed")
                {
                    processInfo.EndTime = DateTime.UtcNow;
                }

                return processInfo;
            },
            cancellationToken);

        if (updated == null)
        {
            _logger.LogWarning("Cannot update status for non-existent process {ProcessId}", processId);
            throw new InvalidOperationException($"Process {processId} not found");
        }

        _logger.LogDebug("Updated process {ProcessId} status to {Status}", processId, status);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProcessInfo>> GetActiveProcessesAsync(CancellationToken cancellationToken = default)
    {
        var activeProcesses = await QueryAsync(
            p => p.Status == "Running" || p.Status == "Pending",
            cancellationToken);

        _logger.LogDebug("Retrieved {Count} active processes from in-memory store", activeProcesses.Count);
        return activeProcesses;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        if (processId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Process ID cannot be null or empty", nameof(processId));
        }

        var removed = await DeleteAsync(processId, cancellationToken);

        if (removed)
        {
            _logger.LogDebug("Deleted process {ProcessId} from in-memory store", processId);
        }
        else
        {
            _logger.LogDebug("Process {ProcessId} not found for deletion", processId);
        }

        return removed;
    }

    /// <inheritdoc/>
    public async Task<bool> CancelProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        if (processId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Process ID cannot be null or empty", nameof(processId));
        }

        // Get existing process
        var processInfo = await GetProcessAsync(processId, cancellationToken);
        if (processInfo == null)
        {
            _logger.LogWarning("Cannot cancel non-existent process {ProcessId}", processId);
            return false;
        }

        // Only cancel if process is still running or pending
        if (processInfo.Status != "Running" && processInfo.Status != "Pending")
        {
            _logger.LogWarning("Cannot cancel process {ProcessId} with status {Status}", processId, processInfo.Status);
            return false;
        }

        // Update status to Cancelled
        await UpdateProcessStatusAsync(processId, "Cancelled", cancellationToken: cancellationToken);

        _logger.LogInformation("Process {ProcessId} marked as cancelled", processId);
        return true;
    }
}

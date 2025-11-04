// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Resilient wrapper for database operations with error boundaries.
/// Provides read-only mode fallback when writes fail.
/// </summary>
public sealed class ResilientDatabaseService
{
    private readonly ILogger<ResilientDatabaseService> _logger;
    private readonly ResilientServiceExecutor _executor;
    private bool _readOnlyMode;
    private DateTime? _readOnlyModeUntil;

    public ResilientDatabaseService(ILogger<ResilientDatabaseService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executor = new ResilientServiceExecutor(logger);
    }

    /// <summary>
    /// Gets whether the database is currently in read-only mode.
    /// </summary>
    public bool IsReadOnlyMode
    {
        get
        {
            // Check if read-only mode has expired
            if (_readOnlyMode && _readOnlyModeUntil.HasValue && DateTime.UtcNow > _readOnlyModeUntil.Value)
            {
                _logger.LogInformation("Read-only mode has expired. Re-enabling writes.");
                _readOnlyMode = false;
                _readOnlyModeUntil = null;
            }

            return _readOnlyMode;
        }
    }

    /// <summary>
    /// Executes a read operation with fallback to default value on failure.
    /// </summary>
    public async Task<FallbackResult<T>> ExecuteReadAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        T defaultValue,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        return await _executor.ExecuteWithDefaultAsync(
            operation,
            defaultValue,
            operationName,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a write operation with automatic read-only mode fallback on failure.
    /// </summary>
    public async Task<bool> ExecuteWriteAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        // Check if in read-only mode
        if (IsReadOnlyMode)
        {
            _logger.LogWarning("Database is in read-only mode. Write operation '{Operation}' will be skipped.", operationName);
            return false;
        }

        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbException ex) when (IsPermanentWriteError(ex))
        {
            _logger.LogError(ex, "Permanent database write error in '{Operation}'. Entering read-only mode.", operationName);
            EnableReadOnlyMode(TimeSpan.FromMinutes(5));
            return false;
        }
        catch (DbException ex)
        {
            _logger.LogWarning(ex, "Transient database write error in '{Operation}'.", operationName);
            throw;
        }
    }

    /// <summary>
    /// Executes a write operation with fallback value.
    /// Returns true if write succeeded, false if it failed but didn't throw.
    /// </summary>
    public async Task<FallbackResult<bool>> ExecuteWriteWithFallbackAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        if (IsReadOnlyMode)
        {
            _logger.LogWarning("Database is in read-only mode. Write operation '{Operation}' will return false.", operationName);
            return FallbackResult<bool>.Fallback(false, FallbackReason.ServiceUnavailable);
        }

        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
            return FallbackResult<bool>.Success(true);
        }
        catch (DbException ex) when (IsPermanentWriteError(ex))
        {
            _logger.LogError(ex, "Permanent database write error in '{Operation}'. Entering read-only mode.", operationName);
            EnableReadOnlyMode(TimeSpan.FromMinutes(5));
            return FallbackResult<bool>.Fallback(false, FallbackReason.ServiceUnavailable, ex);
        }
        catch (DbException ex)
        {
            _logger.LogWarning(ex, "Transient database write error in '{Operation}'.", operationName);
            return FallbackResult<bool>.Failed(ex, false);
        }
    }

    /// <summary>
    /// Manually enables read-only mode for a specified duration.
    /// </summary>
    public void EnableReadOnlyMode(TimeSpan duration)
    {
        _readOnlyMode = true;
        _readOnlyModeUntil = DateTime.UtcNow.Add(duration);
        _logger.LogWarning("Read-only mode enabled until {Until}. Writes will be rejected.", _readOnlyModeUntil);
    }

    /// <summary>
    /// Manually disables read-only mode.
    /// </summary>
    public void DisableReadOnlyMode()
    {
        if (_readOnlyMode)
        {
            _readOnlyMode = false;
            _readOnlyModeUntil = null;
            _logger.LogInformation("Read-only mode manually disabled. Writes re-enabled.");
        }
    }

    private static bool IsPermanentWriteError(DbException ex)
    {
        var message = ex.Message.ToLowerInvariant();

        // Disk full, permission denied, database locked permanently
        return message.Contains("disk full") ||
               message.Contains("permission denied") ||
               message.Contains("access denied") ||
               message.Contains("read-only") ||
               message.Contains("database is locked") && !message.Contains("timeout");
    }
}

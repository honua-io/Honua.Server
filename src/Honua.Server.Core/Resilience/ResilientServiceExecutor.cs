// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Executes operations with fallback support for resilience.
/// Provides multiple fallback strategies for graceful degradation.
/// </summary>
public sealed class ResilientServiceExecutor
{
    private readonly ILogger _logger;

    public ResilientServiceExecutor(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes an operation with a fallback function if the primary fails.
    /// </summary>
    public async Task<FallbackResult<T>> ExecuteWithFallbackAsync<T>(
        Func<CancellationToken, Task<T>> primary,
        Func<Exception, CancellationToken, Task<T>> fallback,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        try
        {
            var result = await primary(cancellationToken).ConfigureAwait(false);
            return FallbackResult<T>.Success(result);
        }
        catch (Exception ex) when (ex is ITransientException transientEx && transientEx.IsTransient)
        {
            _logger.LogWarning(ex, "Transient error in {Operation}. Attempting fallback.", operationName);

            try
            {
                var fallbackResult = await fallback(ex, cancellationToken).ConfigureAwait(false);
                var reason = DetermineFallbackReason(ex);
                return FallbackResult<T>.Fallback(fallbackResult, reason, ex);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback also failed for {Operation}.", operationName);
                throw;
            }
        }
    }

    /// <summary>
    /// Executes an operation with a default value fallback if the primary fails.
    /// </summary>
    public async Task<FallbackResult<T>> ExecuteWithDefaultAsync<T>(
        Func<CancellationToken, Task<T>> primary,
        T defaultValue,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        try
        {
            var result = await primary(cancellationToken).ConfigureAwait(false);
            return FallbackResult<T>.Success(result);
        }
        catch (Exception ex) when (ex is ITransientException transientEx && transientEx.IsTransient)
        {
            _logger.LogWarning(ex, "Transient error in {Operation}. Returning default value.", operationName);
            var reason = DetermineFallbackReason(ex);
            return FallbackResult<T>.Fallback(defaultValue, reason, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Permanent error in {Operation}. Returning default value.", operationName);
            return FallbackResult<T>.Failed(ex, defaultValue);
        }
    }

    /// <summary>
    /// Executes an operation with multiple fallback strategies in order.
    /// Tries each fallback until one succeeds or all fail.
    /// </summary>
    public async Task<FallbackResult<T>> ExecuteWithMultipleFallbacksAsync<T>(
        Func<CancellationToken, Task<T>> primary,
        Func<Exception, CancellationToken, Task<T>>[] fallbacks,
        T defaultValue,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await primary(cancellationToken).ConfigureAwait(false);
            return FallbackResult<T>.Success(result);
        }
        catch (Exception primaryEx) when (primaryEx is ITransientException transientEx && transientEx.IsTransient)
        {
            _logger.LogWarning(primaryEx, "Transient error in {Operation}. Trying {FallbackCount} fallback(s).",
                operationName, fallbacks.Length);

            for (int i = 0; i < fallbacks.Length; i++)
            {
                try
                {
                    _logger.LogDebug("Attempting fallback {Index} for {Operation}", i + 1, operationName);
                    var fallbackResult = await fallbacks[i](primaryEx, cancellationToken).ConfigureAwait(false);
                    var reason = DetermineFallbackReason(primaryEx);
                    _logger.LogInformation("Fallback {Index} succeeded for {Operation}", i + 1, operationName);
                    return FallbackResult<T>.Fallback(fallbackResult, reason, primaryEx);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogWarning(fallbackEx, "Fallback {Index} failed for {Operation}", i + 1, operationName);
                    // Continue to next fallback
                }
            }

            // All fallbacks failed, return default
            _logger.LogError("All fallbacks failed for {Operation}. Returning default value.", operationName);
            return FallbackResult<T>.Failed(primaryEx, defaultValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Permanent error in {Operation}. Returning default value.", operationName);
            return FallbackResult<T>.Failed(ex, defaultValue);
        }
    }

    /// <summary>
    /// Executes an operation with stale cache fallback.
    /// If primary fails, returns stale cached data if available.
    /// </summary>
    public async Task<FallbackResult<T>> ExecuteWithStaleCacheFallbackAsync<T>(
        Func<CancellationToken, Task<T>> primary,
        Func<CancellationToken, Task<T?>> getStaleCache,
        T defaultValue,
        string operationName,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var result = await primary(cancellationToken).ConfigureAwait(false);
            return FallbackResult<T>.Success(result);
        }
        catch (Exception ex) when (ex is ITransientException transientEx && transientEx.IsTransient)
        {
            _logger.LogWarning(ex, "Transient error in {Operation}. Attempting to use stale cache.", operationName);

            try
            {
                var staleData = await getStaleCache(cancellationToken).ConfigureAwait(false);
                if (staleData != null)
                {
                    _logger.LogInformation("Using stale cache data for {Operation}", operationName);
                    return FallbackResult<T>.Fallback(staleData, FallbackReason.StaleCache, ex);
                }

                _logger.LogWarning("No stale cache available for {Operation}. Returning default value.", operationName);
                return FallbackResult<T>.Failed(ex, defaultValue);
            }
            catch (Exception cacheEx)
            {
                _logger.LogError(cacheEx, "Failed to retrieve stale cache for {Operation}. Returning default value.", operationName);
                return FallbackResult<T>.Failed(ex, defaultValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Permanent error in {Operation}. Returning default value.", operationName);
            return FallbackResult<T>.Failed(ex, defaultValue);
        }
    }

    private static FallbackReason DetermineFallbackReason(Exception exception)
    {
        return exception switch
        {
            ServiceUnavailableException => FallbackReason.ServiceUnavailable,
            ServiceTimeoutException => FallbackReason.Timeout,
            CircuitBreakerOpenException => FallbackReason.CircuitBreakerOpen,
            ServiceThrottledException => FallbackReason.Throttled,
            _ => FallbackReason.TransientError
        };
    }
}

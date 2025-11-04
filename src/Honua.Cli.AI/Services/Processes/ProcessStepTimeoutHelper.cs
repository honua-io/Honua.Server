// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Provides timeout enforcement for process steps.
/// Wraps step execution with a timeout mechanism to prevent indefinite hangs.
/// </summary>
public static class ProcessStepTimeoutHelper
{
    /// <summary>
    /// Executes an async operation with a timeout.
    /// If the operation does not complete within the specified timeout, it will be cancelled.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="timeout">The maximum time to wait for the operation to complete</param>
    /// <param name="logger">Logger for timeout events</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="cancellationToken">External cancellation token (user cancellation)</param>
    /// <returns>The result of the operation</returns>
    /// <exception cref="TimeoutException">Thrown when the operation times out</exception>
    public static async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            logger.LogDebug("Executing {OperationName} with timeout of {Timeout}",
                operationName, timeout);

            var result = await operation(cts.Token);

            logger.LogDebug("{OperationName} completed successfully", operationName);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not user cancellation)
            logger.LogError("Process step {OperationName} timed out after {Timeout}",
                operationName, timeout);

            throw new TimeoutException(
                $"Step '{operationName}' timed out after {timeout.TotalSeconds:F0} seconds");
        }
        catch (OperationCanceledException)
        {
            // User cancellation
            logger.LogWarning("{OperationName} was cancelled by user", operationName);
            throw;
        }
    }

    /// <summary>
    /// Executes an async operation with a timeout (void return).
    /// If the operation does not complete within the specified timeout, it will be cancelled.
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="timeout">The maximum time to wait for the operation to complete</param>
    /// <param name="logger">Logger for timeout events</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="cancellationToken">External cancellation token (user cancellation)</param>
    /// <exception cref="TimeoutException">Thrown when the operation times out</exception>
    public static async Task ExecuteWithTimeoutAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            logger.LogDebug("Executing {OperationName} with timeout of {Timeout}",
                operationName, timeout);

            await operation(cts.Token);

            logger.LogDebug("{OperationName} completed successfully", operationName);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not user cancellation)
            logger.LogError("Process step {OperationName} timed out after {Timeout}",
                operationName, timeout);

            throw new TimeoutException(
                $"Step '{operationName}' timed out after {timeout.TotalSeconds:F0} seconds");
        }
        catch (OperationCanceledException)
        {
            // User cancellation
            logger.LogWarning("{OperationName} was cancelled by user", operationName);
            throw;
        }
    }

    /// <summary>
    /// Gets the timeout for a step, using the step's DefaultTimeout if it implements IProcessStepTimeout,
    /// otherwise falling back to the provided default timeout.
    /// </summary>
    /// <param name="step">The process step (may implement IProcessStepTimeout)</param>
    /// <param name="defaultTimeout">Fallback timeout if step doesn't specify one</param>
    /// <returns>The timeout to use for this step</returns>
    public static TimeSpan GetStepTimeout(object step, TimeSpan defaultTimeout)
    {
        if (step is IProcessStepTimeout timeoutStep)
        {
            return timeoutStep.DefaultTimeout;
        }

        return defaultTimeout;
    }
}

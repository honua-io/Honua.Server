// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Microsoft.Extensions.Logging;
using Polly;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Executes database operations with comprehensive resilience patterns:
/// - Retry policy for transient failures (exponential backoff with jitter)
/// - Bulkhead policy for connection pool protection
/// - Metrics and logging for observability
/// </summary>
public sealed class ResilientDatabaseOperationExecutor
{
    private readonly ResiliencePipeline _retryPipeline;
    private readonly BulkheadPolicyProvider _bulkheadProvider;
    private readonly ILogger<ResilientDatabaseOperationExecutor> _logger;

    public ResilientDatabaseOperationExecutor(
        ResiliencePipeline retryPipeline,
        BulkheadPolicyProvider bulkheadProvider,
        ILogger<ResilientDatabaseOperationExecutor> logger)
    {
        _retryPipeline = retryPipeline ?? throw new ArgumentNullException(nameof(retryPipeline));
        _bulkheadProvider = bulkheadProvider ?? throw new ArgumentNullException(nameof(bulkheadProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes a database operation with retry and bulkhead protection.
    /// First applies retry policy for transient failures, then bulkhead to limit concurrency.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The database operation to execute.</param>
    /// <param name="operationName">Name of the operation for logging/metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        // Combine retry + bulkhead: Retry wraps the bulkhead-protected operation
        return await _retryPipeline.ExecuteAsync(
            async ct =>
            {
                return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
                {
                    _logger.LogTrace("Executing database operation '{OperationName}'", operationName);
                    return await operation(ct);
                });
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a void database operation with retry and bulkhead protection.
    /// </summary>
    /// <param name="operation">The database operation to execute.</param>
    /// <param name="operationName">Name of the operation for logging/metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        await _retryPipeline.ExecuteAsync(
            async ct =>
            {
                await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
                {
                    _logger.LogTrace("Executing database operation '{OperationName}'", operationName);
                    await operation(ct);
                });
            },
            cancellationToken).ConfigureAwait(false);
    }
}

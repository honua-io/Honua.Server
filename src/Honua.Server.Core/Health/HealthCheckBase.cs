// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Health;

/// <summary>
/// Abstract base class for health checks that provides standard error handling,
/// timeout enforcement, and consistent result formatting.
/// </summary>
/// <remarks>
/// Eliminates boilerplate code in health check implementations by providing:
/// <list type="bullet">
/// <item><description>Automatic timeout enforcement with configurable duration</description></item>
/// <item><description>Consistent exception handling and logging</description></item>
/// <item><description>Data dictionary management</description></item>
/// <item><description>Standardized HealthCheckResult creation</description></item>
/// </list>
/// Derived classes only need to implement <see cref="ExecuteHealthCheckAsync"/>.
/// </remarks>
public abstract class HealthCheckBase : IHealthCheck
{
    /// <summary>
    /// Logger for health check operations.
    /// </summary>
    protected readonly ILogger Logger;

    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckBase"/> class.
    /// </summary>
    /// <param name="logger">Logger for health check operations.</param>
    /// <param name="timeout">Optional timeout duration. Defaults to 30 seconds if not specified.</param>
    protected HealthCheckBase(ILogger logger, TimeSpan? timeout = null)
    {
        Logger = Guard.NotNull(logger);
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Executes the health check with automatic timeout enforcement and exception handling.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A health check result.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            var result = await ExecuteHealthCheckAsync(data, cts.Token);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (cts.CancelAfter triggered), not an external cancellation
            Logger.LogWarning("Health check timed out after {Timeout}", _timeout);
            return HealthCheckResult.Degraded(
                $"Health check timed out after {_timeout}",
                data: data);
        }
        catch (OperationCanceledException)
        {
            // External cancellation requested
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Health check failed");
            return CreateUnhealthyResult(ex, data);
        }
    }

    /// <summary>
    /// Executes the health check logic.
    /// </summary>
    /// <param name="data">Dictionary to populate with health check data. Shared with exception handlers.</param>
    /// <param name="cancellationToken">Cancellation token that includes timeout enforcement.</param>
    /// <returns>A health check result.</returns>
    /// <remarks>
    /// Implementations should:
    /// <list type="bullet">
    /// <item><description>Populate the data dictionary with relevant information</description></item>
    /// <item><description>Return appropriate HealthCheckResult (Healthy/Degraded/Unhealthy)</description></item>
    /// <item><description>Let exceptions propagate to the base class for standardized handling</description></item>
    /// </list>
    /// </remarks>
    protected abstract Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates an unhealthy result from an exception.
    /// </summary>
    /// <param name="ex">The exception that caused the health check to fail.</param>
    /// <param name="data">Additional data to include in the result.</param>
    /// <returns>An unhealthy health check result.</returns>
    /// <remarks>
    /// Override this method to provide custom unhealthy result creation logic,
    /// such as mapping specific exceptions to degraded states instead of unhealthy.
    /// </remarks>
    protected virtual HealthCheckResult CreateUnhealthyResult(
        Exception ex,
        Dictionary<string, object> data)
    {
        return HealthCheckResult.Unhealthy(ex.Message, ex, data);
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Limits concurrent operations per tenant to prevent noisy neighbor problems.
/// Each tenant gets their own semaphore to control resource usage.
/// </summary>
public class TenantResourceLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _tenantSemaphores;
    private readonly BulkheadOptions _options;
    private readonly ILogger<TenantResourceLimiter> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantResourceLimiter"/> class.
    /// </summary>
    /// <param name="options">Bulkhead configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public TenantResourceLimiter(
        IOptions<BulkheadOptions> options,
        ILogger<TenantResourceLimiter> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
        _tenantSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Tenant resource limiter initialized. Max parallelization per tenant: {Max}",
            _options.PerTenantMaxParallelization);
    }

    /// <summary>
    /// Executes an operation with per-tenant resource limiting.
    /// Fails fast if the tenant has reached their concurrent operation limit.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="TenantResourceLimitExceededException">
    /// Thrown when the tenant has reached their concurrent operation limit.
    /// </exception>
    public async Task<T> ExecuteAsync<T>(
        string tenantId,
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(operation);

        if (!_options.PerTenantEnabled)
        {
            // Per-tenant limiting is disabled, execute directly
            return await operation();
        }

        var semaphore = GetOrCreateSemaphore(tenantId);

        // Fail fast - don't wait, reject immediately if limit exceeded
        if (!await semaphore.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogWarning(
                "Tenant {TenantId} exceeded resource limit. Max concurrent operations: {Max}",
                tenantId,
                _options.PerTenantMaxParallelization);

            throw new TenantResourceLimitExceededException(tenantId, _options.PerTenantMaxParallelization);
        }

        try
        {
            _logger.LogDebug(
                "Executing operation for tenant {TenantId}. Available slots: {Available}/{Max}",
                tenantId,
                semaphore.CurrentCount,
                _options.PerTenantMaxParallelization);

            return await operation();
        }
        finally
        {
            semaphore.Release();

            _logger.LogDebug(
                "Operation completed for tenant {TenantId}. Available slots: {Available}/{Max}",
                tenantId,
                semaphore.CurrentCount,
                _options.PerTenantMaxParallelization);
        }
    }

    /// <summary>
    /// Executes a void operation with per-tenant resource limiting.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TenantResourceLimitExceededException">
    /// Thrown when the tenant has reached their concurrent operation limit.
    /// </exception>
    public async Task ExecuteAsync(
        string tenantId,
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(tenantId, async () =>
        {
            await operation();
            return 0; // Dummy return value
        }, cancellationToken);
    }

    /// <summary>
    /// Gets or creates a semaphore for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <returns>The semaphore for the tenant.</returns>
    private SemaphoreSlim GetOrCreateSemaphore(string tenantId)
    {
        return _tenantSemaphores.GetOrAdd(
            tenantId,
            _ => new SemaphoreSlim(
                _options.PerTenantMaxParallelization,
                _options.PerTenantMaxParallelization));
    }

    /// <summary>
    /// Gets the current number of available slots for a tenant.
    /// Useful for monitoring and diagnostics.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <returns>The number of available slots, or null if the tenant has no semaphore yet.</returns>
    public int? GetAvailableSlots(string tenantId)
    {
        if (_tenantSemaphores.TryGetValue(tenantId, out var semaphore))
        {
            return semaphore.CurrentCount;
        }

        return null;
    }

    /// <summary>
    /// Clears semaphores for tenants that are no longer active.
    /// This is a manual cleanup method - typically called periodically by a background service.
    /// </summary>
    /// <param name="activeTenantIds">The set of currently active tenant IDs.</param>
    /// <returns>The number of semaphores removed.</returns>
    public int CleanupInactiveTenants(ISet<string> activeTenantIds)
    {
        ArgumentNullException.ThrowIfNull(activeTenantIds);

        var removed = 0;

        foreach (var tenantId in _tenantSemaphores.Keys)
        {
            if (!activeTenantIds.Contains(tenantId))
            {
                if (_tenantSemaphores.TryRemove(tenantId, out var semaphore))
                {
                    semaphore.Dispose();
                    removed++;
                }
            }
        }

        if (removed > 0)
        {
            _logger.LogInformation(
                "Cleaned up {Count} inactive tenant semaphores",
                removed);
        }

        return removed;
    }

    /// <summary>
    /// Disposes all semaphores.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var semaphore in _tenantSemaphores.Values)
        {
            semaphore?.Dispose();
        }

        _tenantSemaphores.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}

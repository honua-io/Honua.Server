// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Locking;

/// <summary>
/// In-memory distributed lock implementation for single-instance deployments or development.
/// Uses local semaphores instead of distributed coordination.
/// </summary>
/// <remarks>
/// This implementation provides the same interface as RedisDistributedLock but only coordinates
/// within a single process. It is suitable for:
/// - Single-instance deployments
/// - Development and testing environments
/// - Scenarios where Redis is not available
///
/// WARNING: This does NOT provide distributed coordination. Multiple application instances
/// will each have their own lock registry and can execute the same critical section concurrently.
/// </remarks>
public sealed class InMemoryDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ILogger<InMemoryDistributedLock> _logger;

    public InMemoryDistributedLock(ILogger<InMemoryDistributedLock> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan timeout,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (timeout <= TimeSpan.Zero)
            throw new ArgumentException("Timeout must be positive", nameof(timeout));

        if (expiry <= TimeSpan.Zero)
            throw new ArgumentException("Expiry must be positive", nameof(expiry));

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        _logger.LogDebug(
            "Attempting to acquire in-memory lock: Key={Key}, Timeout={Timeout}",
            key, timeout);

        var acquired = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);

        if (!acquired)
        {
            _logger.LogWarning(
                "Failed to acquire in-memory lock within timeout: Key={Key}, Timeout={Timeout}",
                key, timeout);
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var handle = new InMemoryDistributedLockHandle(
            key,
            semaphore,
            now,
            now + expiry,
            _logger);

        _logger.LogDebug(
            "In-memory lock acquired: Key={Key}, ExpiresAt={ExpiresAt}",
            key, handle.ExpiresAt);

        return handle;
    }
}

/// <summary>
/// Handle for an in-memory distributed lock.
/// Releases the semaphore when disposed.
/// </summary>
internal sealed class InMemoryDistributedLockHandle : IDistributedLockHandle
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger _logger;
    private int _disposed;

    public InMemoryDistributedLockHandle(
        string key,
        SemaphoreSlim semaphore,
        DateTimeOffset acquiredAt,
        DateTimeOffset expiresAt,
        ILogger logger)
    {
        Key = key;
        _semaphore = semaphore;
        AcquiredAt = acquiredAt;
        ExpiresAt = expiresAt;
        _logger = logger;
    }

    public string Key { get; }
    public DateTimeOffset AcquiredAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        try
        {
            _semaphore.Release();
            _logger.LogDebug("In-memory lock released: Key={Key}", Key);
        }
        catch (SemaphoreFullException ex)
        {
            _logger.LogError(ex,
                "Attempted to release an in-memory lock that was not held: Key={Key}",
                Key);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

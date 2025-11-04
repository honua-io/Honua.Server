// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Locking;

/// <summary>
/// Provides distributed locking capabilities for coordinating operations across multiple application instances.
/// </summary>
/// <remarks>
/// Implementations may use Redis (SET NX EX), database advisory locks, or other distributed coordination mechanisms.
/// For single-instance deployments, an in-memory implementation provides the same interface without external dependencies.
/// </remarks>
public interface IDistributedLock
{
    /// <summary>
    /// Attempts to acquire a distributed lock with the specified key.
    /// </summary>
    /// <param name="key">Unique identifier for the lock (e.g., "kerchunk:lock:s3://bucket/file.zarr")</param>
    /// <param name="timeout">Maximum time to wait for the lock to be acquired</param>
    /// <param name="expiry">How long the lock should be held before automatic expiration (prevents deadlocks)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// A disposable lock handle if successful, or null if the lock could not be acquired within the timeout.
    /// The lock is automatically released when the handle is disposed.
    /// </returns>
    Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan timeout,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an acquired distributed lock that will be released when disposed.
/// </summary>
public interface IDistributedLockHandle : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The lock key that was acquired.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// When the lock was acquired.
    /// </summary>
    DateTimeOffset AcquiredAt { get; }

    /// <summary>
    /// When the lock will automatically expire if not released.
    /// </summary>
    DateTimeOffset ExpiresAt { get; }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Coordination;

/// <summary>
/// Provides distributed leader election for high availability deployments.
/// Ensures only one instance in a cluster performs critical operations at a time.
/// </summary>
/// <remarks>
/// Leader election is critical for preventing duplicate processing in multi-instance deployments.
/// The leader is responsible for executing singleton background tasks such as:
/// - Processing queues (build queue, notification queue)
/// - Running scheduled jobs
/// - Performing cleanup operations
/// - Managing distributed workflows
///
/// Implementation uses Redis SET NX with expiry for atomic leader acquisition.
/// Automatic renewal prevents leadership loss due to slow operations.
/// Graceful release ensures clean failover to other instances.
/// </remarks>
public interface ILeaderElection
{
    /// <summary>
    /// Attempts to acquire leadership for the specified resource.
    /// </summary>
    /// <param name="resourceName">The name of the resource to acquire leadership for (e.g., "build-queue-processor").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if leadership was acquired, false if another instance is currently leader.</returns>
    /// <remarks>
    /// This is a non-blocking operation that returns immediately.
    /// Uses Redis SET NX EX for atomic acquisition with automatic expiry.
    /// Leadership is granted with a TTL configured in LeaderElectionOptions.LeaseDuration.
    /// </remarks>
    Task<bool> TryAcquireLeadershipAsync(string resourceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews the leadership lease for the specified resource.
    /// Must be called periodically to maintain leadership before lease expires.
    /// </summary>
    /// <param name="resourceName">The name of the resource to renew leadership for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if renewal succeeded, false if this instance is no longer the leader.</returns>
    /// <remarks>
    /// Uses Lua script to atomically verify ownership and extend TTL.
    /// Should be called at an interval less than LeaseDuration (typically LeaseDuration / 3).
    /// If renewal fails, the instance has lost leadership and should stop processing.
    /// </remarks>
    Task<bool> RenewLeadershipAsync(string resourceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases leadership for the specified resource.
    /// Should be called during graceful shutdown to allow immediate failover.
    /// </summary>
    /// <param name="resourceName">The name of the resource to release leadership for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if leadership was released, false if this instance was not the leader.</returns>
    /// <remarks>
    /// Uses Lua script to atomically verify ownership before deletion.
    /// Prevents accidentally releasing leadership acquired by another instance after expiry.
    /// Best practice: Call this in Dispose/DisposeAsync of background services.
    /// </remarks>
    Task<bool> ReleaseLeadershipAsync(string resourceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this instance is currently the leader for the specified resource.
    /// </summary>
    /// <param name="resourceName">The name of the resource to check leadership for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if this instance is the current leader, false otherwise.</returns>
    /// <remarks>
    /// Verifies both that the lock exists and that it belongs to this instance.
    /// Use this before processing to ensure leadership is still held.
    /// Consider caching the result briefly to avoid excessive Redis queries.
    /// </remarks>
    Task<bool> IsLeaderAsync(string resourceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the unique identifier for this instance.
    /// </summary>
    /// <remarks>
    /// Format: {MachineName}_{ProcessId}_{InstanceGuid}
    /// Used to identify which instance holds leadership in distributed systems.
    /// Useful for debugging and observability.
    /// </remarks>
    string InstanceId { get; }
}

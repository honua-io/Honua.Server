// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Sensors.Models;

namespace Honua.Server.Host.SensorThings;

/// <summary>
/// Service for broadcasting sensor observations to subscribed clients via SignalR.
/// </summary>
public interface ISensorObservationBroadcaster
{
    /// <summary>
    /// Broadcast a single observation to subscribed clients.
    /// </summary>
    /// <param name="observation">The observation to broadcast</param>
    /// <param name="datastream">The associated datastream (for context)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BroadcastObservationAsync(
        Observation observation,
        Datastream datastream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast multiple observations to subscribed clients in batch.
    /// Used for DataArray and batch observation creation.
    /// </summary>
    /// <param name="observations">List of observations with their datastreams</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BroadcastObservationsAsync(
        IReadOnlyList<(Observation Observation, Datastream Datastream)> observations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current configuration for broadcasting.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the rate limit (max observations per second per client group).
    /// </summary>
    int RateLimitPerSecond { get; }
}

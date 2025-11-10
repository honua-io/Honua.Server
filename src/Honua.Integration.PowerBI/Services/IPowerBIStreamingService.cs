// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Sensors.Models;

namespace Honua.Integration.PowerBI.Services;

/// <summary>
/// Service for streaming real-time data to Power BI Push Datasets
/// </summary>
public interface IPowerBIStreamingService
{
    /// <summary>
    /// Streams a sensor observation to Power BI
    /// </summary>
    Task StreamObservationAsync(Observation observation, Datastream datastream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams multiple observations in a batch
    /// </summary>
    Task StreamObservationsAsync(IEnumerable<(Observation Observation, Datastream Datastream)> observations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams an anomaly alert to Power BI
    /// </summary>
    Task StreamAnomalyAlertAsync(string datastreamId, double observedValue, double expectedValue, double threshold, DateTimeOffset timestamp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts automatic streaming for configured datastreams
    /// </summary>
    Task StartAutoStreamingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops automatic streaming
    /// </summary>
    Task StopAutoStreamingAsync();
}

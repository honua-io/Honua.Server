// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Sensors.AnomalyDetection.Models;

namespace Honua.Server.Enterprise.Sensors.AnomalyDetection.Data;

/// <summary>
/// Repository interface for anomaly detection queries
/// Extends SensorThings repository with specialized queries for anomaly detection
/// </summary>
public interface IAnomalyDetectionRepository
{
    /// <summary>
    /// Gets datastreams with no recent observations (stale sensors)
    /// </summary>
    /// <param name="threshold">Time threshold for considering a sensor stale</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenancy</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of stale datastream information</returns>
    Task<IReadOnlyList<StaleDatastreamInfo>> GetStaleDatastreamsAsync(
        TimeSpan threshold,
        string? tenantId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets statistical summary for a datastream over a time window
    /// Used for outlier detection
    /// </summary>
    /// <param name="datastreamId">Datastream ID</param>
    /// <param name="window">Time window for statistics calculation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Statistical summary or null if insufficient data</returns>
    Task<DatastreamStatistics?> GetDatastreamStatisticsAsync(
        string datastreamId,
        TimeSpan window,
        CancellationToken ct = default);

    /// <summary>
    /// Gets recent observations for anomaly analysis
    /// </summary>
    /// <param name="datastreamId">Datastream ID</param>
    /// <param name="since">Get observations since this time</param>
    /// <param name="limit">Maximum number of observations to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of recent observations</returns>
    Task<IReadOnlyList<ObservationSummary>> GetRecentObservationsAsync(
        string datastreamId,
        DateTime since,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all active datastreams for anomaly monitoring
    /// </summary>
    /// <param name="tenantId">Optional tenant ID for multi-tenancy</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of active datastreams</returns>
    Task<IReadOnlyList<DatastreamInfo>> GetActiveDatastreamsAsync(
        string? tenantId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Records an alert to prevent duplicate alerts (for rate limiting)
    /// </summary>
    /// <param name="datastreamId">Datastream ID</param>
    /// <param name="anomalyType">Type of anomaly</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="ct">Cancellation token</param>
    Task RecordAlertAsync(
        string datastreamId,
        AnomalyType anomalyType,
        string? tenantId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if an alert can be sent (rate limit check)
    /// </summary>
    /// <param name="datastreamId">Datastream ID</param>
    /// <param name="anomalyType">Type of anomaly</param>
    /// <param name="window">Rate limit window</param>
    /// <param name="maxAlerts">Maximum alerts within window</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if alert can be sent, false if rate limited</returns>
    Task<bool> CanSendAlertAsync(
        string datastreamId,
        AnomalyType anomalyType,
        TimeSpan window,
        int maxAlerts,
        string? tenantId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the total alert count across all datastreams within a time window
    /// Used for global rate limiting
    /// </summary>
    Task<int> GetTotalAlertCountAsync(
        TimeSpan window,
        string? tenantId = null,
        CancellationToken ct = default);
}

/// <summary>
/// Information about a stale datastream (sensor with no recent data)
/// </summary>
public sealed record StaleDatastreamInfo
{
    public string DatastreamId { get; init; } = default!;
    public string DatastreamName { get; init; } = default!;
    public string ThingId { get; init; } = default!;
    public string ThingName { get; init; } = default!;
    public string SensorId { get; init; } = default!;
    public string SensorName { get; init; } = default!;
    public string ObservedPropertyName { get; init; } = default!;
    public DateTime? LastObservationTime { get; init; }
    public TimeSpan TimeSinceLastObservation { get; init; }
    public string? TenantId { get; init; }
}

/// <summary>
/// Summary information about a datastream for monitoring
/// </summary>
public sealed record DatastreamInfo
{
    public string DatastreamId { get; init; } = default!;
    public string DatastreamName { get; init; } = default!;
    public string ThingId { get; init; } = default!;
    public string ThingName { get; init; } = default!;
    public string SensorId { get; init; } = default!;
    public string SensorName { get; init; } = default!;
    public string ObservedPropertyId { get; init; } = default!;
    public string ObservedPropertyName { get; init; } = default!;
    public DateTime? LastObservationTime { get; init; }
    public int TotalObservations { get; init; }
    public string? TenantId { get; init; }
}

/// <summary>
/// Summary of an observation for anomaly analysis
/// </summary>
public sealed record ObservationSummary
{
    public string Id { get; init; } = default!;
    public DateTime PhenomenonTime { get; init; }
    public object Result { get; init; } = default!;
    public double? NumericResult { get; init; }
    public string DatastreamId { get; init; } = default!;
}

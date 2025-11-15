// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Represents a Service Level Indicator (SLI) measurement point.
/// </summary>
/// <remarks>
/// An SLI is a quantitative measure of a service's behavior. Common SLIs include:
/// - Request latency: How long it takes to return a response
/// - Error rate: The proportion of failed requests
/// - Availability: The proportion of time the service is operational
/// - Throughput: Requests processed per second
/// </remarks>
public sealed class SliMeasurement
{
    /// <summary>
    /// Gets the name of the SLI being measured.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the type of SLI.
    /// </summary>
    public required SliType Type { get; init; }

    /// <summary>
    /// Gets the timestamp when this measurement was recorded.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether this event was "good" according to the SLI definition.
    /// </summary>
    /// <remarks>
    /// For example:
    /// - Latency SLI: IsGood = (duration &lt;= threshold)
    /// - Availability SLI: IsGood = (statusCode &lt; 500)
    /// - Error Rate SLI: IsGood = (statusCode &lt; 500)
    /// </remarks>
    public required bool IsGood { get; init; }

    /// <summary>
    /// Gets the actual value measured (e.g., latency in milliseconds, status code).
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// Gets the threshold value against which this measurement was compared (if applicable).
    /// </summary>
    public double? Threshold { get; init; }

    /// <summary>
    /// Gets the HTTP endpoint associated with this measurement (if applicable).
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Gets the HTTP method associated with this measurement (if applicable).
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Gets the HTTP status code associated with this measurement (if applicable).
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Gets additional metadata about this measurement.
    /// </summary>
    public string? Metadata { get; init; }
}

/// <summary>
/// Aggregated SLI statistics over a time window.
/// </summary>
public sealed class SliStatistics
{
    /// <summary>
    /// Gets the name of the SLI.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the type of SLI.
    /// </summary>
    public required SliType Type { get; init; }

    /// <summary>
    /// Gets the start of the time window for these statistics.
    /// </summary>
    public required DateTimeOffset WindowStart { get; init; }

    /// <summary>
    /// Gets the end of the time window for these statistics.
    /// </summary>
    public required DateTimeOffset WindowEnd { get; init; }

    /// <summary>
    /// Gets the total number of events measured.
    /// </summary>
    public required long TotalEvents { get; init; }

    /// <summary>
    /// Gets the number of "good" events (events meeting the SLI criteria).
    /// </summary>
    public required long GoodEvents { get; init; }

    /// <summary>
    /// Gets the number of "bad" events (events failing the SLI criteria).
    /// </summary>
    public long BadEvents => TotalEvents - GoodEvents;

    /// <summary>
    /// Gets the actual SLI value (ratio of good events to total events).
    /// </summary>
    /// <remarks>
    /// Value ranges from 0.0 to 1.0 (0% to 100%).
    /// Example: 0.995 means 99.5% of events were "good".
    /// </remarks>
    public double ActualSli => TotalEvents > 0 ? (double)GoodEvents / TotalEvents : 0.0;

    /// <summary>
    /// Gets the threshold value for this SLI (if applicable).
    /// </summary>
    public double? Threshold { get; init; }
}

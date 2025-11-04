// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Authorization;

/// <summary>
/// Provides OpenTelemetry metrics for resource authorization operations.
/// </summary>
public sealed class ResourceAuthorizationMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _authorizationChecks;
    private readonly Counter<long> _authorizationDenials;
    private readonly Histogram<double> _authorizationDuration;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly UpDownCounter<int> _cacheSize;

    public ResourceAuthorizationMetrics(IMeterFactory meterFactory)
    {
        if (meterFactory == null)
        {
            throw new ArgumentNullException(nameof(meterFactory));
        }

        _meter = meterFactory.Create("Honua.Server.Authorization");

        _authorizationChecks = _meter.CreateCounter<long>(
            "honua.authorization.checks",
            unit: "{check}",
            description: "Total number of authorization checks performed");

        _authorizationDenials = _meter.CreateCounter<long>(
            "honua.authorization.denials",
            unit: "{denial}",
            description: "Total number of authorization denials");

        _authorizationDuration = _meter.CreateHistogram<double>(
            "honua.authorization.duration",
            unit: "ms",
            description: "Duration of authorization checks in milliseconds");

        _cacheHits = _meter.CreateCounter<long>(
            "honua.authorization.cache.hits",
            unit: "{hit}",
            description: "Total number of authorization cache hits");

        _cacheMisses = _meter.CreateCounter<long>(
            "honua.authorization.cache.misses",
            unit: "{miss}",
            description: "Total number of authorization cache misses");

        _cacheSize = _meter.CreateUpDownCounter<int>(
            "honua.authorization.cache.size",
            unit: "{entry}",
            description: "Current number of entries in the authorization cache");
    }

    /// <summary>
    /// Records an authorization check.
    /// </summary>
    public void RecordAuthorizationCheck(
        string resourceType,
        string operation,
        bool succeeded,
        double durationMs,
        bool fromCache)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("resource_type", resourceType),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", succeeded ? "allow" : "deny"),
            new KeyValuePair<string, object?>("cached", fromCache)
        };

        _authorizationChecks.Add(1, tags);

        if (!succeeded)
        {
            _authorizationDenials.Add(1, tags);
        }

        _authorizationDuration.Record(durationMs, tags);

        if (fromCache)
        {
            _cacheHits.Add(1, new KeyValuePair<string, object?>("resource_type", resourceType));
        }
        else
        {
            _cacheMisses.Add(1, new KeyValuePair<string, object?>("resource_type", resourceType));
        }
    }

    /// <summary>
    /// Updates the cache size metric.
    /// </summary>
    public void UpdateCacheSize(int newSize, int oldSize = 0)
    {
        var delta = newSize - oldSize;
        if (delta != 0)
        {
            _cacheSize.Add(delta);
        }
    }

    /// <summary>
    /// Records a cache invalidation.
    /// </summary>
    public void RecordCacheInvalidation(string resourceType, int entriesInvalidated)
    {
        // Cache invalidations can be tracked via logging or additional metrics if needed
    }
}

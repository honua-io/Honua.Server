// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Honua.Server.Host.Raster;

public interface IRasterTileCacheMetrics
{
    void RecordCacheHit(string datasetId, string? variant = null, string? timeSlice = null);
    void RecordCacheMiss(string datasetId, string? variant = null, string? timeSlice = null);
    void RecordRenderLatency(string datasetId, TimeSpan duration, bool fromPreseed);
    void RecordPreseedJobCompleted(RasterTilePreseedJobSnapshot snapshot);
    void RecordPreseedJobFailed(Guid jobId, string? message);
    void RecordPreseedJobCancelled(Guid jobId);
    void RecordCachePurge(string datasetId, bool succeeded);
}

public sealed class RasterTileCacheMetrics : IRasterTileCacheMetrics, IDisposable
{
    private static readonly KeyValuePair<string, object?> SourceOnDemand = new("source", "request");
    private static readonly KeyValuePair<string, object?> SourcePreseed = new("source", "preseed");

    private readonly Meter _meter;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Histogram<double> _renderLatencyMs;
    private readonly Counter<long> _jobsCompleted;
    private readonly Counter<long> _jobsFailed;
    private readonly Counter<long> _jobsCancelled;
    private readonly Counter<long> _purgesSucceeded;
    private readonly Counter<long> _purgesFailed;

    public RasterTileCacheMetrics()
    {
        _meter = new Meter("Honua.Server.RasterCache");
        _cacheHits = _meter.CreateCounter<long>("honua.raster.cache_hits", description: "Number of raster tile cache hits.");
        _cacheMisses = _meter.CreateCounter<long>("honua.raster.cache_misses", description: "Number of raster tile cache misses.");
        _renderLatencyMs = _meter.CreateHistogram<double>("honua.raster.render_latency_ms", unit: "ms", description: "Raster tile render latency.");
        _jobsCompleted = _meter.CreateCounter<long>("honua.raster.preseed_jobs_completed", description: "Completed raster preseed jobs.");
        _jobsFailed = _meter.CreateCounter<long>("honua.raster.preseed_jobs_failed", description: "Failed raster preseed jobs.");
        _jobsCancelled = _meter.CreateCounter<long>("honua.raster.preseed_jobs_cancelled", description: "Cancelled raster preseed jobs.");
        _purgesSucceeded = _meter.CreateCounter<long>("honua.raster.cache_purges_succeeded", description: "Successful raster cache purges.");
        _purgesFailed = _meter.CreateCounter<long>("honua.raster.cache_purges_failed", description: "Failed raster cache purges.");
    }

    public void RecordCacheHit(string datasetId, string? variant = null, string? timeSlice = null)
    {
        _cacheHits.Add(1, BuildTags(datasetId, variant, timeSlice));
    }

    public void RecordCacheMiss(string datasetId, string? variant = null, string? timeSlice = null)
    {
        _cacheMisses.Add(1, BuildTags(datasetId, variant, timeSlice));
    }

    public void RecordRenderLatency(string datasetId, TimeSpan duration, bool fromPreseed)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("dataset", Normalize(datasetId)),
            fromPreseed ? SourcePreseed : SourceOnDemand
        };

        _renderLatencyMs.Record(duration.TotalMilliseconds, tags);
    }

    public void RecordPreseedJobCompleted(RasterTilePreseedJobSnapshot snapshot)
    {
        var datasets = string.Join(",", snapshot.DatasetIds);
        _jobsCompleted.Add(1,
            new KeyValuePair<string, object?>("jobId", snapshot.JobId.ToString()),
            new KeyValuePair<string, object?>("datasets", datasets));

        if (snapshot.CompletedAtUtc is { } completed)
        {
            var duration = completed - snapshot.CreatedAtUtc;
            _renderLatencyMs.Record(duration.TotalMilliseconds,
                SourcePreseed,
                new KeyValuePair<string, object?>("dataset", "(preseed-job)"),
                new KeyValuePair<string, object?>("jobId", snapshot.JobId.ToString()));
        }
    }

    public void RecordPreseedJobFailed(Guid jobId, string? message)
    {
        _jobsFailed.Add(1,
            new KeyValuePair<string, object?>("jobId", jobId.ToString()),
            new KeyValuePair<string, object?>("error", message ?? string.Empty));
    }

    public void RecordPreseedJobCancelled(Guid jobId)
    {
        _jobsCancelled.Add(1, new KeyValuePair<string, object?>("jobId", jobId.ToString()));
    }

    public void RecordCachePurge(string datasetId, bool succeeded)
    {
        var tag = new KeyValuePair<string, object?>("dataset", Normalize(datasetId));
        if (succeeded)
        {
            _purgesSucceeded.Add(1, tag);
        }
        else
        {
            _purgesFailed.Add(1, tag);
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? "(unknown)" : value;

    private static KeyValuePair<string, object?>[] BuildTags(string datasetId, string? variant, string? timeSlice)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("dataset", Normalize(datasetId))
        };

        if (!string.IsNullOrWhiteSpace(variant))
        {
            tags.Add(new KeyValuePair<string, object?>("variant", variant));
        }

        if (!string.IsNullOrWhiteSpace(timeSlice))
        {
            tags.Add(new KeyValuePair<string, object?>("time", timeSlice));
        }

        return tags.ToArray();
    }
}

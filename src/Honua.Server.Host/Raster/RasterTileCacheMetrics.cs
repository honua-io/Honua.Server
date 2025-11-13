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

    private readonly Meter meter;
    private readonly Counter<long> cacheHits;
    private readonly Counter<long> cacheMisses;
    private readonly Histogram<double> renderLatencyMs;
    private readonly Counter<long> jobsCompleted;
    private readonly Counter<long> jobsFailed;
    private readonly Counter<long> jobsCancelled;
    private readonly Counter<long> purgesSucceeded;
    private readonly Counter<long> purgesFailed;

    public RasterTileCacheMetrics()
    {
        this.meter = new Meter("Honua.Server.RasterCache");
        this.cacheHits = this.meter.CreateCounter<long>("honua.raster.cache_hits", description: "Number of raster tile cache hits.");
        this.cacheMisses = this.meter.CreateCounter<long>("honua.raster.cache_misses", description: "Number of raster tile cache misses.");
        this.renderLatencyMs = this.meter.CreateHistogram<double>("honua.raster.render_latency_ms", unit: "ms", description: "Raster tile render latency.");
        this.jobsCompleted = this.meter.CreateCounter<long>("honua.raster.preseed_jobs_completed", description: "Completed raster preseed jobs.");
        this.jobsFailed = this.meter.CreateCounter<long>("honua.raster.preseed_jobs_failed", description: "Failed raster preseed jobs.");
        this.jobsCancelled = this.meter.CreateCounter<long>("honua.raster.preseed_jobs_cancelled", description: "Cancelled raster preseed jobs.");
        this.purgesSucceeded = this.meter.CreateCounter<long>("honua.raster.cache_purges_succeeded", description: "Successful raster cache purges.");
        this.purgesFailed = this.meter.CreateCounter<long>("honua.raster.cache_purges_failed", description: "Failed raster cache purges.");
    }

    public void RecordCacheHit(string datasetId, string? variant = null, string? timeSlice = null)
    {
        this.cacheHits.Add(1, BuildTags(datasetId, variant, timeSlice));
    }

    public void RecordCacheMiss(string datasetId, string? variant = null, string? timeSlice = null)
    {
        this.cacheMisses.Add(1, BuildTags(datasetId, variant, timeSlice));
    }

    public void RecordRenderLatency(string datasetId, TimeSpan duration, bool fromPreseed)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("dataset", Normalize(datasetId)),
            fromPreseed ? SourcePreseed : SourceOnDemand
        };

        this.renderLatencyMs.Record(duration.TotalMilliseconds, tags);
    }

    public void RecordPreseedJobCompleted(RasterTilePreseedJobSnapshot snapshot)
    {
        var datasets = string.Join(",", snapshot.DatasetIds);
        this.jobsCompleted.Add(1,
            new KeyValuePair<string, object?>("jobId", snapshot.JobId.ToString()),
            new KeyValuePair<string, object?>("datasets", datasets));

        if (snapshot.CompletedAtUtc is { } completed)
        {
            var duration = completed - snapshot.CreatedAtUtc;
            this.renderLatencyMs.Record(duration.TotalMilliseconds,
                SourcePreseed,
                new KeyValuePair<string, object?>("dataset", "(preseed-job)"),
                new KeyValuePair<string, object?>("jobId", snapshot.JobId.ToString()));
        }
    }

    public void RecordPreseedJobFailed(Guid jobId, string? message)
    {
        this.jobsFailed.Add(1,
            new KeyValuePair<string, object?>("jobId", jobId.ToString()),
            new KeyValuePair<string, object?>("error", message ?? string.Empty));
    }

    public void RecordPreseedJobCancelled(Guid jobId)
    {
        this.jobsCancelled.Add(1, new KeyValuePair<string, object?>("jobId", jobId.ToString()));
    }

    public void RecordCachePurge(string datasetId, bool succeeded)
    {
        var tag = new KeyValuePair<string, object?>("dataset", Normalize(datasetId));
        if (succeeded)
        {
            this.purgesSucceeded.Add(1, tag);
        }
        else
        {
            this.purgesFailed.Add(1, tag);
        }
    }

    public void Dispose()
    {
        this.meter.Dispose();
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

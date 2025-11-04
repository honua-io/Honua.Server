// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// OpenTelemetry metrics for vector tile operations.
/// Tracks tile generation, cache efficiency, and feature processing.
/// </summary>
public interface IVectorTileMetrics
{
    void RecordTileGenerated(string layerId, int zoom, TimeSpan duration, int featureCount);
    void RecordTileServed(string layerId, int zoom, bool fromCache);
    void RecordTileError(string layerId, int zoom, string errorType);
    void RecordFeatureSimplification(int originalVertices, int simplifiedVertices, TimeSpan duration);
    void RecordPreseedJobStarted(Guid jobId, string layerId, int minZoom, int maxZoom);
    void RecordPreseedJobCompleted(Guid jobId, TimeSpan duration, int tilesGenerated);
    void RecordPreseedJobFailed(Guid jobId, string errorReason);
}

/// <summary>
/// Implementation of vector tile metrics using OpenTelemetry.
/// </summary>
public sealed class VectorTileMetrics : IVectorTileMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _tilesGenerated;
    private readonly Counter<long> _tilesServed;
    private readonly Counter<long> _tileErrors;
    private readonly Histogram<double> _tileGenerationDuration;
    private readonly Histogram<long> _featuresPerTile;
    private readonly Counter<long> _simplifications;
    private readonly Histogram<double> _simplificationRatio;
    private readonly Counter<long> _preseedJobsStarted;
    private readonly Counter<long> _preseedJobsCompleted;
    private readonly Counter<long> _preseedJobsFailed;
    private readonly Histogram<double> _preseedJobDuration;

    public VectorTileMetrics()
    {
        _meter = new Meter("Honua.Server.VectorTiles", "1.0.0");

        _tilesGenerated = _meter.CreateCounter<long>(
            "honua.vectortile.tiles_generated",
            unit: "{tile}",
            description: "Number of vector tiles generated");

        _tilesServed = _meter.CreateCounter<long>(
            "honua.vectortile.tiles_served",
            unit: "{tile}",
            description: "Number of vector tiles served to clients");

        _tileErrors = _meter.CreateCounter<long>(
            "honua.vectortile.errors",
            unit: "{error}",
            description: "Number of vector tile generation errors");

        _tileGenerationDuration = _meter.CreateHistogram<double>(
            "honua.vectortile.generation_duration",
            unit: "ms",
            description: "Vector tile generation duration");

        _featuresPerTile = _meter.CreateHistogram<long>(
            "honua.vectortile.features_per_tile",
            unit: "{feature}",
            description: "Number of features per generated tile");

        _simplifications = _meter.CreateCounter<long>(
            "honua.vectortile.simplifications",
            unit: "{operation}",
            description: "Number of geometry simplification operations");

        _simplificationRatio = _meter.CreateHistogram<double>(
            "honua.vectortile.simplification_ratio",
            unit: "ratio",
            description: "Ratio of simplified vertices to original vertices");

        _preseedJobsStarted = _meter.CreateCounter<long>(
            "honua.vectortile.preseed_jobs_started",
            unit: "{job}",
            description: "Number of preseed jobs started");

        _preseedJobsCompleted = _meter.CreateCounter<long>(
            "honua.vectortile.preseed_jobs_completed",
            unit: "{job}",
            description: "Number of preseed jobs completed");

        _preseedJobsFailed = _meter.CreateCounter<long>(
            "honua.vectortile.preseed_jobs_failed",
            unit: "{job}",
            description: "Number of preseed jobs failed");

        _preseedJobDuration = _meter.CreateHistogram<double>(
            "honua.vectortile.preseed_job_duration",
            unit: "ms",
            description: "Preseed job execution duration");
    }

    public void RecordTileGenerated(string layerId, int zoom, TimeSpan duration, int featureCount)
    {
        _tilesGenerated.Add(1,
            new("layer.id", Normalize(layerId)),
            new("zoom.level", zoom.ToString()),
            new("zoom.bucket", GetZoomBucket(zoom)));

        _tileGenerationDuration.Record(duration.TotalMilliseconds,
            new("layer.id", Normalize(layerId)),
            new("zoom.level", zoom.ToString()),
            new("zoom.bucket", GetZoomBucket(zoom)));

        _featuresPerTile.Record(featureCount,
            new("layer.id", Normalize(layerId)),
            new("zoom.level", zoom.ToString()),
            new("feature.count.bucket", GetFeatureCountBucket(featureCount)));
    }

    public void RecordTileServed(string layerId, int zoom, bool fromCache)
    {
        _tilesServed.Add(1,
            new("layer.id", Normalize(layerId)),
            new("zoom.level", zoom.ToString()),
            new("zoom.bucket", GetZoomBucket(zoom)),
            new("cache.hit", fromCache.ToString()));
    }

    public void RecordTileError(string layerId, int zoom, string errorType)
    {
        _tileErrors.Add(1,
            new("layer.id", Normalize(layerId)),
            new("zoom.level", zoom.ToString()),
            new("error.type", Normalize(errorType)));
    }

    public void RecordFeatureSimplification(int originalVertices, int simplifiedVertices, TimeSpan duration)
    {
        _simplifications.Add(1,
            new("original.vertices.bucket", GetVerticesBucket(originalVertices)),
            new("simplified.vertices.bucket", GetVerticesBucket(simplifiedVertices)));

        var ratio = originalVertices > 0 ? (double)simplifiedVertices / originalVertices : 1.0;
        _simplificationRatio.Record(ratio,
            new KeyValuePair<string, object?>[] { new("simplification.level", GetSimplificationLevel(ratio)) });
    }

    public void RecordPreseedJobStarted(Guid jobId, string layerId, int minZoom, int maxZoom)
    {
        _preseedJobsStarted.Add(1,
            new("job.id", jobId.ToString()),
            new("layer.id", Normalize(layerId)),
            new("zoom.min", minZoom.ToString()),
            new("zoom.max", maxZoom.ToString()),
            new("zoom.range", $"{minZoom}-{maxZoom}"));
    }

    public void RecordPreseedJobCompleted(Guid jobId, TimeSpan duration, int tilesGenerated)
    {
        _preseedJobsCompleted.Add(1,
            new("job.id", jobId.ToString()),
            new("tiles.generated.bucket", GetTileCountBucket(tilesGenerated)));

        _preseedJobDuration.Record(duration.TotalMilliseconds,
            new("job.id", jobId.ToString()),
            new("duration.bucket", GetDurationBucket(duration)));
    }

    public void RecordPreseedJobFailed(Guid jobId, string errorReason)
    {
        _preseedJobsFailed.Add(1,
            new("job.id", jobId.ToString()),
            new("error.reason", Normalize(errorReason)));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private static string GetZoomBucket(int zoom)
    {
        return zoom switch
        {
            <= 5 => "low",          // World to country level
            <= 10 => "medium",      // State to city level
            <= 15 => "high",        // Neighborhood to street level
            _ => "very_high"        // Building level and beyond
        };
    }

    private static string GetFeatureCountBucket(int count)
    {
        return count switch
        {
            0 => "empty",
            <= 10 => "sparse",
            <= 100 => "normal",
            <= 1000 => "dense",
            _ => "very_dense"
        };
    }

    private static string GetVerticesBucket(int vertices)
    {
        return vertices switch
        {
            <= 10 => "simple",
            <= 100 => "normal",
            <= 1000 => "complex",
            _ => "very_complex"
        };
    }

    private static string GetSimplificationLevel(double ratio)
    {
        return ratio switch
        {
            >= 0.9 => "minimal",
            >= 0.7 => "light",
            >= 0.5 => "moderate",
            >= 0.3 => "aggressive",
            _ => "extreme"
        };
    }

    private static string GetTileCountBucket(int count)
    {
        return count switch
        {
            <= 100 => "small",
            <= 1000 => "medium",
            <= 10000 => "large",
            _ => "very_large"
        };
    }

    private static string GetDurationBucket(TimeSpan duration)
    {
        var seconds = duration.TotalSeconds;
        return seconds switch
        {
            < 60 => "fast",
            < 300 => "normal",
            < 900 => "slow",
            _ => "very_slow"
        };
    }
}

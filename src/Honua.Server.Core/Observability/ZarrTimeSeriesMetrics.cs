// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// OpenTelemetry metrics for Zarr time-series operations.
/// Tracks query performance, chunk efficiency, and data transfer.
/// </summary>
public interface IZarrTimeSeriesMetrics
{
    void RecordTimeSliceQuery(string datasetId, string variable, TimeSpan duration, int dataSizeBytes);
    void RecordTimeRangeQuery(string datasetId, string variable, TimeSpan duration, int sliceCount, int totalDataSizeBytes);
    void RecordChunkRead(string datasetId, int chunksRead, int totalChunks, TimeSpan duration);
    void RecordTimeStepsCached(string datasetId, int timeStepCount);
    void RecordQueryError(string datasetId, string errorType);
}

/// <summary>
/// Implementation of Zarr time-series metrics using OpenTelemetry.
/// </summary>
public sealed class ZarrTimeSeriesMetrics : IZarrTimeSeriesMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _timeSliceQueries;
    private readonly Counter<long> _timeRangeQueries;
    private readonly Counter<long> _chunksRead;
    private readonly Counter<long> _queryErrors;
    private readonly Histogram<double> _queryDuration;
    private readonly Histogram<long> _dataSizeBytes;
    private readonly Histogram<long> _slicesPerQuery;
    private readonly Histogram<double> _chunkEfficiency;
    private readonly Counter<long> _timeStepsCached;

    public ZarrTimeSeriesMetrics()
    {
        _meter = new Meter("Honua.Server.ZarrTimeSeries", "1.0.0");

        _timeSliceQueries = _meter.CreateCounter<long>(
            "honua.zarr.timeseries.time_slice_queries",
            unit: "{query}",
            description: "Number of time slice queries executed");

        _timeRangeQueries = _meter.CreateCounter<long>(
            "honua.zarr.timeseries.time_range_queries",
            unit: "{query}",
            description: "Number of time range queries executed");

        _chunksRead = _meter.CreateCounter<long>(
            "honua.zarr.timeseries.chunks_read",
            unit: "{chunk}",
            description: "Number of Zarr chunks read from storage");

        _queryErrors = _meter.CreateCounter<long>(
            "honua.zarr.timeseries.query_errors",
            unit: "{error}",
            description: "Number of query errors");

        _queryDuration = _meter.CreateHistogram<double>(
            "honua.zarr.timeseries.query_duration",
            unit: "ms",
            description: "Query execution duration");

        _dataSizeBytes = _meter.CreateHistogram<long>(
            "honua.zarr.timeseries.data_size_bytes",
            unit: "By",
            description: "Data transfer size in bytes");

        _slicesPerQuery = _meter.CreateHistogram<long>(
            "honua.zarr.timeseries.slices_per_query",
            unit: "{slice}",
            description: "Number of time slices per query");

        _chunkEfficiency = _meter.CreateHistogram<double>(
            "honua.zarr.timeseries.chunk_efficiency",
            unit: "ratio",
            description: "Ratio of chunks read to total chunks (efficiency)");

        _timeStepsCached = _meter.CreateCounter<long>(
            "honua.zarr.timeseries.timesteps_cached",
            unit: "{timestep}",
            description: "Number of timesteps cached");
    }

    public void RecordTimeSliceQuery(string datasetId, string variable, TimeSpan duration, int dataSizeBytes)
    {
        _timeSliceQueries.Add(1,
            new("dataset.id", Normalize(datasetId)),
            new("variable", Normalize(variable)),
            new("duration.bucket", GetDurationBucket(duration)));

        _queryDuration.Record(duration.TotalMilliseconds,
            new("dataset.id", Normalize(datasetId)),
            new("variable", Normalize(variable)),
            new("query.type", "time_slice"));

        _dataSizeBytes.Record(dataSizeBytes,
            new("dataset.id", Normalize(datasetId)),
            new("variable", Normalize(variable)),
            new("query.type", "time_slice"),
            new("size.bucket", GetSizeBucket(dataSizeBytes)));
    }

    public void RecordTimeRangeQuery(string datasetId, string variable, TimeSpan duration, int sliceCount, int totalDataSizeBytes)
    {
        _timeRangeQueries.Add(1,
            new("dataset.id", Normalize(datasetId)),
            new("variable", Normalize(variable)),
            new("duration.bucket", GetDurationBucket(duration)),
            new("slice.count.bucket", GetSliceCountBucket(sliceCount)));

        _queryDuration.Record(duration.TotalMilliseconds,
            new("dataset.id", Normalize(datasetId)),
            new("variable", Normalize(variable)),
            new("query.type", "time_range"));

        _slicesPerQuery.Record(sliceCount,
            new("dataset.id", Normalize(datasetId)),
            new("variable", Normalize(variable)),
            new("slice.count.bucket", GetSliceCountBucket(sliceCount)));

        _dataSizeBytes.Record(totalDataSizeBytes,
            new("dataset.id", Normalize(datasetId)),
            new("variable", Normalize(variable)),
            new("query.type", "time_range"),
            new("size.bucket", GetSizeBucket(totalDataSizeBytes)));
    }

    public void RecordChunkRead(string datasetId, int chunksRead, int totalChunks, TimeSpan duration)
    {
        _chunksRead.Add(chunksRead,
            new("dataset.id", Normalize(datasetId)),
            new("chunk.count.bucket", GetChunkCountBucket(chunksRead)));

        var efficiency = totalChunks > 0 ? (double)chunksRead / totalChunks : 1.0;
        _chunkEfficiency.Record(efficiency,
            new("dataset.id", Normalize(datasetId)),
            new("efficiency.level", GetEfficiencyLevel(efficiency)));
    }

    public void RecordTimeStepsCached(string datasetId, int timeStepCount)
    {
        _timeStepsCached.Add(timeStepCount,
            new("dataset.id", Normalize(datasetId)),
            new("timestep.count.bucket", GetTimeStepCountBucket(timeStepCount)));
    }

    public void RecordQueryError(string datasetId, string errorType)
    {
        _queryErrors.Add(1,
            new("dataset.id", Normalize(datasetId)),
            new("error.type", Normalize(errorType)));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private static string GetDurationBucket(TimeSpan duration)
    {
        var ms = duration.TotalMilliseconds;
        return ms switch
        {
            < 100 => "fast",           // < 100ms
            < 500 => "normal",         // 100-500ms
            < 2000 => "slow",          // 0.5-2s
            < 10000 => "very_slow",    // 2-10s
            _ => "extremely_slow"      // > 10s
        };
    }

    private static string GetSizeBucket(int bytes)
    {
        return bytes switch
        {
            < 1024 => "tiny",                      // < 1 KB
            < 1024 * 1024 => "small",              // 1 KB - 1 MB
            < 10 * 1024 * 1024 => "medium",        // 1-10 MB
            < 100 * 1024 * 1024 => "large",        // 10-100 MB
            _ => "very_large"                       // > 100 MB
        };
    }

    private static string GetSliceCountBucket(int count)
    {
        return count switch
        {
            1 => "single",
            <= 10 => "few",
            <= 50 => "moderate",
            <= 100 => "many",
            _ => "very_many"
        };
    }

    private static string GetChunkCountBucket(int count)
    {
        return count switch
        {
            <= 1 => "single",
            <= 5 => "few",
            <= 20 => "moderate",
            <= 100 => "many",
            _ => "very_many"
        };
    }

    private static string GetTimeStepCountBucket(int count)
    {
        return count switch
        {
            <= 10 => "short",
            <= 100 => "medium",
            <= 1000 => "long",
            _ => "very_long"
        };
    }

    private static string GetEfficiencyLevel(double efficiency)
    {
        return efficiency switch
        {
            <= 0.1 => "excellent",      // Only 10% of chunks read
            <= 0.25 => "good",          // 10-25% of chunks read
            <= 0.5 => "fair",           // 25-50% of chunks read
            <= 0.75 => "poor",          // 50-75% of chunks read
            _ => "very_poor"            // > 75% of chunks read
        };
    }
}

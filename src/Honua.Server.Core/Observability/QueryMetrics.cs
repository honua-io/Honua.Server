// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// OpenTelemetry metrics for feature query operations.
/// Tracks query performance across different service endpoints, operation types, and layers.
/// Provides detailed performance monitoring to identify bottlenecks and optimization opportunities.
/// </summary>
public interface IQueryMetrics
{
    /// <summary>
    /// Records the execution duration of a query operation.
    /// </summary>
    /// <param name="serviceId">The service identifier (e.g., "buildings-wfs", "parcels-ogcapi")</param>
    /// <param name="layerId">The layer identifier</param>
    /// <param name="endpointType">The endpoint type (WFS, WMS, OGC-API, GeoServices)</param>
    /// <param name="operationType">The operation type (Query, Count, Statistics, Distinct, Extent)</param>
    /// <param name="duration">The total query execution time</param>
    /// <param name="success">Whether the query succeeded</param>
    void RecordQueryDuration(
        string serviceId,
        string layerId,
        string endpointType,
        string operationType,
        TimeSpan duration,
        bool success = true);

    /// <summary>
    /// Records detailed performance breakdown for a query operation.
    /// Tracks time spent in parsing, execution, and serialization phases.
    /// </summary>
    /// <param name="serviceId">The service identifier</param>
    /// <param name="layerId">The layer identifier</param>
    /// <param name="endpointType">The endpoint type</param>
    /// <param name="operationType">The operation type</param>
    /// <param name="breakdown">Performance breakdown details</param>
    void RecordQueryBreakdown(
        string serviceId,
        string layerId,
        string endpointType,
        string operationType,
        QueryPerformanceBreakdown breakdown);

    /// <summary>
    /// Records a slow query warning for queries exceeding performance thresholds.
    /// </summary>
    /// <param name="serviceId">The service identifier</param>
    /// <param name="layerId">The layer identifier</param>
    /// <param name="endpointType">The endpoint type</param>
    /// <param name="operationType">The operation type</param>
    /// <param name="duration">The query execution time</param>
    /// <param name="threshold">The threshold that was exceeded</param>
    void RecordSlowQuery(
        string serviceId,
        string layerId,
        string endpointType,
        string operationType,
        TimeSpan duration,
        TimeSpan threshold);

    /// <summary>
    /// Records query result metrics (record count, data size).
    /// </summary>
    /// <param name="serviceId">The service identifier</param>
    /// <param name="layerId">The layer identifier</param>
    /// <param name="endpointType">The endpoint type</param>
    /// <param name="recordCount">Number of records returned</param>
    /// <param name="estimatedSizeBytes">Estimated response size in bytes (optional)</param>
    void RecordQueryResults(
        string serviceId,
        string layerId,
        string endpointType,
        long recordCount,
        long? estimatedSizeBytes = null);

    /// <summary>
    /// Records query errors for monitoring and alerting.
    /// </summary>
    /// <param name="serviceId">The service identifier</param>
    /// <param name="layerId">The layer identifier</param>
    /// <param name="endpointType">The endpoint type</param>
    /// <param name="operationType">The operation type</param>
    /// <param name="errorType">The error type or exception name</param>
    void RecordQueryError(
        string serviceId,
        string layerId,
        string endpointType,
        string operationType,
        string errorType);

    /// <summary>
    /// Records filter complexity metrics to understand query patterns.
    /// </summary>
    /// <param name="serviceId">The service identifier</param>
    /// <param name="layerId">The layer identifier</param>
    /// <param name="endpointType">The endpoint type</param>
    /// <param name="hasFilter">Whether a filter was applied</param>
    /// <param name="hasSpatialFilter">Whether a spatial filter (bbox) was applied</param>
    /// <param name="filterComplexity">Simple, Medium, or Complex</param>
    void RecordFilterComplexity(
        string serviceId,
        string layerId,
        string endpointType,
        bool hasFilter,
        bool hasSpatialFilter,
        string filterComplexity);
}

/// <summary>
/// Detailed performance breakdown for query operations.
/// Tracks time spent in different phases of query processing.
/// </summary>
public sealed class QueryPerformanceBreakdown
{
    /// <summary>
    /// Time spent parsing and validating the request.
    /// Includes parameter parsing, filter parsing, CRS validation.
    /// </summary>
    public TimeSpan? ParsingTime { get; init; }

    /// <summary>
    /// Time spent executing the database query.
    /// Includes connection acquisition, query execution, and result fetching.
    /// </summary>
    public TimeSpan? ExecutionTime { get; init; }

    /// <summary>
    /// Time spent serializing the response.
    /// Includes GeoJSON/GML writing, geometry transformation, format conversion.
    /// </summary>
    public TimeSpan? SerializationTime { get; init; }

    /// <summary>
    /// Time spent on CRS transformations.
    /// </summary>
    public TimeSpan? TransformationTime { get; init; }

    /// <summary>
    /// Total query duration (should equal sum of all phases).
    /// </summary>
    public TimeSpan TotalTime { get; init; }

    /// <summary>
    /// Number of database round trips.
    /// </summary>
    public int? DatabaseRoundTrips { get; init; }
}

/// <summary>
/// Implementation of query metrics using OpenTelemetry.
/// Follows Prometheus naming conventions for metric names.
/// </summary>
public sealed class QueryMetrics : IQueryMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Histogram<double> _queryDuration;
    private readonly Histogram<double> _parsingDuration;
    private readonly Histogram<double> _executionDuration;
    private readonly Histogram<double> _serializationDuration;
    private readonly Histogram<double> _transformationDuration;
    private readonly Counter<long> _slowQueryCounter;
    private readonly Counter<long> _queryCounter;
    private readonly Counter<long> _queryErrors;
    private readonly Histogram<double> _resultSize;
    private readonly Counter<long> _recordCount;
    private readonly Counter<long> _filterUsage;

    public QueryMetrics()
    {
        _meter = new Meter("Honua.Server.Query", "1.0.0");

        // Primary query duration histogram with endpoint, operation, and layer dimensions
        _queryDuration = _meter.CreateHistogram<double>(
            "honua.query.duration",
            unit: "ms",
            description: "Query execution duration by endpoint type, operation type, and layer");

        // Query breakdown histograms for detailed performance analysis
        _parsingDuration = _meter.CreateHistogram<double>(
            "honua.query.parsing_duration",
            unit: "ms",
            description: "Time spent parsing and validating query parameters");

        _executionDuration = _meter.CreateHistogram<double>(
            "honua.query.execution_duration",
            unit: "ms",
            description: "Time spent executing database query");

        _serializationDuration = _meter.CreateHistogram<double>(
            "honua.query.serialization_duration",
            unit: "ms",
            description: "Time spent serializing query results");

        _transformationDuration = _meter.CreateHistogram<double>(
            "honua.query.transformation_duration",
            unit: "ms",
            description: "Time spent on CRS transformations");

        // Slow query counter for alerting
        _slowQueryCounter = _meter.CreateCounter<long>(
            "honua.query.slow_queries",
            unit: "{query}",
            description: "Number of slow queries exceeding performance thresholds");

        // Total query counter
        _queryCounter = _meter.CreateCounter<long>(
            "honua.query.total",
            unit: "{query}",
            description: "Total number of queries executed by endpoint, operation, and status");

        // Query error counter
        _queryErrors = _meter.CreateCounter<long>(
            "honua.query.errors",
            unit: "{error}",
            description: "Number of query errors by endpoint, operation, and error type");

        // Result size and record count
        _resultSize = _meter.CreateHistogram<double>(
            "honua.query.result_size",
            unit: "bytes",
            description: "Estimated size of query results");

        _recordCount = _meter.CreateCounter<long>(
            "honua.query.records_returned",
            unit: "{record}",
            description: "Total number of records returned by queries");

        // Filter usage tracking
        _filterUsage = _meter.CreateCounter<long>(
            "honua.query.filter_usage",
            unit: "{query}",
            description: "Query filter usage patterns and complexity");
    }

    public void RecordQueryDuration(
        string serviceId,
        string layerId,
        string endpointType,
        string operationType,
        TimeSpan duration,
        bool success = true)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("service.id", NormalizeServiceId(serviceId)),
            new KeyValuePair<string, object?>("layer.id", NormalizeLayerId(layerId)),
            new KeyValuePair<string, object?>("endpoint.type", NormalizeEndpointType(endpointType)),
            new KeyValuePair<string, object?>("operation.type", NormalizeOperationType(operationType)),
            new KeyValuePair<string, object?>("success", success.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("duration.bucket", GetDurationBucket(duration))
        };

        _queryDuration.Record(duration.TotalMilliseconds, tags);
        _queryCounter.Add(1, tags);

        // Add activity tags for distributed tracing
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.AddTag("query.service_id", serviceId);
            activity.AddTag("query.layer_id", layerId);
            activity.AddTag("query.endpoint_type", endpointType);
            activity.AddTag("query.operation_type", operationType);
            activity.AddTag("query.duration_ms", duration.TotalMilliseconds);
            activity.AddTag("query.success", success);
        }
    }

    public void RecordQueryBreakdown(
        string serviceId,
        string layerId,
        string endpointType,
        string operationType,
        QueryPerformanceBreakdown breakdown)
    {
        var baseTags = new[]
        {
            new KeyValuePair<string, object?>("service.id", NormalizeServiceId(serviceId)),
            new KeyValuePair<string, object?>("layer.id", NormalizeLayerId(layerId)),
            new KeyValuePair<string, object?>("endpoint.type", NormalizeEndpointType(endpointType)),
            new KeyValuePair<string, object?>("operation.type", NormalizeOperationType(operationType))
        };

        if (breakdown.ParsingTime.HasValue)
        {
            _parsingDuration.Record(breakdown.ParsingTime.Value.TotalMilliseconds, baseTags);
        }

        if (breakdown.ExecutionTime.HasValue)
        {
            _executionDuration.Record(breakdown.ExecutionTime.Value.TotalMilliseconds, baseTags);
        }

        if (breakdown.SerializationTime.HasValue)
        {
            _serializationDuration.Record(breakdown.SerializationTime.Value.TotalMilliseconds, baseTags);
        }

        if (breakdown.TransformationTime.HasValue)
        {
            _transformationDuration.Record(breakdown.TransformationTime.Value.TotalMilliseconds, baseTags);
        }

        // Add activity tags for performance breakdown
        var activity = Activity.Current;
        if (activity != null)
        {
            if (breakdown.ParsingTime.HasValue)
                activity.AddTag("query.parsing_ms", breakdown.ParsingTime.Value.TotalMilliseconds);
            if (breakdown.ExecutionTime.HasValue)
                activity.AddTag("query.execution_ms", breakdown.ExecutionTime.Value.TotalMilliseconds);
            if (breakdown.SerializationTime.HasValue)
                activity.AddTag("query.serialization_ms", breakdown.SerializationTime.Value.TotalMilliseconds);
            if (breakdown.TransformationTime.HasValue)
                activity.AddTag("query.transformation_ms", breakdown.TransformationTime.Value.TotalMilliseconds);
            if (breakdown.DatabaseRoundTrips.HasValue)
                activity.AddTag("query.db_roundtrips", breakdown.DatabaseRoundTrips.Value);
        }
    }

    public void RecordSlowQuery(
        string serviceId,
        string layerId,
        string endpointType,
        string operationType,
        TimeSpan duration,
        TimeSpan threshold)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("service.id", NormalizeServiceId(serviceId)),
            new KeyValuePair<string, object?>("layer.id", NormalizeLayerId(layerId)),
            new KeyValuePair<string, object?>("endpoint.type", NormalizeEndpointType(endpointType)),
            new KeyValuePair<string, object?>("operation.type", NormalizeOperationType(operationType)),
            new KeyValuePair<string, object?>("threshold.ms", threshold.TotalMilliseconds),
            new KeyValuePair<string, object?>("duration.bucket", GetDurationBucket(duration))
        };

        _slowQueryCounter.Add(1, tags);

        // Add activity tag for alerting
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.AddTag("query.slow", true);
            activity.AddTag("query.threshold_exceeded_ms", duration.TotalMilliseconds - threshold.TotalMilliseconds);
        }
    }

    public void RecordQueryResults(
        string serviceId,
        string layerId,
        string endpointType,
        long recordCount,
        long? estimatedSizeBytes = null)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("service.id", NormalizeServiceId(serviceId)),
            new KeyValuePair<string, object?>("layer.id", NormalizeLayerId(layerId)),
            new KeyValuePair<string, object?>("endpoint.type", NormalizeEndpointType(endpointType)),
            new KeyValuePair<string, object?>("record.count.bucket", GetRecordCountBucket(recordCount))
        };

        _recordCount.Add(recordCount, tags);

        if (estimatedSizeBytes.HasValue)
        {
            var sizeTags = new[]
            {
                new KeyValuePair<string, object?>("service.id", NormalizeServiceId(serviceId)),
                new KeyValuePair<string, object?>("layer.id", NormalizeLayerId(layerId)),
                new KeyValuePair<string, object?>("endpoint.type", NormalizeEndpointType(endpointType)),
                new KeyValuePair<string, object?>("size.bucket", GetSizeBucket(estimatedSizeBytes.Value))
            };

            _resultSize.Record(estimatedSizeBytes.Value, sizeTags);
        }

        // Add activity tags
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.AddTag("query.record_count", recordCount);
            if (estimatedSizeBytes.HasValue)
                activity.AddTag("query.result_size_bytes", estimatedSizeBytes.Value);
        }
    }

    public void RecordQueryError(
        string serviceId,
        string layerId,
        string endpointType,
        string operationType,
        string errorType)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("service.id", NormalizeServiceId(serviceId)),
            new KeyValuePair<string, object?>("layer.id", NormalizeLayerId(layerId)),
            new KeyValuePair<string, object?>("endpoint.type", NormalizeEndpointType(endpointType)),
            new KeyValuePair<string, object?>("operation.type", NormalizeOperationType(operationType)),
            new KeyValuePair<string, object?>("error.type", NormalizeErrorType(errorType))
        };

        _queryErrors.Add(1, tags);

        // Add activity tag
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.AddTag("query.error", errorType);
        }
    }

    public void RecordFilterComplexity(
        string serviceId,
        string layerId,
        string endpointType,
        bool hasFilter,
        bool hasSpatialFilter,
        string filterComplexity)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("service.id", NormalizeServiceId(serviceId)),
            new KeyValuePair<string, object?>("layer.id", NormalizeLayerId(layerId)),
            new KeyValuePair<string, object?>("endpoint.type", NormalizeEndpointType(endpointType)),
            new KeyValuePair<string, object?>("has_filter", hasFilter.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("has_spatial_filter", hasSpatialFilter.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("filter.complexity", NormalizeFilterComplexity(filterComplexity))
        };

        _filterUsage.Add(1, tags);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    // Normalization methods for consistent metric dimensions

    private static string NormalizeServiceId(string? serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            return "unknown";

        // Truncate long service IDs to prevent cardinality explosion
        return serviceId.Length > 50 ? serviceId.Substring(0, 50) : serviceId;
    }

    private static string NormalizeLayerId(string? layerId)
    {
        if (string.IsNullOrWhiteSpace(layerId))
            return "unknown";

        // Truncate long layer IDs to prevent cardinality explosion
        return layerId.Length > 50 ? layerId.Substring(0, 50) : layerId;
    }

    private static string NormalizeEndpointType(string? endpointType)
    {
        if (string.IsNullOrWhiteSpace(endpointType))
            return "unknown";

        return endpointType.ToUpperInvariant() switch
        {
            "WFS" or "WFS-1.0.0" or "WFS-1.1.0" or "WFS-2.0.0" or "WFS-3.0" => "wfs",
            "WMS" or "WMS-1.1.1" or "WMS-1.3.0" => "wms",
            "WMTS" or "WMTS-1.0.0" => "wmts",
            "WCS" or "WCS-1.0.0" or "WCS-2.0.0" => "wcs",
            "OGC-API" or "OGCAPI" or "OGC-API-FEATURES" or "OGC API FEATURES" => "ogc_api_features",
            "OGC-API-TILES" or "OGC API TILES" => "ogc_api_tiles",
            "GEOSERVICES" or "GEOSERVICES-REST" or "ESRI-REST" => "geoservices",
            "STAC" => "stac",
            "CSW" => "csw",
            "ODATA" or "ODATA-V4" => "odata",
            _ => endpointType.ToLowerInvariant().Replace(" ", "_").Replace("-", "_")
        };
    }

    private static string NormalizeOperationType(string? operationType)
    {
        if (string.IsNullOrWhiteSpace(operationType))
            return "unknown";

        return operationType.ToUpperInvariant() switch
        {
            "QUERY" or "GETFEATURE" or "GETFEATURES" or "FEATURES" => "query",
            "COUNT" or "HITS" or "RESULTTYPE=HITS" => "count",
            "STATISTICS" or "STATS" or "AGGREGATE" or "AGGREGATION" => "statistics",
            "DISTINCT" or "UNIQUE_VALUES" or "UNIQUEVALUES" => "distinct",
            "EXTENT" or "BBOX" or "BOUNDINGBOX" or "BOUNDS" => "extent",
            "GET" or "GETFEATUREBYID" => "get",
            "CREATE" or "INSERT" => "create",
            "UPDATE" => "update",
            "DELETE" or "REMOVE" => "delete",
            "TILE" or "MVT" or "VECTORTILE" => "tile",
            _ => operationType.ToLowerInvariant()
        };
    }

    private static string NormalizeErrorType(string? errorType)
    {
        if (string.IsNullOrWhiteSpace(errorType))
            return "unknown";

        var normalized = errorType.ToLowerInvariant();

        if (normalized.Contains("timeout"))
            return "timeout";
        if (normalized.Contains("connection") || normalized.Contains("network"))
            return "connection_error";
        if (normalized.Contains("permission") || normalized.Contains("authorization") || normalized.Contains("forbidden"))
            return "authorization_error";
        if (normalized.Contains("notfound") || normalized.Contains("not_found"))
            return "not_found";
        if (normalized.Contains("validation") || normalized.Contains("invalid"))
            return "validation_error";
        if (normalized.Contains("sql") || normalized.Contains("database"))
            return "database_error";
        if (normalized.Contains("geometry") || normalized.Contains("spatial"))
            return "geometry_error";

        return normalized.Replace("exception", "").Replace("error", "").Trim();
    }

    private static string NormalizeFilterComplexity(string? complexity)
    {
        if (string.IsNullOrWhiteSpace(complexity))
            return "unknown";

        return complexity.ToLowerInvariant() switch
        {
            "simple" or "low" or "basic" => "simple",
            "medium" or "moderate" or "standard" => "medium",
            "complex" or "high" or "advanced" => "complex",
            _ => "unknown"
        };
    }

    private static string GetDurationBucket(TimeSpan duration)
    {
        var ms = duration.TotalMilliseconds;
        return ms switch
        {
            < 50 => "very_fast",      // < 50ms
            < 100 => "fast",          // < 100ms
            < 500 => "medium",        // < 500ms
            < 1000 => "slow",         // < 1s
            < 5000 => "very_slow",    // < 5s
            < 10000 => "critical",    // < 10s
            _ => "extreme"            // >= 10s
        };
    }

    private static string GetRecordCountBucket(long count)
    {
        return count switch
        {
            0 => "empty",
            < 10 => "tiny",
            < 100 => "small",
            < 1000 => "medium",
            < 10000 => "large",
            < 100000 => "very_large",
            _ => "massive"
        };
    }

    private static string GetSizeBucket(long sizeBytes)
    {
        return sizeBytes switch
        {
            < 1024 => "tiny",              // < 1KB
            < 10240 => "small",            // < 10KB
            < 102400 => "medium",          // < 100KB
            < 1048576 => "large",          // < 1MB
            < 10485760 => "very_large",    // < 10MB
            _ => "huge"                    // >= 10MB
        };
    }
}

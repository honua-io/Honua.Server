// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;
using System.Collections.Generic;

namespace Honua.Server.Host.Stac;

/// <summary>
/// OpenTelemetry metrics for STAC API operations.
/// </summary>
public sealed class StacMetrics
{
    private readonly Counter<long> _writeOperationsCounter;
    private readonly Counter<long> _writeOperationErrorsCounter;
    private readonly Histogram<double> _writeOperationDuration;

    // Read operation metrics
    private readonly Counter<long> _readOperationsCounter;
    private readonly Histogram<double> _readOperationDuration;
    private readonly Counter<long> _searchOperationsCounter;
    private readonly Histogram<double> _searchDuration;
    private readonly Histogram<long> _searchResultCount;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;

    public StacMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Server.Stac");

        // Write operation metrics
        _writeOperationsCounter = meter.CreateCounter<long>(
            "stac.write_operations.total",
            unit: "{operation}",
            description: "Total number of STAC write operations");

        _writeOperationErrorsCounter = meter.CreateCounter<long>(
            "stac.write_operations.errors.total",
            unit: "{error}",
            description: "Total number of STAC write operation errors");

        _writeOperationDuration = meter.CreateHistogram<double>(
            "stac.write_operations.duration",
            unit: "ms",
            description: "Duration of STAC write operations in milliseconds");

        // Read operation metrics
        _readOperationsCounter = meter.CreateCounter<long>(
            "stac.read_operations.total",
            unit: "{operation}",
            description: "Total number of STAC read operations");

        _readOperationDuration = meter.CreateHistogram<double>(
            "stac.read_operations.duration",
            unit: "ms",
            description: "Duration of STAC read operations in milliseconds");

        _searchOperationsCounter = meter.CreateCounter<long>(
            "stac.search_operations.total",
            unit: "{search}",
            description: "Total number of STAC search operations");

        _searchDuration = meter.CreateHistogram<double>(
            "stac.search.duration",
            unit: "ms",
            description: "Duration of STAC search operations in milliseconds");

        _searchResultCount = meter.CreateHistogram<long>(
            "stac.search.result_count",
            unit: "{item}",
            description: "Number of items returned by search operations");

        _cacheHits = meter.CreateCounter<long>(
            "stac.cache.hits",
            unit: "{hit}",
            description: "STAC cache hits (ETag/output cache matches)");

        _cacheMisses = meter.CreateCounter<long>(
            "stac.cache.misses",
            unit: "{miss}",
            description: "STAC cache misses");
    }

    /// <summary>
    /// Gets the write operations counter for use with OperationInstrumentation.
    /// </summary>
    public Counter<long> WriteOperationsCounter => _writeOperationsCounter;

    /// <summary>
    /// Gets the write operation errors counter for use with OperationInstrumentation.
    /// </summary>
    public Counter<long> WriteOperationErrorsCounter => _writeOperationErrorsCounter;

    /// <summary>
    /// Gets the write operation duration histogram for use with OperationInstrumentation.
    /// </summary>
    public Histogram<double> WriteOperationDuration => _writeOperationDuration;

    /// <summary>
    /// Gets the read operations counter for use with OperationInstrumentation.
    /// </summary>
    public Counter<long> ReadOperationsCounter => _readOperationsCounter;

    /// <summary>
    /// Gets the read operation duration histogram for use with OperationInstrumentation.
    /// </summary>
    public Histogram<double> ReadOperationDuration => _readOperationDuration;

    public void RecordWriteOperation(string operationType, string resourceType, bool success)
    {
        _writeOperationsCounter.Add(1,
            new("operation", operationType),
            new("resource", resourceType),
            new("success", success.ToString().ToLowerInvariant()));
    }

    public void RecordWriteError(string operationType, string resourceType, string errorType)
    {
        _writeOperationErrorsCounter.Add(1,
            new("operation", operationType),
            new("resource", resourceType),
            new("error_type", errorType));
    }

    public void RecordWriteDuration(string operationType, string resourceType, double durationMs)
    {
        _writeOperationDuration.Record(durationMs,
            new("operation", operationType),
            new("resource", resourceType));
    }

    /// <summary>
    /// Records a successful write operation with both operation counter and duration.
    /// Convenience method that combines RecordWriteOperation(success: true) and RecordWriteDuration.
    /// </summary>
    /// <param name="operationType">The type of operation (e.g., "post", "put", "delete").</param>
    /// <param name="resourceType">The resource type (e.g., "collection", "item").</param>
    /// <param name="durationMs">Duration of the operation in milliseconds.</param>
    public void RecordWriteSuccess(string operationType, string resourceType, double durationMs)
    {
        RecordWriteOperation(operationType, resourceType, success: true);
        RecordWriteDuration(operationType, resourceType, durationMs);
    }

    /// <summary>
    /// Records a STAC read operation (get_collection, get_item, list_collections, list_items).
    /// </summary>
    /// <param name="operation">The type of read operation (e.g., "get_collection", "get_item", "list_collections", "list_items").</param>
    /// <param name="resource">The resource type (e.g., "collection", "item").</param>
    /// <param name="durationMs">Duration of the operation in milliseconds.</param>
    /// <param name="success">Whether the operation succeeded (found resource vs. not found).</param>
    public void RecordReadOperation(string operation, string resource, double durationMs, bool success)
    {
        _readOperationsCounter.Add(1,
            new("operation", operation),
            new("resource", resource),
            new("success", success.ToString().ToLowerInvariant()));

        _readOperationDuration.Record(durationMs,
            new("operation", operation),
            new("resource", resource),
            new("success", success.ToString().ToLowerInvariant()));
    }

    /// <summary>
    /// Records a STAC search operation with detailed search parameters.
    /// </summary>
    /// <param name="durationMs">Duration of the search in milliseconds.</param>
    /// <param name="resultCount">Number of items returned in the search results.</param>
    /// <param name="collectionCount">Number of collections searched (0 means all collections).</param>
    /// <param name="hasBbox">Whether a spatial bounding box filter was applied.</param>
    /// <param name="hasDatetime">Whether a temporal datetime filter was applied.</param>
    public void RecordSearch(double durationMs, int resultCount, int collectionCount, bool hasBbox, bool hasDatetime)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("collection_count", collectionCount),
            new("has_bbox", hasBbox.ToString().ToLowerInvariant()),
            new("has_datetime", hasDatetime.ToString().ToLowerInvariant())
        };

        _searchOperationsCounter.Add(1, tags);
        _searchDuration.Record(durationMs, tags);
        _searchResultCount.Record(resultCount, tags);
    }

    /// <summary>
    /// Records a cache hit for STAC resources (ETag match or output cache hit).
    /// </summary>
    /// <param name="resource">The resource type (e.g., "collection", "item").</param>
    public void RecordCacheHit(string resource)
    {
        _cacheHits.Add(1, new KeyValuePair<string, object?>("resource", resource));
    }

    /// <summary>
    /// Records a cache miss for STAC resources (ETag mismatch or output cache miss).
    /// </summary>
    /// <param name="resource">The resource type (e.g., "collection", "item").</param>
    public void RecordCacheMiss(string resource)
    {
        _cacheMisses.Add(1, new KeyValuePair<string, object?>("resource", resource));
    }
}

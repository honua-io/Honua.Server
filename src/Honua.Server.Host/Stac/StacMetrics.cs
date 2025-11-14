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
    private readonly Counter<long> writeOperationsCounter;
    private readonly Counter<long> writeOperationErrorsCounter;
    private readonly Histogram<double> writeOperationDuration;

    // Read operation metrics
    private readonly Counter<long> readOperationsCounter;
    private readonly Histogram<double> readOperationDuration;
    private readonly Counter<long> searchOperationsCounter;
    private readonly Histogram<double> searchDuration;
    private readonly Histogram<long> searchResultCount;
    private readonly Counter<long> cacheHits;
    private readonly Counter<long> cacheMisses;

    public StacMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Server.Stac");

        // Write operation metrics
        this.writeOperationsCounter = meter.CreateCounter<long>(
            "stac.write_operations.total",
            unit: "{operation}",
            description: "Total number of STAC write operations");

        this.writeOperationErrorsCounter = meter.CreateCounter<long>(
            "stac.write_operations.errors.total",
            unit: "{error}",
            description: "Total number of STAC write operation errors");

        this.writeOperationDuration = meter.CreateHistogram<double>(
            "stac.write_operations.duration",
            unit: "ms",
            description: "Duration of STAC write operations in milliseconds");

        // Read operation metrics
        this.readOperationsCounter = meter.CreateCounter<long>(
            "stac.read_operations.total",
            unit: "{operation}",
            description: "Total number of STAC read operations");

        this.readOperationDuration = meter.CreateHistogram<double>(
            "stac.read_operations.duration",
            unit: "ms",
            description: "Duration of STAC read operations in milliseconds");

        this.searchOperationsCounter = meter.CreateCounter<long>(
            "stac.search_operations.total",
            unit: "{search}",
            description: "Total number of STAC search operations");

        this.searchDuration = meter.CreateHistogram<double>(
            "stac.search.duration",
            unit: "ms",
            description: "Duration of STAC search operations in milliseconds");

        this.searchResultCount = meter.CreateHistogram<long>(
            "stac.search.result_count",
            unit: "{item}",
            description: "Number of items returned by search operations");

        this.cacheHits = meter.CreateCounter<long>(
            "stac.cache.hits",
            unit: "{hit}",
            description: "STAC cache hits (ETag/output cache matches)");

        this.cacheMisses = meter.CreateCounter<long>(
            "stac.cache.misses",
            unit: "{miss}",
            description: "STAC cache misses");
    }

    /// <summary>
    /// Gets the write operations counter for use with OperationInstrumentation.
    /// </summary>
    public Counter<long> WriteOperationsCounter => this.writeOperationsCounter;

    /// <summary>
    /// Gets the write operation errors counter for use with OperationInstrumentation.
    /// </summary>
    public Counter<long> WriteOperationErrorsCounter => this.writeOperationErrorsCounter;

    /// <summary>
    /// Gets the write operation duration histogram for use with OperationInstrumentation.
    /// </summary>
    public Histogram<double> WriteOperationDuration => this.writeOperationDuration;

    /// <summary>
    /// Gets the read operations counter for use with OperationInstrumentation.
    /// </summary>
    public Counter<long> ReadOperationsCounter => this.readOperationsCounter;

    /// <summary>
    /// Gets the read operation duration histogram for use with OperationInstrumentation.
    /// </summary>
    public Histogram<double> ReadOperationDuration => this.readOperationDuration;

    public void RecordWriteOperation(string operationType, string resourceType, bool success)
    {
        this.writeOperationsCounter.Add(1,
            new("operation", operationType),
            new("resource", resourceType),
            new("success", success.ToString().ToLowerInvariant()));
    }

    public void RecordWriteError(string operationType, string resourceType, string errorType)
    {
        this.writeOperationErrorsCounter.Add(1,
            new("operation", operationType),
            new("resource", resourceType),
            new("error_type", errorType));
    }

    public void RecordWriteDuration(string operationType, string resourceType, double durationMs)
    {
        this.writeOperationDuration.Record(durationMs,
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
        this.readOperationsCounter.Add(1,
            new("operation", operation),
            new("resource", resource),
            new("success", success.ToString().ToLowerInvariant()));

        this.readOperationDuration.Record(durationMs,
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

        this.searchOperationsCounter.Add(1, tags);
        this.searchDuration.Record(durationMs, tags);
        this.searchResultCount.Record(resultCount, tags);
    }

    /// <summary>
    /// Records a cache hit for STAC resources (ETag match or output cache hit).
    /// </summary>
    /// <param name="resource">The resource type (e.g., "collection", "item").</param>
    public void RecordCacheHit(string resource)
    {
        this.cacheHits.Add(1, new KeyValuePair<string, object?>("resource", resource));
    }

    /// <summary>
    /// Records a cache miss for STAC resources (ETag mismatch or output cache miss).
    /// </summary>
    /// <param name="resource">The resource type (e.g., "collection", "item").</param>
    public void RecordCacheMiss(string resource)
    {
        this.cacheMisses.Add(1, new KeyValuePair<string, object?>("resource", resource));
    }
}

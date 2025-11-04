// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Stac;

/// <summary>
/// Metrics for STAC catalog operations.
/// </summary>
internal static class StacMetrics
{
    private const string MeterName = "Honua.Stac";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Activity source for distributed tracing.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(MeterName, "1.0.0");

    /// <summary>
    /// Counter for bulk upsert operations.
    /// </summary>
    public static readonly Counter<long> BulkUpsertCount = Meter.CreateCounter<long>(
        "honua.stac.bulk_upsert.count",
        description: "Number of bulk upsert operations");

    /// <summary>
    /// Counter for items processed in bulk operations.
    /// </summary>
    public static readonly Counter<long> BulkUpsertItemsCount = Meter.CreateCounter<long>(
        "honua.stac.bulk_upsert.items",
        description: "Number of items processed in bulk upsert operations");

    /// <summary>
    /// Histogram for bulk upsert duration.
    /// </summary>
    public static readonly Histogram<double> BulkUpsertDuration = Meter.CreateHistogram<double>(
        "honua.stac.bulk_upsert.duration",
        unit: "ms",
        description: "Duration of bulk upsert operations in milliseconds");

    /// <summary>
    /// Histogram for bulk upsert throughput.
    /// </summary>
    public static readonly Histogram<double> BulkUpsertThroughput = Meter.CreateHistogram<double>(
        "honua.stac.bulk_upsert.throughput",
        unit: "items/s",
        description: "Throughput of bulk upsert operations in items per second");

    /// <summary>
    /// Counter for bulk upsert failures.
    /// </summary>
    public static readonly Counter<long> BulkUpsertFailures = Meter.CreateCounter<long>(
        "honua.stac.bulk_upsert.failures",
        description: "Number of failed items in bulk upsert operations");

    /// <summary>
    /// Counter for search operations.
    /// </summary>
    public static readonly Counter<long> SearchCount = Meter.CreateCounter<long>(
        "honua.stac.search.count",
        description: "Number of STAC search operations");

    /// <summary>
    /// Histogram for search duration.
    /// </summary>
    public static readonly Histogram<double> SearchDuration = Meter.CreateHistogram<double>(
        "honua.stac.search.duration",
        unit: "ms",
        description: "Duration of STAC search operations in milliseconds");

    /// <summary>
    /// Histogram for search COUNT query duration.
    /// </summary>
    public static readonly Histogram<double> SearchCountDuration = Meter.CreateHistogram<double>(
        "honua.stac.search.count_duration",
        unit: "ms",
        description: "Duration of STAC search COUNT queries in milliseconds");

    /// <summary>
    /// Counter for COUNT query timeouts.
    /// </summary>
    public static readonly Counter<long> SearchCountTimeouts = Meter.CreateCounter<long>(
        "honua.stac.search.count_timeouts",
        description: "Number of STAC search COUNT queries that timed out");

    /// <summary>
    /// Counter for COUNT estimations used.
    /// </summary>
    public static readonly Counter<long> SearchCountEstimations = Meter.CreateCounter<long>(
        "honua.stac.search.count_estimations",
        description: "Number of times STAC search used count estimation instead of exact count");

    /// <summary>
    /// Counter for batch collection fetch operations.
    /// </summary>
    public static readonly Counter<long> CollectionBatchFetchCount = Meter.CreateCounter<long>(
        "honua.stac.collection.batch_fetch.count",
        description: "Number of batch collection fetch operations");

    /// <summary>
    /// Histogram for batch collection fetch size.
    /// </summary>
    public static readonly Histogram<long> CollectionBatchFetchSize = Meter.CreateHistogram<long>(
        "honua.stac.collection.batch_fetch.size",
        unit: "collections",
        description: "Number of collections requested in batch fetch operations");
}

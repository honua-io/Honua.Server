using System;
using Honua.Server.Core.Observability;
using Honua.Server.Host.Raster;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// No-op implementation of IRasterTileCacheMetrics for testing.
/// All metric recording operations are ignored (no-op).
/// </summary>
/// <remarks>
/// Use this stub when you need to satisfy IRasterTileCacheMetrics dependencies
/// but don't need actual metric collection during tests.
/// </remarks>
public sealed class NullRasterTileCacheMetrics : IRasterTileCacheMetrics
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly NullRasterTileCacheMetrics Instance = new();

    private NullRasterTileCacheMetrics()
    {
    }

    /// <summary>
    /// No-op. Does not record cache hits.
    /// </summary>
    public void RecordCacheHit(string datasetId, string? variant = null, string? timeSlice = null) { }

    /// <summary>
    /// No-op. Does not record cache misses.
    /// </summary>
    public void RecordCacheMiss(string datasetId, string? variant = null, string? timeSlice = null) { }

    /// <summary>
    /// No-op. Does not record render latency.
    /// </summary>
    public void RecordRenderLatency(string datasetId, TimeSpan duration, bool fromPreseed) { }

    /// <summary>
    /// No-op. Does not record preseed job completion.
    /// </summary>
    public void RecordPreseedJobCompleted(RasterTilePreseedJobSnapshot snapshot) { }

    /// <summary>
    /// No-op. Does not record preseed job failure.
    /// </summary>
    public void RecordPreseedJobFailed(Guid jobId, string? message) { }

    /// <summary>
    /// No-op. Does not record preseed job cancellation.
    /// </summary>
    public void RecordPreseedJobCancelled(Guid jobId) { }

    /// <summary>
    /// No-op. Does not record cache purge operations.
    /// </summary>
    public void RecordCachePurge(string datasetId, bool succeeded) { }
}

/// <summary>
/// No-op implementation of IApiMetrics for testing.
/// All metric recording operations are ignored (no-op).
/// </summary>
/// <remarks>
/// Use this stub when you need to satisfy IApiMetrics dependencies
/// but don't need actual metric collection during tests.
/// </remarks>
public sealed class NullApiMetrics : IApiMetrics
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly NullApiMetrics Instance = new();

    private NullApiMetrics()
    {
    }

    /// <summary>
    /// No-op. Does not record requests.
    /// </summary>
    public void RecordRequest(string apiProtocol, string? serviceId, string? layerId) { }

    /// <summary>
    /// No-op. Does not record request duration.
    /// </summary>
    public void RecordRequestDuration(string apiProtocol, string? serviceId, string? layerId, TimeSpan duration, int statusCode) { }

    /// <summary>
    /// No-op. Does not record errors.
    /// </summary>
    public void RecordError(string apiProtocol, string? serviceId, string? layerId, string errorType) { }

    /// <summary>
    /// No-op. Does not record errors.
    /// </summary>
    public void RecordError(string apiProtocol, string? serviceId, string? layerId, Exception exception, string? additionalContext = null) { }

    /// <summary>
    /// No-op. Does not record feature counts.
    /// </summary>
    public void RecordFeatureCount(string apiProtocol, string? serviceId, string? layerId, long count) { }

    /// <summary>
    /// No-op. Does not record HTTP requests.
    /// </summary>
    public void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration) { }

    /// <summary>
    /// No-op. Does not record HTTP errors.
    /// </summary>
    public void RecordHttpError(string method, string endpoint, int statusCode, string errorType) { }

    /// <summary>
    /// No-op. Does not record rate limit hits.
    /// </summary>
    public void RecordRateLimitHit(string endpoint, string clientIp) { }
}

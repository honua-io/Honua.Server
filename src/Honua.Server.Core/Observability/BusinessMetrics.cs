// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// OpenTelemetry metrics for business-level operations.
/// Tracks features served, data ingestion, STAC searches, and user activity.
/// </summary>
public interface IBusinessMetrics
{
    void RecordFeaturesServed(string layerId, long featureCount, string protocol);
    void RecordRasterTilesServed(string datasetId, int zoom, long tileCount);
    void RecordVectorTilesServed(string layerId, int zoom, long tileCount);
    void RecordDataIngestion(string sourceType, long recordCount, TimeSpan duration);
    void RecordStacSearch(int resultCount, TimeSpan duration, bool usesBbox, bool usesDatetime);
    void RecordStacCatalogAccess(string collectionId, string itemId);
    void RecordExport(string format, long featureCount, TimeSpan duration, long? sizeBytes = null);
    void RecordActiveSession(string sessionId, string? userId = null);
    void RecordSessionEnded(string sessionId, TimeSpan duration);
    void RecordDatasetAccess(string datasetId, string accessType);
}

/// <summary>
/// Implementation of business metrics using OpenTelemetry.
/// </summary>
public sealed class BusinessMetrics : IBusinessMetrics, IDisposable
{
    private readonly Meter _meter;

    // Counters
    private readonly Counter<long> _featuresServed;
    private readonly Counter<long> _rasterTilesServed;
    private readonly Counter<long> _vectorTilesServed;
    private readonly Counter<long> _dataIngestions;
    private readonly Counter<long> _stacSearches;
    private readonly Counter<long> _stacCatalogAccess;
    private readonly Counter<long> _exports;
    private readonly Counter<long> _datasetAccesses;

    // Histograms
    private readonly Histogram<double> _ingestionDuration;
    private readonly Histogram<long> _ingestionRecordCount;
    private readonly Histogram<double> _stacSearchDuration;
    private readonly Histogram<long> _stacSearchResults;
    private readonly Histogram<double> _exportDuration;
    private readonly Histogram<long> _exportSize;
    private readonly Histogram<double> _sessionDuration;

    // Active session tracking
    private readonly ConcurrentDictionary<string, SessionInfo> _activeSessions;

    public BusinessMetrics()
    {
        _meter = new Meter("Honua.Server.Business", "1.0.0");
        _activeSessions = new ConcurrentDictionary<string, SessionInfo>();

        // Create counters
        _featuresServed = _meter.CreateCounter<long>(
            "honua.business.features_served",
            unit: "{feature}",
            description: "Number of features served to clients");

        _rasterTilesServed = _meter.CreateCounter<long>(
            "honua.business.raster_tiles_served",
            unit: "{tile}",
            description: "Number of raster tiles served to clients");

        _vectorTilesServed = _meter.CreateCounter<long>(
            "honua.business.vector_tiles_served",
            unit: "{tile}",
            description: "Number of vector tiles served to clients");

        _dataIngestions = _meter.CreateCounter<long>(
            "honua.business.data_ingestions",
            unit: "{ingestion}",
            description: "Number of data ingestion operations");

        _stacSearches = _meter.CreateCounter<long>(
            "honua.business.stac_searches",
            unit: "{search}",
            description: "Number of STAC catalog searches");

        _stacCatalogAccess = _meter.CreateCounter<long>(
            "honua.business.stac_catalog_access",
            unit: "{access}",
            description: "Number of STAC catalog item accesses");

        _exports = _meter.CreateCounter<long>(
            "honua.business.exports",
            unit: "{export}",
            description: "Number of data export operations");

        _datasetAccesses = _meter.CreateCounter<long>(
            "honua.business.dataset_accesses",
            unit: "{access}",
            description: "Number of dataset access operations");

        // Create histograms
        _ingestionDuration = _meter.CreateHistogram<double>(
            "honua.business.ingestion_duration",
            unit: "ms",
            description: "Data ingestion operation duration");

        _ingestionRecordCount = _meter.CreateHistogram<long>(
            "honua.business.ingestion_record_count",
            unit: "{record}",
            description: "Number of records ingested per operation");

        _stacSearchDuration = _meter.CreateHistogram<double>(
            "honua.business.stac_search_duration",
            unit: "ms",
            description: "STAC search operation duration");

        _stacSearchResults = _meter.CreateHistogram<long>(
            "honua.business.stac_search_results",
            unit: "{result}",
            description: "Number of results returned from STAC searches");

        _exportDuration = _meter.CreateHistogram<double>(
            "honua.business.export_duration",
            unit: "ms",
            description: "Data export operation duration");

        _exportSize = _meter.CreateHistogram<long>(
            "honua.business.export_size",
            unit: "bytes",
            description: "Size of exported data");

        _sessionDuration = _meter.CreateHistogram<double>(
            "honua.business.session_duration",
            unit: "ms",
            description: "User session duration");

        // Observable gauge for active sessions
        _meter.CreateObservableGauge(
            "honua.business.active_sessions",
            () => _activeSessions.Count,
            unit: "{session}",
            description: "Number of currently active user sessions");

        // Observable gauge for features per second
        _meter.CreateObservableGauge(
            "honua.business.throughput.features_per_second",
            () => CalculateFeaturesPerSecond(),
            unit: "{feature}/s",
            description: "Features served per second (1-minute moving average)");
    }

    public void RecordFeaturesServed(string layerId, long featureCount, string protocol)
    {
        _featuresServed.Add(featureCount,
            new("layer.id", Normalize(layerId)),
            new("protocol", NormalizeProtocol(protocol)),
            new("feature.count.bucket", GetFeatureCountBucket(featureCount)));
    }

    public void RecordRasterTilesServed(string datasetId, int zoom, long tileCount)
    {
        _rasterTilesServed.Add(tileCount,
            new("dataset.id", Normalize(datasetId)),
            new("zoom.level", zoom.ToString()),
            new("zoom.bucket", GetZoomBucket(zoom)));
    }

    public void RecordVectorTilesServed(string layerId, int zoom, long tileCount)
    {
        _vectorTilesServed.Add(tileCount,
            new("layer.id", Normalize(layerId)),
            new("zoom.level", zoom.ToString()),
            new("zoom.bucket", GetZoomBucket(zoom)));
    }

    public void RecordDataIngestion(string sourceType, long recordCount, TimeSpan duration)
    {
        _dataIngestions.Add(1,
            new("source.type", NormalizeSourceType(sourceType)),
            new("record.count.bucket", GetRecordCountBucket(recordCount)));

        _ingestionDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>[] { new("source.type", NormalizeSourceType(sourceType)) });

        _ingestionRecordCount.Record(recordCount,
            new KeyValuePair<string, object?>[] { new("source.type", NormalizeSourceType(sourceType)) });
    }

    public void RecordStacSearch(int resultCount, TimeSpan duration, bool usesBbox, bool usesDatetime)
    {
        _stacSearches.Add(1,
            new("uses.bbox", usesBbox.ToString()),
            new("uses.datetime", usesDatetime.ToString()),
            new("result.count.bucket", GetResultCountBucket(resultCount)));

        _stacSearchDuration.Record(duration.TotalMilliseconds,
            new("uses.bbox", usesBbox.ToString()),
            new("uses.datetime", usesDatetime.ToString()));

        _stacSearchResults.Record(resultCount,
            new("uses.bbox", usesBbox.ToString()),
            new("uses.datetime", usesDatetime.ToString()));
    }

    public void RecordStacCatalogAccess(string collectionId, string itemId)
    {
        _stacCatalogAccess.Add(1,
            new("collection.id", Normalize(collectionId)),
            new("has.item", (!string.IsNullOrWhiteSpace(itemId)).ToString()));
    }

    public void RecordExport(string format, long featureCount, TimeSpan duration, long? sizeBytes = null)
    {
        _exports.Add(1,
            new("export.format", NormalizeExportFormat(format)),
            new("feature.count.bucket", GetFeatureCountBucket(featureCount)));

        _exportDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>[] { new("export.format", NormalizeExportFormat(format)) });

        if (sizeBytes.HasValue)
        {
            _exportSize.Record(sizeBytes.Value,
                new("export.format", NormalizeExportFormat(format)),
                new("size.bucket", GetSizeBucket(sizeBytes.Value)));
        }
    }

    public void RecordActiveSession(string sessionId, string? userId = null)
    {
        var sessionInfo = new SessionInfo
        {
            SessionId = sessionId,
            UserId = userId,
            StartTime = DateTimeOffset.UtcNow
        };

        _activeSessions.TryAdd(sessionId, sessionInfo);
    }

    public void RecordSessionEnded(string sessionId, TimeSpan duration)
    {
        _activeSessions.TryRemove(sessionId, out _);

        _sessionDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>[] { new("duration.bucket", GetSessionDurationBucket(duration)) });
    }

    public void RecordDatasetAccess(string datasetId, string accessType)
    {
        _datasetAccesses.Add(1,
            new("dataset.id", Normalize(datasetId)),
            new("access.type", NormalizeAccessType(accessType)));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private static string NormalizeProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
            return "unknown";

        return protocol.ToLowerInvariant() switch
        {
            "wfs" or "web feature service" => "wfs",
            "ogc-api-features" or "oapif" => "ogc-api-features",
            "esri-rest" or "geoservices" => "esri-rest",
            "odata" => "odata",
            _ => protocol.ToLowerInvariant()
        };
    }

    private static string NormalizeSourceType(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            return "unknown";

        return sourceType.ToLowerInvariant() switch
        {
            var s when s.Contains("geojson") => "geojson",
            var s when s.Contains("shapefile") => "shapefile",
            var s when s.Contains("geopackage") || s.Contains("gpkg") => "geopackage",
            var s when s.Contains("csv") => "csv",
            var s when s.Contains("kml") => "kml",
            var s when s.Contains("gml") => "gml",
            var s when s.Contains("postgis") || s.Contains("postgres") => "postgis",
            _ => sourceType.ToLowerInvariant()
        };
    }

    private static string NormalizeExportFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return "unknown";

        return format.ToLowerInvariant() switch
        {
            "geojson" or "json" => "geojson",
            "shapefile" or "shp" => "shapefile",
            "geopackage" or "gpkg" => "geopackage",
            "csv" => "csv",
            "kml" => "kml",
            "gml" => "gml",
            "pdf" => "pdf",
            _ => format.ToLowerInvariant()
        };
    }

    private static string NormalizeAccessType(string? accessType)
    {
        if (string.IsNullOrWhiteSpace(accessType))
            return "unknown";

        return accessType.ToLowerInvariant() switch
        {
            "read" or "query" or "view" => "read",
            "metadata" => "metadata",
            "tile" => "tile",
            "export" => "export",
            _ => accessType.ToLowerInvariant()
        };
    }

    private static string GetFeatureCountBucket(long count)
    {
        return count switch
        {
            0 => "empty",
            <= 10 => "tiny",
            <= 100 => "small",
            <= 1000 => "medium",
            <= 10000 => "large",
            _ => "very_large"
        };
    }

    private static string GetRecordCountBucket(long count)
    {
        return count switch
        {
            <= 100 => "small",
            <= 1000 => "medium",
            <= 10000 => "large",
            <= 100000 => "very_large",
            _ => "massive"
        };
    }

    private static string GetResultCountBucket(int count)
    {
        return count switch
        {
            0 => "empty",
            <= 10 => "small",
            <= 50 => "medium",
            <= 100 => "large",
            _ => "very_large"
        };
    }

    private static string GetZoomBucket(int zoom)
    {
        return zoom switch
        {
            <= 5 => "low",
            <= 10 => "medium",
            <= 15 => "high",
            _ => "very_high"
        };
    }

    private static string GetSizeBucket(long sizeBytes)
    {
        return sizeBytes switch
        {
            < 1024 => "tiny",
            < 102400 => "small",          // < 100KB
            < 1048576 => "medium",         // < 1MB
            < 10485760 => "large",         // < 10MB
            _ => "very_large"              // >= 10MB
        };
    }

    private static string GetSessionDurationBucket(TimeSpan duration)
    {
        var minutes = duration.TotalMinutes;
        return minutes switch
        {
            < 5 => "very_short",
            < 30 => "short",
            < 120 => "normal",
            < 480 => "long",
            _ => "very_long"
        };
    }

    private int CalculateFeaturesPerSecond()
    {
        // This is a placeholder - in a real implementation, you would
        // track features served over a rolling time window
        return 0;
    }

    private class SessionInfo
    {
        public required string SessionId { get; init; }
        public string? UserId { get; init; }
        public DateTimeOffset StartTime { get; init; }
    }
}

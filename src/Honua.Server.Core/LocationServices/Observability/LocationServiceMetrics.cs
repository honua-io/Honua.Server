// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics.Metrics;

namespace Honua.Server.Core.LocationServices.Observability;

/// <summary>
/// Provides centralized metrics instrumentation for all location services.
/// Uses .NET 8+ System.Diagnostics.Metrics API with OpenTelemetry integration.
/// </summary>
public class LocationServiceMetrics
{
    private const string MeterName = "Honua.LocationServices";

    // Request metrics
    private readonly Counter<long> _requestCount;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _errorCount;

    // Cache metrics
    private readonly Counter<long> _cacheOperations;

    // Geocoding-specific metrics
    private readonly Histogram<double> _geocodingConfidence;
    private readonly Counter<long> _geocodingResultCount;

    // Routing-specific metrics
    private readonly Histogram<double> _routeDistance;
    private readonly Histogram<double> _routeDuration;
    private readonly Histogram<int> _waypointCount;

    // Basemap tile-specific metrics
    private readonly Counter<long> _tileRequests;
    private readonly Histogram<long> _tileSize;
    private readonly Counter<long> _tileFormatCounter;

    // Provider health metrics
    private readonly ObservableGauge<int> _providerHealth;
    private int _geocodingProviderHealthy = 1;
    private int _routingProviderHealthy = 1;
    private int _basemapProviderHealthy = 1;

    public LocationServiceMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        // Request metrics
        _requestCount = meter.CreateCounter<long>(
            "location_service.requests.total",
            unit: "{request}",
            description: "Total number of location service requests");

        _requestDuration = meter.CreateHistogram<double>(
            "location_service.request.duration",
            unit: "ms",
            description: "Duration of location service requests in milliseconds",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
            });

        _errorCount = meter.CreateCounter<long>(
            "location_service.errors.total",
            unit: "{error}",
            description: "Total number of location service errors");

        // Cache metrics
        _cacheOperations = meter.CreateCounter<long>(
            "location_service.cache.operations.total",
            unit: "{operation}",
            description: "Total number of cache operations");

        // Geocoding-specific metrics
        _geocodingConfidence = meter.CreateHistogram<double>(
            "location_service.geocoding.confidence",
            description: "Confidence score distribution for geocoding results",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0]
            });

        _geocodingResultCount = meter.CreateCounter<long>(
            "location_service.geocoding.results.total",
            unit: "{result}",
            description: "Total number of geocoding results returned");

        // Routing-specific metrics
        _routeDistance = meter.CreateHistogram<double>(
            "location_service.routing.distance",
            unit: "m",
            description: "Distribution of calculated route distances in meters",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [100, 500, 1000, 5000, 10000, 25000, 50000, 100000, 250000, 500000]
            });

        _routeDuration = meter.CreateHistogram<double>(
            "location_service.routing.duration",
            unit: "s",
            description: "Distribution of calculated route durations in seconds",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [60, 300, 600, 1800, 3600, 7200, 14400, 28800, 43200, 86400]
            });

        _waypointCount = meter.CreateHistogram<int>(
            "location_service.routing.waypoints",
            unit: "{waypoint}",
            description: "Distribution of waypoint counts in routing requests",
            advice: new InstrumentAdvice<int>
            {
                HistogramBucketBoundaries = [2, 3, 4, 5, 10, 15, 20, 25, 50, 100]
            });

        // Basemap tile-specific metrics
        _tileRequests = meter.CreateCounter<long>(
            "location_service.tiles.requests.total",
            unit: "{request}",
            description: "Total number of tile requests");

        _tileSize = meter.CreateHistogram<long>(
            "location_service.tiles.size",
            unit: "By",
            description: "Distribution of tile sizes in bytes",
            advice: new InstrumentAdvice<long>
            {
                HistogramBucketBoundaries = [1024, 5120, 10240, 51200, 102400, 256000, 512000, 1048576, 2097152, 5242880]
            });

        _tileFormatCounter = meter.CreateCounter<long>(
            "location_service.tiles.format.total",
            unit: "{tile}",
            description: "Total number of tiles by format type");

        // Provider health gauge
        _providerHealth = meter.CreateObservableGauge(
            "location_service.provider.health",
            () =>
            [
                new Measurement<int>(_geocodingProviderHealthy,
                    new KeyValuePair<string, object?>("provider_type", "geocoding")),
                new Measurement<int>(_routingProviderHealthy,
                    new KeyValuePair<string, object?>("provider_type", "routing")),
                new Measurement<int>(_basemapProviderHealthy,
                    new KeyValuePair<string, object?>("provider_type", "basemap"))
            ],
            unit: "{status}",
            description: "Health status of location service providers (1=healthy, 0=unhealthy)");
    }

    /// <summary>
    /// Records a location service request.
    /// </summary>
    /// <param name="providerType">Type of provider (geocoding, routing, basemap).</param>
    /// <param name="providerKey">Provider identifier (azure-maps, nominatim, etc.).</param>
    /// <param name="operation">Operation type (geocode, reverse_geocode, calculate_route, get_tile).</param>
    /// <param name="success">Whether the request succeeded.</param>
    public void RecordRequest(string providerType, string providerKey, string operation, bool success)
    {
        _requestCount.Add(1,
            new KeyValuePair<string, object?>("provider_type", providerType),
            new KeyValuePair<string, object?>("provider", providerKey),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", success ? "success" : "error"));
    }

    /// <summary>
    /// Records the duration of a location service request.
    /// </summary>
    /// <param name="providerType">Type of provider (geocoding, routing, basemap).</param>
    /// <param name="providerKey">Provider identifier (azure-maps, nominatim, etc.).</param>
    /// <param name="operation">Operation type (geocode, reverse_geocode, calculate_route, get_tile).</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    public void RecordRequestDuration(string providerType, string providerKey, string operation, double durationMs)
    {
        _requestDuration.Record(durationMs,
            new KeyValuePair<string, object?>("provider_type", providerType),
            new KeyValuePair<string, object?>("provider", providerKey),
            new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    /// Records an error in a location service request.
    /// </summary>
    /// <param name="providerType">Type of provider (geocoding, routing, basemap).</param>
    /// <param name="providerKey">Provider identifier (azure-maps, nominatim, etc.).</param>
    /// <param name="operation">Operation type (geocode, reverse_geocode, calculate_route, get_tile).</param>
    /// <param name="errorType">Type of error (timeout, http_error, validation_error, etc.).</param>
    public void RecordError(string providerType, string providerKey, string operation, string errorType)
    {
        _errorCount.Add(1,
            new KeyValuePair<string, object?>("provider_type", providerType),
            new KeyValuePair<string, object?>("provider", providerKey),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Records a cache operation (hit or miss).
    /// </summary>
    /// <param name="providerType">Type of provider (geocoding, routing, basemap).</param>
    /// <param name="operation">Operation type (geocode, reverse_geocode, calculate_route, get_tile).</param>
    /// <param name="result">Cache operation result (hit, miss).</param>
    public void RecordCacheOperation(string providerType, string operation, string result)
    {
        _cacheOperations.Add(1,
            new KeyValuePair<string, object?>("provider_type", providerType),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", result));
    }

    /// <summary>
    /// Records geocoding confidence scores.
    /// </summary>
    /// <param name="providerKey">Provider identifier.</param>
    /// <param name="confidence">Confidence score (0-1).</param>
    public void RecordGeocodingConfidence(string providerKey, double confidence)
    {
        _geocodingConfidence.Record(confidence,
            new KeyValuePair<string, object?>("provider", providerKey));
    }

    /// <summary>
    /// Records the number of geocoding results returned.
    /// </summary>
    /// <param name="providerKey">Provider identifier.</param>
    /// <param name="resultCount">Number of results.</param>
    /// <param name="operationType">Type of geocoding operation (forward, reverse).</param>
    public void RecordGeocodingResults(string providerKey, int resultCount, string operationType)
    {
        _geocodingResultCount.Add(resultCount,
            new KeyValuePair<string, object?>("provider", providerKey),
            new KeyValuePair<string, object?>("operation_type", operationType));
    }

    /// <summary>
    /// Records route distance metrics.
    /// </summary>
    /// <param name="providerKey">Provider identifier.</param>
    /// <param name="distanceMeters">Route distance in meters.</param>
    /// <param name="travelMode">Travel mode (car, truck, bicycle, pedestrian).</param>
    public void RecordRouteDistance(string providerKey, double distanceMeters, string travelMode)
    {
        _routeDistance.Record(distanceMeters,
            new KeyValuePair<string, object?>("provider", providerKey),
            new KeyValuePair<string, object?>("travel_mode", travelMode));
    }

    /// <summary>
    /// Records route duration metrics.
    /// </summary>
    /// <param name="providerKey">Provider identifier.</param>
    /// <param name="durationSeconds">Route duration in seconds.</param>
    /// <param name="travelMode">Travel mode (car, truck, bicycle, pedestrian).</param>
    /// <param name="withTraffic">Whether traffic was considered.</param>
    public void RecordRouteDuration(string providerKey, double durationSeconds, string travelMode, bool withTraffic)
    {
        _routeDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("provider", providerKey),
            new KeyValuePair<string, object?>("travel_mode", travelMode),
            new KeyValuePair<string, object?>("with_traffic", withTraffic));
    }

    /// <summary>
    /// Records waypoint count distribution.
    /// </summary>
    /// <param name="providerKey">Provider identifier.</param>
    /// <param name="waypointCount">Number of waypoints in the route.</param>
    public void RecordWaypointCount(string providerKey, int waypointCount)
    {
        _waypointCount.Record(waypointCount,
            new KeyValuePair<string, object?>("provider", providerKey));
    }

    /// <summary>
    /// Records a tile request.
    /// </summary>
    /// <param name="providerKey">Provider identifier.</param>
    /// <param name="tilesetId">Tileset identifier.</param>
    /// <param name="zoomLevel">Zoom level of the tile.</param>
    /// <param name="format">Tile format (raster, vector).</param>
    public void RecordTileRequest(string providerKey, string tilesetId, int zoomLevel, string format)
    {
        _tileRequests.Add(1,
            new KeyValuePair<string, object?>("provider", providerKey),
            new KeyValuePair<string, object?>("tileset", tilesetId),
            new KeyValuePair<string, object?>("zoom_level", zoomLevel),
            new KeyValuePair<string, object?>("format", format));
    }

    /// <summary>
    /// Records tile size distribution.
    /// </summary>
    /// <param name="providerKey">Provider identifier.</param>
    /// <param name="tilesetId">Tileset identifier.</param>
    /// <param name="sizeBytes">Size of the tile in bytes.</param>
    /// <param name="format">Tile format (raster, vector).</param>
    public void RecordTileSize(string providerKey, string tilesetId, long sizeBytes, string format)
    {
        _tileSize.Record(sizeBytes,
            new KeyValuePair<string, object?>("provider", providerKey),
            new KeyValuePair<string, object?>("tileset", tilesetId),
            new KeyValuePair<string, object?>("format", format));
    }

    /// <summary>
    /// Records tile format counts.
    /// </summary>
    /// <param name="providerKey">Provider identifier.</param>
    /// <param name="format">Tile format (raster, vector).</param>
    /// <param name="contentType">Specific content type (image/png, application/vnd.mapbox-vector-tile).</param>
    public void RecordTileFormat(string providerKey, string format, string contentType)
    {
        _tileFormatCounter.Add(1,
            new KeyValuePair<string, object?>("provider", providerKey),
            new KeyValuePair<string, object?>("format", format),
            new KeyValuePair<string, object?>("content_type", contentType));
    }

    /// <summary>
    /// Updates the health status of a geocoding provider.
    /// </summary>
    /// <param name="isHealthy">Whether the provider is healthy.</param>
    public void UpdateGeocodingProviderHealth(bool isHealthy)
    {
        _geocodingProviderHealthy = isHealthy ? 1 : 0;
    }

    /// <summary>
    /// Updates the health status of a routing provider.
    /// </summary>
    /// <param name="isHealthy">Whether the provider is healthy.</param>
    public void UpdateRoutingProviderHealth(bool isHealthy)
    {
        _routingProviderHealthy = isHealthy ? 1 : 0;
    }

    /// <summary>
    /// Updates the health status of a basemap provider.
    /// </summary>
    /// <param name="isHealthy">Whether the provider is healthy.</param>
    public void UpdateBasemapProviderHealth(bool isHealthy)
    {
        _basemapProviderHealthy = isHealthy ? 1 : 0;
    }
}

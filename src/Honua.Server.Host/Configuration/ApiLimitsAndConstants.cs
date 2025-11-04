// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Host.Configuration;

/// <summary>
/// API-wide limits and constants for request handling, validation, and performance.
/// This centralized location ensures consistency across all API endpoints.
/// </summary>
public static class ApiLimitsAndConstants
{
    // ============================================================================
    // COORDINATE LIMITS (Geographic/Projected)
    // ============================================================================

    /// <summary>
    /// Maximum longitude in degrees for WGS84 (EPSG:4326) coordinate system.
    /// </summary>
    public const double MaxLongitude = 180.0;

    /// <summary>
    /// Minimum longitude in degrees for WGS84 (EPSG:4326) coordinate system.
    /// </summary>
    public const double MinLongitude = -180.0;

    /// <summary>
    /// Maximum latitude in degrees for WGS84 (EPSG:4326) coordinate system.
    /// </summary>
    public const double MaxLatitude = 90.0;

    /// <summary>
    /// Minimum latitude in degrees for WGS84 (EPSG:4326) coordinate system.
    /// </summary>
    public const double MinLatitude = -90.0;

    /// <summary>
    /// Maximum altitude/elevation in meters. Approximately Mt. Everest height + atmosphere.
    /// </summary>
    public const double MaxAltitudeMeters = 9_000.0;

    /// <summary>
    /// Minimum altitude/depth in meters. Approximately Mariana Trench depth.
    /// </summary>
    public const double MinAltitudeMeters = -11_000.0;

    /// <summary>
    /// Maximum extent for projected coordinate systems in meters.
    /// Approximately half of Earth's circumference (~20,000 km).
    /// </summary>
    public const double MaxProjectedExtentMeters = 20_000_000.0;

    // ============================================================================
    // REQUEST SIZE LIMITS
    // ============================================================================

    /// <summary>
    /// Default maximum request body size: 100 MB.
    /// Used for GeoJSON uploads and general API requests.
    /// </summary>
    public const long DefaultMaxRequestBodyBytes = 100L * 1024 * 1024;

    /// <summary>
    /// Maximum request body size for STAC metadata: 10 MB.
    /// STAC documents are typically small JSON files.
    /// </summary>
    public const long StacMaxRequestBodyBytes = 10L * 1024 * 1024;

    /// <summary>
    /// Maximum request body size for file uploads: 500 MB.
    /// Used for large file uploads and data ingestion.
    /// </summary>
    public const long MaxUploadSizeBytes = 500L * 1024 * 1024;

    /// <summary>
    /// Default maximum XML input stream size: 50 MB.
    /// Prevents XXE and DoS attacks through large XML payloads.
    /// </summary>
    public const int MaxXmlInputStreamBytes = 50 * 1024 * 1024;

    /// <summary>
    /// Maximum request logging body size: 256 KB.
    /// Prevents excessive logging of large request bodies.
    /// </summary>
    public const long MaxRequestBodyLogBytes = 256L * 1024;

    // ============================================================================
    // BUFFER AND STREAM SIZES
    // ============================================================================

    /// <summary>
    /// Standard copy buffer size: 64 KB.
    /// Used for streaming responses to clients.
    /// </summary>
    public const int StandardBufferSize = 64 * 1024;

    /// <summary>
    /// Large copy buffer size: 128 KB.
    /// Used for server-to-server streaming and bulk operations.
    /// </summary>
    public const int LargeBufferSize = 128 * 1024;

    /// <summary>
    /// Output cache flush threshold: 8 KB.
    /// Triggers output cache write when this many bytes accumulate.
    /// </summary>
    public const int OutputCacheFlushThresholdBytes = 8192;

    /// <summary>
    /// Request buffering threshold before spilling to disk: 16 KB.
    /// </summary>
    public const int RequestBufferThresholdBytes = 16 * 1024;

    /// <summary>
    /// Header size limit: 32 KB.
    /// Prevents header-based DoS attacks.
    /// </summary>
    public const int MaxRequestHeadersTotalBytes = 32 * 1024;

    /// <summary>
    /// Request buffer size before spilling to disk: 1 MB.
    /// </summary>
    public const int MaxRequestBufferSizeBytes = 1 * 1024 * 1024;

    // ============================================================================
    // PAGINATION AND COLLECTION LIMITS
    // ============================================================================

    /// <summary>
    /// Maximum number of identifiers in a single request (e.g., ids parameter).
    /// Prevents unbounded OR expressions that could hammer the database.
    /// </summary>
    public const int MaxIdentifiersPerRequest = 1_000;

    /// <summary>
    /// Overlay fetch batch size for map overlay operations.
    /// </summary>
    public const int OverlayFetchBatchSize = 500;

    /// <summary>
    /// Maximum features to fetch in overlay operations.
    /// </summary>
    public const int OverlayFetchMaxFeatures = 10_000;

    /// <summary>
    /// Maximum tile size in pixels.
    /// Standard Web Mercator tile is 256x256.
    /// </summary>
    public const int MaxTileSize = 2048;

    /// <summary>
    /// Default tile size in pixels.
    /// </summary>
    public const int DefaultTileSize = 256;

    /// <summary>
    /// Maximum feature count for feature info requests (WMS).
    /// </summary>
    public const int MaxFeatureInfoCount = 50;

    /// <summary>
    /// Default catalog limit (OGC API default page size).
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Maximum catalog limit per request.
    /// </summary>
    public const int MaxCatalogLimit = 1_000;

    // ============================================================================
    // JSON VALIDATION LIMITS
    // ============================================================================

    /// <summary>
    /// Maximum JSON nesting depth for general requests.
    /// Prevents deeply nested JSON attacks.
    /// </summary>
    public const int MaxJsonNestingDepth = 256;

    /// <summary>
    /// Maximum JSON nesting depth for property/schema definitions.
    /// </summary>
    public const int MaxPropertyJsonNestingDepth = 64;

    // ============================================================================
    // STRING/ID LENGTH LIMITS
    // ============================================================================

    /// <summary>
    /// Maximum length for collection/item identifiers: 256 characters.
    /// </summary>
    public const int MaxIdentifierLength = 256;

    /// <summary>
    /// Maximum length for string fields in forms/validation: 255 characters.
    /// </summary>
    public const int MaxStringFieldLength = 255;

    // ============================================================================
    // TIMEOUT AND PERFORMANCE LIMITS
    // ============================================================================

    /// <summary>
    /// Maximum timeout for hedging operations: 5000 milliseconds (5 seconds).
    /// </summary>
    public const int MaxHedgingDelayMilliseconds = 5_000;

    /// <summary>
    /// Maximum hedging timeout: 60 seconds.
    /// </summary>
    public const int MaxHedgingTimeoutSeconds = 60;

    /// <summary>
    /// Redis health check timeout threshold: 100 milliseconds.
    /// Indicates slow Redis response if exceeded.
    /// </summary>
    public const int RedisHealthCheckThresholdMilliseconds = 100;

    /// <summary>
    /// Maximum operation time for export operations: 5000 milliseconds.
    /// Prevents long-running exports from blocking requests.
    /// </summary>
    public const int MaxExportOperationMilliseconds = 5_000;

    /// <summary>
    /// CSRF token expiration: 3600 seconds (1 hour).
    /// </summary>
    public const int CsrfTokenExpirationSeconds = 3600;

    /// <summary>
    /// Maximum time span for temporal ranges: 100 years.
    /// </summary>
    public static readonly TimeSpan MaximumTimeSpan = TimeSpan.FromDays(365.25 * 100);

    // ============================================================================
    // OGC/WMS/WCS LIMITS
    // ============================================================================

    /// <summary>
    /// Maximum bounding box area for queries in square degrees (for geographic CRS).
    /// Prevents expensive queries over entire world.
    /// </summary>
    public const double MaxBoundingBoxAreaSquareDegrees = 10_000.0;

    /// <summary>
    /// Default WMS/WCS version.
    /// </summary>
    public const string DefaultWmsVersion = "1.3.0";

    /// <summary>
    /// Default Web Mercator extent (half circumference in both directions).
    /// </summary>
    public const double WebMercatorExtentMeters = 20_037_508.3427892 * 2;

    // ============================================================================
    // DEFAULT BBOX (World Bounds)
    // ============================================================================

    /// <summary>
    /// Default world bounding box: [-180, -90, 180, 90] (WGS84).
    /// </summary>
    public static double[] DefaultWorldBoundingBox => new[] { MinLongitude, MinLatitude, MaxLongitude, MaxLatitude };
}

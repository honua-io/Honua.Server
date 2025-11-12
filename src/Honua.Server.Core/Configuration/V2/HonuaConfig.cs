// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.Server.Core.Configuration.V2;

/// <summary>
/// Root configuration object for Honua Server 2.0 declarative configuration.
/// Represents the complete configuration parsed from a .honua or .hcl file.
/// </summary>
public sealed record class HonuaConfig
{
    /// <summary>
    /// Global Honua settings (version, environment, logging, CORS, etc.).
    /// </summary>
    public HonuaGlobalSettings Honua { get; set; } = new();

    /// <summary>
    /// Data source definitions (databases, file paths, etc.).
    /// Key is the data source identifier.
    /// </summary>
    public Dictionary<string, DataSourceBlock> DataSources { get; init; } = new();

    /// <summary>
    /// Service definitions (OData, OGC API, WFS, etc.).
    /// Key is the service identifier.
    /// </summary>
    public Dictionary<string, ServiceBlock> Services { get; init; } = new();

    /// <summary>
    /// Layer definitions (feature layers, raster datasets).
    /// Key is the layer identifier.
    /// </summary>
    public Dictionary<string, LayerBlock> Layers { get; init; } = new();

    /// <summary>
    /// Cache configuration (Redis, in-memory).
    /// Key is the cache identifier.
    /// </summary>
    public Dictionary<string, CacheBlock> Caches { get; init; } = new();

    /// <summary>
    /// Rate limiting configuration.
    /// </summary>
    public RateLimitBlock? RateLimit { get; set; }

    /// <summary>
    /// Variables defined in the configuration file.
    /// Key is the variable name (without 'var.' prefix).
    /// </summary>
    public Dictionary<string, object?> Variables { get; init; } = new();
}

/// <summary>
/// Global Honua server settings.
/// </summary>
public sealed record class HonuaGlobalSettings
{
    /// <summary>
    /// Configuration schema version (e.g., "1.0").
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Environment name (development, staging, production).
    /// </summary>
    public string Environment { get; init; } = "development";

    /// <summary>
    /// Logging level (trace, debug, information, warning, error, critical).
    /// </summary>
    public string LogLevel { get; init; } = "information";

    /// <summary>
    /// CORS configuration.
    /// </summary>
    public CorsSettings? Cors { get; init; }
}

/// <summary>
/// CORS settings.
/// </summary>
public sealed record class CorsSettings
{
    /// <summary>
    /// Allow any origin (not recommended for production).
    /// </summary>
    public bool AllowAnyOrigin { get; init; } = false;

    /// <summary>
    /// List of allowed origins.
    /// </summary>
    public List<string> AllowedOrigins { get; init; } = new();

    /// <summary>
    /// Allow credentials (cookies, authorization headers).
    /// </summary>
    public bool AllowCredentials { get; init; } = false;
}

/// <summary>
/// Data source block - defines a database or data connection.
/// </summary>
public sealed record class DataSourceBlock
{
    /// <summary>
    /// Data source identifier (used for references).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Provider type (sqlite, postgresql, sqlserver, mysql, etc.).
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Connection string or file path.
    /// May contain environment variable references like ${env:VAR_NAME}.
    /// </summary>
    public required string Connection { get; init; }

    /// <summary>
    /// Optional health check query.
    /// </summary>
    public string? HealthCheck { get; init; }

    /// <summary>
    /// Connection pool settings.
    /// </summary>
    public PoolSettings? Pool { get; init; }
}

/// <summary>
/// Connection pool settings.
/// </summary>
public sealed record class PoolSettings
{
    /// <summary>
    /// Minimum connection pool size.
    /// </summary>
    public int MinSize { get; init; } = 1;

    /// <summary>
    /// Maximum connection pool size.
    /// </summary>
    public int MaxSize { get; init; } = 10;

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int Timeout { get; init; } = 30;
}

/// <summary>
/// Service block - defines a service (OData, OGC API, WFS, etc.).
/// </summary>
public sealed record class ServiceBlock
{
    /// <summary>
    /// Service identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Service type (odata, ogc_api, wfs, wms, wmts, etc.).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether the service is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Service-specific settings (stored as a dictionary for flexibility).
    /// Examples: allow_writes, max_page_size, conformance, etc.
    /// </summary>
    public Dictionary<string, object?> Settings { get; init; } = new();
}

/// <summary>
/// Layer block - defines a feature layer or raster dataset.
/// </summary>
public sealed record class LayerBlock
{
    /// <summary>
    /// Layer identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Reference to data source (e.g., "sqlite-test").
    /// </summary>
    public required string DataSource { get; init; }

    /// <summary>
    /// Table or view name in the database.
    /// </summary>
    public required string Table { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Geometry configuration.
    /// </summary>
    public GeometrySettings? Geometry { get; init; }

    /// <summary>
    /// Primary key field name.
    /// </summary>
    public required string IdField { get; init; }

    /// <summary>
    /// Display field name (for labels).
    /// </summary>
    public string? DisplayField { get; init; }

    /// <summary>
    /// Whether to introspect fields from the database.
    /// If false, fields must be explicitly defined.
    /// </summary>
    public bool IntrospectFields { get; init; } = true;

    /// <summary>
    /// Explicit field definitions (when introspect_fields = false).
    /// Key is the field name.
    /// </summary>
    public Dictionary<string, FieldDefinition>? Fields { get; init; }

    /// <summary>
    /// List of service IDs that should expose this layer.
    /// </summary>
    public List<string> Services { get; init; } = new();
}

/// <summary>
/// Geometry settings for a layer.
/// </summary>
public sealed record class GeometrySettings
{
    /// <summary>
    /// Geometry column name.
    /// </summary>
    public required string Column { get; init; }

    /// <summary>
    /// Geometry type (Point, LineString, Polygon, MultiPoint, MultiLineString, MultiPolygon, Geometry).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Spatial reference identifier (e.g., 4326 for WGS84).
    /// </summary>
    public int Srid { get; init; } = 4326;
}

/// <summary>
/// Field definition for explicit field schemas.
/// </summary>
public sealed record class FieldDefinition
{
    /// <summary>
    /// Field data type (int, string, double, datetime, bool, geometry).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether the field is nullable.
    /// </summary>
    public bool Nullable { get; init; } = true;
}

/// <summary>
/// Cache block - defines a cache (Redis, in-memory).
/// </summary>
public sealed record class CacheBlock
{
    /// <summary>
    /// Cache identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Cache type (redis, memory).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether the cache is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Connection string for distributed caches (Redis).
    /// May contain environment variable references.
    /// </summary>
    public string? Connection { get; init; }

    /// <summary>
    /// Environments where this cache is required.
    /// </summary>
    public List<string> RequiredIn { get; init; } = new();
}

/// <summary>
/// Rate limiting configuration.
/// </summary>
public sealed record class RateLimitBlock
{
    /// <summary>
    /// Whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Storage backend for rate limit counters (redis, memory).
    /// </summary>
    public string Store { get; init; } = "memory";

    /// <summary>
    /// Rate limit rules.
    /// Key is the rule name (default, authenticated, etc.).
    /// </summary>
    public Dictionary<string, RateLimitRule> Rules { get; init; } = new();
}

/// <summary>
/// Rate limit rule.
/// </summary>
public sealed record class RateLimitRule
{
    /// <summary>
    /// Number of requests allowed.
    /// </summary>
    public int Requests { get; init; } = 1000;

    /// <summary>
    /// Time window (e.g., "1m", "1h", "1d").
    /// </summary>
    public string Window { get; init; } = "1m";
}

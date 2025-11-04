// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Discovery;

/// <summary>
/// Configuration options for automatic table discovery.
/// Enables zero-configuration exposure of PostGIS tables as OData and OGC API Features collections.
/// </summary>
public sealed class AutoDiscoveryOptions
{
    /// <summary>
    /// Enable automatic discovery of PostGIS tables as OData collections.
    /// When enabled, all geometry tables will automatically be exposed at /odata.
    /// Default: true
    /// </summary>
    public bool DiscoverPostGISTablesAsODataCollections { get; set; } = true;

    /// <summary>
    /// Enable automatic discovery of PostGIS tables as OGC API Features collections.
    /// When enabled, all geometry tables will automatically be exposed at /collections.
    /// Default: true
    /// </summary>
    public bool DiscoverPostGISTablesAsOgcCollections { get; set; } = true;

    /// <summary>
    /// Default SRID to use if a table doesn't specify one.
    /// Default: 4326 (WGS84)
    /// </summary>
    public int DefaultSRID { get; set; } = 4326;

    /// <summary>
    /// Database schemas to exclude from discovery.
    /// System schemas are always excluded.
    /// Default: topology schema
    /// </summary>
    public string[] ExcludeSchemas { get; set; } = new[]
    {
        "topology"
    };

    /// <summary>
    /// Table name patterns to exclude (supports * wildcards).
    /// Useful for excluding temporary or staging tables.
    /// Default: temp_*, staging_*, and tables starting with underscore
    /// </summary>
    public string[] ExcludeTablePatterns { get; set; } = new[]
    {
        "temp_*",
        "staging_*",
        "_*"
    };

    /// <summary>
    /// Only discover tables that have spatial indexes.
    /// This ensures good query performance on discovered tables.
    /// Default: false
    /// </summary>
    public bool RequireSpatialIndex { get; set; } = false;

    /// <summary>
    /// Maximum number of tables to auto-discover (safety limit).
    /// Prevents accidental exposure of hundreds of tables.
    /// Set to 0 for no limit.
    /// Default: 100
    /// </summary>
    public int MaxTables { get; set; } = 100;

    /// <summary>
    /// Cache discovery results for this duration.
    /// Discovery queries can be expensive, so caching is recommended.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Automatically create collections with friendly names.
    /// Converts snake_case and PascalCase to "Friendly Names".
    /// Default: true
    /// </summary>
    public bool UseFriendlyNames { get; set; } = true;

    /// <summary>
    /// Generate OpenAPI documentation for discovered collections.
    /// Default: true
    /// </summary>
    public bool GenerateOpenApiDocs { get; set; } = true;

    /// <summary>
    /// Compute extent (bounding box) for each table.
    /// This can be slow for large tables.
    /// Default: false (extent computed on-demand)
    /// </summary>
    public bool ComputeExtentOnDiscovery { get; set; } = false;

    /// <summary>
    /// Include tables without geometry columns.
    /// When true, all tables (spatial and non-spatial) are discovered.
    /// Default: false (only spatial tables)
    /// </summary>
    public bool IncludeNonSpatialTables { get; set; } = false;

    /// <summary>
    /// Default folder ID for auto-discovered services.
    /// If not specified, discovered tables are placed in a default folder.
    /// </summary>
    public string? DefaultFolderId { get; set; }

    /// <summary>
    /// Default folder title for auto-discovered services.
    /// Default: "Discovered Tables"
    /// </summary>
    public string DefaultFolderTitle { get; set; } = "Discovered Tables";

    /// <summary>
    /// Data source ID to discover tables from.
    /// If not specified, the first PostGIS data source is used.
    /// </summary>
    public string? DataSourceId { get; set; }

    /// <summary>
    /// Enable auto-discovery feature.
    /// Set to false to completely disable auto-discovery.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Refresh discovery cache in the background.
    /// When enabled, cache is refreshed periodically without blocking requests.
    /// Default: true
    /// </summary>
    public bool BackgroundRefresh { get; set; } = true;

    /// <summary>
    /// How often to refresh the cache in the background.
    /// Only applies if BackgroundRefresh is true.
    /// Default: Same as CacheDuration
    /// </summary>
    public TimeSpan? BackgroundRefreshInterval { get; set; }
}

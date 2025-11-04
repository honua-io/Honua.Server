// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Honua.Server.Core.Configuration;

public sealed class HonuaConfiguration
{
    public required MetadataConfiguration Metadata { get; init; }
    public ServicesConfiguration Services { get; init; } = ServicesConfiguration.Default;
    public AttachmentConfiguration Attachments { get; init; } = AttachmentConfiguration.Default;
    public ExternalServiceSecurityConfiguration ExternalServiceSecurity { get; init; } = ExternalServiceSecurityConfiguration.Default;
    public RasterCacheConfiguration RasterCache { get; init; } = RasterCacheConfiguration.Default;
}

public sealed class MetadataConfiguration
{
    public required string Provider { get; init; }
    public required string Path { get; init; }
}

public sealed class ODataConfiguration
{
    public static ODataConfiguration Default => new();

    public bool Enabled { get; init; } = true;
    public bool AllowWrites { get; init; }
    public int DefaultPageSize { get; init; } = 100;
    public int MaxPageSize { get; init; } = 1000;
    public bool EmitWktShadowProperties { get; init; }
}

public sealed class ServicesConfiguration
{
    public static ServicesConfiguration Default => new();

    public OgcApiConfiguration OgcApi { get; init; } = OgcApiConfiguration.Default;
    public WfsConfiguration Wfs { get; init; } = WfsConfiguration.Default;
    public WmsConfiguration Wms { get; init; } = WmsConfiguration.Default;
    public WmtsConfiguration Wmts { get; init; } = WmtsConfiguration.Default;
    public CswConfiguration Csw { get; init; } = CswConfiguration.Default;
    public WcsConfiguration Wcs { get; init; } = WcsConfiguration.Default;
    public ODataConfiguration OData { get; init; } = ODataConfiguration.Default;
    public CartoConfiguration Carto { get; init; } = CartoConfiguration.Default;
    public PrintServiceConfiguration Print { get; init; } = PrintServiceConfiguration.Default;
    public RasterTileCacheConfiguration RasterTiles { get; init; } = RasterTileCacheConfiguration.Default;
    public StacCatalogConfiguration Stac { get; init; } = StacCatalogConfiguration.Default;
    public GeometryServiceConfiguration Geometry { get; init; } = GeometryServiceConfiguration.Default;
    public GeoservicesRESTConfiguration GeoservicesREST { get; init; } = GeoservicesRESTConfiguration.Default;
    public ZarrApiConfiguration Zarr { get; init; } = ZarrApiConfiguration.Default;
}

public sealed class GeometryServiceConfiguration
{
    public static GeometryServiceConfiguration Default => new();

    public bool Enabled { get; init; } = true;
    public int MaxGeometries { get; init; } = 1000;
    public int MaxCoordinateCount { get; init; } = 100_000;
    public IReadOnlyList<int>? AllowedSrids { get; init; }
    public bool EnableGdalOperations { get; init; } = true;
}

public sealed class AttachmentConfiguration
{
    public static AttachmentConfiguration Default => new();

    public int DefaultMaxSizeMiB { get; init; } = 25;
    public Dictionary<string, AttachmentStorageProfileConfiguration> Profiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AttachmentStorageProfileConfiguration
{
    public static AttachmentStorageProfileConfiguration Default => new();

    public string Provider { get; init; } = "filesystem";
    public AttachmentFileSystemStorageConfiguration FileSystem { get; init; } = AttachmentFileSystemStorageConfiguration.Default;
    public AttachmentS3StorageConfiguration S3 { get; init; } = AttachmentS3StorageConfiguration.Default;
    public AttachmentAzureBlobStorageConfiguration Azure { get; init; } = AttachmentAzureBlobStorageConfiguration.Default;
    public AttachmentGcsStorageConfiguration Gcs { get; init; } = AttachmentGcsStorageConfiguration.Default;
    public AttachmentDatabaseStorageConfiguration Database { get; init; } = AttachmentDatabaseStorageConfiguration.Default;
}

public sealed class AttachmentFileSystemStorageConfiguration
{
    public static AttachmentFileSystemStorageConfiguration Default => new();

    public string RootPath { get; init; } = Path.Combine("data", "attachments");
}

public sealed class AttachmentS3StorageConfiguration
{
    public static AttachmentS3StorageConfiguration Default => new();

    public string? BucketName { get; init; }
    public string? Prefix { get; init; }
    public string? Region { get; init; }
    public string? ServiceUrl { get; init; }
    public string? AccessKeyId { get; init; }
    public string? SecretAccessKey { get; init; }
    public bool ForcePathStyle { get; init; }
    public bool UseInstanceProfile { get; init; } = true;
    public int PresignExpirySeconds { get; init; } = 900;
}

public sealed class AttachmentAzureBlobStorageConfiguration
{
    public static AttachmentAzureBlobStorageConfiguration Default => new();

    public string? ConnectionString { get; init; }
    public string? ContainerName { get; init; }
    public string? Prefix { get; init; }
    public bool EnsureContainer { get; init; } = true;
}

public sealed class AttachmentGcsStorageConfiguration
{
    public static AttachmentGcsStorageConfiguration Default => new();

    public string? BucketName { get; init; }
    public string? Prefix { get; init; }
    public string? ProjectId { get; init; }
    public string? CredentialsPath { get; init; }
    public bool UseApplicationDefaultCredentials { get; init; } = true;
}

public sealed class AttachmentDatabaseStorageConfiguration
{
    public static AttachmentDatabaseStorageConfiguration Default => new();

    public string Provider { get; init; } = "sqlite";
    public string? ConnectionString { get; init; }
    public string? Schema { get; init; }
    public string? TableName { get; init; }
    public string AttachmentIdColumn { get; init; } = "attachment_id";
    public string ContentColumn { get; init; } = "content";
    public string? FileNameColumn { get; init; }
}

public sealed class WfsConfiguration
{
    public static WfsConfiguration Default => new();

    public bool Enabled { get; init; } = true;
}

public sealed class WmsConfiguration
{
    public static WmsConfiguration Default => new();

    public bool Enabled { get; init; } = true;
}

public sealed class PrintServiceConfiguration
{
    public static PrintServiceConfiguration Default => new();

    public bool Enabled { get; init; } = true;
    public string Provider { get; init; } = "embedded";
    public string? ConfigurationPath { get; init; }
}

public sealed class CswConfiguration
{
    public static CswConfiguration Default => new();

    public bool Enabled { get; init; } = true;
}

public sealed class WcsConfiguration
{
    public static WcsConfiguration Default => new();

    public bool Enabled { get; init; } = true;
}

public sealed class WmtsConfiguration
{
    public static WmtsConfiguration Default => new();

    public bool Enabled { get; init; } = true;
}

public sealed class OgcApiConfiguration
{
    public static OgcApiConfiguration Default => new();

    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Maximum size in bytes for GeoJSON feature upload payloads (default: 100 MB).
    /// Prevents DoS attacks via oversized JSON documents that exhaust memory.
    /// Set to 0 to disable the limit (not recommended in production).
    /// </summary>
    public long MaxFeatureUploadSizeBytes { get; init; } = 100 * 1024 * 1024; // 100 MB
}

public sealed class CartoConfiguration
{
    public static CartoConfiguration Default => new();

    public bool Enabled { get; init; } = true;
}

public sealed class ZarrApiConfiguration
{
    public static ZarrApiConfiguration Default => new();

    public bool Enabled { get; init; } = true;
}

public sealed class RasterTileCacheConfiguration
{
    public static RasterTileCacheConfiguration Default => new();

    public bool Enabled { get; init; } = true;

    public string Provider { get; init; } = "filesystem";

    public RasterTileFileSystemConfiguration FileSystem { get; init; } = RasterTileFileSystemConfiguration.Default;

    public RasterTileS3Configuration S3 { get; init; } = RasterTileS3Configuration.Default;

    public RasterTileAzureConfiguration Azure { get; init; } = RasterTileAzureConfiguration.Default;

    public RasterTilePreseedConfiguration Preseed { get; init; } = RasterTilePreseedConfiguration.Default;
}

public sealed class RasterTileFileSystemConfiguration
{
    public static RasterTileFileSystemConfiguration Default => new();

    public string RootPath { get; init; } = Path.Combine("data", "raster-cache");
}

public sealed class RasterTileS3Configuration
{
    public static RasterTileS3Configuration Default => new();

    public string? BucketName { get; init; }

    public string? Prefix { get; init; }

    public string? Region { get; init; }

    public string? ServiceUrl { get; init; }

    public string? AccessKeyId { get; init; }

    public string? SecretAccessKey { get; init; }

    public bool EnsureBucket { get; init; }

    public bool ForcePathStyle { get; init; }
}

public sealed class RasterTileAzureConfiguration
{
    public static RasterTileAzureConfiguration Default => new();

    public string? ConnectionString { get; init; }

    public string? ContainerName { get; init; }

    public bool EnsureContainer { get; init; } = true;
}

public sealed class RasterTilePreseedConfiguration
{
    public static RasterTilePreseedConfiguration Default => new();

    public int BatchSize { get; init; } = 32;

    public int MaxDegreeOfParallelism { get; init; } = 1;
}

public sealed class StacCatalogConfiguration
{
    public static StacCatalogConfiguration Default => new();

    public bool Enabled { get; init; } = true;
    public string Provider { get; init; } = "sqlite";
    public string? ConnectionString { get; init; }
    public string? FilePath { get; init; }
}

/// <summary>
/// Configuration for hybrid COG + Zarr raster cache.
/// Implements the recommended architecture: COG cache for spatial + Zarr for time-series.
/// </summary>
public sealed class RasterCacheConfiguration
{
    public static RasterCacheConfiguration Default => new();

    /// <summary>
    /// Enable COG caching (recommended: true).
    /// Converts NetCDF/HDF5/GRIB2 to Cloud Optimized GeoTIFF for fast access.
    /// </summary>
    public bool CogCacheEnabled { get; init; } = true;

    /// <summary>
    /// Storage provider for COG cache (filesystem, s3, azure).
    /// </summary>
    public string CogCacheProvider { get; init; } = "filesystem";

    /// <summary>
    /// Cache directory for COG files (filesystem provider).
    /// </summary>
    public string CogCacheDirectory { get; init; } = Path.Combine("data", "raster-cog-cache");

    /// <summary>
    /// S3 bucket for COG cache (s3 provider).
    /// </summary>
    public string? CogCacheS3Bucket { get; init; }

    /// <summary>
    /// S3 prefix for COG cache.
    /// </summary>
    public string? CogCacheS3Prefix { get; init; }

    /// <summary>
    /// Optional S3 region (e.g. us-east-1).
    /// </summary>
    public string? CogCacheS3Region { get; init; }

    /// <summary>
    /// Optional custom service URL for S3-compatible stores.
    /// </summary>
    public string? CogCacheS3ServiceUrl { get; init; }

    /// <summary>
    /// Optional access key id for S3 cache provider.
    /// </summary>
    public string? CogCacheS3AccessKeyId { get; init; }

    /// <summary>
    /// Optional secret access key for S3 cache provider.
    /// </summary>
    public string? CogCacheS3SecretAccessKey { get; init; }

    /// <summary>
    /// Use path-style requests for S3-compatible providers.
    /// </summary>
    public bool CogCacheS3ForcePathStyle { get; init; }

    /// <summary>
    /// Azure container for COG cache (azure provider).
    /// </summary>
    public string? CogCacheAzureContainer { get; init; }

    /// <summary>
    /// Azure prefix for COG cache.
    /// </summary>
    public string? CogCacheAzurePrefix { get; init; }

    /// <summary>
    /// Azure Blob connection string for COG cache.
    /// </summary>
    public string? CogCacheAzureConnectionString { get; init; }

    /// <summary>
    /// Ensure Azure container exists on startup.
    /// </summary>
    public bool CogCacheAzureEnsureContainer { get; init; } = true;

    /// <summary>
    /// Google Cloud Storage bucket for COG cache (gcs provider).
    /// </summary>
    public string? CogCacheGcsBucket { get; init; }

    /// <summary>
    /// Optional key prefix for COG cache within the GCS bucket.
    /// </summary>
    public string? CogCacheGcsPrefix { get; init; }

    /// <summary>
    /// Optional path to a Google credentials JSON file.
    /// </summary>
    public string? CogCacheGcsCredentialsPath { get; init; }

    /// <summary>
    /// Default COG compression (DEFLATE, LZW, WEBP, ZSTD).
    /// </summary>
    public string CogCompression { get; init; } = "DEFLATE";

    /// <summary>
    /// COG block/tile size (256, 512, etc.).
    /// </summary>
    public int CogBlockSize { get; init; } = 512;

    /// <summary>
    /// Enable Zarr support for time-series data (enabled by default for MVP).
    /// Requires Python with xarray and zarr packages: pip install xarray zarr netcdf4 h5netcdf
    /// </summary>
    public bool ZarrEnabled { get; init; } = true;

    /// <summary>
    /// Zarr storage directory.
    /// </summary>
    public string ZarrDirectory { get; init; } = Path.Combine("data", "raster-zarr");

    /// <summary>
    /// Zarr compression (zstd, gzip, lz4).
    /// </summary>
    public string ZarrCompression { get; init; } = "zstd";

    /// <summary>
    /// Maximum age for cached COG files (in days). 0 = no expiration.
    /// </summary>
    public int CacheTtlDays { get; init; } = 7;

    /// <summary>
    /// Automatic cache cleanup enabled.
    /// </summary>
    public bool AutoCleanupEnabled { get; init; } = true;

    /// <summary>
    /// Enable distributed locking for kerchunk reference generation (recommended for multi-instance deployments).
    /// When enabled, uses Redis for distributed coordination to prevent cache stampede.
    /// When disabled or Redis is unavailable, falls back to in-memory locking (single-instance only).
    /// </summary>
    public bool EnableDistributedLocking { get; init; } = true;

    /// <summary>
    /// Timeout for acquiring distributed locks during kerchunk generation (default: 5 minutes).
    /// </summary>
    public TimeSpan DistributedLockTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Expiry time for distributed locks (default: 10 minutes).
    /// Prevents deadlocks if a process crashes while holding a lock.
    /// Should be longer than the expected kerchunk generation time.
    /// </summary>
    public TimeSpan DistributedLockExpiry { get; init; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Configuration for GeoservicesREST API (Esri FeatureServer/MapServer).
/// </summary>
public sealed class GeoservicesRESTConfiguration
{
    public static GeoservicesRESTConfiguration Default => new();

    /// <summary>
    /// Enable GeoservicesREST API endpoints (default: true).
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Default maximum record count when resultRecordCount is not specified (default: 1000).
    /// </summary>
    public int DefaultMaxRecordCount { get; init; } = 1000;

    /// <summary>
    /// Maximum allowed record count per request (default: 10000).
    /// Setting this too high can cause memory exhaustion and performance issues.
    /// </summary>
    public int MaxRecordCount { get; init; } = 10000;

    /// <summary>
    /// Default response format (json, geojson, etc.) (default: "json").
    /// </summary>
    public string DefaultFormat { get; init; } = "json";

    /// <summary>
    /// Enable telemetry for GeoservicesREST operations (default: true).
    /// </summary>
    public bool EnableTelemetry { get; init; } = true;
}

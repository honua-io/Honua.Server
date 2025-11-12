// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for hybrid COG + Zarr raster cache.
/// Implements the recommended architecture: COG cache for spatial + Zarr for time-series.
/// </summary>
public sealed class RasterCacheOptions
{
    public const string SectionName = "Honua:RasterCache";

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

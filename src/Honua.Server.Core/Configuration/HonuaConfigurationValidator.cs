// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates the main Honua configuration.
/// </summary>
public sealed class HonuaConfigurationValidator : IValidateOptions<HonuaConfiguration>
{
    public ValidateOptionsResult Validate(string? name, HonuaConfiguration options)
    {
        var failures = new List<string>();

        // Validate Metadata configuration (required)
        if (options.Metadata is null)
        {
            failures.Add("Metadata configuration is required. Set 'honua:metadata' in appsettings.json.");
        }
        else
        {
            ValidateMetadata(options.Metadata, failures);
        }

        // Validate OData configuration
        if (options.Services?.OData is not null)
        {
            ValidateOData(options.Services.OData, failures);
        }

        // Validate Services configuration
        if (options.Services is not null)
        {
            ValidateServices(options.Services, failures);
        }

        // Validate Attachments configuration
        if (options.Attachments is not null)
        {
            ValidateAttachments(options.Attachments, failures);
        }

        // Validate RasterCache configuration
        if (options.RasterCache is not null)
        {
            ValidateRasterCache(options.RasterCache, failures);
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateMetadata(MetadataConfiguration metadata, List<string> failures)
    {
        if (metadata.Provider.IsNullOrWhiteSpace())
        {
            failures.Add("Metadata provider is required. Set 'honua:metadata:provider' to 'json' or 'yaml'.");
        }
        else
        {
            var validProviders = new[] { "json", "yaml" };
            if (!validProviders.Contains(metadata.Provider, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add($"Metadata provider '{metadata.Provider}' is invalid. Valid values: {string.Join(", ", validProviders)}.");
            }
        }

        if (metadata.Path.IsNullOrWhiteSpace())
        {
            failures.Add("Metadata path is required. Set 'honua:metadata:path' to the metadata file or directory path.");
        }
    }

    private static void ValidateOData(ODataConfiguration odata, List<string> failures)
    {
        if (!odata.Enabled)
            return;

        if (odata.DefaultPageSize <= 0)
        {
            failures.Add($"OData DefaultPageSize must be > 0. Current: {odata.DefaultPageSize}. Set 'honua:odata:defaultPageSize' to a positive value.");
        }

        if (odata.MaxPageSize <= 0)
        {
            failures.Add($"OData MaxPageSize must be > 0. Current: {odata.MaxPageSize}. Set 'honua:odata:maxPageSize' to a positive value.");
        }

        if (odata.DefaultPageSize > odata.MaxPageSize)
        {
            failures.Add($"OData DefaultPageSize ({odata.DefaultPageSize}) cannot exceed MaxPageSize ({odata.MaxPageSize}). Adjust 'honua:odata:defaultPageSize' or 'honua:odata:maxPageSize'.");
        }

        if (odata.MaxPageSize > 5000)
        {
            failures.Add($"OData MaxPageSize ({odata.MaxPageSize}) exceeds recommended limit of 5000. This can cause memory exhaustion and DoS attacks. Reduce 'honua:odata:maxPageSize'.");
        }
    }

    private static void ValidateServices(ServicesConfiguration services, List<string> failures)
    {
        // Validate STAC configuration
        if (services.Stac?.Enabled == true)
        {
            if (services.Stac.Provider.IsNullOrWhiteSpace())
            {
                failures.Add("STAC provider is required when STAC is enabled. Set 'honua:services:stac:provider' to 'sqlite', 'postgres', or 'sqlserver'.");
            }
            else
            {
                var validProviders = new[] { "sqlite", "postgres", "sqlserver" };
                if (!validProviders.Contains(services.Stac.Provider, StringComparer.OrdinalIgnoreCase))
                {
                    failures.Add($"STAC provider '{services.Stac.Provider}' is invalid. Valid values: {string.Join(", ", validProviders)}.");
                }
            }
        }

        // Validate RasterTiles configuration
        if (services.RasterTiles?.Enabled == true)
        {
            if (services.RasterTiles.Provider.IsNullOrWhiteSpace())
            {
                failures.Add("RasterTiles provider is required when RasterTiles is enabled. Set 'honua:services:rasterTiles:provider' to 'filesystem', 's3', or 'azure'.");
            }
            else
            {
                var validProviders = new[] { "filesystem", "s3", "azure" };
                if (!validProviders.Contains(services.RasterTiles.Provider, StringComparer.OrdinalIgnoreCase))
                {
                    failures.Add($"RasterTiles provider '{services.RasterTiles.Provider}' is invalid. Valid values: {string.Join(", ", validProviders)}.");
                }

                // Validate provider-specific settings
                if (services.RasterTiles.Provider.Equals("s3", StringComparison.OrdinalIgnoreCase))
                {
                    if (services.RasterTiles.S3?.BucketName.IsNullOrWhiteSpace() == true)
                    {
                        failures.Add("S3 bucket name is required when RasterTiles provider is 's3'. Set 'honua:services:rasterTiles:s3:bucketName'.");
                    }
                }
                else if (services.RasterTiles.Provider.Equals("azure", StringComparison.OrdinalIgnoreCase))
                {
                    if (services.RasterTiles.Azure?.ContainerName.IsNullOrWhiteSpace() == true)
                    {
                        failures.Add("Azure container name is required when RasterTiles provider is 'azure'. Set 'honua:services:rasterTiles:azure:containerName'.");
                    }
                }
            }

            // Validate preseed configuration
            if (services.RasterTiles.Preseed is not null)
            {
                if (services.RasterTiles.Preseed.BatchSize <= 0)
                {
                    failures.Add($"RasterTiles preseed BatchSize must be > 0. Current: {services.RasterTiles.Preseed.BatchSize}.");
                }

                if (services.RasterTiles.Preseed.MaxDegreeOfParallelism < 1)
                {
                    failures.Add($"RasterTiles preseed MaxDegreeOfParallelism must be >= 1. Current: {services.RasterTiles.Preseed.MaxDegreeOfParallelism}.");
                }
            }
        }

        // Validate Geometry service limits
        if (services.Geometry?.Enabled == true)
        {
            if (services.Geometry.MaxGeometries <= 0)
            {
                failures.Add($"Geometry MaxGeometries must be > 0. Current: {services.Geometry.MaxGeometries}. Set 'honua:services:geometry:maxGeometries'.");
            }

            if (services.Geometry.MaxGeometries > 10000)
            {
                failures.Add($"Geometry MaxGeometries ({services.Geometry.MaxGeometries}) exceeds recommended limit of 10000. This can lead to DoS attacks. Reduce 'honua:services:geometry:maxGeometries'.");
            }

            if (services.Geometry.MaxCoordinateCount <= 0)
            {
                failures.Add($"Geometry MaxCoordinateCount must be > 0. Current: {services.Geometry.MaxCoordinateCount}. Set 'honua:services:geometry:maxCoordinateCount'.");
            }

            if (services.Geometry.MaxCoordinateCount > 1_000_000)
            {
                failures.Add($"Geometry MaxCoordinateCount ({services.Geometry.MaxCoordinateCount}) exceeds recommended limit of 1000000. This can cause memory exhaustion. Reduce 'honua:services:geometry:maxCoordinateCount'.");
            }
        }

        // Validate GeoservicesREST configuration
        if (services.GeoservicesREST?.Enabled == true)
        {
            if (services.GeoservicesREST.DefaultMaxRecordCount <= 0)
            {
                failures.Add($"GeoservicesREST DefaultMaxRecordCount must be > 0. Current: {services.GeoservicesREST.DefaultMaxRecordCount}. Set 'honua:services:geoservicesREST:defaultMaxRecordCount'.");
            }

            if (services.GeoservicesREST.MaxRecordCount <= 0)
            {
                failures.Add($"GeoservicesREST MaxRecordCount must be > 0. Current: {services.GeoservicesREST.MaxRecordCount}. Set 'honua:services:geoservicesREST:maxRecordCount'.");
            }

            if (services.GeoservicesREST.DefaultMaxRecordCount > services.GeoservicesREST.MaxRecordCount)
            {
                failures.Add($"GeoservicesREST DefaultMaxRecordCount ({services.GeoservicesREST.DefaultMaxRecordCount}) cannot exceed MaxRecordCount ({services.GeoservicesREST.MaxRecordCount}). Adjust 'honua:services:geoservicesREST:defaultMaxRecordCount' or 'honua:services:geoservicesREST:maxRecordCount'.");
            }

            if (services.GeoservicesREST.MaxRecordCount > 100_000)
            {
                failures.Add($"GeoservicesREST MaxRecordCount ({services.GeoservicesREST.MaxRecordCount}) exceeds recommended limit of 100000. This can cause memory exhaustion and DoS attacks. Reduce 'honua:services:geoservicesREST:maxRecordCount'.");
            }

            if (services.GeoservicesREST.DefaultFormat.IsNullOrWhiteSpace())
            {
                failures.Add("GeoservicesREST DefaultFormat cannot be empty. Set 'honua:services:geoservicesREST:defaultFormat' to 'json', 'geojson', etc.");
            }
        }
    }

    private static void ValidateAttachments(AttachmentConfiguration attachments, List<string> failures)
    {
        if (attachments.DefaultMaxSizeMiB <= 0)
        {
            failures.Add($"Attachment DefaultMaxSizeMiB must be > 0. Current: {attachments.DefaultMaxSizeMiB}. Set 'honua:attachments:defaultMaxSizeMiB'.");
        }

        if (attachments.DefaultMaxSizeMiB > 100)
        {
            failures.Add($"Attachment DefaultMaxSizeMiB ({attachments.DefaultMaxSizeMiB}) exceeds recommended limit of 100 MB. Large attachments can cause storage and bandwidth issues. Reduce 'honua:attachments:defaultMaxSizeMiB'.");
        }

        // Validate attachment storage profiles
        foreach (var (profileName, profile) in attachments.Profiles)
        {
            if (profile.Provider.IsNullOrWhiteSpace())
            {
                failures.Add($"Attachment profile '{profileName}' has no provider specified. Set 'honua:attachments:profiles:{profileName}:provider'.");
                continue;
            }

            var validProviders = new[] { "filesystem", "s3", "azure", "database" };
            if (!validProviders.Contains(profile.Provider, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add($"Attachment profile '{profileName}' has invalid provider '{profile.Provider}'. Valid values: {string.Join(", ", validProviders)}.");
                continue;
            }

            // Validate provider-specific settings
            if (profile.Provider.Equals("s3", StringComparison.OrdinalIgnoreCase))
            {
                if (profile.S3?.BucketName.IsNullOrWhiteSpace() == true)
                {
                    failures.Add($"Attachment profile '{profileName}' uses S3 provider but bucket name is not set. Set 'honua:attachments:profiles:{profileName}:s3:bucketName'.");
                }

                if (profile.S3?.PresignExpirySeconds <= 0)
                {
                    failures.Add($"Attachment profile '{profileName}' S3 presign expiry must be > 0. Current: {profile.S3?.PresignExpirySeconds ?? 0}.");
                }
            }
            else if (profile.Provider.Equals("azure", StringComparison.OrdinalIgnoreCase))
            {
                if (profile.Azure?.ContainerName.IsNullOrWhiteSpace() == true)
                {
                    failures.Add($"Attachment profile '{profileName}' uses Azure provider but container name is not set. Set 'honua:attachments:profiles:{profileName}:azure:containerName'.");
                }
            }
            else if (profile.Provider.Equals("database", StringComparison.OrdinalIgnoreCase))
            {
                var validDbProviders = new[] { "sqlite", "postgres", "sqlserver", "mysql" };
                if (!validDbProviders.Contains(profile.Database?.Provider ?? "", StringComparer.OrdinalIgnoreCase))
                {
                    failures.Add($"Attachment profile '{profileName}' uses database provider but database provider is invalid. Valid values: {string.Join(", ", validDbProviders)}.");
                }
            }
        }
    }

    private static void ValidateRasterCache(RasterCacheConfiguration rasterCache, List<string> failures)
    {
        // Validate COG cache settings
        if (rasterCache.CogCacheEnabled)
        {
            var validProviders = new[] { "filesystem", "s3", "azure", "gcs" };
            if (!validProviders.Contains(rasterCache.CogCacheProvider, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add($"RasterCache COG provider '{rasterCache.CogCacheProvider}' is invalid. Valid values: {string.Join(", ", validProviders)}.");
            }

            if (rasterCache.CogCacheProvider.Equals("s3", StringComparison.OrdinalIgnoreCase))
            {
                if (rasterCache.CogCacheS3Bucket.IsNullOrWhiteSpace())
                {
                    failures.Add("RasterCache COG S3 bucket is required when provider is 's3'. Set 'honua:rasterCache:cogCacheS3Bucket'.");
                }
            }
            else if (rasterCache.CogCacheProvider.Equals("gcs", StringComparison.OrdinalIgnoreCase))
            {
                if (rasterCache.CogCacheGcsBucket.IsNullOrWhiteSpace())
                {
                    failures.Add("RasterCache COG GCS bucket is required when provider is 'gcs'. Set 'honua:rasterCache:cogCacheGcsBucket'.");
                }
            }
            else if (rasterCache.CogCacheProvider.Equals("azure", StringComparison.OrdinalIgnoreCase))
            {
                if (rasterCache.CogCacheAzureContainer.IsNullOrWhiteSpace())
                {
                    failures.Add("RasterCache COG Azure container is required when provider is 'azure'. Set 'honua:rasterCache:cogCacheAzureContainer'.");
                }

                if (rasterCache.CogCacheAzureConnectionString.IsNullOrWhiteSpace())
                {
                    failures.Add("RasterCache COG Azure provider requires a connection string. Set 'honua:rasterCache:cogCacheAzureConnectionString'.");
                }
            }

            // Validate COG compression
            var validCompressions = new[] { "DEFLATE", "LZW", "WEBP", "ZSTD", "NONE" };
            if (!validCompressions.Contains(rasterCache.CogCompression, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add($"RasterCache COG compression '{rasterCache.CogCompression}' is invalid. Valid values: {string.Join(", ", validCompressions)}.");
            }

            // Validate COG block size
            var validBlockSizes = new[] { 128, 256, 512, 1024 };
            if (!validBlockSizes.Contains(rasterCache.CogBlockSize))
            {
                failures.Add($"RasterCache COG block size {rasterCache.CogBlockSize} is invalid. Valid values: {string.Join(", ", validBlockSizes)}.");
            }
        }

        // Validate Zarr settings
        if (rasterCache.ZarrEnabled)
        {
            var validCompressions = new[] { "zstd", "gzip", "lz4", "none" };
            if (!validCompressions.Contains(rasterCache.ZarrCompression, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add($"RasterCache Zarr compression '{rasterCache.ZarrCompression}' is invalid. Valid values: {string.Join(", ", validCompressions)}.");
            }
        }

        // Validate cache TTL
        if (rasterCache.CacheTtlDays < 0)
        {
            failures.Add($"RasterCache TTL days cannot be negative. Current: {rasterCache.CacheTtlDays}. Set 'honua:rasterCache:cacheTtlDays' to 0 (no expiry) or positive value.");
        }
    }
}

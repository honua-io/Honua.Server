// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Azure.Storage.Blobs;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Factory implementation for creating raster tile cache provider instances.
/// </summary>
public sealed class RasterTileCacheProviderFactory : ProviderFactoryBase<IRasterTileCacheProvider>, IRasterTileCacheProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Observability.ICircuitBreakerMetrics? _metrics;

    public RasterTileCacheProviderFactory(
        ILoggerFactory loggerFactory,
        Observability.ICircuitBreakerMetrics? metrics = null)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _metrics = metrics;

        // Register null provider as singleton
        RegisterProviderInstance("null", NullRasterTileCacheProvider.Instance);
    }

    public IRasterTileCacheProvider Create(RasterCacheOptions configuration)
    {
        if (configuration is null || !configuration.CogCacheEnabled)
        {
            return NullRasterTileCacheProvider.Instance;
        }

        var providerKey = configuration.CogCacheProvider?.Trim();
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return NullRasterTileCacheProvider.Instance;
        }

        // For configuration-based providers, we need to pass the config
        // so we can't use the registration system directly
        var normalizedKey = NormalizeProviderName(providerKey);
        return normalizedKey switch
        {
            "filesystem" => CreateFileSystemCacheProvider(configuration),
            "s3" => CreateS3CacheProvider(configuration),
            "azure" => CreateAzureCacheProvider(configuration),
            "null" => NullRasterTileCacheProvider.Instance,

            _ => throw new NotSupportedException($"Raster tile cache provider '{configuration.CogCacheProvider}' is not supported. Supported providers: filesystem, s3, azure")
        };
    }

    private IRasterTileCacheProvider CreateFileSystemCacheProvider(RasterCacheOptions config)
    {
        var logger = _loggerFactory.CreateLogger<FileSystemRasterTileCacheProvider>();
        var rootPath = config.CogCacheDirectory;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = Path.Combine("data", "raster-cog-cache");
        }

        return new FileSystemRasterTileCacheProvider(rootPath!, logger);
    }

    private IRasterTileCacheProvider CreateS3CacheProvider(RasterCacheOptions configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.CogCacheS3Bucket))
        {
            throw new InvalidDataException("Raster tile cache provider 's3' requires a bucketName.");
        }

        var logger = _loggerFactory.CreateLogger<S3RasterTileCacheProvider>();
        var clientOptions = new S3RasterTileCacheProvider.S3ClientOptions
        {
            Region = configuration.CogCacheS3Region,
            ServiceUrl = configuration.CogCacheS3ServiceUrl,
            AccessKeyId = configuration.CogCacheS3AccessKeyId,
            SecretAccessKey = configuration.CogCacheS3SecretAccessKey,
            ForcePathStyle = configuration.CogCacheS3ForcePathStyle
        };

        var client = S3RasterTileCacheProvider.CreateClient(clientOptions);
        // Factory creates the client, so provider owns it
        return new S3RasterTileCacheProvider(client, configuration.CogCacheS3Bucket!, configuration.CogCacheS3Prefix, true, logger, _metrics, ownsClient: true);
    }

    private IRasterTileCacheProvider CreateAzureCacheProvider(RasterCacheOptions configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.CogCacheAzureConnectionString))
        {
            throw new InvalidDataException("Raster tile cache provider 'azure' requires a connectionString.");
        }

        var containerName = string.IsNullOrWhiteSpace(configuration.CogCacheAzureContainer)
            ? "raster-tiles"
            : configuration.CogCacheAzureContainer;

        var blobClient = new BlobContainerClient(configuration.CogCacheAzureConnectionString, containerName);
        var logger = _loggerFactory.CreateLogger<AzureBlobRasterTileCacheProvider>();
        // Factory creates the client, so provider owns it
        return new AzureBlobRasterTileCacheProvider(blobClient, configuration.CogCacheAzureEnsureContainer, logger, _metrics, ownsContainer: true);
    }
}

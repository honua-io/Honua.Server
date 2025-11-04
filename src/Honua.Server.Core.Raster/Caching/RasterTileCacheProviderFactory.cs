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

    public IRasterTileCacheProvider Create(RasterTileCacheConfiguration configuration)
    {
        if (configuration is null || !configuration.Enabled)
        {
            return NullRasterTileCacheProvider.Instance;
        }

        var providerKey = configuration.Provider?.Trim();
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return NullRasterTileCacheProvider.Instance;
        }

        // For configuration-based providers, we need to pass the config
        // so we can't use the registration system directly
        var normalizedKey = NormalizeProviderName(providerKey);
        return normalizedKey switch
        {
            "filesystem" => CreateFileSystemCacheProvider(configuration.FileSystem),
            "s3" => CreateS3CacheProvider(configuration),
            "azure" => CreateAzureCacheProvider(configuration),
            "null" => NullRasterTileCacheProvider.Instance,

            _ => throw new NotSupportedException($"Raster tile cache provider '{configuration.Provider}' is not supported. Supported providers: filesystem, s3, azure")
        };
    }

    private IRasterTileCacheProvider CreateFileSystemCacheProvider(RasterTileFileSystemConfiguration? fileSystem)
    {
        var logger = _loggerFactory.CreateLogger<FileSystemRasterTileCacheProvider>();
        var rootPath = fileSystem?.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = RasterTileFileSystemConfiguration.Default.RootPath;
        }

        return new FileSystemRasterTileCacheProvider(rootPath!, logger);
    }

    private IRasterTileCacheProvider CreateS3CacheProvider(RasterTileCacheConfiguration configuration)
    {
        var s3 = configuration.S3 ?? RasterTileS3Configuration.Default;
        if (string.IsNullOrWhiteSpace(s3.BucketName))
        {
            throw new InvalidDataException("Raster tile cache provider 's3' requires a bucketName.");
        }

        var logger = _loggerFactory.CreateLogger<S3RasterTileCacheProvider>();
        var clientOptions = new S3RasterTileCacheProvider.S3ClientOptions
        {
            Region = s3.Region,
            ServiceUrl = s3.ServiceUrl,
            AccessKeyId = s3.AccessKeyId,
            SecretAccessKey = s3.SecretAccessKey,
            ForcePathStyle = s3.ForcePathStyle
        };

        var client = S3RasterTileCacheProvider.CreateClient(clientOptions);
        // Factory creates the client, so provider owns it
        return new S3RasterTileCacheProvider(client, s3.BucketName!, s3.Prefix, s3.EnsureBucket, logger, _metrics, ownsClient: true);
    }

    private IRasterTileCacheProvider CreateAzureCacheProvider(RasterTileCacheConfiguration configuration)
    {
        var azure = configuration.Azure ?? RasterTileAzureConfiguration.Default;
        if (string.IsNullOrWhiteSpace(azure.ConnectionString))
        {
            throw new InvalidDataException("Raster tile cache provider 'azure' requires a connectionString.");
        }

        var containerName = string.IsNullOrWhiteSpace(azure.ContainerName)
            ? "raster-tiles"
            : azure.ContainerName;

        var blobClient = new BlobContainerClient(azure.ConnectionString, containerName);
        var logger = _loggerFactory.CreateLogger<AzureBlobRasterTileCacheProvider>();
        // Factory creates the client, so provider owns it
        return new AzureBlobRasterTileCacheProvider(blobClient, azure.EnsureContainer, logger, _metrics, ownsContainer: true);
    }
}

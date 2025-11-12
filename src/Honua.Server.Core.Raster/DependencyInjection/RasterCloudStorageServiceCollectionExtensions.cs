// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Azure.Storage.Blobs;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Raster.Cache.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.DependencyInjection;

/// <summary>
/// Extension methods for registering cloud-based raster storage providers (S3, Azure Blob, GCS COG cache).
/// </summary>
public static class RasterCloudStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers cloud-based COG cache storage providers (S3, Azure Blob, GCS).
    /// </summary>
    public static IServiceCollection AddCloudCogCacheStorage(
        this IServiceCollection services,
        RasterCacheOptions rasterCacheConfig,
        string basePath)
    {
        ArgumentNullException.ThrowIfNull(rasterCacheConfig);
        ArgumentException.ThrowIfNullOrEmpty(basePath);

        var providerKey = rasterCacheConfig.CogCacheProvider?.Trim().ToLowerInvariant() ?? "filesystem";

        switch (providerKey)
        {
            case "s3":
                services.AddSingleton<ICogCacheStorage>(sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    return CreateS3CogStorage(rasterCacheConfig, loggerFactory);
                });
                break;

            case "azure":
                services.AddSingleton<ICogCacheStorage>(sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    return CreateAzureCogStorage(rasterCacheConfig, loggerFactory);
                });
                break;

            case "gcs":
                services.AddSingleton<ICogCacheStorage>(sp =>
                {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    return CreateGcsCogStorage(rasterCacheConfig, basePath, loggerFactory);
                });
                break;
        }

        return services;
    }

    private static S3CogCacheStorage CreateS3CogStorage(RasterCacheOptions cfg, ILoggerFactory loggerFactory)
    {
        if (cfg.CogCacheS3Bucket.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException("RasterCache COG S3 bucket must be configured when using the S3 provider.");
        }

        var clientConfig = new AmazonS3Config();
        if (cfg.CogCacheS3ServiceUrl.HasValue())
        {
            clientConfig.ServiceURL = cfg.CogCacheS3ServiceUrl.Trim();
        }
        else if (cfg.CogCacheS3Region.HasValue())
        {
            clientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(cfg.CogCacheS3Region.Trim());
        }

        if (cfg.CogCacheS3ForcePathStyle)
        {
            clientConfig.ForcePathStyle = true;
        }

        AmazonS3Client client;
        if (cfg.CogCacheS3AccessKeyId.HasValue() && cfg.CogCacheS3SecretAccessKey.HasValue())
        {
            client = new AmazonS3Client(
                new BasicAWSCredentials(cfg.CogCacheS3AccessKeyId.Trim(), cfg.CogCacheS3SecretAccessKey.Trim()),
                clientConfig);
        }
        else
        {
            client = new AmazonS3Client(clientConfig);
        }

        var storageLogger = loggerFactory.CreateLogger<S3CogCacheStorage>();
        return new S3CogCacheStorage(client, cfg.CogCacheS3Bucket!, cfg.CogCacheS3Prefix, storageLogger, ownsClient: true);
    }

    private static AzureBlobCogCacheStorage CreateAzureCogStorage(RasterCacheOptions cfg, ILoggerFactory loggerFactory)
    {
        if (cfg.CogCacheAzureConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException("RasterCache COG Azure provider requires a connection string.");
        }

        var containerName = cfg.CogCacheAzureContainer.IsNullOrWhiteSpace()
            ? "cog-cache"
            : cfg.CogCacheAzureContainer.Trim();

        var containerClient = new BlobContainerClient(cfg.CogCacheAzureConnectionString, containerName);
        var storageLogger = loggerFactory.CreateLogger<AzureBlobCogCacheStorage>();
        return new AzureBlobCogCacheStorage(containerClient, cfg.CogCacheAzurePrefix, cfg.CogCacheAzureEnsureContainer, storageLogger, ownsContainer: true);
    }

    private static GcsCogCacheStorage CreateGcsCogStorage(RasterCacheOptions cfg, string basePath, ILoggerFactory loggerFactory)
    {
        if (cfg.CogCacheGcsBucket.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException("RasterCache COG GCS bucket must be configured when using the GCS provider.");
        }

        StorageClient client;
        var clientOwned = false;

        if (cfg.CogCacheGcsCredentialsPath.HasValue())
        {
            var credentialsPath = cfg.CogCacheGcsCredentialsPath;
            if (!System.IO.Path.IsPathFullyQualified(credentialsPath))
            {
                credentialsPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(basePath, credentialsPath));
            }

            var credential = GoogleCredential.FromFile(credentialsPath);
            client = StorageClient.Create(credential);
            clientOwned = true;
        }
        else
        {
            client = StorageClient.Create();
        }

        var storageLogger = loggerFactory.CreateLogger<GcsCogCacheStorage>();
        return new GcsCogCacheStorage(client, cfg.CogCacheGcsBucket!, cfg.CogCacheGcsPrefix, storageLogger, clientOwned);
    }
}

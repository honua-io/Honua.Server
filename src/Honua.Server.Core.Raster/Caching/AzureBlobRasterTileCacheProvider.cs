// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Azure Blob Storage-based raster tile cache provider implementation using CloudRasterTileCacheProviderBase.
/// </summary>
public sealed class AzureBlobRasterTileCacheProvider : CloudRasterTileCacheProviderBase<BlobContainerClient, RequestFailedException>
{
    public AzureBlobRasterTileCacheProvider(
        BlobContainerClient container,
        bool ensureContainer,
        ILogger<AzureBlobRasterTileCacheProvider> logger,
        ICircuitBreakerMetrics? metrics = null,
        bool ownsContainer = false)
        : base(container, container.Name, null, ensureContainer, logger, "Azure Blob", metrics, ownsContainer)
    {
    }

    protected override async Task<Stream?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken)
    {
        var blobClient = Client.GetBlobClient(objectKey);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Value.Content;
    }

    protected override async Task<(string? contentType, DateTimeOffset lastModified)> GetObjectMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var blobClient = Client.GetBlobClient(objectKey);
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var contentType = properties.Value.ContentType;
        var lastModified = properties.Value.LastModified == default
            ? DateTimeOffset.UtcNow
            : properties.Value.LastModified;
        return (contentType, lastModified);
    }

    protected override async Task PutObjectAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var blobClient = Client.GetBlobClient(objectKey);
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        };

        await blobClient.UploadAsync(content, options, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var blobClient = Client.GetBlobClient(objectKey);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, conditions: null, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    protected override async Task PurgeObjectsWithPrefixAsync(string prefix, string datasetId, CancellationToken cancellationToken)
    {
        await foreach (var blob in Client.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await Client.DeleteBlobIfExistsAsync(blob.Name, DeleteSnapshotsOption.IncludeSnapshots, conditions: null, cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex)
            {
                Logger.LogWarning(ex, "Failed to delete cached tile {BlobName} while purging dataset {DatasetId} from {Container}.", blob.Name, datasetId, ContainerName);
            }
        }
    }

    protected override async Task<bool> EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        await Client.CreateIfNotExistsAsync(publicAccessType: PublicAccessType.None, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    protected override bool IsNotFoundException(RequestFailedException exception)
    {
        return exception.Status == 404;
    }

    // Override TryGetAsync to use a more efficient implementation that gets metadata from the initial download
    public override async ValueTask<RasterTileCacheHit?> TryGetAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        var blobClient = Client.GetBlobClient(BuildObjectKey(key));
        try
        {
            return await CircuitBreaker.ExecuteAsync(async ct =>
            {
                var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct).ConfigureAwait(false);
                await using var stream = response.Value.Content;
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, ct).ConfigureAwait(false);
                var bytes = memory.ToArray();
                var contentType = response.Value.Details.ContentType ?? key.Format;
                var lastModified = response.Value.Details.LastModified == default
                    ? DateTimeOffset.UtcNow
                    : response.Value.Details.LastModified;
                return new RasterTileCacheHit(bytes, contentType, lastModified);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Cache.Storage;

/// <summary>
/// Stores cached COG files in Azure Blob Storage.
/// </summary>
public sealed class AzureBlobCogCacheStorage : CloudStorageCacheProviderBase, IAsyncDisposable
{
    private readonly BlobContainerClient _container;
    private readonly bool _ownsContainer;

    public AzureBlobCogCacheStorage(
        BlobContainerClient container,
        string? prefix,
        bool ensureContainer,
        ILogger<AzureBlobCogCacheStorage> logger,
        bool ownsContainer = false)
        : base(container?.Name ?? throw new ArgumentNullException(nameof(container)), prefix, logger)
    {
        _container = container;
        _ownsContainer = ownsContainer;

        if (ensureContainer)
        {
            _container.CreateIfNotExists(PublicAccessType.None);
        }
    }

    protected override async Task<CogStorageMetadata?> GetMetadataInternalAsync(string objectKey, CancellationToken cancellationToken)
    {
        var blobClient = _container.GetBlobClient(objectKey);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return new CogStorageMetadata(
            blobClient.Uri.ToString(),
            properties.Value.ContentLength,
            properties.Value.LastModified.UtcDateTime);
    }

    protected override async Task<CogStorageMetadata> UploadInternalAsync(
        string objectKey,
        Stream fileStream,
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var blobClient = _container.GetBlobClient(objectKey);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "image/tiff"
            }
        };

        await blobClient.UploadAsync(fileStream, uploadOptions, cancellationToken).ConfigureAwait(false);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return new CogStorageMetadata(
            blobClient.Uri.ToString(),
            properties.Value.ContentLength,
            properties.Value.LastModified.UtcDateTime);
    }

    protected override async Task DeleteInternalAsync(string objectKey, CancellationToken cancellationToken)
    {
        var blobClient = _container.GetBlobClient(objectKey);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    protected override bool IsNotFoundException(Exception exception)
    {
        return exception is RequestFailedException { Status: 404 };
    }

    protected override string BuildStorageUri(string objectKey)
    {
        return _container.GetBlobClient(objectKey).Uri.ToString();
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsContainer && _container is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}

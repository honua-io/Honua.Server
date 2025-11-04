// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Cache.Storage;

/// <summary>
/// Stores cached COG files in Google Cloud Storage.
/// </summary>
public sealed class GcsCogCacheStorage : CloudStorageCacheProviderBase, IAsyncDisposable
{
    private readonly StorageClient _client;
    private readonly bool _clientOwned;

    public GcsCogCacheStorage(
        StorageClient client,
        string bucket,
        string? prefix,
        ILogger<GcsCogCacheStorage> logger,
        bool clientOwned = false)
        : base(bucket, prefix, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _clientOwned = clientOwned;
    }

    protected override async Task<CogStorageMetadata?> GetMetadataInternalAsync(string objectKey, CancellationToken cancellationToken)
    {
        var obj = await _client.GetObjectAsync(Bucket, objectKey, cancellationToken: cancellationToken).ConfigureAwait(false);

        var size = obj.Size.HasValue ? (long)obj.Size.Value : 0L;
        var updated = obj.Updated.HasValue
            ? NormalizeToUtc(obj.Updated.Value)
            : DateTime.UtcNow;

        return new CogStorageMetadata(
            BuildStorageUri(objectKey),
            size,
            updated);
    }

    protected override async Task<CogStorageMetadata> UploadInternalAsync(
        string objectKey,
        Stream fileStream,
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var uploadedObject = await _client.UploadObjectAsync(
            bucket: Bucket,
            objectName: objectKey,
            contentType: "image/tiff",
            source: fileStream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var size = uploadedObject.Size.HasValue ? (long)uploadedObject.Size.Value : fileStream.Length;
        var updated = uploadedObject.Updated.HasValue
            ? NormalizeToUtc(uploadedObject.Updated.Value)
            : DateTime.UtcNow;

        return new CogStorageMetadata(
            BuildStorageUri(objectKey),
            size,
            updated);
    }

    protected override async Task DeleteInternalAsync(string objectKey, CancellationToken cancellationToken)
    {
        await _client.DeleteObjectAsync(Bucket, objectKey, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    protected override bool IsNotFoundException(Exception exception)
    {
        return exception is GoogleApiException { HttpStatusCode: HttpStatusCode.NotFound };
    }

    protected override string BuildStorageUri(string objectKey)
    {
        return $"gs://{Bucket}/{objectKey}";
    }

    public ValueTask DisposeAsync()
    {
        if (_clientOwned && _client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}

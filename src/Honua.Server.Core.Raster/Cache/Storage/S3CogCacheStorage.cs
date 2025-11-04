// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Raster.Cache.Storage;

/// <summary>
/// Stores cached COG files in an Amazon S3 bucket.
/// </summary>
public sealed class S3CogCacheStorage : CloudStorageCacheProviderBase, IAsyncDisposable
{
    private readonly IAmazonS3 _client;
    private readonly bool _ownsClient;

    public S3CogCacheStorage(
        IAmazonS3 client,
        string bucket,
        string? prefix,
        ILogger<S3CogCacheStorage> logger,
        bool ownsClient = false)
        : base(bucket, prefix, logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
    }

    protected override async Task<CogStorageMetadata?> GetMetadataInternalAsync(string objectKey, CancellationToken cancellationToken)
    {
        var response = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = Bucket,
            Key = objectKey
        }, cancellationToken).ConfigureAwait(false);

        var lastModified = NormalizeToUtc(response.LastModified ?? DateTime.UtcNow);

        return new CogStorageMetadata(
            BuildStorageUri(objectKey),
            response.ContentLength,
            lastModified);
    }

    protected override async Task<CogStorageMetadata> UploadInternalAsync(
        string objectKey,
        Stream fileStream,
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = Bucket,
            Key = objectKey,
            InputStream = fileStream,
            AutoCloseStream = false,
            ContentType = "image/tiff"
        };

        await _client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);

        // Fetch metadata to get LastModified (PutObject returns ETag but not timestamp)
        var metadata = await GetMetadataInternalAsync(objectKey, cancellationToken).ConfigureAwait(false);

        // Fallback to local file information if HEAD fails
        return metadata ?? CreateFallbackMetadata(BuildStorageUri(objectKey), fileInfo);
    }

    protected override async Task DeleteInternalAsync(string objectKey, CancellationToken cancellationToken)
    {
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = Bucket,
            Key = objectKey
        }, cancellationToken).ConfigureAwait(false);
    }

    protected override bool IsNotFoundException(Exception exception)
    {
        return exception is AmazonS3Exception { StatusCode: HttpStatusCode.NotFound };
    }

    protected override string BuildStorageUri(string objectKey)
    {
        return $"s3://{Bucket}/{objectKey}";
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient && _client is IDisposable disposableClient)
        {
            disposableClient.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}

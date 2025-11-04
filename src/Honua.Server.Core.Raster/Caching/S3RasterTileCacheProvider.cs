// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// S3-based raster tile cache provider implementation using CloudRasterTileCacheProviderBase.
/// </summary>
public sealed class S3RasterTileCacheProvider : CloudRasterTileCacheProviderBase<IAmazonS3, AmazonS3Exception>
{
    public S3RasterTileCacheProvider(
        IAmazonS3 s3,
        string bucketName,
        string? prefix,
        bool ensureBucket,
        ILogger<S3RasterTileCacheProvider> logger,
        ICircuitBreakerMetrics? metrics = null,
        bool ownsClient = false)
        : base(s3, bucketName, prefix, ensureBucket, logger, "S3", metrics, ownsClient)
    {
    }

    protected override async Task<Stream?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken)
    {
        var request = new GetObjectRequest
        {
            BucketName = ContainerName,
            Key = objectKey
        };

        var response = await Client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
        return new S3ResponseStream(response);
    }

    protected override Task<(string? contentType, DateTimeOffset lastModified)> GetObjectMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        // Metadata is included in the GetObject response, so we can extract it from the stream wrapper
        // For S3, we'll get this from the response that was already fetched
        return Task.FromResult<(string?, DateTimeOffset)>((null, DateTimeOffset.UtcNow)); // Will be overridden by the response stream
    }

    protected override async Task PutObjectAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = ContainerName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = true
        };

        await Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = ContainerName,
            Key = objectKey
        };

        await Client.DeleteObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task PurgeObjectsWithPrefixAsync(string prefix, string datasetId, CancellationToken cancellationToken)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = ContainerName,
            Prefix = prefix
        };

        do
        {
            var response = await Client.ListObjectsV2Async(request, cancellationToken).ConfigureAwait(false);
            if (response.S3Objects.Count == 0)
            {
                break;
            }

            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = ContainerName
            };

            foreach (var s3Object in response.S3Objects)
            {
                deleteRequest.AddKey(s3Object.Key);
            }

            try
            {
                await Client.DeleteObjectsAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex)
            {
                Logger.LogWarning(ex, "Failed to purge one or more cached tiles for dataset {DatasetId} in bucket {Bucket}.", datasetId, ContainerName);
                break;
            }

            request.ContinuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (!request.ContinuationToken.IsNullOrEmpty());
    }

    protected override async Task<bool> EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (!await AmazonS3Util.DoesS3BucketExistV2Async(Client, ContainerName).ConfigureAwait(false))
        {
            try
            {
                await Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = ContainerName
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                Logger.LogDebug("Bucket {Bucket} already exists when attempting creation for raster cache.", ContainerName);
            }
        }

        return true;
    }

    protected override bool IsNotFoundException(AmazonS3Exception exception)
    {
        return exception.StatusCode == HttpStatusCode.NotFound ||
               string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase);
    }

    // Override TryGetAsync to use a more efficient implementation that gets metadata from the initial request
    public override async ValueTask<RasterTileCacheHit?> TryGetAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        var objectKey = BuildObjectKey(key);
        try
        {
            return await CircuitBreaker.ExecuteAsync(async ct =>
            {
                var request = new GetObjectRequest
                {
                    BucketName = ContainerName,
                    Key = objectKey
                };

                using var response = await Client.GetObjectAsync(request, ct).ConfigureAwait(false);
                await using var responseStream = response.ResponseStream;
                using var memory = new MemoryStream();
                await responseStream.CopyToAsync(memory, ct).ConfigureAwait(false);
                var bytes = memory.ToArray();
                var created = response.LastModified.HasValue && response.LastModified.Value != DateTime.MinValue
                    ? DateTime.SpecifyKind(response.LastModified.Value, DateTimeKind.Utc)
                    : DateTimeOffset.UtcNow;

                return new RasterTileCacheHit(bytes, response.Headers.ContentType ?? key.Format, created);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex) when (IsNotFoundException(ex))
        {
            return null;
        }
    }

    private sealed class S3ResponseStream : Stream
    {
        private readonly GetObjectResponse _response;
        private readonly Stream _inner;

        public S3ResponseStream(GetObjectResponse response)
        {
            _response = response;
            _inner = response.ResponseStream;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
            _response.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    public static IAmazonS3 CreateClient(S3ClientOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle,
        };

        if (options.Region.HasValue())
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        if (options.ServiceUrl.HasValue())
        {
            config.ServiceURL = options.ServiceUrl;
        }

        if (options.AccessKeyId.HasValue() && options.SecretAccessKey.HasValue())
        {
            var credentials = new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey);
            return new AmazonS3Client(credentials, config);
        }

        return new AmazonS3Client(config);
    }

    public sealed class S3ClientOptions
    {
        public string? Region { get; set; }

        public string? ServiceUrl { get; set; }

        public string? AccessKeyId { get; set; }

        public string? SecretAccessKey { get; set; }

        public bool ForcePathStyle { get; set; }
    }
}

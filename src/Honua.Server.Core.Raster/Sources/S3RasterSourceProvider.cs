// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Polly;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

public sealed class S3RasterSourceProvider : IRasterSourceProvider, IAsyncDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly bool _ownsClient;
    private readonly ResiliencePipeline _circuitBreaker;

    public S3RasterSourceProvider(IAmazonS3 s3Client, ILogger<S3RasterSourceProvider> logger, bool ownsClient = false)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _ownsClient = ownsClient;
        _circuitBreaker = Caching.ExternalServiceResiliencePolicies.CreateCircuitBreakerPipeline("S3 Source", logger);
    }

    public string ProviderKey => "s3";

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return uri.StartsWith("s3://", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        var (bucket, key) = ParseS3Uri(uri);

        return await _circuitBreaker.ExecuteAsync(async ct =>
        {
            var request = new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request, ct).ConfigureAwait(false);
            return response.ResponseStream;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null, CancellationToken cancellationToken = default)
    {
        var (bucket, key) = ParseS3Uri(uri);

        return await _circuitBreaker.ExecuteAsync(async ct =>
        {
            var request = new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            };

            // S3 range header format: "bytes=start-end"
            if (length.HasValue)
            {
                var end = offset + length.Value - 1;
                request.ByteRange = new ByteRange(offset, end);
            }
            else
            {
                request.ByteRange = new ByteRange(offset, long.MaxValue);
            }

            var response = await _s3Client.GetObjectAsync(request, ct).ConfigureAwait(false);
            return response.ResponseStream;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static (string bucket, string key) ParseS3Uri(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) || parsedUri.Scheme != "s3")
        {
            throw new ArgumentException($"Invalid S3 URI: {uri}", nameof(uri));
        }

        var bucket = parsedUri.Host;
        var key = parsedUri.AbsolutePath.TrimStart('/');

        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new ArgumentException($"S3 bucket not specified in URI: {uri}", nameof(uri));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException($"S3 key not specified in URI: {uri}", nameof(uri));
        }

        return (bucket, key);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsClient)
        {
            if (_s3Client is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (_s3Client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

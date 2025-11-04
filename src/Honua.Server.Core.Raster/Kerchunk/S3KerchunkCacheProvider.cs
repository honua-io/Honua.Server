// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Polly;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Kerchunk;

/// <summary>
/// S3-based cache provider for kerchunk references.
/// Stores JSON reference files in an S3 bucket.
/// </summary>
public sealed class S3KerchunkCacheProvider : IKerchunkCacheProvider, IAsyncDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly bool _ownsClient;
    private readonly string _bucketName;
    private readonly string _prefix;
    private readonly ILogger<S3KerchunkCacheProvider> _logger;
    private readonly ResiliencePipeline _circuitBreaker;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public S3KerchunkCacheProvider(
        IAmazonS3 s3Client,
        string bucketName,
        string prefix,
        ILogger<S3KerchunkCacheProvider> logger,
        bool ownsClient = false)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _ownsClient = ownsClient;
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        _prefix = prefix ?? "kerchunk-refs";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _circuitBreaker = Caching.ExternalServiceResiliencePolicies.CreateCircuitBreakerPipeline(
            "S3 Kerchunk Cache",
            logger);
    }

    public async Task<KerchunkReferences?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var s3Key = GetS3Key(key);

        try
        {
            return await _circuitBreaker.ExecuteAsync(async ct =>
            {
                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                };

                var response = await _s3Client.GetObjectAsync(request, ct);

                await using var stream = response.ResponseStream;
                var refs = await JsonSerializer.DeserializeAsync<KerchunkReferences>(
                    stream,
                    JsonOptions,
                    ct);

                _logger.LogDebug("Cache hit for key: {Key} (S3: {S3Key})", key, s3Key);
                return refs;
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Cache miss for key: {Key} (S3: {S3Key})", key, s3Key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve cached kerchunk references for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(
        string key,
        KerchunkReferences references,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var s3Key = GetS3Key(key);

        try
        {
            await _circuitBreaker.ExecuteAsync(async ct =>
            {
                // Serialize to JSON
                var json = JsonSerializer.Serialize(references, JsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);

                await using var stream = new MemoryStream(bytes);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key,
                    InputStream = stream,
                    ContentType = "application/json",
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
                };

                // Add metadata
                request.Metadata.Add("generated-at", references.GeneratedAt.ToString("O"));
                if (references.SourceUri != null)
                {
                    request.Metadata.Add("source-uri", references.SourceUri);
                }

                // Set expiration if TTL specified
                if (ttl.HasValue)
                {
                    var expiresAt = DateTimeOffset.UtcNow.Add(ttl.Value);
                    request.Headers.Expires = expiresAt.UtcDateTime;
                }

                await _s3Client.PutObjectAsync(request, ct);

                _logger.LogDebug(
                    "Cached kerchunk references for key: {Key} (S3: {S3Key}, {Size} bytes)",
                    key, s3Key, bytes.Length);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache kerchunk references for key: {Key}", key);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var s3Key = GetS3Key(key);

        try
        {
            return await _circuitBreaker.ExecuteAsync(async ct =>
            {
                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                };

                await _s3Client.GetObjectMetadataAsync(request, ct);
                return true;
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check existence of cached kerchunk references for key: {Key}", key);
            return false;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var s3Key = GetS3Key(key);

        try
        {
            await _circuitBreaker.ExecuteAsync(async ct =>
            {
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key
                };

                await _s3Client.DeleteObjectAsync(request, ct);

                _logger.LogDebug("Deleted cached kerchunk references for key: {Key} (S3: {S3Key})", key, s3Key);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete cached kerchunk references for key: {Key}", key);
        }
    }

    private string GetS3Key(string key)
    {
        // Build S3 key: prefix/hash.json
        // Sanitize key to ensure it's S3-safe
        var sanitizedKey = key.Replace('\\', '/');
        return $"{_prefix}/{sanitizedKey}.json";
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

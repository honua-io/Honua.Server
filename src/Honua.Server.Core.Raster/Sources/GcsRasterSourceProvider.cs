// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

/// <summary>
/// Provides raster data from Google Cloud Storage buckets.
/// </summary>
public sealed class GcsRasterSourceProvider : IRasterSourceProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GcsRasterSourceProvider> _logger;
    private readonly StorageClient _storageClient;
    private readonly string _defaultBucket;

    public string ProviderKey => "gcs";

    public GcsRasterSourceProvider(
        IConfiguration configuration,
        ILogger<GcsRasterSourceProvider> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _storageClient = StorageClient.Create();

        _defaultBucket = _configuration["GoogleCloud:Storage:RasterBucket"]
            ?? throw new InvalidOperationException("GCS raster bucket not configured");

        _logger.LogInformation("Initialized GCS raster source provider with bucket: {Bucket}", _defaultBucket);
    }

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        // Handle gs:// URLs
        if (uri.StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
            return true;

        // Handle gcs: prefix
        if (uri.StartsWith("gcs:", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public async Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        try
        {
            var (bucket, objectName) = ParseGcsPath(uri);

            _logger.LogDebug("Opening stream for GCS object: {Bucket}/{Object}", bucket, objectName);

            var stream = new MemoryStream();
            await _storageClient.DownloadObjectAsync(bucket, objectName, stream, cancellationToken: cancellationToken);
            stream.Position = 0;

            _logger.LogDebug("Successfully opened stream for: {Bucket}/{Object}, Size: {Size} bytes",
                bucket, objectName, stream.Length);

            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening stream from GCS: {Uri}", uri);
            throw;
        }
    }

    public async Task<Stream> OpenReadRangeAsync(
        string uri,
        long offset,
        long? length = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (bucket, objectName) = ParseGcsPath(uri);

            _logger.LogDebug("Opening range stream for GCS object: {Bucket}/{Object}, Offset: {Offset}, Length: {Length}",
                bucket, objectName, offset, length);

            // Get object metadata to validate range
            var obj = await _storageClient.GetObjectAsync(bucket, objectName, cancellationToken: cancellationToken).ConfigureAwait(false);
            var objectSize = (long)(obj.Size ?? 0);

            if (offset >= objectSize)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset exceeds object size");
            }

            var actualLength = length.HasValue
                ? Math.Min(length.Value, objectSize - offset)
                : objectSize - offset;

            // Download the range
            var stream = new MemoryStream();
            var downloadOptions = new DownloadObjectOptions
            {
                Range = new System.Net.Http.Headers.RangeHeaderValue(offset, offset + actualLength - 1)
            };

            await _storageClient.DownloadObjectAsync(
                bucket,
                objectName,
                stream,
                downloadOptions,
                cancellationToken);

            stream.Position = 0;

            _logger.LogDebug("Successfully opened range stream: {Bucket}/{Object}, Downloaded: {Size} bytes",
                bucket, objectName, stream.Length);

            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening range stream from GCS: {Uri}", uri);
            throw;
        }
    }

    private (string bucket, string objectName) ParseGcsPath(string uri)
    {
        // Handle gs:// URLs
        if (uri.StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri.Substring(5);
            var parts = path.Split('/', 2);
            return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], string.Empty);
        }

        // Handle gcs: prefix
        if (uri.StartsWith("gcs:", StringComparison.OrdinalIgnoreCase))
        {
            uri = uri.Substring(4).TrimStart('/');
        }

        // Handle bucket/path format
        if (uri.Contains('/'))
        {
            var parts = uri.Split('/', 2);
            return (parts[0], parts[1]);
        }

        // Default to configured bucket
        return (_defaultBucket, uri);
    }
}

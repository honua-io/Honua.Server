// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Google Cloud Storage-based raster tile cache provider implementation using CloudRasterTileCacheProviderBase.
/// </summary>
public sealed class GcsRasterTileCacheProvider : CloudRasterTileCacheProviderBase<StorageClient, Google.GoogleApiException>
{
    private readonly string _bucketName;

    public GcsRasterTileCacheProvider(
        StorageClient storage,
        string bucketName,
        string? prefix,
        bool ensureBucket,
        ILogger<GcsRasterTileCacheProvider> logger,
        ICircuitBreakerMetrics? metrics = null,
        bool ownsClient = false)
        : base(storage, bucketName, prefix, ensureBucket, logger, "GCS", metrics, ownsClient)
    {
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    protected override async Task<Stream?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken)
    {
        var memory = new MemoryStream();
        await Client.DownloadObjectAsync(
            _bucketName,
            objectKey,
            memory,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        memory.Position = 0;
        return memory;
    }

    protected override async Task<(string? contentType, DateTimeOffset lastModified)> GetObjectMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var obj = await Client.GetObjectAsync(
            _bucketName,
            objectKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var contentType = obj.ContentType;
        var lastModified = obj.UpdatedDateTimeOffset ?? DateTimeOffset.UtcNow;
        return (contentType, lastModified);
    }

    protected override async Task PutObjectAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        await Client.UploadObjectAsync(
            _bucketName,
            objectKey,
            contentType,
            content,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    protected override async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        await Client.DeleteObjectAsync(_bucketName, objectKey, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    protected override async Task PurgeObjectsWithPrefixAsync(string prefix, string datasetId, CancellationToken cancellationToken)
    {
        var objects = Client.ListObjectsAsync(_bucketName, prefix, new ListObjectsOptions { });
        await foreach (var obj in objects.WithCancellation(cancellationToken))
        {
            try
            {
                await Client.DeleteObjectAsync(_bucketName, obj.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Google.GoogleApiException ex)
            {
                Logger.LogWarning(ex, "Failed to delete cached tile {ObjectName} for dataset {DatasetId} in bucket {Bucket}.", obj.Name, datasetId, _bucketName);
            }
        }
    }

    protected override async Task<bool> EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try to get the bucket to check if it exists
            await Client.GetBucketAsync(_bucketName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            try
            {
                // Bucket doesn't exist, create it
                await Client.CreateBucketAsync(
                    Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "honua-project",
                    _bucketName,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                Logger.LogInformation("Created GCS bucket {Bucket} for raster tile cache.", _bucketName);
            }
            catch (Google.GoogleApiException createEx) when (createEx.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
            {
                Logger.LogDebug("Bucket {Bucket} already exists when attempting creation for raster cache.", _bucketName);
            }
        }

        return true;
    }

    protected override bool IsNotFoundException(Google.GoogleApiException exception)
    {
        return exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound;
    }

    // Override TryGetAsync to use a more efficient implementation that gets metadata from the initial download
    public override async ValueTask<RasterTileCacheHit?> TryGetAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        var objectName = BuildObjectKey(key);
        try
        {
            return await CircuitBreaker.ExecuteAsync(async ct =>
            {
                using var memory = new MemoryStream();
                var gcsObject = await Client.DownloadObjectAsync(
                    _bucketName,
                    objectName,
                    memory,
                    cancellationToken: ct).ConfigureAwait(false);

                var bytes = memory.ToArray();
                var created = gcsObject.UpdatedDateTimeOffset ?? DateTimeOffset.UtcNow;

                return new RasterTileCacheHit(bytes, gcsObject.ContentType ?? key.Format, created);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public static StorageClient CreateClient(GcsClientOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.EmulatorHost.HasValue())
        {
            // For testing with fake-gcs-server emulator
            Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", options.EmulatorHost);
            return StorageClient.Create();
        }

        if (options.CredentialsJson.HasValue())
        {
            var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(options.CredentialsJson);
            return StorageClient.Create(credential);
        }

        // Use default credentials (from environment or ADC)
        return StorageClient.Create();
    }

    public sealed class GcsClientOptions
    {
        public string? EmulatorHost { get; set; }

        public string? CredentialsJson { get; set; }
    }
}

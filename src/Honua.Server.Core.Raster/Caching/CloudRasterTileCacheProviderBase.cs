// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Polly;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Base class for cloud-based raster tile cache providers (S3, Azure Blob, GCS).
/// Provides common implementations for circuit breaker integration, stream reading, and error handling.
/// </summary>
/// <typeparam name="TClient">The cloud storage client type (e.g., IAmazonS3, BlobContainerClient).</typeparam>
/// <typeparam name="TException">The cloud-specific exception type for 404 handling.</typeparam>
public abstract class CloudRasterTileCacheProviderBase<TClient, TException> : IRasterTileCacheProvider, IAsyncDisposable
    where TException : Exception
{
    private readonly TClient _client;
    private readonly bool _ownsClient;
    private readonly string _containerName;
    private readonly string _prefix;
    private readonly bool _ensureContainer;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _containerReady;
    private readonly ResiliencePipeline _circuitBreaker;

    /// <summary>
    /// Initializes a new instance of the cloud raster tile cache provider.
    /// </summary>
    /// <param name="client">The cloud storage client.</param>
    /// <param name="containerName">The container/bucket name.</param>
    /// <param name="prefix">Optional prefix for tile keys.</param>
    /// <param name="ensureContainer">Whether to ensure the container exists.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceName">The service name for circuit breaker logging.</param>
    /// <param name="metrics">Optional circuit breaker metrics collector.</param>
    /// <param name="ownsClient">Whether this instance should dispose the client.</param>
    protected CloudRasterTileCacheProviderBase(
        TClient client,
        string containerName,
        string? prefix,
        bool ensureContainer,
        ILogger logger,
        string serviceName,
        ICircuitBreakerMetrics? metrics = null,
        bool ownsClient = false)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _containerName = string.IsNullOrWhiteSpace(containerName)
            ? throw new ArgumentNullException(nameof(containerName))
            : containerName;
        _prefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix!.Trim().Trim('/') + "/";
        _ensureContainer = ensureContainer;
        _ownsClient = ownsClient;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _circuitBreaker = ExternalServiceResiliencePolicies.CreateCircuitBreakerPipeline(serviceName, logger, metrics);
    }

    /// <summary>
    /// Gets the cloud storage client.
    /// </summary>
    protected TClient Client => _client;

    /// <summary>
    /// Gets the container/bucket name.
    /// </summary>
    protected string ContainerName => _containerName;

    /// <summary>
    /// Gets the prefix for tile keys.
    /// </summary>
    protected string TilePrefix => _prefix;

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Gets the circuit breaker pipeline.
    /// </summary>
    protected ResiliencePipeline CircuitBreaker => _circuitBreaker;

    /// <inheritdoc/>
    public virtual async ValueTask<RasterTileCacheHit?> TryGetAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        if (!await EnsureContainerAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var objectKey = BuildObjectKey(key);
        try
        {
            var stream = await GetObjectStreamAsync(objectKey, cancellationToken).ConfigureAwait(false);
            if (stream == null)
            {
                return null;
            }

            try
            {
                // Read stream into memory
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
                var bytes = memory.ToArray();

                var (contentType, lastModified) = await GetObjectMetadataAsync(objectKey, cancellationToken).ConfigureAwait(false);
                var finalContentType = contentType ?? key.Format;
                var finalLastModified = lastModified == default ? DateTimeOffset.UtcNow : lastModified;

                return new RasterTileCacheHit(bytes, finalContentType, finalLastModified);
            }
            finally
            {
                if (stream is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (stream is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        catch (TException ex) when (IsNotFoundException(ex))
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task StoreAsync(RasterTileCacheKey key, RasterTileCacheEntry entry, CancellationToken cancellationToken = default)
    {
        if (!await EnsureContainerAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var objectKey = BuildObjectKey(key);
        using var stream = new MemoryStream(entry.Content.ToArray());
        await PutObjectAsync(objectKey, stream, entry.ContentType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        if (!await EnsureContainerAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var objectKey = BuildObjectKey(key);
        try
        {
            await DeleteObjectAsync(objectKey, cancellationToken).ConfigureAwait(false);
        }
        catch (TException ex) when (IsNotFoundException(ex))
        {
            _logger.LogDebug("Attempted to delete missing raster tile {Key} in container {Container}.", objectKey, _containerName);
        }
    }

    /// <inheritdoc/>
    public async Task PurgeDatasetAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(datasetId))
            throw new ArgumentException("Dataset ID cannot be null or empty", nameof(datasetId));

        if (!await EnsureContainerAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var datasetPrefix = _prefix + RasterTileCachePathHelper.GetDatasetPrefix(datasetId, '/');
        await PurgeObjectsWithPrefixAsync(datasetPrefix, datasetId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets an object stream from cloud storage.
    /// </summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The object stream, or null if not found.</returns>
    protected abstract Task<Stream?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Gets object metadata (content type and last modified date).
    /// </summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the content type and last modified date.</returns>
    protected abstract Task<(string? contentType, DateTimeOffset lastModified)> GetObjectMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Uploads an object to cloud storage.
    /// </summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="content">The content stream.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task PutObjectAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an object from cloud storage.
    /// </summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Purges all objects with the given prefix.
    /// </summary>
    /// <param name="prefix">The prefix to purge.</param>
    /// <param name="datasetId">The dataset ID (for logging).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task PurgeObjectsWithPrefixAsync(string prefix, string datasetId, CancellationToken cancellationToken);

    /// <summary>
    /// Ensures the container/bucket exists.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the container is ready, false otherwise.</returns>
    protected abstract Task<bool> EnsureContainerExistsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether the exception represents a not found error.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception indicates the object was not found.</returns>
    protected abstract bool IsNotFoundException(TException exception);

    /// <summary>
    /// Builds the full object key from a raster tile cache key.
    /// </summary>
    protected string BuildObjectKey(RasterTileCacheKey key)
    {
        var relative = RasterTileCachePathHelper.GetRelativePath(key, '/');
        return string.IsNullOrEmpty(_prefix) ? relative : string.Concat(_prefix, relative);
    }

    /// <summary>
    /// Ensures the container is initialized.
    /// </summary>
    private async Task<bool> EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (!_ensureContainer)
        {
            return true;
        }

        if (_containerReady)
        {
            return true;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_containerReady)
            {
                return true;
            }

            var result = await EnsureContainerExistsAsync(cancellationToken).ConfigureAwait(false);
            _containerReady = result;
            return result;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _initializationLock.Dispose();

        if (_ownsClient)
        {
            if (_client is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (_client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        GC.SuppressFinalize(this);
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Raster.Cache.Storage;

/// <summary>
/// Abstract base class for cloud storage-backed COG cache providers.
/// Consolidates common patterns across S3, Azure Blob Storage, and Google Cloud Storage implementations.
/// </summary>
/// <remarks>
/// This class implements the Template Method pattern, providing shared infrastructure for:
/// <list type="bullet">
/// <item>Parameter validation and normalization</item>
/// <item>Consistent logging patterns</item>
/// <item>Key/object name generation with prefix support</item>
/// <item>UTC timestamp handling</item>
/// <item>Exception handling and error categorization</item>
/// </list>
/// Derived classes implement provider-specific storage operations.
/// </remarks>
public abstract class CloudStorageCacheProviderBase : ICogCacheStorage
{
    private readonly string _bucket;
    private readonly string? _prefix;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the base cloud storage provider.
    /// </summary>
    /// <param name="bucket">The bucket or container name.</param>
    /// <param name="prefix">Optional prefix to prepend to all object keys. Leading/trailing slashes are trimmed.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="bucket"/> or <paramref name="logger"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="bucket"/> is empty or whitespace.</exception>
    protected CloudStorageCacheProviderBase(string bucket, string? prefix, ILogger logger)
    {
        _bucket = bucket.IsNullOrWhiteSpace()
            ? throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucket))
            : bucket.Trim();
        _prefix = prefix?.Trim().Trim('/');
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the bucket or container name.
    /// </summary>
    protected string Bucket => _bucket;

    /// <summary>
    /// Gets the optional prefix for all object keys.
    /// </summary>
    protected string? Prefix => _prefix;

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    protected ILogger Logger => _logger;

    #region ICogCacheStorage Implementation

    /// <inheritdoc/>
    public async Task<CogStorageMetadata?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        var objectKey = BuildObjectKey(cacheKey);

        try
        {
            var metadata = await GetMetadataInternalAsync(objectKey, cancellationToken).ConfigureAwait(false);

            if (metadata != null)
            {
                LogCacheMetadataRetrieved(objectKey);
            }

            return metadata;
        }
        catch (Exception ex) when (IsNotFoundException(ex))
        {
            // Not found is a normal condition, not logged as error
            return null;
        }
        catch (Exception ex)
        {
            LogOperationError("retrieve metadata", objectKey, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CogStorageMetadata> SaveAsync(string cacheKey, string localFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(localFilePath);

        var objectKey = BuildObjectKey(cacheKey);
        var storageUri = BuildStorageUri(objectKey);

        LogUploadStarting(storageUri);

        try
        {
            await using var fileStream = File.OpenRead(localFilePath);
            var fileInfo = new FileInfo(localFilePath);

            var metadata = await UploadInternalAsync(
                objectKey,
                fileStream,
                fileInfo,
                cancellationToken).ConfigureAwait(false);

            LogUploadCompleted(storageUri, metadata.SizeBytes);
            return metadata;
        }
        catch (Exception ex)
        {
            LogOperationError("upload", objectKey, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        var objectKey = BuildObjectKey(cacheKey);

        try
        {
            await DeleteInternalAsync(objectKey, cancellationToken).ConfigureAwait(false);
            LogDeletionCompleted(objectKey);
        }
        catch (Exception ex) when (IsNotFoundException(ex))
        {
            // Already deleted or never existed - this is idempotent, so no error
            LogDeletionSkipped(objectKey);
        }
        catch (Exception ex)
        {
            LogOperationError("delete", objectKey, ex);
            throw;
        }
    }

    #endregion

    #region Template Methods - Derived Classes Implement These

    /// <summary>
    /// Retrieves metadata for an object from the cloud storage provider.
    /// </summary>
    /// <param name="objectKey">The full object key/name including any prefix.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata if the object exists; null otherwise.</returns>
    /// <remarks>
    /// Implementations should:
    /// <list type="bullet">
    /// <item>Return null when the object does not exist (404/NotFound)</item>
    /// <item>Throw provider-specific exceptions for other errors</item>
    /// <item>Ensure LastModifiedUtc is in UTC timezone</item>
    /// </list>
    /// </remarks>
    protected abstract Task<CogStorageMetadata?> GetMetadataInternalAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a file stream to the cloud storage provider.
    /// </summary>
    /// <param name="objectKey">The full object key/name including any prefix.</param>
    /// <param name="fileStream">The file stream to upload.</param>
    /// <param name="fileInfo">File information for fallback metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata describing the uploaded object.</returns>
    /// <remarks>
    /// Implementations should:
    /// <list type="bullet">
    /// <item>Set content type to "image/tiff"</item>
    /// <item>Overwrite existing objects if they exist</item>
    /// <item>Return accurate size and last modified timestamp</item>
    /// <item>Use <paramref name="fileInfo"/> for fallback metadata if the provider doesn't return it</item>
    /// </list>
    /// </remarks>
    protected abstract Task<CogStorageMetadata> UploadInternalAsync(
        string objectKey,
        Stream fileStream,
        FileInfo fileInfo,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an object from the cloud storage provider.
    /// </summary>
    /// <param name="objectKey">The full object key/name including any prefix.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Implementations should:
    /// <list type="bullet">
    /// <item>Be idempotent - deleting a non-existent object should not throw</item>
    /// <item>Throw provider-specific exceptions for access errors or service failures</item>
    /// </list>
    /// </remarks>
    protected abstract Task DeleteInternalAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Determines if an exception represents a "not found" condition (404, object doesn't exist).
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if this is a not-found exception; false otherwise.</returns>
    /// <remarks>
    /// Implementations should return true for provider-specific 404/NotFound exceptions:
    /// <list type="bullet">
    /// <item>S3: AmazonS3Exception with StatusCode == HttpStatusCode.NotFound</item>
    /// <item>Azure: RequestFailedException with Status == 404</item>
    /// <item>GCS: GoogleApiException with HttpStatusCode == HttpStatusCode.NotFound</item>
    /// </list>
    /// </remarks>
    protected abstract bool IsNotFoundException(Exception exception);

    /// <summary>
    /// Builds the provider-specific storage URI for an object.
    /// </summary>
    /// <param name="objectKey">The full object key/name including any prefix.</param>
    /// <returns>A URI string representing the object's location (e.g., s3://bucket/key, gs://bucket/key).</returns>
    protected abstract string BuildStorageUri(string objectKey);

    #endregion

    #region Shared Helper Methods

    /// <summary>
    /// Builds the full object key/name by combining the cache key with the prefix and file extension.
    /// </summary>
    /// <param name="cacheKey">The cache key generated by the raster cache service.</param>
    /// <returns>The full object key/name including prefix (if configured) and .tif extension.</returns>
    /// <remarks>
    /// Format: [prefix/]cacheKey.tif
    /// <para>
    /// Examples:
    /// <list type="bullet">
    /// <item>No prefix: "abc123" -> "abc123.tif"</item>
    /// <item>With prefix "cache": "abc123" -> "cache/abc123.tif"</item>
    /// </list>
    /// </para>
    /// </remarks>
    protected string BuildObjectKey(string cacheKey)
    {
        var fileName = $"{cacheKey}.tif";
        return _prefix.IsNullOrEmpty() ? fileName : $"{_prefix}/{fileName}";
    }

    /// <summary>
    /// Normalizes a DateTime to UTC timezone.
    /// </summary>
    /// <param name="timestamp">The timestamp to normalize.</param>
    /// <returns>The timestamp in UTC.</returns>
    /// <remarks>
    /// Handles timestamps that:
    /// <list type="bullet">
    /// <item>Are already UTC - returned as-is</item>
    /// <item>Are local time - converted to UTC</item>
    /// <item>Are unspecified - assumed to be UTC and kind is set</item>
    /// </list>
    /// </remarks>
    protected static DateTime NormalizeToUtc(DateTime timestamp)
    {
        return timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            _ => timestamp
        };
    }

    /// <summary>
    /// Creates fallback metadata when provider metadata is unavailable.
    /// </summary>
    /// <param name="storageUri">The storage URI for the object.</param>
    /// <param name="fileInfo">File information from the local file.</param>
    /// <returns>Fallback metadata based on local file information.</returns>
    protected static CogStorageMetadata CreateFallbackMetadata(string storageUri, FileInfo fileInfo)
    {
        return new CogStorageMetadata(
            storageUri,
            fileInfo.Exists ? fileInfo.Length : 0,
            DateTime.UtcNow);
    }

    #endregion

    #region Logging Methods

    /// <summary>
    /// Logs successful metadata retrieval.
    /// </summary>
    private void LogCacheMetadataRetrieved(string objectKey)
    {
        _logger.LogDebug("Retrieved cache metadata for object: {ObjectKey}", objectKey);
    }

    /// <summary>
    /// Logs upload start.
    /// </summary>
    private void LogUploadStarting(string storageUri)
    {
        _logger.LogInformation("Uploading COG cache entry to {StorageUri}", storageUri);
    }

    /// <summary>
    /// Logs successful upload completion.
    /// </summary>
    private void LogUploadCompleted(string storageUri, long sizeBytes)
    {
        _logger.LogInformation(
            "Successfully uploaded COG cache entry to {StorageUri} ({SizeBytes} bytes)",
            storageUri,
            sizeBytes);
    }

    /// <summary>
    /// Logs successful deletion.
    /// </summary>
    private void LogDeletionCompleted(string objectKey)
    {
        _logger.LogInformation("Deleted cache object: {ObjectKey}", objectKey);
    }

    /// <summary>
    /// Logs deletion of non-existent object (idempotent operation).
    /// </summary>
    private void LogDeletionSkipped(string objectKey)
    {
        _logger.LogDebug("Object already deleted or never existed: {ObjectKey}", objectKey);
    }

    /// <summary>
    /// Logs operation errors with consistent formatting.
    /// </summary>
    private void LogOperationError(string operation, string objectKey, Exception exception)
    {
        _logger.LogError(
            exception,
            "Failed to {Operation} cache object {ObjectKey}: {ErrorMessage}",
            operation,
            objectKey,
            exception.Message);
    }

    #endregion
}

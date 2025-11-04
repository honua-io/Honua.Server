// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

/// <summary>
/// Base class for cloud-based attachment stores (S3, Azure Blob, GCS).
/// Provides common implementations for metadata handling, error handling, and stream management.
/// </summary>
/// <typeparam name="TClient">The cloud storage client type (e.g., IAmazonS3, BlobContainerClient).</typeparam>
/// <typeparam name="TException">The cloud-specific exception type for 404 handling.</typeparam>
public abstract class CloudAttachmentStoreBase<TClient, TException> : IAttachmentStore, IAsyncDisposable
    where TException : Exception
{
    private readonly TClient _client;
    private readonly bool _ownsClient;
    private readonly string? _prefix;
    private readonly string _providerKey;

    /// <summary>
    /// Initializes a new instance of the cloud attachment store.
    /// </summary>
    /// <param name="client">The cloud storage client.</param>
    /// <param name="prefix">Optional prefix for object keys.</param>
    /// <param name="providerKey">The storage provider key (e.g., "s3", "azureblob").</param>
    /// <param name="ownsClient">Whether this instance should dispose the client.</param>
    protected CloudAttachmentStoreBase(TClient client, string? prefix, string providerKey, bool ownsClient = false)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix!.Trim('/');
        _providerKey = providerKey ?? throw new ArgumentNullException(nameof(providerKey));
        _ownsClient = ownsClient;
    }

    /// <summary>
    /// Gets the cloud storage client.
    /// </summary>
    protected TClient Client => _client;

    /// <summary>
    /// Gets the prefix for object keys.
    /// </summary>
    protected string? Prefix => _prefix;

    /// <summary>
    /// Gets the provider key.
    /// </summary>
    protected string ProviderKey => _providerKey;

    /// <inheritdoc/>
    public async Task<AttachmentStoreWriteResult> PutAsync(Stream content, AttachmentStorePutRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(content);
        Guard.NotNull(request);

        var objectKey = BuildObjectKey(request.AttachmentId);

        // Normalize metadata
        var metadata = new Dictionary<string, string>();
        foreach (var kvp in request.Metadata)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value is not null)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        await PutObjectAsync(objectKey, content, request.MimeType, metadata, cancellationToken).ConfigureAwait(false);

        return new AttachmentStoreWriteResult
        {
            Pointer = new AttachmentPointer(_providerKey, objectKey)
        };
    }

    /// <inheritdoc/>
    public async Task<AttachmentReadResult?> TryGetAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default)
    {
        var objectKey = ResolveObjectKey(pointer);

        try
        {
            return await GetObjectAsync(objectKey, cancellationToken).ConfigureAwait(false);
        }
        catch (TException ex) when (IsNotFoundException(ex))
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default)
    {
        var objectKey = ResolveObjectKey(pointer);

        try
        {
            return await DeleteObjectAsync(objectKey, cancellationToken).ConfigureAwait(false);
        }
        catch (TException ex) when (IsNotFoundException(ex))
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public abstract IAsyncEnumerable<AttachmentPointer> ListAsync(string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an object to cloud storage.
    /// </summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="content">The content stream.</param>
    /// <param name="mimeType">The MIME type.</param>
    /// <param name="metadata">The metadata dictionary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task PutObjectAsync(
        string objectKey,
        Stream content,
        string mimeType,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads an object from cloud storage.
    /// </summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attachment read result, or null if not found.</returns>
    protected abstract Task<AttachmentReadResult?> GetObjectAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an object from cloud storage.
    /// </summary>
    /// <param name="objectKey">The object key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the object was deleted, false if it didn't exist.</returns>
    protected abstract Task<bool> DeleteObjectAsync(string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether the exception represents a not found error.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception indicates the object was not found.</returns>
    protected abstract bool IsNotFoundException(TException exception);

    /// <summary>
    /// Builds the full object key from an attachment ID.
    /// </summary>
    protected string BuildObjectKey(string attachmentId)
    {
        return string.IsNullOrWhiteSpace(_prefix)
            ? attachmentId
            : string.Concat(_prefix, "/", attachmentId);
    }

    /// <summary>
    /// Resolves the object key from an attachment pointer.
    /// </summary>
    protected string ResolveObjectKey(AttachmentPointer pointer)
    {
        if (!string.Equals(pointer.StorageProvider, _providerKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Pointer does not belong to {_providerKey} store: {pointer.StorageProvider}");
        }

        return pointer.StorageKey;
    }

    /// <summary>
    /// Combines a base prefix with an additional prefix.
    /// </summary>
    protected static string? CombinePrefixes(string? basePrefix, string? additional)
    {
        if (string.IsNullOrWhiteSpace(basePrefix))
        {
            return string.IsNullOrWhiteSpace(additional) ? null : additional.Trim('/');
        }

        if (string.IsNullOrWhiteSpace(additional))
        {
            return basePrefix;
        }

        return string.Concat(basePrefix.Trim('/'), "/", additional.Trim('/'));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
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

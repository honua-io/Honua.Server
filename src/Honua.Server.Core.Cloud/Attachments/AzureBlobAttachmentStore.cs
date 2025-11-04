// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Honua.Server.Core.Utilities;
using Honua.Server.Core.Attachments;
namespace Honua.Server.Core.Cloud.Attachments;

/// <summary>
/// Azure Blob Storage-based attachment store implementation using CloudAttachmentStoreBase.
/// </summary>
internal sealed class AzureBlobAttachmentStore : CloudAttachmentStoreBase<BlobContainerClient, RequestFailedException>
{
    public AzureBlobAttachmentStore(
        BlobContainerClient containerClient,
        string? prefix,
        bool ownsContainer = false)
        : base(containerClient, prefix, AttachmentStoreProviderKeys.AzureBlob, ownsContainer)
    {
    }

    protected override async Task PutObjectAsync(
        string objectKey,
        Stream content,
        string mimeType,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var blobClient = Client.GetBlobClient(objectKey);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = mimeType
            },
            Metadata = metadata
        };

        await blobClient.UploadAsync(content, uploadOptions, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task<AttachmentReadResult?> GetObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var blobClient = Client.GetBlobClient(objectKey);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AttachmentReadResult
        {
            Content = response.Value.Content,
            MimeType = response.Value.Details.ContentType,
            SizeBytes = response.Value.Details.ContentLength,
            FileName = Path.GetFileName(objectKey),
            ChecksumSha256 = response.Value.Details.ETag.ToString().Trim('\"')
        };
    }

    protected override async Task<bool> DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var blobClient = Client.GetBlobClient(objectKey);

        try
        {
            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    protected override bool IsNotFoundException(RequestFailedException exception)
    {
        return exception.Status == 404;
    }

    public override async IAsyncEnumerable<AttachmentPointer> ListAsync(
        string? prefix = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchPrefix = CombinePrefixes(Prefix, prefix);

        await foreach (var blob in Client.GetBlobsAsync(prefix: searchPrefix, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return new AttachmentPointer(ProviderKey, blob.Name);
        }
    }
}

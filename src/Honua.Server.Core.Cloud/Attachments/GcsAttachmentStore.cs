// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;

using Honua.Server.Core.Utilities;
using Honua.Server.Core.Attachments;
namespace Honua.Server.Core.Cloud.Attachments;

/// <summary>
/// Google Cloud Storage-based attachment store implementation using CloudAttachmentStoreBase.
/// </summary>
internal sealed class GcsAttachmentStore : CloudAttachmentStoreBase<StorageClient, Google.GoogleApiException>
{
    private readonly string _bucketName;

    public GcsAttachmentStore(
        StorageClient storageClient,
        string bucketName,
        string? prefix,
        bool ownsClient = false)
        : base(storageClient, prefix, AttachmentStoreProviderKeys.Gcs, ownsClient)
    {
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    protected override async Task PutObjectAsync(
        string objectKey,
        Stream content,
        string mimeType,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        var uploadObject = new Google.Apis.Storage.v1.Data.Object
        {
            Bucket = _bucketName,
            Name = objectKey,
            ContentType = mimeType,
            Metadata = metadata
        };

        var uploadOptions = new UploadObjectOptions
        {
            PredefinedAcl = PredefinedObjectAcl.ProjectPrivate
        };

        await Client.UploadObjectAsync(
            uploadObject,
            content,
            uploadOptions,
            cancellationToken).ConfigureAwait(false);
    }

    protected override async Task<AttachmentReadResult?> GetObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var stream = new MemoryStream();

        await Client.DownloadObjectAsync(
            _bucketName,
            objectKey,
            stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        stream.Position = 0;

        // Get object metadata
        var obj = await Client.GetObjectAsync(
            _bucketName,
            objectKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AttachmentReadResult
        {
            Content = stream,
            MimeType = obj.ContentType,
            SizeBytes = (long?)obj.Size,
            FileName = obj.Metadata?.TryGetValue("fileName", out var fn) == true ? fn : null,
            ChecksumSha256 = obj.Metadata?.TryGetValue("checksum", out var cs) == true ? cs : null
        };
    }

    protected override async Task<bool> DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        await Client.DeleteObjectAsync(
            _bucketName,
            objectKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }

    protected override bool IsNotFoundException(Google.GoogleApiException exception)
    {
        return exception.Error?.Code == 404;
    }

    public override async IAsyncEnumerable<AttachmentPointer> ListAsync(
        string? prefix = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchPrefix = CombinePrefixes(Prefix, prefix);

        var objects = Client.ListObjectsAsync(_bucketName, searchPrefix);

        await foreach (var obj in objects.WithCancellation(cancellationToken))
        {
            yield return new AttachmentPointer(ProviderKey, obj.Name);
        }
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using Honua.Server.Core.Utilities;
using Honua.Server.Core.Attachments;
namespace Honua.Server.Core.Cloud.Attachments;

/// <summary>
/// S3-based attachment store implementation using CloudAttachmentStoreBase.
/// </summary>
internal sealed class S3AttachmentStore : CloudAttachmentStoreBase<IAmazonS3, AmazonS3Exception>
{
    private readonly string _bucketName;

    public S3AttachmentStore(IAmazonS3 client, string bucketName, string? prefix, bool ownsClient = false)
        : base(client, prefix, AttachmentStoreProviderKeys.S3, ownsClient)
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
        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = mimeType,
            AutoCloseStream = false
        };

        foreach (var kvp in metadata)
        {
            putRequest.Metadata[kvp.Key] = kvp.Value;
        }

        await Client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task<AttachmentReadResult?> GetObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var response = await Client.GetObjectAsync(_bucketName, objectKey, cancellationToken).ConfigureAwait(false);
        return new AttachmentReadResult
        {
            Content = new S3ObjectStream(response),
            MimeType = response.Headers.ContentType,
            SizeBytes = response.ContentLength,
            FileName = Path.GetFileName(objectKey),
            ChecksumSha256 = response.ETag?.Trim('\"')
        };
    }

    protected override async Task<bool> DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var response = await Client.DeleteObjectAsync(_bucketName, objectKey, cancellationToken).ConfigureAwait(false);
        return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent || response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }

    protected override bool IsNotFoundException(AmazonS3Exception exception)
    {
        return exception.StatusCode == System.Net.HttpStatusCode.NotFound;
    }

    public override async IAsyncEnumerable<AttachmentPointer> ListAsync(
        string? prefix = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = CombinePrefixes(Prefix, prefix)
        };

        ListObjectsV2Response response;
        do
        {
            response = await Client.ListObjectsV2Async(request, cancellationToken).ConfigureAwait(false);
            foreach (var obj in response.S3Objects)
            {
                yield return new AttachmentPointer(ProviderKey, obj.Key);
            }

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated == true && !cancellationToken.IsCancellationRequested);
    }

    private sealed class S3ObjectStream : Stream
    {
        private readonly GetObjectResponse _response;
        private readonly Stream _inner;

        public S3ObjectStream(GetObjectResponse response)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));
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
        public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            return await _inner.ReadAsync(destination, cancellationToken);
        }

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
}

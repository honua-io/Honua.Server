// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

public sealed class FileSystemRasterSourceProvider : IRasterSourceProvider
{
    public string ProviderKey => "file";

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
        {
            return parsedUri.IsFile;
        }

        // Also handle local file paths without scheme
        return File.Exists(uri);
    }

    public Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        var path = GetLocalPath(uri);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Raster file not found: {path}");
        }

        var stream = File.OpenRead(path);
        return Task.FromResult<Stream>(stream);
    }

    public Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null, CancellationToken cancellationToken = default)
    {
        var path = GetLocalPath(uri);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Raster file not found: {path}");
        }

        var stream = File.OpenRead(path);
        stream.Seek(offset, SeekOrigin.Begin);

        if (length.HasValue)
        {
            // Create a bounded stream that only reads the specified length
            return Task.FromResult<Stream>(new BoundedStream(stream, length.Value));
        }

        return Task.FromResult<Stream>(stream);
    }

    private static string GetLocalPath(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) && parsedUri.IsFile)
        {
            return parsedUri.LocalPath;
        }

        return uri;
    }

    private sealed class BoundedStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _maxLength;
        private long _position;

        public BoundedStream(Stream baseStream, long maxLength)
        {
            _baseStream = baseStream;
            _maxLength = maxLength;
            _position = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _maxLength;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _maxLength - _position;
            if (remaining <= 0)
            {
                return 0;
            }

            var toRead = (int)Math.Min(count, remaining);
            var read = _baseStream.Read(buffer, offset, toRead);
            _position += read;
            return read;
        }

        public override void Flush() => _baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

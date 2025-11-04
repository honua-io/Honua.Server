// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// Stream implementation that uses HTTP range requests for efficient remote file access.
/// Optimized for Cloud Optimized GeoTIFF (COG) access patterns.
/// </summary>
public sealed class HttpRangeStream : Stream
{
    private readonly HttpClient _httpClient;
    private readonly string _uri;
    private readonly ILogger<HttpRangeStream> _logger;
    private readonly long _contentLength;
    private long _position;
    private byte[]? _readAheadBuffer;
    private long _readAheadStart;
    private const int DefaultReadAheadSize = 16384; // 16 KB

    public HttpRangeStream(
        HttpClient httpClient,
        string uri,
        long contentLength,
        ILogger<HttpRangeStream> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentLength = contentLength;
        _position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _contentLength;

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > _contentLength)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // NOTE: Synchronous Read() required by Stream base class.
        // Calls async implementation with blocking. This is safe because:
        // 1. Raster operations run in background threads (not ASP.NET request context)
        // 2. Stream API does not provide async-only option
        // 3. Callers should prefer ReadAsync() when possible
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_position >= _contentLength)
        {
            return 0;
        }

        // Check if we can serve from read-ahead buffer
        if (_readAheadBuffer != null &&
            _position >= _readAheadStart &&
            _position < _readAheadStart + _readAheadBuffer.Length)
        {
            var bufferOffset = (int)(_position - _readAheadStart);
            var available = _readAheadBuffer.Length - bufferOffset;
            var toRead = Math.Min(count, available);

            Array.Copy(_readAheadBuffer, bufferOffset, buffer, offset, toRead);
            _position += toRead;

            _logger.LogTrace("Read {Bytes} bytes from read-ahead buffer at position {Position}",
                toRead, _position - toRead);

            return toRead;
        }

        // Determine range to fetch (with read-ahead for sequential access)
        var requestSize = Math.Max(count, DefaultReadAheadSize);
        var endPosition = Math.Min(_position + requestSize - 1, _contentLength - 1);

        _logger.LogDebug("HTTP range request: {Uri} bytes={Start}-{End} (requested={Count}, fetching={FetchSize})",
            _uri, _position, endPosition, count, endPosition - _position + 1);

        // Perform HTTP range request
        var request = new HttpRequestMessage(HttpMethod.Get, _uri);
        request.Headers.Range = new RangeHeaderValue(_position, endPosition);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Read response into read-ahead buffer
        _readAheadBuffer = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        _readAheadStart = _position;

        // Copy requested data to output buffer
        var bytesToCopy = Math.Min(count, _readAheadBuffer.Length);
        Array.Copy(_readAheadBuffer, 0, buffer, offset, bytesToCopy);

        _position += bytesToCopy;

        _logger.LogTrace("Read {Bytes} bytes via HTTP range request (buffer size: {BufferSize})",
            bytesToCopy, _readAheadBuffer.Length);

        return bytesToCopy;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _contentLength + offset,
            _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
        };

        if (newPosition < 0 || newPosition > _contentLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Seek position out of range");
        }

        _position = newPosition;
        return _position;
    }

    public override void Flush()
    {
        // No-op for read-only stream
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("Cannot set length on HTTP stream");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Cannot write to HTTP stream");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _readAheadBuffer = null;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Create an HttpRangeStream by performing a HEAD request to get content length.
    /// </summary>
    public static async Task<HttpRangeStream> CreateAsync(
        HttpClient httpClient,
        string uri,
        ILogger<HttpRangeStream> logger,
        CancellationToken cancellationToken = default)
    {
        // Perform HEAD request to get content length
        var request = new HttpRequestMessage(HttpMethod.Head, uri);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (!contentLength.HasValue)
        {
            throw new InvalidOperationException($"Remote resource does not specify Content-Length: {uri}");
        }

        // Verify server supports range requests
        if (!response.Headers.AcceptRanges.Contains("bytes"))
        {
            logger.LogWarning("Server may not support range requests for {Uri}", uri);
        }

        return new HttpRangeStream(httpClient, uri, contentLength.Value, logger);
    }
}

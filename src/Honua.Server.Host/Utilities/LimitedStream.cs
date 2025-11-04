// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Utilities;

/// <summary>
/// A stream wrapper that enforces a maximum size limit to prevent DoS attacks via unbounded request bodies.
/// </summary>
/// <remarks>
/// SECURITY: Protects against:
/// 1. Memory exhaustion attacks - attackers send extremely large request bodies to consume all available memory
/// 2. Disk exhaustion attacks - large requests consume temporary storage
/// 3. CPU exhaustion attacks - parsing/processing large payloads
/// 4. Slowloris-style attacks - slow transmission of large bodies to tie up connections
///
/// Attack Scenario: Without size limits, an attacker can:
/// - Send a 10GB XML transaction request to WFS-T, causing OOM exceptions
/// - Upload massive feature collections to exhaust disk space
/// - Paralyze the server by saturating network bandwidth with multiple large requests
///
/// Related CWE: CWE-400 (Uncontrolled Resource Consumption)
/// </remarks>
public sealed class LimitedStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _maxSize;
    private long _bytesRead;
    private bool _disposed;

    /// <summary>
    /// Default maximum request size (50 MB).
    /// </summary>
    public const long DefaultMaxSizeBytes = 50 * 1024 * 1024; // 50 MB

    /// <summary>
    /// Creates a new LimitedStream that enforces a maximum size.
    /// </summary>
    /// <param name="innerStream">The underlying stream to read from.</param>
    /// <param name="maxSizeBytes">Maximum allowed size in bytes. Defaults to 50 MB.</param>
    /// <exception cref="ArgumentNullException">Thrown when innerStream is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxSizeBytes is less than or equal to zero.</exception>
    public LimitedStream(Stream innerStream, long maxSizeBytes = DefaultMaxSizeBytes)
    {
        _innerStream = Guard.NotNull(innerStream);

        if (maxSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSizeBytes),
                maxSizeBytes,
                "Maximum size must be greater than zero.");
        }

        _maxSize = maxSizeBytes;
        _bytesRead = 0;
    }

    /// <summary>
    /// Gets the number of bytes read so far.
    /// </summary>
    public long BytesRead => _bytesRead;

    /// <summary>
    /// Gets the maximum allowed size in bytes.
    /// </summary>
    public long MaxSize => _maxSize;

    /// <summary>
    /// Gets the remaining bytes before reaching the limit.
    /// </summary>
    public long BytesRemaining => Math.Max(0, _maxSize - _bytesRead);

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => false; // Disable seeking to prevent bypassing the limit
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException("Length is not supported on LimitedStream.");
    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException("Setting position is not supported on LimitedStream.");
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        ThrowIfMaxSizeExceeded(count);

        var bytesRead = _innerStream.Read(buffer, offset, count);
        _bytesRead += bytesRead;

        // Check again after reading in case we exceeded the limit
        ThrowIfMaxSizeExceeded(0);

        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ThrowIfMaxSizeExceeded(count);

        var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        _bytesRead += bytesRead;

        // Check again after reading in case we exceeded the limit
        ThrowIfMaxSizeExceeded(0);

        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfMaxSizeExceeded(buffer.Length);

        var bytesRead = await _innerStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        _bytesRead += bytesRead;

        // Check again after reading in case we exceeded the limit
        ThrowIfMaxSizeExceeded(0);

        return bytesRead;
    }

    public override void Flush()
    {
        ThrowIfDisposed();
        _innerStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _innerStream.FlushAsync(cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("Seeking is not supported on LimitedStream to prevent bypassing size limits.");
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("SetLength is not supported on LimitedStream.");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Writing is not supported on LimitedStream.");
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Do NOT dispose the inner stream - let the caller manage its lifetime
                // We only wrap it for read operations
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LimitedStream));
        }
    }

    private void ThrowIfMaxSizeExceeded(int additionalBytes)
    {
        var totalBytes = _bytesRead + additionalBytes;
        if (totalBytes > _maxSize)
        {
            throw new RequestTooLargeException(
                $"Request size limit exceeded. Maximum allowed: {FormatBytes(_maxSize)}, attempted: {FormatBytes(totalBytes)}.",
                _maxSize,
                totalBytes);
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024L * 1024L => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB",
            >= 1024L * 1024L => $"{bytes / (1024.0 * 1024.0):F2} MB",
            >= 1024L => $"{bytes / 1024.0:F2} KB",
            _ => $"{bytes} bytes"
        };
    }
}

/// <summary>
/// Exception thrown when a request exceeds the maximum allowed size.
/// </summary>
public sealed class RequestTooLargeException : InvalidOperationException
{
    /// <summary>
    /// Gets the maximum allowed size in bytes.
    /// </summary>
    public long MaxSize { get; }

    /// <summary>
    /// Gets the attempted size in bytes.
    /// </summary>
    public long AttemptedSize { get; }

    /// <summary>
    /// Creates a new RequestTooLargeException.
    /// </summary>
    public RequestTooLargeException(string message, long maxSize, long attemptedSize)
        : base(message)
    {
        MaxSize = maxSize;
        AttemptedSize = attemptedSize;
    }

    /// <summary>
    /// Creates a new RequestTooLargeException with inner exception.
    /// </summary>
    public RequestTooLargeException(string message, long maxSize, long attemptedSize, Exception innerException)
        : base(message, innerException)
    {
        MaxSize = maxSize;
        AttemptedSize = attemptedSize;
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Buffers;
using System.IO;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Honua.Server.Host.Attachments;

/// <summary>
/// Shared helper for downloading attachments across both GeoservicesREST and OGC protocols.
/// Encapsulates descriptor validation, storage resolution, and file streaming logic.
/// </summary>
/// <remarks>
/// <para><strong>Async/Await Best Practices:</strong></para>
/// <list type="bullet">
/// <item>All I/O operations use proper async/await patterns with <c>ConfigureAwait(false)</c></item>
/// <item>No synchronous blocking calls (.Result, .Wait(), .GetAwaiter().GetResult())</item>
/// <item>CancellationToken properly propagated through all async operations</item>
/// <item>Stream operations use <see cref="Memory{T}"/> instead of byte[] for better performance</item>
/// <item>Large streams (&gt;10MB) avoid memory exhaustion using temp file buffering</item>
/// <item>Async disposal with <c>await using</c> and <c>DisposeAsync()</c></item>
/// </list>
/// <para><strong>Performance Optimizations:</strong></para>
/// <list type="bullet">
/// <item>Seekable streams enable HTTP range processing for efficient partial downloads</item>
/// <item>Non-seekable streams &lt;10MB are buffered in memory for seekability</item>
/// <item>Non-seekable streams &gt;10MB stream directly to avoid memory pressure</item>
/// <item>80KB buffer size for optimal I/O performance</item>
/// <item>Temporary files use FileOptions.DeleteOnClose for automatic cleanup</item>
/// </list>
/// </remarks>
public static class AttachmentDownloadHelper
{
    /// <summary>
    /// Error messages for consistent Problem() responses across protocols.
    /// </summary>
    public static class ErrorMessages
    {
        public const string StorageProfileNotConfigured = "Attachment storage profile is not configured for this layer.";
        public const string StorageProfileNotResolved = "Attachment storage profile could not be resolved.";
        public const string Title = "Attachment download unavailable";
    }

    /// <summary>
    /// Result of attempting to download an attachment.
    /// </summary>
    public sealed record DownloadResult
    {
        public static DownloadResult Success(AttachmentReadResult readResult, AttachmentDescriptor descriptor)
            => new() { IsSuccess = true, ReadResult = readResult, Descriptor = descriptor };

        public static DownloadResult NotFound()
            => new() { IsSuccess = false, IsNotFound = true };

        public static DownloadResult StorageProfileMissing()
            => new() { IsSuccess = false, StorageProfileError = ErrorMessages.StorageProfileNotConfigured };

        public static DownloadResult StorageProfileUnresolvable()
            => new() { IsSuccess = false, StorageProfileError = ErrorMessages.StorageProfileNotResolved };

        public bool IsSuccess { get; init; }
        public bool IsNotFound { get; init; }
        public string? StorageProfileError { get; init; }
        public AttachmentReadResult? ReadResult { get; init; }
        public AttachmentDescriptor? Descriptor { get; init; }
    }

    /// <summary>
    /// Attempts to download an attachment by fetching the descriptor, validating storage configuration,
    /// resolving the attachment store, and retrieving the file content.
    /// </summary>
    /// <param name="descriptor">The attachment descriptor (must not be null).</param>
    /// <param name="storageProfileId">The storage profile ID from the layer configuration.</param>
    /// <param name="attachmentStoreSelector">The service for resolving attachment stores.</param>
    /// <param name="logger">Logger for recording errors.</param>
    /// <param name="serviceId">Service ID for logging context.</param>
    /// <param name="layerId">Layer ID for logging context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A DownloadResult indicating success or specific error condition.</returns>
    public static async Task<DownloadResult> TryDownloadAsync(
        AttachmentDescriptor descriptor,
        string? storageProfileId,
        IAttachmentStoreSelector attachmentStoreSelector,
        ILogger logger,
        string serviceId,
        string layerId,
        CancellationToken cancellationToken)
    {
        // Resolve attachment store using the descriptor's storage provider
        // This ensures we can retrieve attachments even if the layer's storage profile has changed
        IAttachmentStore store;
        try
        {
            store = attachmentStoreSelector.Resolve(descriptor.StorageProvider);
        }
        catch (AttachmentStoreNotFoundException)
        {
            logger.LogWarning(
                "Attachment storage provider {StorageProvider} could not be resolved for attachment in layer {LayerId}, service {ServiceId}. Attempting fallback to current layer profile {StorageProfileId}.",
                descriptor.StorageProvider,
                layerId,
                serviceId,
                storageProfileId);

            // Fallback: try the current layer's storage profile for backward compatibility
            if (string.IsNullOrWhiteSpace(storageProfileId))
            {
                logger.LogError(
                    "Attachment storage profile is not configured for layer {LayerId} in service {ServiceId}.",
                    layerId,
                    serviceId);
                return DownloadResult.StorageProfileMissing();
            }

            try
            {
                store = attachmentStoreSelector.Resolve(storageProfileId);
            }
            catch (AttachmentStoreNotFoundException)
            {
                logger.LogError(
                    "Attachment storage profile {StorageProfileId} could not be resolved for layer {LayerId} in service {ServiceId}.",
                    storageProfileId,
                    layerId,
                    serviceId);
                return DownloadResult.StorageProfileUnresolvable();
            }
        }

        // Retrieve file content
        var pointer = new AttachmentPointer(descriptor.StorageProvider, descriptor.StorageKey);
        var readResult = await store.TryGetAsync(pointer, cancellationToken).ConfigureAwait(false);
        if (readResult is null || readResult.Content is null)
        {
            return DownloadResult.NotFound();
        }

        return DownloadResult.Success(readResult, descriptor);
    }

    /// <summary>
    /// Converts a DownloadResult into an IActionResult for MVC controllers (GeoservicesREST).
    /// </summary>
    public static async Task<IActionResult> ToActionResultAsync(DownloadResult result, ControllerBase controller, CancellationToken cancellationToken = default)
    {
        if (result.IsNotFound)
        {
            return controller.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(result.StorageProfileError))
        {
            return controller.Problem(
                result.StorageProfileError,
                statusCode: StatusCodes.Status500InternalServerError,
                title: ErrorMessages.Title);
        }

        if (!result.IsSuccess || result.ReadResult is null || result.Descriptor is null)
        {
            return controller.NotFound();
        }

        var descriptor = result.Descriptor;
        var readResult = result.ReadResult;

        // Set ETag if checksum is available
        if (!string.IsNullOrWhiteSpace(descriptor.ChecksumSha256))
        {
            controller.Response.Headers["ETag"] = $"\"{descriptor.ChecksumSha256}\"";
        }

        // SECURITY FIX: Use ContentDispositionHeaderValue with proper filename encoding to prevent header injection attacks.
        // This properly escapes special characters, handles RFC 5987 encoding for international characters via
        // FileNameStar, and prevents CRLF injection vulnerabilities. Direct string interpolation with
        // user-controlled filenames can allow attackers to inject arbitrary headers via filenames
        // containing \r\n sequences or unescaped quotes.
        //
        // The FileNameStar property uses RFC 5987 encoding (UTF-8 percent-encoding) which is the modern
        // standard for non-ASCII filenames in HTTP headers and is supported by all modern browsers.
        var contentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = descriptor.Name
        };
        controller.Response.Headers["Content-Disposition"] = contentDisposition.ToString();

        var (stream, enableRangeProcessing) = await PrepareDownloadStreamAsync(readResult, cancellationToken).ConfigureAwait(false);

        if (!enableRangeProcessing && readResult.SizeBytes.HasValue)
        {
            controller.Response.ContentLength = readResult.SizeBytes.Value;
        }

        return controller.File(
            stream,
            readResult.MimeType ?? descriptor.MimeType,
            descriptor.Name,
            enableRangeProcessing: enableRangeProcessing);
    }


    /// <summary>
    /// Converts a DownloadResult into an IResult for minimal APIs (OGC).
    /// </summary>
    public static async Task<IResult> ToResultAsync(DownloadResult result, OgcCacheHeaderService? cacheHeaderService = null, CancellationToken cancellationToken = default)
    {
        if (result.IsNotFound)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(result.StorageProfileError))
        {
            return Results.Problem(
                result.StorageProfileError,
                statusCode: StatusCodes.Status500InternalServerError,
                title: ErrorMessages.Title);
        }

        if (!result.IsSuccess || result.ReadResult is null || result.Descriptor is null)
        {
            return Results.NotFound();
        }

        var descriptor = result.Descriptor;
        var readResult = result.ReadResult;

        var mimeType = readResult.MimeType ?? descriptor.MimeType ?? "application/octet-stream";
        var fileName = descriptor.Name ?? readResult.FileName ?? $"attachment-{descriptor.AttachmentObjectId}";

        var (stream, enableRangeProcessing) = await PrepareDownloadStreamAsync(readResult, cancellationToken).ConfigureAwait(false);

        var fileResult = Results.File(stream, mimeType, fileName, enableRangeProcessing: enableRangeProcessing);

        if (cacheHeaderService is not null)
        {
            var etag = !string.IsNullOrWhiteSpace(descriptor.ChecksumSha256)
                ? cacheHeaderService.GenerateETag(descriptor.ChecksumSha256)
                : null;
            var lastModified = descriptor.UpdatedUtc ?? descriptor.CreatedUtc;
            return fileResult.WithFeatureCacheHeaders(cacheHeaderService, etag, lastModified);
        }

        return fileResult;
    }


    private const long MaxBufferedAttachmentSize = 10 * 1024 * 1024; // 10 MB ceiling for in-memory buffering

    private static async Task<(Stream Stream, bool EnableRangeProcessing)> PrepareDownloadStreamAsync(AttachmentReadResult readResult, CancellationToken cancellationToken)
    {
        var stream = readResult.Content;

        if (stream.CanSeek)
        {
            if (stream.CanRead && stream.Position != 0)
            {
                stream.Position = 0;
            }

            return (stream, true);
        }

        if (readResult.SizeBytes is long size && size <= MaxBufferedAttachmentSize)
        {
            var seekableStream = await EnsureSeekableStreamAsync(stream, cancellationToken).ConfigureAwait(false);
            return (seekableStream, seekableStream.CanSeek);
        }

        // Fall back to streaming the source as-is and disable range processing to avoid buffering large payloads.
        return (stream, false);
    }

    /// <summary>
    /// Ensures a stream is seekable by copying small non-seekable streams to a MemoryStream,
    /// or for large streams (>10MB), uses a temporary file to avoid memory exhaustion.
    /// This is required for enabling range processing (HTTP 206 Partial Content support).
    /// </summary>
    /// <param name="stream">The input stream (may be seekable or non-seekable).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A seekable stream (either the original if already seekable, a MemoryStream for small files, or a FileStream for large files).</returns>
    private static async Task<Stream> EnsureSeekableStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            return stream;
        }

        // For non-seekable streams, use a size-based strategy:
        // - Small streams (<= 10MB): buffer in memory for performance
        // - Large streams (> 10MB): use temp file to prevent memory exhaustion
        const long MaxMemoryBufferSize = 10 * 1024 * 1024; // 10 MB

        // Try to determine stream size if available
        long? streamSize = null;
        try
        {
            if (stream.CanSeek)
            {
                streamSize = stream.Length;
            }
        }
        catch
        {
            // Length may not be available for some stream types, continue without it
        }

        // If we know the size and it's large, use temp file immediately
        if (streamSize.HasValue && streamSize.Value > MaxMemoryBufferSize)
        {
            return await CopyToTempFileAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        // For unknown size or small streams, start buffering in memory
        // but switch to temp file if it exceeds threshold
        var memoryStream = new MemoryStream();

        // Use ArrayPool to reduce GC pressure (ASP.NET Core best practice)
        const int bufferSize = 81920; // 80 KB buffer for efficient copying
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            var bufferMemory = buffer.AsMemory(0, bufferSize);
            int bytesRead;
            long totalBytesRead = 0;

            while ((bytesRead = await stream.ReadAsync(bufferMemory, cancellationToken).ConfigureAwait(false)) > 0)
            {
                totalBytesRead += bytesRead;

                // If we exceed threshold, switch to temp file
                if (totalBytesRead > MaxMemoryBufferSize)
                {
                    // Create temp file and copy memory stream contents plus remaining stream data
                    // Buffer will be returned to pool by finally block after this method completes
                    var tempFileStream = await CopyToTempFileAsync(memoryStream, stream, buffer, bytesRead, cancellationToken).ConfigureAwait(false);

                    // Dispose resources
                    await memoryStream.DisposeAsync().ConfigureAwait(false);
                    await stream.DisposeAsync().ConfigureAwait(false);

                    return tempFileStream;
                }

                await memoryStream.WriteAsync(bufferMemory[..bytesRead], cancellationToken).ConfigureAwait(false);
            }

            // Stream fit in memory, return MemoryStream
            memoryStream.Position = 0;
            await stream.DisposeAsync().ConfigureAwait(false);
            return memoryStream;
        }
        finally
        {
            // Return buffer to pool (ASP.NET Core best practice for reducing allocations)
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Copies a stream to a temporary file for seekable access.
    /// The temp file is created with FileOptions.DeleteOnClose to ensure cleanup.
    /// </summary>
    private static async Task<FileStream> CopyToTempFileAsync(Stream source, CancellationToken cancellationToken)
    {
        var tempPath = Path.GetTempFileName();
        var tempStream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        try
        {
            await source.CopyToAsync(tempStream, cancellationToken).ConfigureAwait(false);
            tempStream.Position = 0;
            await source.DisposeAsync().ConfigureAwait(false);
            return tempStream;
        }
        catch
        {
            await tempStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Copies already-buffered data and remaining stream data to a temporary file.
    /// Used when we exceed memory threshold during streaming.
    /// </summary>
    private static async Task<FileStream> CopyToTempFileAsync(
        MemoryStream bufferedData,
        Stream remainingSource,
        byte[] currentBuffer,
        int currentBufferBytesRead,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.GetTempFileName();
        var tempStream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        try
        {
            // Copy buffered data from memory
            bufferedData.Position = 0;
            await bufferedData.CopyToAsync(tempStream, cancellationToken).ConfigureAwait(false);

            // Write current buffer
            await tempStream.WriteAsync(currentBuffer.AsMemory(0, currentBufferBytesRead), cancellationToken).ConfigureAwait(false);

            // Copy remaining data from source
            await remainingSource.CopyToAsync(tempStream, cancellationToken).ConfigureAwait(false);

            tempStream.Position = 0;
            return tempStream;
        }
        catch
        {
            await tempStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

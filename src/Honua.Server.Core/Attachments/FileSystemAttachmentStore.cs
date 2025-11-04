// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

internal sealed class FileSystemAttachmentStore : IAttachmentStore
{
    private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100MB limit

    /// <summary>
    /// Whitelist of allowed file extensions for upload security.
    /// Includes common image formats, documents, geospatial data, and archive formats.
    /// </summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Image formats
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico", ".svg",
        // Document formats
        ".pdf", ".txt", ".csv", ".json", ".xml", ".md",
        // Geospatial formats
        ".geojson", ".kml", ".kmz", ".gpx", ".shp", ".dbf", ".shx", ".prj", ".cpg", ".qix",
        ".gml", ".topojson", ".mvt",
        // Archive formats (for shapefile bundles, etc.)
        ".zip", ".tar", ".gz", ".7z",
        // Raster formats
        ".tif", ".tiff", ".geotiff", ".img", ".nc", ".hdf", ".hdf5", ".ecw", ".sid"
    };

    /// <summary>
    /// Maps file extensions to their expected MIME types for validation.
    /// This prevents MIME type spoofing attacks.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Image formats
        { ".jpg", new[] { "image/jpeg" } },
        { ".jpeg", new[] { "image/jpeg" } },
        { ".png", new[] { "image/png" } },
        { ".gif", new[] { "image/gif" } },
        { ".bmp", new[] { "image/bmp", "image/x-bmp", "image/x-ms-bmp" } },
        { ".webp", new[] { "image/webp" } },
        { ".tiff", new[] { "image/tiff", "image/x-tiff" } },
        { ".tif", new[] { "image/tiff", "image/x-tiff" } },
        { ".ico", new[] { "image/x-icon", "image/vnd.microsoft.icon" } },
        { ".svg", new[] { "image/svg+xml" } },

        // Document formats
        { ".pdf", new[] { "application/pdf" } },
        { ".txt", new[] { "text/plain" } },
        { ".csv", new[] { "text/csv", "text/comma-separated-values", "application/csv" } },
        { ".json", new[] { "application/json", "text/json" } },
        { ".xml", new[] { "application/xml", "text/xml" } },
        { ".md", new[] { "text/markdown", "text/plain" } },

        // Geospatial formats
        { ".geojson", new[] { "application/geo+json", "application/json" } },
        { ".kml", new[] { "application/vnd.google-earth.kml+xml", "application/xml" } },
        { ".kmz", new[] { "application/vnd.google-earth.kmz", "application/zip" } },
        { ".gpx", new[] { "application/gpx+xml", "application/xml" } },
        { ".shp", new[] { "application/x-esri-shape", "application/octet-stream" } },
        { ".dbf", new[] { "application/x-dbf", "application/octet-stream" } },
        { ".shx", new[] { "application/octet-stream" } },
        { ".prj", new[] { "text/plain", "application/octet-stream" } },
        { ".cpg", new[] { "text/plain", "application/octet-stream" } },
        { ".qix", new[] { "application/octet-stream" } },
        { ".gml", new[] { "application/gml+xml", "application/xml" } },
        { ".topojson", new[] { "application/json" } },
        { ".mvt", new[] { "application/vnd.mapbox-vector-tile", "application/octet-stream" } },

        // Archive formats
        { ".zip", new[] { "application/zip", "application/x-zip-compressed" } },
        { ".tar", new[] { "application/x-tar" } },
        { ".gz", new[] { "application/gzip", "application/x-gzip" } },
        { ".7z", new[] { "application/x-7z-compressed" } },

        // Raster formats
        { ".geotiff", new[] { "image/tiff", "application/octet-stream" } },
        { ".img", new[] { "application/octet-stream" } },
        { ".nc", new[] { "application/x-netcdf", "application/netcdf" } },
        { ".hdf", new[] { "application/x-hdf", "application/octet-stream" } },
        { ".hdf5", new[] { "application/x-hdf5", "application/octet-stream" } },
        { ".ecw", new[] { "application/octet-stream" } },
        { ".sid", new[] { "application/octet-stream" } }
    };

    private readonly string _rootPath;
    private readonly ILogger<FileSystemAttachmentStore> _logger;
    private readonly Histogram<double>? _fileWriteDuration;
    private readonly Histogram<double>? _flushDuration;
    private readonly Counter<long>? _bytesWritten;

    public FileSystemAttachmentStore(string rootPath, ILogger<FileSystemAttachmentStore> logger, IMeterFactory? meterFactory = null)
    {
        _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Directory.CreateDirectory(_rootPath);

        // Initialize metrics if meter factory is available
        if (meterFactory != null)
        {
            var meter = meterFactory.Create("Honua.Attachments.FileSystem");
            _fileWriteDuration = meter.CreateHistogram<double>(
                "honua.attachments.filesystem.write.duration",
                unit: "ms",
                description: "Duration of file write operations including flush to disk");
            _flushDuration = meter.CreateHistogram<double>(
                "honua.attachments.filesystem.flush.duration",
                unit: "ms",
                description: "Duration of async disk flush operations");
            _bytesWritten = meter.CreateCounter<long>(
                "honua.attachments.filesystem.bytes_written",
                description: "Total bytes written to disk");
        }
    }

    /// <summary>
    /// Validates file upload security constraints including extension whitelist and MIME type verification.
    /// </summary>
    /// <param name="request">The attachment upload request to validate.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the file extension is not allowed or when the MIME type doesn't match the extension.
    /// </exception>
    /// <remarks>
    /// This method implements defense-in-depth security by:
    /// 1. Extracting the file extension from the filename
    /// 2. Checking if the extension is in the allowed whitelist
    /// 3. Verifying the MIME type matches expected values for that extension
    /// This prevents uploading of executables, scripts, or other potentially malicious files.
    /// </remarks>
    private static void ValidateFileUpload(AttachmentStorePutRequest request)
    {
        // Extract extension from filename
        var extension = Path.GetExtension(request.FileName);

        // Reject files with no extension or disallowed extensions
        if (extension.IsNullOrEmpty() || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"File extension '{extension}' is not allowed. " +
                $"Allowed extensions: {string.Join(", ", AllowedExtensions.OrderBy(e => e))}");
        }

        // Validate MIME type matches expected type(s) for the extension
        if (!request.MimeType.IsNullOrEmpty())
        {
            if (AllowedMimeTypes.TryGetValue(extension, out var expectedMimeTypes))
            {
                var matches = expectedMimeTypes.Any(mimeType =>
                    request.MimeType.StartsWith(mimeType, StringComparison.OrdinalIgnoreCase));

                if (!matches)
                {
                    throw new InvalidOperationException(
                        $"MIME type '{request.MimeType}' does not match expected type(s) " +
                        $"for extension '{extension}': {string.Join(", ", expectedMimeTypes)}");
                }
            }
        }
    }

    public async Task<AttachmentStoreWriteResult> PutAsync(Stream content, AttachmentStorePutRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(content);
        Guard.NotNull(request);

        // Validate file extension and MIME type before accepting upload
        ValidateFileUpload(request);

        var relativePath = BuildRelativePath(request.AttachmentId);
        var fullPath = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var totalStopwatch = Stopwatch.StartNew();
        long totalBytesWritten = 0;
        await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            // Copy with size limit enforcement
            byte[] buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                totalBytesWritten += bytesRead;
                if (totalBytesWritten > MaxFileSizeBytes)
                {
                    // Clean up partial file
                    fileStream.Close();
                    File.Delete(fullPath);
                    throw new InvalidOperationException($"Attachment exceeds maximum size of {MaxFileSizeBytes / (1024 * 1024)}MB");
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }

            // CRITICAL: Ensure all data is physically written to disk before returning success
            // Without this, power failure could result in an empty/corrupt file with a valid database record
            // First flush the managed buffer to the OS
            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Then ensure OS buffer is physically written to disk
            // Note: Flush(flushToDisk: true) is synchronous and can block the thread pool.
            // We offload it to a background thread to prevent thread pool starvation under high load.
            // This is the recommended pattern until .NET provides a native FlushToDiskAsync() API.
            // The trade-off is minimal - we're not adding extra threads, just using the thread pool more efficiently.
            var flushStopwatch = Stopwatch.StartNew();
            await Task.Run(() => fileStream.Flush(flushToDisk: true), cancellationToken).ConfigureAwait(false);
            flushStopwatch.Stop();

            // Record flush performance metrics
            _flushDuration?.Record(flushStopwatch.Elapsed.TotalMilliseconds);

            _logger.LogTrace("Flushed {Bytes} bytes to disk in {FlushMs}ms",
                totalBytesWritten, flushStopwatch.Elapsed.TotalMilliseconds);
        }

        totalStopwatch.Stop();

        // Record performance metrics
        _fileWriteDuration?.Record(totalStopwatch.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("size_kb", totalBytesWritten / 1024));
        _bytesWritten?.Add(totalBytesWritten);

        _logger.LogDebug(
            "Wrote {Bytes} bytes to {Path} in {Duration}ms (flush: {FlushDuration}ms)",
            totalBytesWritten, relativePath, totalStopwatch.Elapsed.TotalMilliseconds,
            _flushDuration != null ? "tracked" : "not tracked");

        // Set restrictive file permissions on Unix-like systems
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set file permissions for {Path}", fullPath);
            }
        }

        return new AttachmentStoreWriteResult
        {
            Pointer = new AttachmentPointer(AttachmentStoreProviderKeys.FileSystem, relativePath)
        };
    }

    public Task<AttachmentReadResult?> TryGetAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(pointer);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<AttachmentReadResult?>(null);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        stream.Position = 0;
        var fileInfo = new FileInfo(fullPath);
        var result = new AttachmentReadResult
        {
            Content = stream,
            MimeType = null,
            SizeBytes = fileInfo.Length,
            FileName = Path.GetFileName(pointer.StorageKey),
            ChecksumSha256 = null
        };

        return Task.FromResult<AttachmentReadResult?>(result);
    }

    public Task<bool> DeleteAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(pointer);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete attachment at {Path}", fullPath);
            return Task.FromResult(false);
        }
    }

    public async IAsyncEnumerable<AttachmentPointer> ListAsync(string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootPath))
        {
            yield break;
        }

        var files = Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(_rootPath, file);
            if (prefix.HasValue() && !relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new AttachmentPointer(AttachmentStoreProviderKeys.FileSystem, relativePath);
            await Task.Yield();
        }
    }

    private static string BuildRelativePath(string attachmentId)
    {
        if (attachmentId.IsNullOrWhiteSpace() || attachmentId.Length < 4)
        {
            return attachmentId;
        }

        var first = attachmentId.Substring(0, 2);
        var second = attachmentId.Substring(2, 2);
        return Path.Combine(first, second, attachmentId);
    }

    private string ResolveFullPath(AttachmentPointer pointer)
    {
        if (!string.Equals(pointer.StorageProvider, AttachmentStoreProviderKeys.FileSystem, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Pointer does not belong to filesystem store: {pointer.StorageProvider}");
        }

        // Normalize root path
        var normalizedRoot = Path.GetFullPath(_rootPath);

        // Split path and validate each segment
        var segments = pointer.StorageKey
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        var sanitizedSegments = new List<string>();

        foreach (var segment in segments)
        {
            // Reject any segment containing traversal sequences
            if (segment.Contains("..") ||
                segment.Contains(".") && segment.Length <= 2 ||
                segment.Contains(':') ||
                segment.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException(
                    $"Invalid attachment path segment: '{segment}'. " +
                    "Path traversal attempts are not allowed.");
            }

            // Additional validation for suspicious characters
            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid characters in attachment path segment: '{segment}'");
            }

            sanitizedSegments.Add(segment);
        }

        // Rebuild path from sanitized segments
        var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), sanitizedSegments);

        // Combine and normalize
        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

        // CRITICAL: Verify final path is within root (after all normalization)
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var rootWithSeparator = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, comparison) &&
            !fullPath.Equals(normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), comparison))
        {
            _logger.LogError(
                "Path traversal attempt detected: StorageKey={StorageKey}, ResolvedPath={ResolvedPath}, Root={Root}",
                pointer.StorageKey, fullPath, normalizedRoot);

            throw new InvalidOperationException(
                $"Invalid attachment path: resolves outside storage root. This incident has been logged.");
        }

        return fullPath;
    }
}

public static class AttachmentStoreProviderKeys
{
    public const string FileSystem = "filesystem";
    public const string S3 = "s3";
    public const string AzureBlob = "azureblob";
    public const string Gcs = "gcs";
    public const string Database = "database";
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Provides static helper methods for validating uploaded files from multipart form-data requests.
/// </summary>
/// <remarks>
/// This helper centralizes common validation logic for file uploads including:
/// - File existence and content checks
/// - File size validation against configurable limits
/// - Content type validation against allowed MIME types
/// - File extension validation against allowed extensions
/// - Sanitized filename generation to prevent path traversal attacks
/// </remarks>
internal static class FormFileValidationHelper
{
    /// <summary>
    /// Validates an uploaded file against security and format requirements.
    /// </summary>
    /// <param name="file">The uploaded file to validate. May be null.</param>
    /// <param name="maxSizeBytes">Maximum allowed file size in bytes.</param>
    /// <param name="allowedContentTypes">Collection of allowed MIME content types. Empty collection skips content type validation.</param>
    /// <param name="allowedExtensions">Collection of allowed file extensions (e.g., ".json", ".zip"). Must include the leading dot.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsValid: true if validation passed, false otherwise
    /// - ErrorMessage: null if valid, otherwise a descriptive error message
    /// - SafeFileName: null if invalid, otherwise a sanitized filename in the format "{guid}{extension}"
    /// </returns>
    /// <remarks>
    /// The safe filename is generated as a GUID (N format - 32 hex digits) plus the original file extension.
    /// This prevents path traversal attacks and ensures unique filenames.
    ///
    /// Content type validation is case-insensitive and uses partial matching (Contains) to handle
    /// MIME types with parameters (e.g., "application/json; charset=utf-8").
    ///
    /// Extension validation is case-insensitive and requires exact matches from the allowedExtensions collection.
    /// </remarks>
    /// <example>
    /// <code>
    /// var allowedTypes = new[] { "application/json", "application/zip" };
    /// var allowedExtensions = new[] { ".json", ".zip" };
    /// var (isValid, error, safeName) = FormFileValidationHelper.ValidateUploadedFile(
    ///     file,
    ///     maxSizeBytes: 500L * 1024 * 1024, // 500MB
    ///     allowedTypes,
    ///     allowedExtensions);
    ///
    /// if (!isValid)
    /// {
    ///     return Results.BadRequest(new { error });
    /// }
    ///
    /// // Use safeName for storing the file
    /// var targetPath = Path.Combine(workingDir, safeName);
    /// </code>
    /// </example>
    public static (bool IsValid, string? ErrorMessage, string? SafeFileName) ValidateUploadedFile(
        IFormFile? file,
        long maxSizeBytes,
        IReadOnlyCollection<string> allowedContentTypes,
        IReadOnlyCollection<string> allowedExtensions)
    {
        Guard.NotNull(allowedContentTypes);
        Guard.NotNull(allowedExtensions);

        if (allowedExtensions.Count == 0)
        {
            throw new ArgumentException("At least one allowed extension must be specified.", nameof(allowedExtensions));
        }

        // Check file exists and has content
        if (file is null || file.Length == 0)
        {
            return (false, "A non-empty dataset file must be provided.", null);
        }

        // Validate file size
        if (file.Length > maxSizeBytes)
        {
            return (false, $"File size {file.Length:N0} bytes exceeds maximum of {maxSizeBytes:N0} bytes ({FormatBytes(maxSizeBytes)})", null);
        }

        // Validate content type (if content types are specified)
        if (allowedContentTypes.Count > 0 && !file.ContentType.IsNullOrEmpty())
        {
            var isValidContentType = allowedContentTypes.Any(ct =>
                file.ContentType.Contains(ct, StringComparison.OrdinalIgnoreCase));

            if (!isValidContentType)
            {
                return (false, $"Invalid content type '{file.ContentType}'. Allowed types: {string.Join(", ", allowedContentTypes)}", null);
            }
        }

        // Validate file extension
        var fileName = file.FileName.IsNullOrWhiteSpace() ? "dataset" : Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Invalid file type '{extension}'. Allowed types: {string.Join(", ", allowedExtensions)}", null);
        }

        // Validate file signature (magic bytes)
        using (var stream = file.OpenReadStream())
        {
            if (!ValidateFileSignature(stream, extension))
            {
                return (false, $"File signature does not match extension '{extension}'. The file may be corrupted or renamed.", null);
            }
        }

        // Generate sanitized filename to prevent path traversal attacks
        var safeFileName = $"{Guid.NewGuid():N}{extension}";

        return (true, null, safeFileName);
    }

    /// <summary>
    /// Validates that a file's magic bytes match its declared extension.
    /// </summary>
    /// <param name="fileStream">The file stream to validate.</param>
    /// <param name="extension">The expected file extension (e.g., ".pdf").</param>
    /// <returns>True if the file signature matches the extension, false otherwise.</returns>
    /// <remarks>
    /// This method reads up to 16 bytes from the stream to check the file signature.
    /// The stream position is reset after validation.
    ///
    /// Note: Some formats (like .docx, .xlsx, .zip) share the same signature (ZIP format).
    /// This validation prevents obvious spoofing but cannot distinguish between ZIP-based formats.
    /// </remarks>
    public static bool ValidateFileSignature(Stream fileStream, string extension)
    {
        if (fileStream == null || !fileStream.CanRead || !fileStream.CanSeek)
            return false;

        // Check if we have signatures for this extension
        if (!FileSignatures.KnownSignatures.TryGetValue(extension, out var signatures))
        {
            // No known signatures for this extension - allow it
            // (Validation is opt-in for known types only)
            return true;
        }

        // Save original position
        var originalPosition = fileStream.Position;

        try
        {
            fileStream.Seek(0, SeekOrigin.Begin);

            // Read up to 16 bytes for signature checking
            var headerBytes = new byte[16];
            var bytesRead = fileStream.Read(headerBytes, 0, headerBytes.Length);

            if (bytesRead == 0)
                return false; // Empty file

            // Check against all known signatures for this extension
            foreach (var signature in signatures)
            {
                if (bytesRead >= signature.Length)
                {
                    bool matches = true;
                    for (int i = 0; i < signature.Length; i++)
                    {
                        if (headerBytes[i] != signature[i])
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                        return true;
                }
            }

            return false; // No signature matched
        }
        finally
        {
            // Always reset stream position
            fileStream.Seek(originalPosition, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// Formats a byte count into a human-readable string (e.g., "500MB", "1GB").
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => $"{bytes / GB}GB",
            >= MB => $"{bytes / MB}MB",
            >= KB => $"{bytes / KB}KB",
            _ => $"{bytes} bytes"
        };
    }
}

/// <summary>
/// Known file signatures (magic bytes) for common file types.
/// </summary>
internal static class FileSignatures
{
    /// <summary>
    /// File signature definitions mapping extensions to their magic byte patterns.
    /// Multiple signatures per extension handle format variations.
    /// </summary>
    public static readonly Dictionary<string, byte[][]> KnownSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        { ".png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        { ".jpg", new[] {
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 },
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 },
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 }
        } },
        { ".jpeg", new[] {
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 },
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 },
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 }
        } },
        { ".gif", new[] {
            new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, // GIF87a
            new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }  // GIF89a
        } },
        { ".bmp", new[] { new byte[] { 0x42, 0x4D } } },
        { ".tif", new[] {
            new byte[] { 0x49, 0x49, 0x2A, 0x00 }, // Little-endian
            new byte[] { 0x4D, 0x4D, 0x00, 0x2A }  // Big-endian
        } },
        { ".tiff", new[] {
            new byte[] { 0x49, 0x49, 0x2A, 0x00 },
            new byte[] { 0x4D, 0x4D, 0x00, 0x2A }
        } },
        { ".webp", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } },

        // Documents
        { ".pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D } } },
        { ".docx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } }, // ZIP-based
        { ".xlsx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } }, // ZIP-based
        { ".pptx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } }, // ZIP-based

        // Archives
        { ".zip", new[] {
            new byte[] { 0x50, 0x4B, 0x03, 0x04 },
            new byte[] { 0x50, 0x4B, 0x05, 0x06 },
            new byte[] { 0x50, 0x4B, 0x07, 0x08 }
        } },
        { ".7z", new[] { new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C } } },
        { ".rar", new[] { new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 } } },
        { ".gz", new[] { new byte[] { 0x1F, 0x8B } } },
        { ".tar", new[] { new byte[] { 0x75, 0x73, 0x74, 0x61, 0x72 } } },

        // GIS formats
        { ".shp", new[] { new byte[] { 0x00, 0x00, 0x27, 0x0A } } },
        { ".geojson", new[] { new byte[] { 0x7B } } }, // JSON starts with {
        { ".json", new[] {
            new byte[] { 0x7B }, // {
            new byte[] { 0x5B }  // [
        } },
        { ".xml", new[] {
            new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C }, // <?xml
            new byte[] { 0xEF, 0xBB, 0xBF, 0x3C, 0x3F, 0x78, 0x6D, 0x6C } // BOM + <?xml
        } },
        { ".kml", new[] {
            new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C },
            new byte[] { 0xEF, 0xBB, 0xBF, 0x3C, 0x3F, 0x78, 0x6D, 0x6C }
        } },

        // CSV (plain text - harder to validate, just check for valid UTF-8)
        { ".csv", new[] {
            new byte[] { 0xEF, 0xBB, 0xBF }, // UTF-8 BOM (optional)
        } }
    };
}

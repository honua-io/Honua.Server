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

        // Generate sanitized filename to prevent path traversal attacks
        var safeFileName = $"{Guid.NewGuid():N}{extension}";

        return (true, null, safeFileName);
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

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Validation;

/// <summary>
/// Provides secure file name sanitization to prevent path traversal and other file-related security issues.
/// SECURITY: Critical component for preventing directory traversal attacks and malicious file operations.
/// </summary>
/// <remarks>
/// This sanitizer performs the following security measures:
/// - Removes path traversal sequences (../ and ~)
/// - Strips directory separators and path components
/// - Whitelists allowed characters [a-zA-Z0-9._-]
/// - Limits file name length to 255 characters (filesystem limit)
/// - Prevents reserved Windows file names (CON, PRN, AUX, NUL, etc.)
/// - Preserves file extension for proper handling
/// - Prevents hidden files (starting with .)
/// </remarks>
public static class FileNameSanitizer
{
    /// <summary>
    /// Maximum allowed file name length (255 characters is the common filesystem limit).
    /// </summary>
    public const int MaxFileNameLength = 255;

    /// <summary>
    /// Maximum allowed file extension length (including the dot).
    /// </summary>
    public const int MaxExtensionLength = 20;

    /// <summary>
    /// Minimum file name length (excluding extension).
    /// </summary>
    public const int MinFileNameLength = 1;

    /// <summary>
    /// Reserved file names on Windows that cannot be used.
    /// </summary>
    /// <remarks>
    /// These are special device names in Windows that can cause issues if used as file names.
    /// See: https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file
    /// </remarks>
    private static readonly string[] WindowsReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    /// <summary>
    /// Pattern for allowed characters in file names (alphanumeric, underscore, hyphen, period).
    /// </summary>
    private static readonly Regex AllowedCharactersPattern = new(@"[^a-zA-Z0-9._-]", RegexOptions.Compiled);

    /// <summary>
    /// Pattern for detecting multiple consecutive periods (potential security issue).
    /// </summary>
    private static readonly Regex MultiplePeriodsPattern = new(@"\.{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Sanitizes a user-provided file name to ensure it is safe for file system operations.
    /// </summary>
    /// <param name="fileName">The original file name to sanitize.</param>
    /// <returns>A sanitized file name safe for use in file operations.</returns>
    /// <exception cref="ArgumentException">Thrown when the file name is null, empty, or invalid.</exception>
    /// <remarks>
    /// The sanitization process:
    /// 1. Removes any path components (extracts just the file name)
    /// 2. Replaces unsafe characters with underscores
    /// 3. Prevents path traversal sequences
    /// 4. Limits length to filesystem maximum
    /// 5. Prevents reserved Windows names
    /// 6. Preserves the file extension
    /// </remarks>
    public static string Sanitize(string fileName)
    {
        if (fileName.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
        }

        // Remove any path components - only use the file name
        fileName = Path.GetFileName(fileName);

        if (fileName.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("File name cannot be empty after removing path components.", nameof(fileName));
        }

        // Split into name and extension
        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Validate extension length
        if (extension.Length > MaxExtensionLength)
        {
            throw new ArgumentException(
                $"File extension exceeds maximum length of {MaxExtensionLength} characters.",
                nameof(fileName));
        }

        // Sanitize the extension (remove any invalid characters)
        if (!extension.IsNullOrEmpty())
        {
            extension = AllowedCharactersPattern.Replace(extension, "_");
            // Ensure extension starts with a period
            if (!extension.StartsWith('.'))
            {
                extension = "." + extension;
            }
        }

        // Sanitize the name portion
        nameWithoutExtension = SanitizeNamePortion(nameWithoutExtension);

        // Validate name length
        if (nameWithoutExtension.Length < MinFileNameLength)
        {
            throw new ArgumentException(
                $"File name must be at least {MinFileNameLength} character(s) long.",
                nameof(fileName));
        }

        // Combine name and extension
        var sanitizedFileName = nameWithoutExtension + extension;

        // Enforce total length limit
        if (sanitizedFileName.Length > MaxFileNameLength)
        {
            // Truncate the name portion to fit
            var availableNameLength = MaxFileNameLength - extension.Length;
            if (availableNameLength < MinFileNameLength)
            {
                throw new ArgumentException(
                    $"File name with extension exceeds maximum length of {MaxFileNameLength} characters.",
                    nameof(fileName));
            }

            nameWithoutExtension = nameWithoutExtension[..availableNameLength];
            sanitizedFileName = nameWithoutExtension + extension;
        }

        // Check for Windows reserved names
        ValidateNotReservedName(nameWithoutExtension);

        // Final validation
        if (sanitizedFileName.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Sanitized file name is empty or invalid.", nameof(fileName));
        }

        return sanitizedFileName;
    }

    /// <summary>
    /// Tries to sanitize a file name, returning false if sanitization fails.
    /// </summary>
    /// <param name="fileName">The original file name.</param>
    /// <param name="sanitizedFileName">The sanitized file name if successful.</param>
    /// <param name="errorMessage">The error message if sanitization fails.</param>
    /// <returns>True if sanitization succeeded; otherwise, false.</returns>
    public static bool TrySanitize(string fileName, out string? sanitizedFileName, out string? errorMessage)
    {
        try
        {
            sanitizedFileName = Sanitize(fileName);
            errorMessage = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            sanitizedFileName = null;
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Validates that a file name is safe without modifying it.
    /// </summary>
    /// <param name="fileName">The file name to validate.</param>
    /// <returns>True if the file name is safe; otherwise, false.</returns>
    public static bool IsSafe(string fileName)
    {
        if (fileName.IsNullOrWhiteSpace())
        {
            return false;
        }

        // Check for path separators
        if (fileName.Contains('/') || fileName.Contains('\\'))
        {
            return false;
        }

        // Check for path traversal
        if (fileName.Contains("..") || fileName.Contains('~'))
        {
            return false;
        }

        // Check length
        if (fileName.Length > MaxFileNameLength)
        {
            return false;
        }

        // Check for invalid characters
        if (AllowedCharactersPattern.IsMatch(fileName.Replace(".", "")))
        {
            return false;
        }

        // Check for multiple consecutive periods
        if (MultiplePeriodsPattern.IsMatch(fileName))
        {
            return false;
        }

        // Check for hidden files
        if (fileName.StartsWith('.'))
        {
            return false;
        }

        // Check name portion (without extension)
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (nameWithoutExtension.IsNullOrWhiteSpace())
        {
            return false;
        }

        // Check for reserved names
        if (WindowsReservedNames.Contains(nameWithoutExtension.ToUpperInvariant()))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sanitizes the name portion of a file (without extension).
    /// </summary>
    private static string SanitizeNamePortion(string name)
    {
        if (name.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("File name portion cannot be empty.", nameof(name));
        }

        // Remove any whitespace
        name = name.Trim();

        // Replace unsafe characters with underscores
        name = AllowedCharactersPattern.Replace(name, "_");

        // Remove multiple consecutive periods (potential security issue)
        name = MultiplePeriodsPattern.Replace(name, ".");

        // Remove leading periods (hidden files on Unix-like systems)
        name = name.TrimStart('.');

        // Remove trailing periods (not allowed on Windows)
        name = name.TrimEnd('.');

        // Remove leading/trailing underscores and hyphens
        name = name.Trim('_', '-');

        // Collapse multiple underscores
        while (name.Contains("__"))
        {
            name = name.Replace("__", "_");
        }

        if (name.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("File name portion is empty after sanitization.", nameof(name));
        }

        return name;
    }

    /// <summary>
    /// Validates that a name is not a Windows reserved name.
    /// </summary>
    private static void ValidateNotReservedName(string nameWithoutExtension)
    {
        var upperName = nameWithoutExtension.ToUpperInvariant();

        // Check exact match
        if (WindowsReservedNames.Contains(upperName))
        {
            throw new ArgumentException(
                $"File name '{nameWithoutExtension}' is a reserved system name and cannot be used.",
                nameof(nameWithoutExtension));
        }

        // Check if it starts with a reserved name followed by a number (e.g., CON1)
        foreach (var reservedName in WindowsReservedNames)
        {
            if (upperName.StartsWith(reservedName) && upperName.Length > reservedName.Length)
            {
                var suffix = upperName[reservedName.Length..];
                if (suffix.All(char.IsDigit))
                {
                    throw new ArgumentException(
                        $"File name '{nameWithoutExtension}' uses a reserved system name pattern.",
                        nameof(nameWithoutExtension));
                }
            }
        }
    }

    /// <summary>
    /// Generates a safe random file name with the specified extension.
    /// </summary>
    /// <param name="extension">The file extension (with or without leading period).</param>
    /// <param name="prefix">Optional prefix for the file name.</param>
    /// <returns>A safe random file name.</returns>
    /// <remarks>
    /// Useful for generating unique file names for uploads or temporary files.
    /// </remarks>
    public static string GenerateSafeFileName(string extension, string? prefix = null)
    {
        if (extension.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Extension cannot be null or empty.", nameof(extension));
        }

        // Ensure extension starts with a period
        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        // Sanitize the extension
        extension = AllowedCharactersPattern.Replace(extension, "_");

        // Generate unique identifier
        var uniqueId = Guid.NewGuid().ToString("N"); // 32-character hex string

        // Add optional prefix
        var fileName = prefix.IsNullOrWhiteSpace()
            ? uniqueId
            : $"{SanitizeNamePortion(prefix)}_{uniqueId}";

        return fileName + extension;
    }

    /// <summary>
    /// Validates a file extension against a whitelist.
    /// </summary>
    /// <param name="fileName">The file name to check.</param>
    /// <param name="allowedExtensions">Array of allowed extensions (with or without leading period).</param>
    /// <returns>True if the extension is allowed; otherwise, false.</returns>
    public static bool HasAllowedExtension(string fileName, params string[] allowedExtensions)
    {
        if (fileName.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (allowedExtensions == null || allowedExtensions.Length == 0)
        {
            return true; // No restrictions
        }

        var extension = Path.GetExtension(fileName);
        if (extension.IsNullOrEmpty())
        {
            return false;
        }

        // Normalize extensions (ensure they all have a leading period and are lowercase)
        var normalizedExtension = extension.ToLowerInvariant();
        var normalizedAllowed = allowedExtensions.Select(ext =>
        {
            var normalized = ext.ToLowerInvariant();
            return normalized.StartsWith('.') ? normalized : "." + normalized;
        });

        return normalizedAllowed.Contains(normalizedExtension);
    }
}

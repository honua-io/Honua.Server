// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Security;

/// <summary>
/// Validates ZIP archives for security threats including path traversal,
/// malicious file types, and zip bombs.
/// </summary>
public static class ZipArchiveValidator
{
    /// <summary>
    /// Default maximum uncompressed size for zip archives (1 GB).
    /// </summary>
    public const long DefaultMaxUncompressedSize = 1L * 1024 * 1024 * 1024;

    /// <summary>
    /// Default maximum compression ratio (100:1).
    /// Zip bombs often have ratios of 1000:1 or higher.
    /// </summary>
    public const int DefaultMaxCompressionRatio = 100;

    /// <summary>
    /// Default maximum number of entries in a zip archive.
    /// </summary>
    public const int DefaultMaxEntries = 10_000;

    /// <summary>
    /// Maximum allowed file name length to prevent overflow attacks.
    /// </summary>
    private const int MaxFileNameLength = 255;

    /// <summary>
    /// Dangerous file extensions that should be blocked.
    /// </summary>
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".so", ".dylib", ".bat", ".cmd", ".sh", ".ps1", ".psm1",
        ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".msi", ".msp", ".scr",
        ".com", ".pif", ".application", ".gadget", ".msc", ".jar", ".app",
        ".deb", ".rpm", ".dmg", ".pkg", ".run"
    };

    /// <summary>
    /// Result of zip archive validation.
    /// </summary>
    public sealed record ValidationResult
    {
        public bool IsValid { get; init; }
        public string? ErrorMessage { get; init; }
        public List<string> Warnings { get; init; } = new();
        public long TotalUncompressedSize { get; init; }
        public int EntryCount { get; init; }
        public List<string> ValidatedEntries { get; init; } = new();

        public static ValidationResult Success(long totalSize, int entryCount, List<string> entries)
            => new()
            {
                IsValid = true,
                TotalUncompressedSize = totalSize,
                EntryCount = entryCount,
                ValidatedEntries = entries
            };

        public static ValidationResult Failure(string errorMessage)
            => new()
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
    }

    /// <summary>
    /// Validates a zip archive stream for security threats.
    /// </summary>
    /// <param name="zipStream">The stream containing the zip archive</param>
    /// <param name="allowedExtensions">Set of allowed file extensions (null = allow all safe extensions)</param>
    /// <param name="maxUncompressedSize">Maximum total uncompressed size in bytes</param>
    /// <param name="maxCompressionRatio">Maximum compression ratio to detect zip bombs</param>
    /// <param name="maxEntries">Maximum number of entries allowed</param>
    /// <returns>Validation result with details</returns>
    public static ValidationResult ValidateZipArchive(
        Stream zipStream,
        ISet<string>? allowedExtensions = null,
        long maxUncompressedSize = DefaultMaxUncompressedSize,
        int maxCompressionRatio = DefaultMaxCompressionRatio,
        int maxEntries = DefaultMaxEntries)
    {
        Guard.NotNull(zipStream);

        if (!zipStream.CanRead)
        {
            return ValidationResult.Failure("Stream is not readable");
        }

        try
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

            // Check entry count
            var entries = archive.Entries.ToList();
            if (entries.Count > maxEntries)
            {
                return ValidationResult.Failure(
                    $"Archive contains too many entries ({entries.Count}). Maximum allowed: {maxEntries}");
            }

            if (entries.Count == 0)
            {
                return ValidationResult.Failure("Archive is empty");
            }

            long totalUncompressedSize = 0;
            long totalCompressedSize = 0;
            var validatedEntries = new List<string>();
            var warnings = new List<string>();

            foreach (var entry in entries)
            {
                // Skip directory entries
                if (entry.Name.IsNullOrEmpty() || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    continue;
                }

                // Validate entry name
                var nameValidation = ValidateEntryName(entry.FullName);
                if (!nameValidation.IsValid)
                {
                    return ValidationResult.Failure(
                        $"Invalid entry name '{entry.FullName}': {nameValidation.ErrorMessage}");
                }

                // Check file name length
                if (entry.FullName.Length > MaxFileNameLength)
                {
                    return ValidationResult.Failure(
                        $"Entry name too long: '{entry.FullName}' ({entry.FullName.Length} characters). Maximum: {MaxFileNameLength}");
                }

                // Validate file extension
                var extension = Path.GetExtension(entry.Name).ToLowerInvariant();

                // Check against dangerous extensions
                if (DangerousExtensions.Contains(extension))
                {
                    return ValidationResult.Failure(
                        $"Dangerous file type detected: '{entry.FullName}' with extension '{extension}'");
                }

                // Check against allowed extensions if specified
                if (allowedExtensions != null && !extension.IsNullOrEmpty())
                {
                    if (!allowedExtensions.Contains(extension))
                    {
                        return ValidationResult.Failure(
                            $"File type '{extension}' is not allowed in entry '{entry.FullName}'. " +
                            $"Allowed types: {string.Join(", ", allowedExtensions)}");
                    }
                }

                // Check for zip bombs
                totalUncompressedSize += entry.Length;
                totalCompressedSize += entry.CompressedLength;

                if (totalUncompressedSize > maxUncompressedSize)
                {
                    return ValidationResult.Failure(
                        $"Archive uncompressed size ({totalUncompressedSize:N0} bytes) exceeds maximum allowed ({maxUncompressedSize:N0} bytes). Possible zip bomb.");
                }

                // Check individual entry compression ratio
                if (entry.CompressedLength > 0)
                {
                    var ratio = (double)entry.Length / entry.CompressedLength;
                    if (ratio > maxCompressionRatio)
                    {
                        return ValidationResult.Failure(
                            $"Entry '{entry.FullName}' has suspicious compression ratio ({ratio:F1}:1). Maximum allowed: {maxCompressionRatio}:1. Possible zip bomb.");
                    }
                }

                // Check for nested zip files (potential zip bomb technique)
                if (extension == ".zip")
                {
                    warnings.Add($"Nested zip file detected: '{entry.FullName}'. This will not be extracted.");
                }

                validatedEntries.Add(entry.FullName);
            }

            // Check overall compression ratio
            if (totalCompressedSize > 0)
            {
                var overallRatio = (double)totalUncompressedSize / totalCompressedSize;
                if (overallRatio > maxCompressionRatio)
                {
                    return ValidationResult.Failure(
                        $"Archive has suspicious overall compression ratio ({overallRatio:F1}:1). Maximum allowed: {maxCompressionRatio}:1. Possible zip bomb.");
                }
            }

            var result = ValidationResult.Success(totalUncompressedSize, validatedEntries.Count, validatedEntries);
            foreach (var warning in warnings)
            {
                result.Warnings.Add(warning);
            }

            return result;
        }
        catch (InvalidDataException ex)
        {
            return ValidationResult.Failure($"Invalid or corrupted zip archive: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException || ex is NotSupportedException)
        {
            return ValidationResult.Failure($"Error reading zip archive: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a zip archive file for security threats.
    /// </summary>
    public static ValidationResult ValidateZipFile(
        string zipFilePath,
        ISet<string>? allowedExtensions = null,
        long maxUncompressedSize = DefaultMaxUncompressedSize,
        int maxCompressionRatio = DefaultMaxCompressionRatio,
        int maxEntries = DefaultMaxEntries)
    {
        Guard.NotNullOrWhiteSpace(zipFilePath);

        if (!File.Exists(zipFilePath))
        {
            return ValidationResult.Failure($"Zip file not found: {zipFilePath}");
        }

        try
        {
            using var stream = File.OpenRead(zipFilePath);
            return ValidateZipArchive(stream, allowedExtensions, maxUncompressedSize, maxCompressionRatio, maxEntries);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ValidationResult.Failure($"Access denied to zip file: {ex.Message}");
        }
        catch (IOException ex)
        {
            return ValidationResult.Failure($"Error reading zip file: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely extracts a validated zip archive to a target directory.
    /// </summary>
    /// <param name="zipStream">The stream containing the zip archive</param>
    /// <param name="targetDirectory">The directory to extract to</param>
    /// <param name="validationResult">The validation result from a prior validation</param>
    /// <returns>List of extracted file paths</returns>
    public static List<string> SafeExtract(Stream zipStream, string targetDirectory, ValidationResult validationResult)
    {
        Guard.NotNull(zipStream);
        Guard.NotNullOrWhiteSpace(targetDirectory);
        Guard.NotNull(validationResult);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException("Cannot extract an archive that failed validation");
        }

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var extractedFiles = new List<string>();

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            // Skip directory entries
            if (entry.Name.IsNullOrEmpty() || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                continue;
            }

            // Skip nested zip files
            if (entry.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Create a safe file name by removing any directory components
            // This prevents path traversal even if validation somehow missed it
            var safeFileName = Path.GetFileName(entry.Name);
            var destinationPath = Path.Combine(targetDirectory, safeFileName);

            // Double-check the path is within target directory (defense in depth)
            var fullDestPath = Path.GetFullPath(destinationPath);
            var fullTargetPath = Path.GetFullPath(targetDirectory);
            if (!fullDestPath.StartsWith(fullTargetPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Entry would extract outside target directory: {entry.FullName}");
            }

            // Extract the file
            entry.ExtractToFile(destinationPath, overwrite: false);
            extractedFiles.Add(destinationPath);
        }

        return extractedFiles;
    }

    /// <summary>
    /// Validates an entry name for path traversal and other attacks.
    /// </summary>
    private static ValidationResult ValidateEntryName(string entryName)
    {
        if (entryName.IsNullOrWhiteSpace())
        {
            return ValidationResult.Failure("Entry name is empty or whitespace");
        }

        // Check for null bytes
        if (entryName.Contains('\0'))
        {
            return ValidationResult.Failure("Entry name contains null byte");
        }

        // Check for absolute paths
        if (Path.IsPathRooted(entryName))
        {
            return ValidationResult.Failure("Entry name is an absolute path");
        }

        // Normalize path separators
        var normalizedName = entryName.Replace('\\', '/');

        // Check for path traversal attempts
        var segments = normalizedName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                return ValidationResult.Failure("Entry name contains path traversal (..)");
            }

            // Check for suspicious patterns
            if (segment.Contains("..") && segment.Length > 2)
            {
                // Allow double dots in file names like "version..2.txt" but be suspicious
                // This is a heuristic check
                if (segment.StartsWith("..") || segment.EndsWith(".."))
                {
                    return ValidationResult.Failure("Entry name contains suspicious pattern");
                }
            }
        }

        // Check for UNC paths
        if (entryName.StartsWith(@"\\") || entryName.StartsWith("//"))
        {
            return ValidationResult.Failure("Entry name appears to be a UNC path");
        }

        // Check for URL encoding
        if (entryName.Contains("%2e", StringComparison.OrdinalIgnoreCase) ||
            entryName.Contains("%2f", StringComparison.OrdinalIgnoreCase) ||
            entryName.Contains("%5c", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure("Entry name contains URL encoding");
        }

        // Check for control characters
        foreach (var c in entryName)
        {
            if (char.IsControl(c) && c != '\t')
            {
                return ValidationResult.Failure($"Entry name contains control character (0x{(int)c:X2})");
            }
        }

        return ValidationResult.Success(0, 0, new List<string>());
    }

    /// <summary>
    /// Gets the default allowed extensions for geospatial data files.
    /// </summary>
    public static HashSet<string> GetGeospatialExtensions()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".shp", ".shx", ".dbf", ".prj", ".cpg", ".sbn", ".sbx", ".qix",
            ".geojson", ".json", ".kml", ".gml", ".xml", ".csv", ".txt",
            ".gpkg", ".sqlite", ".db"
        };
    }
}

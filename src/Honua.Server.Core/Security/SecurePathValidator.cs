// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Security;

/// <summary>
/// Validates file paths to prevent path traversal attacks.
/// Ensures all file access is restricted to allowed base directories.
/// </summary>
public static class SecurePathValidator
{
    /// <summary>
    /// Validates that a requested path is within an allowed base directory.
    /// </summary>
    /// <param name="requestedPath">The path requested by the user or system</param>
    /// <param name="baseDirectory">The base directory that access must be restricted to</param>
    /// <returns>The validated absolute path</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the path is outside the allowed directory</exception>
    /// <exception cref="ArgumentException">Thrown when the path contains invalid characters or patterns</exception>
    public static string ValidatePath(string requestedPath, string baseDirectory)
    {
        Guard.NotNullOrWhiteSpace(requestedPath, nameof(requestedPath));
        Guard.NotNullOrWhiteSpace(baseDirectory, nameof(baseDirectory));

        // Reject obvious attack patterns before further processing
        ValidatePathPattern(requestedPath);

        try
        {
            // Resolve both paths to absolute canonical paths
            // This resolves "..", ".", symlinks, and normalizes separators
            var fullPath = Path.GetFullPath(requestedPath);
            var fullBasePath = Path.GetFullPath(baseDirectory);

            // Ensure the base path ends with a directory separator to prevent partial matches
            // e.g., prevent /data/public-unsafe from matching /data/public
            if (!fullBasePath.EndsWith(Path.DirectorySeparatorChar) &&
                !fullBasePath.EndsWith(Path.AltDirectorySeparatorChar))
            {
                fullBasePath += Path.DirectorySeparatorChar;
            }

            // Check if the requested path is within the base directory
            // Use OrdinalIgnoreCase for Windows compatibility, Ordinal for Unix
            var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullPath.StartsWith(fullBasePath, comparison))
            {
                throw new UnauthorizedAccessException(
                    $"Access to path outside allowed directory is forbidden. " +
                    $"Requested path resolves outside the allowed directory.");
            }

            return fullPath;
        }
        catch (ArgumentException ex)
        {
            // Path.GetFullPath throws ArgumentException for invalid paths
            throw new ArgumentException($"Invalid path format: {ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            // Path.GetFullPath throws NotSupportedException for unsupported path formats
            throw new ArgumentException($"Unsupported path format: {ex.Message}", ex);
        }
        catch (PathTooLongException ex)
        {
            throw new ArgumentException($"Path is too long: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates a path against multiple allowed base directories.
    /// Returns the first valid path found.
    /// </summary>
    /// <param name="requestedPath">The path requested by the user or system</param>
    /// <param name="allowedDirectories">Array of allowed base directories</param>
    /// <returns>The validated absolute path</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the path is not within any allowed directory</exception>
    public static string ValidatePathMultiple(string requestedPath, params string[] allowedDirectories)
    {
        Guard.NotNullOrWhiteSpace(requestedPath, nameof(requestedPath));

        if (allowedDirectories == null || allowedDirectories.Length == 0)
        {
            throw new ArgumentException("At least one allowed directory must be provided", nameof(allowedDirectories));
        }

        // Try each allowed directory
        UnauthorizedAccessException? lastException = null;
        foreach (var baseDir in allowedDirectories.Where(d => d.HasValue()))
        {
            try
            {
                return ValidatePath(requestedPath, baseDir);
            }
            catch (UnauthorizedAccessException ex)
            {
                lastException = ex;
                // Continue to next directory
            }
        }

        // None of the directories matched
        throw new UnauthorizedAccessException(
            $"Access to path outside all allowed directories is forbidden. " +
            $"Path must be within one of {allowedDirectories.Length} configured directories.");
    }

    /// <summary>
    /// Validates that a path doesn't contain suspicious patterns commonly used in path traversal attacks.
    /// This is a defense-in-depth measure; the primary protection is canonical path comparison.
    /// </summary>
    /// <remarks>
    /// This method allows absolute paths since they may come from trusted internal code.
    /// The actual security check happens in ValidatePath via canonical path comparison.
    /// </remarks>
    private static void ValidatePathPattern(string path)
    {
        // Check for null bytes (common in directory traversal attacks)
        if (path.Contains('\0'))
        {
            throw new ArgumentException("Path contains null byte character", nameof(path));
        }

        // Check for UNC paths on Windows (\\server\share)
        // These should be handled through proper configuration, not user input
        if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("UNC paths are not allowed", nameof(path));
        }

        // Check for URL-encoded path traversal attempts
        if (path.Contains("%2e", StringComparison.OrdinalIgnoreCase) ||  // encoded .
            path.Contains("%2f", StringComparison.OrdinalIgnoreCase) ||  // encoded /
            path.Contains("%5c", StringComparison.OrdinalIgnoreCase))    // encoded \
        {
            throw new ArgumentException("URL-encoded path characters are not allowed", nameof(path));
        }

        // Check for other common encoding attempts
        if (path.Contains("..%", StringComparison.Ordinal) ||
            path.Contains("%00", StringComparison.Ordinal))
        {
            throw new ArgumentException("Encoded path traversal patterns are not allowed", nameof(path));
        }

        // NOTE: We intentionally allow absolute paths here since they may come from trusted
        // internal code (e.g., ShapefileExporter creating temp directories).
        // The actual security validation happens in ValidatePath() through canonical path
        // comparison, which ensures the resolved absolute path is within the allowed directory.
        // This approach provides defense-in-depth while supporting legitimate use cases.
    }

    /// <summary>
    /// Checks if a path exists and is within the allowed directory.
    /// Combines validation with existence check.
    /// </summary>
    /// <param name="requestedPath">The path to validate and check</param>
    /// <param name="baseDirectory">The base directory that access must be restricted to</param>
    /// <param name="logger">Optional logger for recording validation failures</param>
    /// <returns>True if the path is valid and exists, false otherwise</returns>
    public static bool IsValidAndExists(string requestedPath, string baseDirectory, ILogger? logger = null)
    {
        try
        {
            var validPath = ValidatePath(requestedPath, baseDirectory);
            return File.Exists(validPath) || Directory.Exists(validPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogWarning(ex, "Path validation failed for requested path. This may indicate a path traversal attempt");
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Path validation or existence check failed for requested path: {RequestedPath}", requestedPath);
            return false;
        }
    }
}

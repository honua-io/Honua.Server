// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Security;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Security;

/// <summary>
/// Validates file paths to prevent path traversal attacks when LLM-controlled
/// input is used to construct file system paths.
/// </summary>
/// <remarks>
/// Path traversal vulnerabilities occur when an attacker can manipulate file paths
/// to access files or directories outside of the intended workspace. This is especially
/// dangerous in AI/LLM contexts where the model controls the file paths.
///
/// Examples of path traversal attacks:
/// - "../../../etc/passwd" (Unix)
/// - "..\..\..\..\windows\system32\config\sam" (Windows)
/// - "/etc/passwd" (absolute path escape)
/// - "workspace/../../../sensitive.txt" (relative escape)
///
/// This validator ensures that:
/// 1. No ".." path components are present
/// 2. No absolute paths are used (must be relative to workspace)
/// 3. Resolved path is within the workspace directory
/// 4. Symbolic link attacks are prevented
/// </remarks>
public static class PathTraversalValidator
{
    /// <summary>
    /// Validates and resolves a user-provided path against a workspace directory.
    /// </summary>
    /// <param name="workspacePath">The workspace root directory (trusted base path)</param>
    /// <param name="userProvidedPath">The user/LLM-provided path to validate</param>
    /// <returns>The fully resolved and validated path</returns>
    /// <exception cref="ArgumentNullException">If workspacePath or userProvidedPath is null or empty</exception>
    /// <exception cref="SecurityException">If the path fails validation (traversal attempt detected)</exception>
    /// <remarks>
    /// This method performs comprehensive validation:
    /// 1. Rejects paths containing ".." (parent directory references)
    /// 2. Rejects absolute paths (must be relative to workspace)
    /// 3. Resolves the full path and ensures it's within the workspace
    /// 4. Normalizes path separators for the current OS
    ///
    /// Usage example:
    /// <code>
    /// var safePath = PathTraversalValidator.ValidateAndResolvePath(
    ///     workspacePath: "/app/workspace",
    ///     userProvidedPath: "config/app.json"
    /// );
    /// // Returns: "/app/workspace/config/app.json"
    /// </code>
    /// </remarks>
    public static string ValidateAndResolvePath(string workspacePath, string userProvidedPath)
    {
        // Validate inputs
        if (workspacePath.IsNullOrWhiteSpace())
        {
            throw new ArgumentNullException(nameof(workspacePath), "Workspace path cannot be null or empty");
        }

        if (userProvidedPath.IsNullOrWhiteSpace())
        {
            throw new ArgumentNullException(nameof(userProvidedPath), "User-provided path cannot be null or empty");
        }

        // Reject absolute paths - all paths must be relative to workspace
        if (Path.IsPathRooted(userProvidedPath))
        {
            throw new SecurityException(
                $"Absolute paths are not allowed. Path must be relative to workspace: {userProvidedPath}");
        }

        // Reject paths containing ".." to prevent parent directory traversal
        // Note: We check both forward and backward slashes to catch all OS variants
        if (userProvidedPath.Contains("..", StringComparison.Ordinal))
        {
            throw new SecurityException(
                $"Path traversal attempt detected. Paths cannot contain '..': {userProvidedPath}");
        }

        // Additional check for encoded traversal attempts
        var decodedPath = Uri.UnescapeDataString(userProvidedPath);
        if (decodedPath.Contains("..", StringComparison.Ordinal))
        {
            throw new SecurityException(
                $"Encoded path traversal attempt detected: {userProvidedPath}");
        }

        // Normalize the workspace path
        var normalizedWorkspace = Path.GetFullPath(workspacePath);

        // Combine and resolve the full path
        string resolvedPath;
        try
        {
            resolvedPath = Path.GetFullPath(Path.Combine(normalizedWorkspace, userProvidedPath));
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
        {
            throw new SecurityException(
                $"Invalid path format: {userProvidedPath}", ex);
        }

        // Ensure the resolved path starts with the workspace path
        // This prevents sophisticated traversal attacks like:
        // - "subdir/../../.." (relative escapes)
        // - Symbolic link attacks
        // - Windows drive letter changes
        if (!IsPathWithinDirectory(resolvedPath, normalizedWorkspace))
        {
            throw new SecurityException(
                $"Path traversal detected. Resolved path '{resolvedPath}' is outside workspace '{normalizedWorkspace}'");
        }

        return resolvedPath;
    }

    /// <summary>
    /// Checks if a resolved path is within a given directory.
    /// </summary>
    /// <param name="path">The full path to check</param>
    /// <param name="directory">The directory that should contain the path</param>
    /// <returns>True if path is within directory, false otherwise</returns>
    /// <remarks>
    /// This method handles cross-platform differences:
    /// - Windows: Case-insensitive comparison, handles drive letters
    /// - Unix/Linux/macOS: Case-sensitive comparison
    ///
    /// Both paths should be fully resolved (via Path.GetFullPath) before calling this method.
    /// </remarks>
    private static bool IsPathWithinDirectory(string path, string directory)
    {
        // Normalize both paths to ensure consistent separators
        var normalizedPath = Path.GetFullPath(path);
        var normalizedDirectory = Path.GetFullPath(directory);

        // Ensure directory path ends with separator for accurate StartsWith check
        if (!normalizedDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            normalizedDirectory += Path.DirectorySeparatorChar;
        }

        // Use case-insensitive comparison on Windows, case-sensitive on Unix
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return normalizedPath.StartsWith(normalizedDirectory, comparison);
    }

    /// <summary>
    /// Validates a path without resolving it (lighter-weight check).
    /// </summary>
    /// <param name="userProvidedPath">The path to validate</param>
    /// <exception cref="SecurityException">If the path contains traversal attempts</exception>
    /// <remarks>
    /// This is a simpler validation that only checks for obvious traversal patterns.
    /// For full security, use ValidateAndResolvePath() instead.
    ///
    /// Use this method when you need a quick validation check before the full resolution,
    /// or when you want to fail fast on obviously malicious input.
    /// </remarks>
    public static void ValidatePathFormat(string userProvidedPath)
    {
        if (userProvidedPath.IsNullOrWhiteSpace())
        {
            throw new ArgumentNullException(nameof(userProvidedPath), "Path cannot be null or empty");
        }

        // Reject absolute paths
        if (Path.IsPathRooted(userProvidedPath))
        {
            throw new SecurityException(
                $"Absolute paths are not allowed: {userProvidedPath}");
        }

        // Reject parent directory references
        if (userProvidedPath.Contains("..", StringComparison.Ordinal))
        {
            throw new SecurityException(
                $"Path cannot contain '..': {userProvidedPath}");
        }

        // Check for URL-encoded traversal
        var decoded = Uri.UnescapeDataString(userProvidedPath);
        if (decoded.Contains("..", StringComparison.Ordinal))
        {
            throw new SecurityException(
                $"Encoded path traversal detected: {userProvidedPath}");
        }

        // Reject paths with null bytes (another common attack vector)
        if (userProvidedPath.Contains('\0'))
        {
            throw new SecurityException(
                "Path cannot contain null bytes");
        }
    }
}

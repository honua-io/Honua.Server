// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Utilities;

/// <summary>
/// Provides centralized file I/O operations with consistent error handling, retry logic, and cleanup.
/// Eliminates duplicate file operation patterns across the codebase.
/// </summary>
public static class FileOperationHelper
{
    /// <summary>
    /// Safely reads all text from a file with optional retry logic for transient failures.
    /// </summary>
    /// <param name="path">The file path to read.</param>
    /// <param name="encoding">The text encoding to use. Defaults to UTF-8 if not specified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file contents as a string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="IOException">Thrown when a persistent I/O error occurs after retries.</exception>
    public static async Task<string> SafeReadAllTextAsync(
        string path,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        return await ExecuteWithRetryAsync(
            async () => await File.ReadAllTextAsync(path, encoding ?? Encoding.UTF8, cancellationToken).ConfigureAwait(false),
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
    }

    /// <summary>
    /// Safely writes all text to a file with automatic directory creation and optional retry logic.
    /// </summary>
    /// <param name="path">The file path to write.</param>
    /// <param name="content">The text content to write.</param>
    /// <param name="encoding">The text encoding to use. Defaults to UTF-8 if not specified.</param>
    /// <param name="createDirectory">Whether to automatically create the parent directory if it doesn't exist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when path or content is null or whitespace.</exception>
    /// <exception cref="IOException">Thrown when a persistent I/O error occurs after retries.</exception>
    public static async Task SafeWriteAllTextAsync(
        string path,
        string content,
        Encoding? encoding = null,
        bool createDirectory = true,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(path);
        Guard.NotNull(content);

        if (createDirectory)
        {
            var directory = Path.GetDirectoryName(path);
            if (directory.HasValue())
            {
                EnsureDirectoryExists(directory);
            }
        }

        await ExecuteWithRetryAsync(
            async () =>
            {
                await File.WriteAllTextAsync(path, content, encoding ?? Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                return 0; // Return type for ExecuteWithRetryAsync
            },
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
    }

    /// <summary>
    /// Safely writes all bytes to a file with automatic directory creation and optional retry logic.
    /// </summary>
    /// <param name="path">The file path to write.</param>
    /// <param name="bytes">The byte array to write.</param>
    /// <param name="createDirectory">Whether to automatically create the parent directory if it doesn't exist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when path or bytes is null.</exception>
    /// <exception cref="IOException">Thrown when a persistent I/O error occurs after retries.</exception>
    public static async Task SafeWriteAllBytesAsync(
        string path,
        byte[] bytes,
        bool createDirectory = true,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(path);
        Guard.NotNull(bytes);

        if (createDirectory)
        {
            var directory = Path.GetDirectoryName(path);
            if (directory.HasValue())
            {
                EnsureDirectoryExists(directory);
            }
        }

        await ExecuteWithRetryAsync(
            async () =>
            {
                await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
                return 0; // Return type for ExecuteWithRetryAsync
            },
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
    }

    /// <summary>
    /// Safely reads all bytes from a file with optional retry logic for transient failures.
    /// </summary>
    /// <param name="path">The file path to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file contents as a byte array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="IOException">Thrown when a persistent I/O error occurs after retries.</exception>
    public static async Task<byte[]> SafeReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        return await ExecuteWithRetryAsync(
            async () => await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false),
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
    }

    /// <summary>
    /// Safely deletes a file with optional retry logic for file locks and transient failures.
    /// </summary>
    /// <param name="path">The file path to delete.</param>
    /// <param name="ignoreNotFound">Whether to silently ignore if the file doesn't exist. Defaults to true.</param>
    /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when ignoreNotFound is false and the file doesn't exist.</exception>
    /// <exception cref="IOException">Thrown when a persistent I/O error occurs after retries.</exception>
    public static async Task SafeDeleteAsync(
        string path,
        bool ignoreNotFound = true)
    {
        Guard.NotNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            if (!ignoreNotFound)
            {
                throw new FileNotFoundException($"File not found: {path}", path);
            }
            return;
        }

        await ExecuteWithRetryAsync(
            async () =>
            {
                await Task.Run(() => File.Delete(path)).ConfigureAwait(false);
                return 0;
            },
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
    }

    /// <summary>
    /// Safely copies a file from source to destination with optional overwrite and retry logic.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="overwrite">Whether to overwrite the destination file if it exists.</param>
    /// <param name="createDirectory">Whether to automatically create the destination directory if it doesn't exist.</param>
    /// <exception cref="ArgumentNullException">Thrown when sourcePath or destinationPath is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist.</exception>
    /// <exception cref="IOException">Thrown when a persistent I/O error occurs after retries.</exception>
    public static async Task SafeCopyAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        bool createDirectory = true)
    {
        Guard.NotNullOrWhiteSpace(sourcePath);
        Guard.NotNullOrWhiteSpace(destinationPath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}", sourcePath);
        }

        if (createDirectory)
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (directory.HasValue())
            {
                EnsureDirectoryExists(directory);
            }
        }

        await ExecuteWithRetryAsync(
            async () =>
            {
                await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite)).ConfigureAwait(false);
                return 0;
            },
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
    }

    /// <summary>
    /// Safely moves a file from source to destination with cross-volume fallback support.
    /// If a move fails due to cross-volume limitations, automatically falls back to copy + delete.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="overwrite">Whether to overwrite the destination file if it exists.</param>
    /// <param name="createDirectory">Whether to automatically create the destination directory if it doesn't exist.</param>
    /// <exception cref="ArgumentNullException">Thrown when sourcePath or destinationPath is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the source file does not exist.</exception>
    /// <exception cref="IOException">Thrown when a persistent I/O error occurs after retries.</exception>
    public static async Task SafeMoveAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        bool createDirectory = true)
    {
        Guard.NotNullOrWhiteSpace(sourcePath);
        Guard.NotNullOrWhiteSpace(destinationPath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}", sourcePath);
        }

        if (createDirectory)
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (directory.HasValue())
            {
                EnsureDirectoryExists(directory);
            }
        }

        // Normalize paths for comparison
        var sourceFullPath = Path.GetFullPath(sourcePath);
        var destinationFullPath = Path.GetFullPath(destinationPath);

        // If source and destination are the same, nothing to do
        if (string.Equals(sourceFullPath, destinationFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await ExecuteWithRetryAsync(
            async () =>
            {
                await Task.Run(() =>
                {
                    // Delete destination if overwrite is requested
                    if (overwrite && File.Exists(destinationFullPath))
                    {
                        File.Delete(destinationFullPath);
                    }

                    try
                    {
                        File.Move(sourceFullPath, destinationFullPath, overwrite);
                    }
                    catch (IOException)
                    {
                        // Cross-volume move fallback: copy then delete
                        File.Copy(sourceFullPath, destinationFullPath, overwrite);
                        File.Delete(sourceFullPath);
                    }
                }).ConfigureAwait(false);
                return 0;
            },
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with automatic retry logic for transient failures.
    /// Retries on IOException with exponential backoff.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="maxRetries">Maximum number of retry attempts. Defaults to 3.</param>
    /// <param name="delay">Initial delay between retries. Defaults to 100ms with exponential backoff.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when operation is null.</exception>
    /// <exception cref="IOException">Thrown when all retry attempts are exhausted.</exception>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        Guard.NotNull(operation);
        Guard.Require(maxRetries >= 0, "Max retries must be non-negative", nameof(maxRetries));

        var retryDelay = delay ?? TimeSpan.FromMilliseconds(100);
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= maxRetries)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (IOException ex) when (attempt < maxRetries)
            {
                lastException = ex;
                attempt++;

                // Exponential backoff: delay * 2^(attempt-1)
                var currentDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                await Task.Delay(currentDelay).ConfigureAwait(false);
            }
        }

        // If we get here, all retries were exhausted
        throw new IOException(
            $"File operation failed after {maxRetries} retries.",
            lastException);
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// Thread-safe and handles race conditions where the directory is created between the check and creation.
    /// </summary>
    /// <param name="path">The directory path to ensure exists.</param>
    /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace.</exception>
    /// <exception cref="IOException">Thrown when directory creation fails.</exception>
    public static void EnsureDirectoryExists(string path)
    {
        Guard.NotNullOrWhiteSpace(path);

        if (Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(path);
        }
        catch (IOException) when (Directory.Exists(path))
        {
            // Race condition: directory was created between our check and CreateDirectory call
            // This is fine, ignore the exception
        }
    }

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace.</exception>
    public static bool FileExists(string path)
    {
        Guard.NotNullOrWhiteSpace(path);
        return File.Exists(path);
    }

    /// <summary>
    /// Checks if a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <returns>True if the directory exists, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace.</exception>
    public static bool DirectoryExists(string path)
    {
        Guard.NotNullOrWhiteSpace(path);
        return Directory.Exists(path);
    }

    /// <summary>
    /// Gets the size of a file in bytes.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file size in bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static long GetFileSize(string path)
    {
        Guard.NotNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        var fileInfo = new FileInfo(path);
        return fileInfo.Length;
    }

    /// <summary>
    /// Gets the last write time (UTC) of a file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The last write time in UTC.</returns>
    /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static DateTime GetLastWriteTimeUtc(string path)
    {
        Guard.NotNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        return File.GetLastWriteTimeUtc(path);
    }
}

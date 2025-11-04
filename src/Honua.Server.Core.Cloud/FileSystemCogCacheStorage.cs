// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Raster.Cache.Storage;

/// <summary>
/// Stores cached COG files on the local filesystem.
/// </summary>
public sealed class FileSystemCogCacheStorage : ICogCacheStorage
{
    private readonly string _rootDirectory;

    public FileSystemCogCacheStorage(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory cannot be null or empty", nameof(rootDirectory));
        }

        _rootDirectory = Path.GetFullPath(rootDirectory);
        FileOperationHelper.EnsureDirectoryExists(_rootDirectory);
    }

    public Task<CogStorageMetadata?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        var path = GetDestinationPath(cacheKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<CogStorageMetadata?>(null);
        }

        var info = new FileInfo(path);
        var metadata = new CogStorageMetadata(
            path,
            info.Exists ? info.Length : 0,
            info.Exists ? info.LastWriteTimeUtc : DateTime.UtcNow);

        return Task.FromResult<CogStorageMetadata?>(metadata);
    }

    public async Task<CogStorageMetadata> SaveAsync(string cacheKey, string localFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(localFilePath);

        var destinationPath = GetDestinationPath(cacheKey);

        await FileOperationHelper.SafeMoveAsync(localFilePath, destinationPath, overwrite: true, createDirectory: true).ConfigureAwait(false);

        var info = new FileInfo(destinationPath);
        info.Refresh();

        var metadata = new CogStorageMetadata(
            destinationPath,
            info.Exists ? info.Length : 0,
            info.Exists ? info.LastWriteTimeUtc : DateTime.UtcNow);

        return metadata;
    }

    public async Task DeleteAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        var path = GetDestinationPath(cacheKey);
        await FileOperationHelper.SafeDeleteAsync(path, ignoreNotFound: true).ConfigureAwait(false);
    }

    private string GetDestinationPath(string cacheKey)
    {
        return Path.Combine(_rootDirectory, $"{cacheKey}.tif");
    }
}

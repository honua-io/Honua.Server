// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Logging;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

public sealed class FileSystemRasterTileCacheProvider : IRasterTileCacheProvider
{
    private readonly string _rootPath;
    private readonly ILogger<FileSystemRasterTileCacheProvider> _logger;
    private readonly IDiskQuotaService? _diskQuotaService;
    private readonly IRasterTileCacheMetadataStore? _metadataStore;
    private readonly IDiskQuotaMetrics? _quotaMetrics;

    public FileSystemRasterTileCacheProvider(
        string rootPath,
        ILogger<FileSystemRasterTileCacheProvider> logger,
        IDiskQuotaService? diskQuotaService = null,
        IRasterTileCacheMetadataStore? metadataStore = null,
        IDiskQuotaMetrics? quotaMetrics = null)
    {
        if (rootPath.IsNullOrWhiteSpace())
        {
            throw new ArgumentNullException(nameof(rootPath));
        }

        _rootPath = Path.GetFullPath(rootPath);
        FileOperationHelper.EnsureDirectoryExists(_rootPath);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _diskQuotaService = diskQuotaService;
        _metadataStore = metadataStore;
        _quotaMetrics = quotaMetrics;
    }

    public async ValueTask<RasterTileCacheHit?> TryGetAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var length = stream.Length;
            if (length <= 0)
            {
                return null;
            }

            var buffer = new byte[length];
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            if (offset == 0)
            {
                return null;
            }

            if (offset != buffer.Length)
            {
                Array.Resize(ref buffer, offset);
            }

            // Record tile access in metadata store if available
            if (_metadataStore != null)
            {
                try
                {
                    await _metadataStore.RecordTileAccessAsync(key, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogOperationFailure(ex, "Record tile access", key.ToString());
                }
            }

            var createdUtc = File.GetLastWriteTimeUtc(path);
            return new RasterTileCacheHit(buffer, key.Format, createdUtc == DateTime.MinValue ? DateTimeOffset.UtcNow : new DateTimeOffset(createdUtc));
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read cached tile {Key} from {Path}.", key, path);
            return null;
        }
    }

    public async Task StoreAsync(RasterTileCacheKey key, RasterTileCacheEntry entry, CancellationToken cancellationToken = default)
    {
        var data = entry.Content.ToArray();
        var dataSize = data.Length;

        // Check disk quota before writing if service is available
        if (_diskQuotaService != null)
        {
            var hasSufficientSpace = await _diskQuotaService.HasSufficientSpaceAsync(_rootPath, dataSize, cancellationToken);

            if (!hasSufficientSpace)
            {
                _logger.LogWarning(
                    "Insufficient disk space to cache tile {Key}, size={Size} bytes. Attempting cleanup.",
                    key, dataSize);

                _quotaMetrics?.RecordPreWriteCheck(key.DatasetId, false, "insufficient_space");

                // Attempt to free up space if automatic cleanup is enabled
                if (_diskQuotaService.Options.EnableAutomaticCleanup)
                {
                    try
                    {
                        var targetFreeSpace = _diskQuotaService.Options.MinimumFreeSpaceBytes + dataSize;
                        var cleanupResult = await _diskQuotaService.FreeUpSpaceAsync(_rootPath, targetFreeSpace, cancellationToken);

                        _logger.LogInformation(
                            "Cleanup completed: freed {BytesFreed} bytes by removing {FilesRemoved} files in {Duration}ms",
                            cleanupResult.BytesFreed, cleanupResult.FilesRemoved, cleanupResult.Duration.TotalMilliseconds);

                        // Re-check after cleanup
                        hasSufficientSpace = await _diskQuotaService.HasSufficientSpaceAsync(_rootPath, dataSize, cancellationToken);

                        if (!hasSufficientSpace)
                        {
                            _logger.LogError(
                                "Still insufficient disk space after cleanup. Skipping cache write for tile {Key}.",
                                key);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogOperationFailure(ex, "Free up disk space", key.ToString());
                        return;
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Automatic cleanup disabled. Skipping cache write for tile {Key}.",
                        key);
                    return;
                }
            }
            else
            {
                _quotaMetrics?.RecordPreWriteCheck(key.DatasetId, true, "sufficient_space");
            }
        }

        var path = ResolvePath(key);
        var directory = Path.GetDirectoryName(path);
        if (!directory.IsNullOrEmpty())
        {
            FileOperationHelper.EnsureDirectoryExists(directory);
        }

        try
        {
            await File.WriteAllBytesAsync(path, data, cancellationToken).ConfigureAwait(false);
            File.SetLastWriteTimeUtc(path, entry.CreatedUtc.UtcDateTime);

            // Record tile creation in metadata store if available
            if (_metadataStore != null)
            {
                await _metadataStore.RecordTileCreationAsync(key, dataSize, cancellationToken);
            }

            _logger.LogDebug("Successfully cached tile {Key}, size={Size} bytes", key, dataSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write tile {Key} to cache", key);
            throw;
        }
    }

    public async Task RemoveAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        try
        {
            if (FileOperationHelper.FileExists(path))
            {
                await FileOperationHelper.SafeDeleteAsync(path, ignoreNotFound: true).ConfigureAwait(false);

                // Record tile removal in metadata store if available
                if (_metadataStore != null)
                {
                    await _metadataStore.RecordTileRemovalAsync(key, cancellationToken);
                }
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to remove cached tile {Key} from {Path}.", key, path);
        }
    }

    public Task PurgeDatasetAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(_rootPath, CacheKeyNormalizer.SanitizeForFilesystem(datasetId));
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to purge raster cache for dataset {DatasetId} at {Path}.", datasetId, root);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(RasterTileCacheKey key)
    {
        var relative = RasterTileCachePathHelper.GetRelativePath(key, Path.DirectorySeparatorChar);
        return Path.Combine(_rootPath, relative);
    }
}

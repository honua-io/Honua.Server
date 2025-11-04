// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Cli.AI.Services.Rollback;

/// <summary>
/// File system-based snapshot manager for configuration backups.
/// Creates compressed archives of configuration files before changes.
/// </summary>
public sealed class FileSystemSnapshotManager : ISnapshotManager
{
    private readonly string _snapshotDirectory;
    private readonly ILogger<FileSystemSnapshotManager> _logger;
    private readonly string _metadataFile;

    public FileSystemSnapshotManager(
        string snapshotDirectory,
        ILogger<FileSystemSnapshotManager> logger)
    {
        _snapshotDirectory = snapshotDirectory;
        _logger = logger;
        _metadataFile = Path.Combine(_snapshotDirectory, "snapshots.json");

        // Ensure snapshot directory exists
        FileOperationHelper.EnsureDirectoryExists(_snapshotDirectory);
    }

    public async Task<string> CreateSnapshotAsync(
        string workspacePath,
        string planId,
        CancellationToken cancellationToken = default)
    {
        var snapshotId = $"snapshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var snapshotPath = Path.Combine(_snapshotDirectory, $"{snapshotId}.zip");

        try
        {
            _logger.LogInformation("Creating snapshot {SnapshotId} of {WorkspacePath}",
                snapshotId, workspacePath);

            // Create compressed archive of workspace
            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(snapshotPath, ZipArchiveMode.Create);

                // Add all relevant configuration files
                var configFiles = GetConfigurationFiles(workspacePath);
                foreach (var file in configFiles)
                {
                    if (File.Exists(file))
                    {
                        var entryName = Path.GetRelativePath(workspacePath, file);
                        archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                        _logger.LogDebug("Added {File} to snapshot", entryName);
                    }
                }
            }, cancellationToken);

            // Get snapshot size
            var fileInfo = new FileInfo(snapshotPath);
            var sizeBytes = fileInfo.Length;

            // Create snapshot metadata
            var snapshot = new Snapshot
            {
                Id = snapshotId,
                PlanId = planId,
                WorkspacePath = workspacePath,
                CreatedAt = DateTime.UtcNow,
                SizeBytes = sizeBytes,
                Type = SnapshotType.Configuration,
                StoragePath = snapshotPath,
                Metadata = new Dictionary<string, string>
                {
                    ["WorkspaceName"] = Path.GetFileName(workspacePath),
                    ["CompressionRatio"] = "optimal"
                }
            };

            // Save metadata
            await SaveSnapshotMetadataAsync(snapshot, cancellationToken);

            _logger.LogInformation("Snapshot {SnapshotId} created successfully ({SizeKB} KB)",
                snapshotId, sizeBytes / 1024);

            return snapshotId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create snapshot {SnapshotId}", snapshotId);

            // Clean up partial snapshot
            if (FileOperationHelper.FileExists(snapshotPath))
            {
                try
                {
                    await FileOperationHelper.SafeDeleteAsync(snapshotPath, ignoreNotFound: true).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            throw;
        }
    }

    public async Task RestoreSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Restoring snapshot {SnapshotId}", snapshotId);

            var snapshot = await GetSnapshotAsync(snapshotId, cancellationToken);
            if (snapshot == null)
            {
                throw new InvalidOperationException($"Snapshot {snapshotId} not found");
            }

            if (!File.Exists(snapshot.StoragePath))
            {
                throw new InvalidOperationException($"Snapshot file not found: {snapshot.StoragePath}");
            }

            // Extract archive to workspace
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(snapshot.StoragePath);

                foreach (var entry in archive.Entries)
                {
                    var destinationPath = Path.Combine(snapshot.WorkspacePath, entry.FullName);

                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(destinationPath);
                    if (!directory.IsNullOrEmpty())
                    {
                        FileOperationHelper.EnsureDirectoryExists(directory);
                    }

                    // Extract file
                    entry.ExtractToFile(destinationPath, overwrite: true);
                    _logger.LogDebug("Restored {File}", entry.FullName);
                }
            }, cancellationToken);

            // Update snapshot metadata
            snapshot.IsRestored = true;
            snapshot.RestoredAt = DateTime.UtcNow;
            await SaveSnapshotMetadataAsync(snapshot, cancellationToken);

            _logger.LogInformation("Snapshot {SnapshotId} restored successfully", snapshotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore snapshot {SnapshotId}", snapshotId);
            throw;
        }
    }

    public async Task DeleteSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting snapshot {SnapshotId}", snapshotId);

            var snapshot = await GetSnapshotAsync(snapshotId, cancellationToken);
            if (snapshot == null)
            {
                _logger.LogWarning("Snapshot {SnapshotId} not found", snapshotId);
                return;
            }

            // Delete snapshot file
            if (FileOperationHelper.FileExists(snapshot.StoragePath))
            {
                await FileOperationHelper.SafeDeleteAsync(snapshot.StoragePath, ignoreNotFound: true).ConfigureAwait(false);
            }

            // Remove from metadata
            var snapshots = await LoadSnapshotMetadataAsync(cancellationToken);
            snapshots.RemoveAll(s => s.Id == snapshotId);
            await SaveAllSnapshotMetadataAsync(snapshots, cancellationToken);

            _logger.LogInformation("Snapshot {SnapshotId} deleted", snapshotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete snapshot {SnapshotId}", snapshotId);
            throw;
        }
    }

    public async Task<IReadOnlyList<Snapshot>> ListSnapshotsAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await LoadSnapshotMetadataAsync(cancellationToken);
        return snapshots.AsReadOnly();
    }

    public async Task<Snapshot?> GetSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await LoadSnapshotMetadataAsync(cancellationToken);
        return snapshots.FirstOrDefault(s => s.Id == snapshotId);
    }

    private IEnumerable<string> GetConfigurationFiles(string workspacePath)
    {
        // Get all configuration files that should be snapshotted
        var patterns = new[]
        {
            "*.json",
            "*.yaml",
            "*.yml",
            "*.config",
            "*.conf"
        };

        var files = new List<string>();

        if (Directory.Exists(workspacePath))
        {
            foreach (var pattern in patterns)
            {
                files.AddRange(Directory.GetFiles(workspacePath, pattern, SearchOption.AllDirectories));
            }
        }
        else if (File.Exists(workspacePath))
        {
            // If workspace path is a file, just snapshot that file
            files.Add(workspacePath);
        }

        return files;
    }

    private async Task<List<Snapshot>> LoadSnapshotMetadataAsync(CancellationToken cancellationToken)
    {
        if (!FileOperationHelper.FileExists(_metadataFile))
        {
            return new List<Snapshot>();
        }

        try
        {
            var json = await FileOperationHelper.SafeReadAllTextAsync(_metadataFile, cancellationToken: cancellationToken).ConfigureAwait(false);
            return JsonHelper.TryDeserialize<List<Snapshot>>(json, out var snapshots, out _)
                ? snapshots
                : new List<Snapshot>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load snapshot metadata");
            return new List<Snapshot>();
        }
    }

    private async Task SaveSnapshotMetadataAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        var snapshots = await LoadSnapshotMetadataAsync(cancellationToken);

        // Remove existing snapshot with same ID if present
        snapshots.RemoveAll(s => s.Id == snapshot.Id);

        // Add new/updated snapshot
        snapshots.Add(snapshot);

        await SaveAllSnapshotMetadataAsync(snapshots, cancellationToken);
    }

    private async Task SaveAllSnapshotMetadataAsync(List<Snapshot> snapshots, CancellationToken cancellationToken)
    {
        var json = JsonHelper.SerializeIndented(snapshots);

        await FileOperationHelper.SafeWriteAllTextAsync(_metadataFile, json, createDirectory: true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}

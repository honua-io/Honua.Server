// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Cli.Services.Metadata;

public sealed class FileMetadataSnapshotService : IMetadataSnapshotService
{
    private readonly IHonuaCliEnvironment _environment;
    private readonly ISystemClock _clock;
    private readonly IMetadataSchemaValidator _schemaValidator;

    public FileMetadataSnapshotService(IHonuaCliEnvironment environment, ISystemClock clock, IMetadataSchemaValidator schemaValidator)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
    }

    public async Task<MetadataSnapshotResult> CreateSnapshotAsync(MetadataSnapshotRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        _environment.EnsureInitialized();

        var workspace = EnsureWorkspace(request.WorkspacePath);
        var snapshotRoot = ResolveSnapshotsRoot(request.SnapshotsRootOverride);
        Directory.CreateDirectory(snapshotRoot);

        var label = request.Label.HasValue()
            ? SanitizeLabel(request.Label!)
            : GenerateLabel();

        if (label.IsNullOrWhiteSpace())
        {
            label = GenerateLabel();
        }

        var targetPath = EnsureUniquePath(Path.Combine(snapshotRoot, label));

        // Validate path to prevent traversal
        ValidatePathWithinRoot(targetPath, snapshotRoot);

        await CopyDirectory(workspace.FullName, targetPath, cancellationToken).ConfigureAwait(false);

        var manifest = new SnapshotManifest
        {
            Label = Path.GetFileName(targetPath),
            CreatedAtUtc = _clock.UtcNow,
            Notes = request.Notes
        };

        var manifestPath = Path.Combine(targetPath, "manifest.json");
        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await FileOperationHelper.SafeWriteAllTextAsync(manifestPath, manifestJson, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new MetadataSnapshotResult(manifest.Label, targetPath, manifest.CreatedAtUtc, manifest.Notes);
    }

    public Task<IReadOnlyList<MetadataSnapshotDescriptor>> ListSnapshotsAsync(string? snapshotsRootOverride, CancellationToken cancellationToken)
    {
        _environment.EnsureInitialized();
        var snapshotRoot = ResolveSnapshotsRoot(snapshotsRootOverride);

        if (!Directory.Exists(snapshotRoot))
        {
            return Task.FromResult<IReadOnlyList<MetadataSnapshotDescriptor>>(Array.Empty<MetadataSnapshotDescriptor>());
        }

        var descriptors = new List<MetadataSnapshotDescriptor>();

        foreach (var directory in Directory.EnumerateDirectories(snapshotRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifest = LoadManifest(directory);
            var size = TryCalculateSize(directory, cancellationToken);
            descriptors.Add(new MetadataSnapshotDescriptor(manifest.Label, manifest.CreatedAtUtc, size, manifest.Notes));
        }

        descriptors.Sort((left, right) => right.CreatedAtUtc.CompareTo(left.CreatedAtUtc));
        return Task.FromResult<IReadOnlyList<MetadataSnapshotDescriptor>>(descriptors);
    }

    public async Task RestoreSnapshotAsync(MetadataRestoreRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        _environment.EnsureInitialized();

        var workspace = EnsureWorkspace(request.WorkspacePath);
        var snapshotRoot = ResolveSnapshotsRoot(request.SnapshotsRootOverride);
        var source = Path.Combine(snapshotRoot, SanitizeLabel(request.Label));

        // Validate path to prevent traversal
        ValidatePathWithinRoot(source, snapshotRoot);

        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Snapshot '{request.Label}' was not found under '{snapshotRoot}'.");
        }

        await CopyDirectory(source, workspace.FullName, cancellationToken, overwrite: request.Overwrite).ConfigureAwait(false);
    }

    public async Task<MetadataValidationResult> ValidateAsync(MetadataValidationRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        if (!Directory.Exists(request.WorkspacePath))
        {
            errors.Add($"Workspace path '{request.WorkspacePath}' does not exist.");
        }
        else
        {
            var metadataFiles = Directory.EnumerateFiles(request.WorkspacePath, "metadata.*", SearchOption.TopDirectoryOnly).ToList();
            if (metadataFiles.Count == 0)
            {
                warnings.Add("No metadata.* file was found in the workspace root.");
            }

            var fileCount = Directory.EnumerateFiles(request.WorkspacePath, "*", SearchOption.AllDirectories).Take(2).Count();
            if (fileCount == 0)
            {
                errors.Add("The workspace is empty; nothing to validate.");
            }

            foreach (var metadataFile in metadataFiles)
            {
                try
                {
                    var payload = await FileOperationHelper.SafeReadAllTextAsync(metadataFile, cancellationToken: cancellationToken).ConfigureAwait(false);
                    var validation = _schemaValidator.Validate(payload);
                    if (!validation.IsValid)
                    {
                        foreach (var error in validation.Errors)
                        {
                            errors.Add($"{Path.GetFileName(metadataFile)}: {error}");
                        }
                    }

                    foreach (var warning in validation.Warnings)
                    {
                        warnings.Add($"{Path.GetFileName(metadataFile)}: {warning}");
                    }
                }
                catch (IOException ex)
                {
                    errors.Add($"Failed to read '{metadataFile}': {ex.Message}");
                }
            }

            if (errors.Count == 0 && metadataFiles.Count > 0)
            {
                foreach (var metadataFile in metadataFiles)
                {
                    try
                    {
                        // TODO: Update to use HclMetadataProvider with Configuration V2
                        // JsonMetadataProvider has been removed - this service needs migration
                        throw new NotSupportedException(
                            "FileMetadataSnapshotService requires migration to Configuration V2. " +
                            "JsonMetadataProvider has been removed. Use HclMetadataProvider instead.");

                        foreach (var layer in snapshot.Layers)
                        {
                            var storage = layer.Storage;
                            if (storage is null)
                            {
                                continue;
                            }

                            var hasSrid = storage.Srid.HasValue && storage.Srid.Value > 0;
                            var hasCrs = storage.Crs.HasValue();

                            if (!hasSrid && !hasCrs)
                            {
                                warnings.Add($"Layer '{layer.Id}' does not specify storage.srid or storage.crs. CRS negotiation may be inaccurate.");
                                continue;
                            }

                            if (hasSrid && hasCrs)
                            {
                                var normalizedFromSrid = CrsHelper.NormalizeIdentifier($"EPSG:{storage.Srid!.Value}");
                                var normalizedFromCrs = CrsHelper.NormalizeIdentifier(storage.Crs!);

                                if (!string.Equals(normalizedFromSrid, normalizedFromCrs, StringComparison.OrdinalIgnoreCase))
                                {
                                    warnings.Add($"Layer '{layer.Id}' storage.srid ({storage.Srid}) does not match storage.crs ('{storage.Crs}'). The numeric SRID will be used.");
                                }
                            }
                        }
                    }
                    catch (Exception ex) when (ex is InvalidDataException or JsonException)
                    {
                        warnings.Add($"{Path.GetFileName(metadataFile)}: Unable to inspect storage CRS details ({ex.Message}).");
                    }
                }
            }
        }

        var result = new MetadataValidationResult(errors.Count == 0, errors, warnings);
        return result;
    }

    private static DirectoryInfo EnsureWorkspace(string path)
    {
        var workspace = new DirectoryInfo(path);
        if (!workspace.Exists)
        {
            throw new DirectoryNotFoundException($"Workspace '{workspace.FullName}' could not be found.");
        }

        return workspace;
    }

    private string ResolveSnapshotsRoot(string? overridePath)
    {
        if (overridePath.IsNullOrWhiteSpace())
        {
            return _environment.SnapshotsRoot;
        }

        var root = Path.GetFullPath(overridePath);
        Directory.CreateDirectory(root);
        return root;
    }

    private string EnsureUniquePath(string targetPath)
    {
        var candidate = targetPath;
        var counter = 1;
        while (Directory.Exists(candidate))
        {
            candidate = $"{targetPath}-{counter}";
            counter++;
        }

        return candidate;
    }

    private static string SanitizeLabel(string label)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(label.Where(ch => !invalidChars.Contains(ch)).ToArray());

        // Remove path separators and traversal sequences to prevent path traversal attacks
        cleaned = cleaned.Replace("..", "").Replace("/", "").Replace("\\", "");

        if (cleaned.IsNullOrWhiteSpace())
        {
            return $"snapshot-{Guid.NewGuid():N}";
        }

        return cleaned.Trim();
    }

    private static void ValidatePathWithinRoot(string path, string rootPath)
    {
        var fullPath = Path.GetFullPath(path);
        var normalizedRoot = Path.GetFullPath(rootPath);

        // Use OS-appropriate comparison
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.StartsWith(normalizedRoot, comparison))
        {
            throw new SecurityException($"Path traversal detected: {path}");
        }
    }

    private string GenerateLabel() => $"snapshot-{_clock.UtcNow:yyyyMMddHHmmss}";

    private static async Task CopyDirectory(string sourcePath, string destinationPath, CancellationToken cancellationToken, bool overwrite = false)
    {
        FileOperationHelper.EnsureDirectoryExists(destinationPath);

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourcePath, file);
            var targetFile = Path.Combine(destinationPath, relativePath);
            await FileOperationHelper.SafeCopyAsync(file, targetFile, overwrite, createDirectory: true).ConfigureAwait(false);
        }
    }

    private static long? TryCalculateSize(string directory, CancellationToken cancellationToken)
    {
        try
        {
            long size = 0;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                size += new FileInfo(file).Length;
            }

            return size;
        }
        catch
        {
            return null;
        }
    }

    private static SnapshotManifest LoadManifest(string directory)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (FileOperationHelper.FileExists(manifestPath))
        {
            try
            {
                var content = Task.Run(() => FileOperationHelper.SafeReadAllTextAsync(manifestPath)).GetAwaiter().GetResult();
                var manifest = JsonSerializer.Deserialize<SnapshotManifest>(content);
                if (manifest is not null)
                {
                    return manifest with { Label = Path.GetFileName(directory) };
                }
            }
            catch (JsonException)
            {
                // Fall through to return default manifest
            }
        }

        return new SnapshotManifest
        {
            Label = Path.GetFileName(directory),
            CreatedAtUtc = Directory.GetCreationTimeUtc(directory)
        };
    }

    private record SnapshotManifest
    {
        public string Label { get; init; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
        public string? Notes { get; init; }
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Primitives;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata.Snapshots;

public sealed class FileMetadataSnapshotStore : IMetadataSnapshotStore, IDisposable
{
    private const string MetadataFileName = "metadata.json";
    private const string ManifestFileName = "manifest.json";

    // Use centralized JsonHelper for consistent serialization
    private static readonly JsonSerializerOptions SerializerOptions = JsonHelper.CreateOptions(
        writeIndented: true,
        camelCase: true,
        caseInsensitive: true,
        maxDepth: 64
    );

    private readonly IHonuaConfigurationService _configuration;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _pathSync = new();
    private IDisposable? _changeRegistration;
    private string _metadataPath = string.Empty;
    private string _snapshotsRoot = string.Empty;

    public FileMetadataSnapshotStore(IHonuaConfigurationService configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        UpdatePaths(configuration.Current);
        _changeRegistration = ChangeToken.OnChange(
            () => _configuration.GetChangeToken(),
            () => UpdatePaths(_configuration.Current));
    }

    public async Task<MetadataSnapshotDescriptor> CreateAsync(MetadataSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (metadataPath, snapshotsRoot) = GetPaths();
        if (!File.Exists(metadataPath))
        {
            throw new FileNotFoundException("Metadata file not found.", metadataPath);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            FilePermissionHelper.EnsureDirectorySecure(snapshotsRoot);

            var label = GenerateLabel(request.Label);
            var folderName = EnsureUniqueFolder(snapshotsRoot, label);
            var targetDirectory = Path.Combine(snapshotsRoot, folderName);
            FilePermissionHelper.EnsureDirectorySecure(targetDirectory);

            var destinationMetadata = Path.Combine(targetDirectory, MetadataFileName);
            File.Copy(metadataPath, destinationMetadata, overwrite: true);
            FilePermissionHelper.ApplyFilePermissions(destinationMetadata);

            long size = new FileInfo(destinationMetadata).Length;
            var checksum = ComputeSha256(destinationMetadata);

            var manifest = new SnapshotManifest
            {
                Label = folderName,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Notes = request.Notes,
                SizeBytes = size,
                Checksum = checksum
            };

            await File.WriteAllTextAsync(
                Path.Combine(targetDirectory, ManifestFileName),
                JsonSerializer.Serialize(manifest, SerializerOptions),
                cancellationToken).ConfigureAwait(false);
            FilePermissionHelper.ApplyFilePermissions(Path.Combine(targetDirectory, ManifestFileName));

            return new MetadataSnapshotDescriptor(folderName, manifest.CreatedAtUtc, size, request.Notes, checksum);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<MetadataSnapshotDescriptor>> ListAsync(CancellationToken cancellationToken = default)
    {
        var (_, snapshotsRoot) = GetPaths();
        if (!Directory.Exists(snapshotsRoot))
        {
            return Array.Empty<MetadataSnapshotDescriptor>();
        }

        var descriptors = new List<MetadataSnapshotDescriptor>();
        foreach (var directory in Directory.EnumerateDirectories(snapshotsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = await ReadManifestAsync(directory, cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                continue;
            }

            descriptors.Add(new MetadataSnapshotDescriptor(
                manifest.Label,
                manifest.CreatedAtUtc,
                manifest.SizeBytes,
                manifest.Notes,
                manifest.Checksum));
        }

        descriptors.Sort((left, right) => right.CreatedAtUtc.CompareTo(left.CreatedAtUtc));
        return descriptors;
    }

    public async Task<MetadataSnapshotDetails?> GetAsync(string label, CancellationToken cancellationToken = default)
    {
        if (label.IsNullOrWhiteSpace())
        {
            return null;
        }

        var (_, snapshotsRoot) = GetPaths();
        var sanitized = SanitizeLabel(label);
        var path = Path.Combine(snapshotsRoot, sanitized);
        if (!Directory.Exists(path))
        {
            return null;
        }

        var manifest = await ReadManifestAsync(path, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return null;
        }

        var metadataPath = Path.Combine(path, MetadataFileName);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var metadata = await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false);
        return new MetadataSnapshotDetails(
            new MetadataSnapshotDescriptor(manifest.Label, manifest.CreatedAtUtc, manifest.SizeBytes, manifest.Notes, manifest.Checksum),
            metadata);
    }

    public Task RestoreAsync(string label, CancellationToken cancellationToken = default)
    {
        if (label.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Snapshot label must be provided.", nameof(label));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var (metadataPath, snapshotsRoot) = GetPaths();
        var sanitized = SanitizeLabel(label);
        var snapshotDirectory = Path.Combine(snapshotsRoot, sanitized);
        if (!Directory.Exists(snapshotDirectory))
        {
            throw new DirectoryNotFoundException($"Snapshot '{label}' was not found.");
        }

        var snapshotMetadataPath = Path.Combine(snapshotDirectory, MetadataFileName);
        if (!File.Exists(snapshotMetadataPath))
        {
            throw new FileNotFoundException("Snapshot metadata file is missing.", snapshotMetadataPath);
        }

        var metadataDirectory = Path.GetDirectoryName(metadataPath);
        if (metadataDirectory.IsNullOrWhiteSpace())
        {
            metadataDirectory = Directory.GetCurrentDirectory();
        }

        FilePermissionHelper.EnsureDirectorySecure(metadataDirectory!);
        File.Copy(snapshotMetadataPath, metadataPath, overwrite: true);
        FilePermissionHelper.ApplyFilePermissions(metadataPath);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _changeRegistration?.Dispose();
        _gate.Dispose();
    }

    private void UpdatePaths(HonuaConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.Metadata);

        lock (_pathSync)
        {
            var metadataPath = Path.GetFullPath(configuration.Metadata.Path);
            var metadataDirectory = Path.GetDirectoryName(metadataPath);
            if (metadataDirectory.IsNullOrWhiteSpace())
            {
                metadataDirectory = Directory.GetCurrentDirectory();
            }

            var metadataStem = Path.GetFileNameWithoutExtension(metadataPath);
            if (metadataStem.IsNullOrWhiteSpace())
            {
                metadataStem = "honua-metadata";
            }

            _metadataPath = metadataPath;
            _snapshotsRoot = Path.Combine(metadataDirectory, $"{metadataStem}-snapshots");
        }
    }

    private (string MetadataPath, string SnapshotsRoot) GetPaths()
    {
        lock (_pathSync)
        {
            return (_metadataPath, _snapshotsRoot);
        }
    }

    private static async Task<SnapshotManifest?> ReadManifestAsync(string directory, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(manifestPath);
        return await JsonSerializer.DeserializeAsync<SnapshotManifest>(stream, SerializerOptions, cancellationToken);
    }

    private static string GenerateLabel(string? requested)
    {
        if (requested.HasValue())
        {
            var sanitized = SanitizeLabel(requested);
            if (sanitized.HasValue())
            {
                return sanitized;
            }
        }

        return $"snapshot-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private static string SanitizeLabel(string label)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(label.Length);
        foreach (var ch in label.Trim())
        {
            if (!invalid.Contains(ch) && !char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
            else if (char.IsWhiteSpace(ch))
            {
                builder.Append('-');
            }
        }

        return builder.Length == 0 ? $"snapshot-{Guid.NewGuid():N}" : builder.ToString();
    }

    private static string EnsureUniqueFolder(string root, string label)
    {
        var candidate = label;
        var counter = 1;
        while (Directory.Exists(Path.Combine(root, candidate)))
        {
            candidate = $"{label}-{counter}";
            counter++;
        }

        return candidate;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private sealed class SnapshotManifest
    {
        public string Label { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public string? Notes { get; set; }
        public long? SizeBytes { get; set; }
        public string? Checksum { get; set; }
    }
}

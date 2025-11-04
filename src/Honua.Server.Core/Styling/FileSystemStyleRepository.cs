// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Styling;

/// <summary>
/// File system-based implementation of style repository with versioning
/// </summary>
public sealed class FileSystemStyleRepository : IStyleRepository
{
    private readonly string _stylesDirectory;
    private readonly string _versionsDirectory;
    private readonly ILogger<FileSystemStyleRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemStyleRepository(
        string stylesDirectory,
        ILogger<FileSystemStyleRepository> logger)
    {
        Guard.NotNullOrWhiteSpace(stylesDirectory);
        _stylesDirectory = stylesDirectory;
        _versionsDirectory = Path.Combine(stylesDirectory, "_versions");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = JsonSerializerOptionsRegistry.WebIndented;

        Directory.CreateDirectory(_stylesDirectory);
        Directory.CreateDirectory(_versionsDirectory);
    }

    public async Task<StyleDefinition?> GetAsync(string styleId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(styleId);

        var filePath = GetStyleFilePath(styleId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<StyleDefinition>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read style {StyleId} from {FilePath}", styleId, filePath);
            throw;
        }
    }

    public async Task<IReadOnlyList<StyleDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var files = Directory.GetFiles(_stylesDirectory, "*.json", SearchOption.TopDirectoryOnly);
        var styles = new List<StyleDefinition>();

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var style = JsonSerializer.Deserialize<StyleDefinition>(json, _jsonOptions);
                if (style is not null)
                {
                    styles.Add(style);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read style from {FilePath}", file);
            }
        }

        return styles;
    }

    public async Task<StyleDefinition> CreateAsync(StyleDefinition style, string? createdBy = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(style);
        Guard.NotNullOrWhiteSpace(style.Id);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var filePath = GetStyleFilePath(style.Id);
            if (File.Exists(filePath))
            {
                throw new InvalidOperationException($"Style '{style.Id}' already exists. Use UpdateAsync to modify it.");
            }

            // Save current version
            var json = JsonSerializer.Serialize(style, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);

            // Save to version history
            await SaveVersionAsync(style, 1, createdBy, "Initial creation", cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created style {StyleId} by {CreatedBy}", style.Id, createdBy ?? "system");
            return style;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<StyleDefinition> UpdateAsync(string styleId, StyleDefinition style, string? updatedBy = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(styleId);
        Guard.NotNull(style);

        // Ensure the style ID matches
        if (!string.Equals(styleId, style.Id, StringComparison.OrdinalIgnoreCase))
        {
            style = style with { Id = styleId };
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var filePath = GetStyleFilePath(styleId);
            var exists = File.Exists(filePath);

            // Get current version number
            var versions = await GetVersionHistoryAsync(styleId, cancellationToken).ConfigureAwait(false);
            var nextVersion = versions.Count > 0 ? versions.Max(v => v.Version) + 1 : 1;

            // Save current version
            var json = JsonSerializer.Serialize(style, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);

            // Save to version history
            var changeDescription = exists ? "Updated" : "Created via update";
            await SaveVersionAsync(style, nextVersion, updatedBy, changeDescription, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Updated style {StyleId} to version {Version} by {UpdatedBy}", styleId, nextVersion, updatedBy ?? "system");
            return style;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string styleId, string? deletedBy = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(styleId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var filePath = GetStyleFilePath(styleId);
            if (!File.Exists(filePath))
            {
                return false;
            }

            // Read current style before deletion
            var style = await GetAsync(styleId, cancellationToken).ConfigureAwait(false);
            if (style is not null)
            {
                // Save deletion marker to version history
                var versions = await GetVersionHistoryAsync(styleId, cancellationToken).ConfigureAwait(false);
                var nextVersion = versions.Count > 0 ? versions.Max(v => v.Version) + 1 : 1;
                await SaveVersionAsync(style, nextVersion, deletedBy, "Deleted", cancellationToken).ConfigureAwait(false);
            }

            // Delete current version file
            File.Delete(filePath);

            _logger.LogInformation("Deleted style {StyleId} by {DeletedBy}", styleId, deletedBy ?? "system");
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<StyleVersion>> GetVersionHistoryAsync(string styleId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(styleId);

        var versionDir = GetVersionDirectory(styleId);
        if (!Directory.Exists(versionDir))
        {
            return Array.Empty<StyleVersion>();
        }

        var files = Directory.GetFiles(versionDir, "*.json", SearchOption.TopDirectoryOnly);
        var versions = new List<StyleVersion>();

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var version = JsonSerializer.Deserialize<StyleVersion>(json, _jsonOptions);
                if (version is not null)
                {
                    versions.Add(version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read version from {FilePath}", file);
            }
        }

        return versions.OrderBy(v => v.Version).ToList();
    }

    public async Task<StyleDefinition?> GetVersionAsync(string styleId, int version, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(styleId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        var versionFile = GetVersionFilePath(styleId, version);
        if (!File.Exists(versionFile))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(versionFile, cancellationToken).ConfigureAwait(false);
            var styleVersion = JsonSerializer.Deserialize<StyleVersion>(json, _jsonOptions);
            return styleVersion?.Definition;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read style {StyleId} version {Version} from {FilePath}", styleId, version, versionFile);
            throw;
        }
    }

    public Task<bool> ExistsAsync(string styleId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(styleId);
        var filePath = GetStyleFilePath(styleId);
        return Task.FromResult(File.Exists(filePath));
    }

    private async Task SaveVersionAsync(StyleDefinition style, int version, string? author, string? changeDescription, CancellationToken cancellationToken)
    {
        var versionDir = GetVersionDirectory(style.Id);
        Directory.CreateDirectory(versionDir);

        var styleVersion = new StyleVersion
        {
            StyleId = style.Id,
            Version = version,
            Definition = style,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = author,
            ChangeDescription = changeDescription
        };

        var versionFile = GetVersionFilePath(style.Id, version);
        var json = JsonSerializer.Serialize(styleVersion, _jsonOptions);
        await File.WriteAllTextAsync(versionFile, json, cancellationToken).ConfigureAwait(false);
    }

    private string GetStyleFilePath(string styleId)
    {
        var sanitized = SanitizeFileName(styleId);
        return Path.Combine(_stylesDirectory, $"{sanitized}.json");
    }

    private string GetVersionDirectory(string styleId)
    {
        var sanitized = SanitizeFileName(styleId);
        return Path.Combine(_versionsDirectory, sanitized);
    }

    private string GetVersionFilePath(string styleId, int version)
    {
        var versionDir = GetVersionDirectory(styleId);
        return Path.Combine(versionDir, $"v{version:D4}.json");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(fileName.Select(c => invalid.Contains(c) ? '_' : c));
    }
}

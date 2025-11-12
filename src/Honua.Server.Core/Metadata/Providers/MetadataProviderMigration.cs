// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata.Providers;

/// <summary>
/// Utility for migrating metadata between different provider implementations.
/// </summary>
public sealed class MetadataProviderMigration
{
    private readonly ILogger<MetadataProviderMigration> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public MetadataProviderMigration(ILogger<MetadataProviderMigration> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Migrates metadata from one provider to another.
    /// </summary>
    /// <param name="source">Source metadata provider to read from</param>
    /// <param name="destination">Destination provider to write to (must support IMutableMetadataProvider)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <example>
    /// // Migrate from File to Redis
    /// var fileProvider = new JsonMetadataProvider("./metadata.json");
    /// var redisProvider = new RedisMetadataProvider(redis, options, logger);
    /// await migration.MigrateAsync(fileProvider, redisProvider);
    /// </example>
    public async Task MigrateAsync(
        IMetadataProvider source,
        IMetadataProvider destination,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (destination is not IMutableMetadataProvider mutableDestination)
        {
            throw new ArgumentException(
                $"Destination provider must implement IMutableMetadataProvider. " +
                $"Provider type: {destination.GetType().Name}",
                nameof(destination));
        }

        _logger.LogInformation(
            "Starting metadata migration: {Source} -> {Destination}",
            source.GetType().Name,
            destination.GetType().Name);

        try
        {
            // 1. Load from source
            _logger.LogInformation("Loading metadata from source provider...");
            var snapshot = await source.LoadAsync(cancellationToken);

            _logger.LogInformation(
                "Loaded metadata snapshot: {Services} services, {Layers} layers, {DataSources} datasources",
                snapshot.Services.Count,
                snapshot.Layers.Count,
                snapshot.DataSources.Count);

            // 2. Save to destination
            _logger.LogInformation("Saving metadata to destination provider...");
            await mutableDestination.SaveAsync(snapshot, cancellationToken);

            _logger.LogInformation("✓ Metadata migration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ Metadata migration failed");
            throw;
        }
    }

    /// <summary>
    /// Exports metadata from a provider to a JSON file.
    /// Useful for backups or migrating to file-based provider.
    /// </summary>
    public async Task ExportToFileAsync(
        IMetadataProvider source,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (outputPath.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Output path must be specified", nameof(outputPath));
        }

        _logger.LogInformation(
            "Exporting metadata from {Provider} to file: {Path}",
            source.GetType().Name,
            outputPath);

        try
        {
            var snapshot = await source.LoadAsync(cancellationToken);

            // Convert to JSON with proper formatting
            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!directory.IsNullOrEmpty())
            {
                Directory.CreateDirectory(directory);
            }

            // Write to file
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);

            _logger.LogInformation(
                "✓ Metadata exported successfully ({Size} bytes)",
                json.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ Metadata export failed");
            throw;
        }
    }

    /// <summary>
    /// Imports metadata from a JSON file into a provider.
    /// </summary>
    public async Task ImportFromFileAsync(
        string inputPath,
        IMetadataProvider destination,
        CancellationToken cancellationToken = default)
    {
        if (inputPath.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Input path must be specified", nameof(inputPath));
        }

        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (destination is not IMutableMetadataProvider mutableDestination)
        {
            throw new ArgumentException(
                "Destination provider must implement IMutableMetadataProvider",
                nameof(destination));
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Metadata file not found: {inputPath}");
        }

        _logger.LogInformation(
            "Importing metadata from file {Path} to {Provider}",
            inputPath,
            destination.GetType().Name);

        try
        {
            // Load from file - note: JsonMetadataProvider has been removed
            // This method is now deprecated and should use HclMetadataProvider instead
            throw new NotSupportedException("JsonMetadataProvider has been removed. Use HclMetadataProvider with Configuration V2 instead.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ Metadata import failed");
            throw;
        }
    }

    /// <summary>
    /// Creates a backup of current metadata to a JSON file with timestamp.
    /// </summary>
    public async Task<string> BackupAsync(
        IMetadataProvider source,
        string backupDirectory = "./backups",
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var filename = $"metadata-backup-{timestamp}.json";
        var backupPath = Path.Combine(backupDirectory, filename);

        await ExportToFileAsync(source, backupPath, cancellationToken);

        return backupPath;
    }
}

/// <summary>
/// Command-line tool for metadata provider migrations.
/// Can be invoked via: dotnet run --project Honua.Cli -- metadata migrate ...
/// </summary>
public static class MetadataProviderMigrationCli
{
    /// <summary>
    /// Example CLI command to migrate from file to Redis.
    /// </summary>
    /// <example>
    /// dotnet run --project Honua.Cli -- metadata migrate \
    ///   --from file --file-path ./metadata.json \
    ///   --to redis --redis localhost:6379
    /// </example>
    public static async Task MigrateFromCliAsync(string[] args)
    {
        // This is a placeholder for a future CLI implementation
        // For now, migrations should be done via code or a custom script
        await Task.CompletedTask;
        throw new NotImplementedException(
            "CLI migration tool not yet implemented. " +
            "Use MetadataProviderMigration class in code for now.");
    }
}

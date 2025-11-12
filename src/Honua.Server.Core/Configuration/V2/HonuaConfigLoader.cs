// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Configuration.V2;

/// <summary>
/// Loads and parses Honua configuration files (.honua, .hcl, .json).
/// </summary>
public static class HonuaConfigLoader
{
    /// <summary>
    /// Load configuration from a file path.
    /// Supports .honua, .hcl, and .json formats.
    /// </summary>
    /// <param name="filePath">Path to the configuration file.</param>
    /// <returns>Parsed configuration object.</returns>
    public static HonuaConfig Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Configuration file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".json" => LoadFromJson(filePath),
            ".hcl" => LoadFromHcl(filePath),
            ".honua" => LoadFromHcl(filePath),
            _ => throw new NotSupportedException($"Configuration file format not supported: {extension}. Supported formats: .json, .hcl, .honua")
        };
    }

    /// <summary>
    /// Load configuration asynchronously.
    /// </summary>
    public static async Task<HonuaConfig> LoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Configuration file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".json" => await LoadFromJsonAsync(filePath),
            ".hcl" => await LoadFromHclAsync(filePath),
            ".honua" => await LoadFromHclAsync(filePath),
            _ => throw new NotSupportedException($"Configuration file format not supported: {extension}")
        };
    }

    private static HonuaConfig LoadFromJson(string filePath)
    {
        var jsonContent = File.ReadAllText(filePath);
        var rawConfig = JsonSerializer.Deserialize<HonuaConfig>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        });

        if (rawConfig == null)
        {
            throw new InvalidOperationException("Failed to parse configuration file.");
        }

        // Process environment variables and references
        var processor = new ConfigurationProcessor();
        return processor.Process(rawConfig);
    }

    private static async Task<HonuaConfig> LoadFromJsonAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var rawConfig = await JsonSerializer.DeserializeAsync<HonuaConfig>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        });

        if (rawConfig == null)
        {
            throw new InvalidOperationException("Failed to parse configuration file.");
        }

        // Process environment variables and references
        var processor = new ConfigurationProcessor();
        return processor.Process(rawConfig);
    }

    private static HonuaConfig LoadFromHcl(string filePath)
    {
        var hclContent = File.ReadAllText(filePath);
        var parser = new HclParser();
        var rawConfig = parser.Parse(hclContent);

        // Process environment variables and references
        var processor = new ConfigurationProcessor();
        return processor.Process(rawConfig);
    }

    private static async Task<HonuaConfig> LoadFromHclAsync(string filePath)
    {
        var hclContent = await File.ReadAllTextAsync(filePath);
        var parser = new HclParser();
        var rawConfig = parser.Parse(hclContent);

        // Process environment variables and references
        var processor = new ConfigurationProcessor();
        return processor.Process(rawConfig);
    }

    /// <summary>
    /// Reloads configuration from a file path, invalidating any caches.
    /// This method is designed to be called when configuration file changes are detected.
    /// </summary>
    /// <param name="filePath">Path to the configuration file to reload.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the reload operation.</param>
    /// <returns>The reloaded configuration object.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the configuration file does not exist.</exception>
    /// <exception cref="NotSupportedException">Thrown when the file format is not supported.</exception>
    public static async Task<HonuaConfig> ReloadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Configuration file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        // Note: If caching is added in the future, cache invalidation should happen here
        // For now, we simply reload the configuration from disk

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".json" => await LoadFromJsonAsync(filePath, cancellationToken),
            ".hcl" => await LoadFromHclAsync(filePath, cancellationToken),
            ".honua" => await LoadFromHclAsync(filePath, cancellationToken),
            _ => throw new NotSupportedException($"Configuration file format not supported: {extension}")
        };
    }

    private static async Task<HonuaConfig> LoadFromJsonAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var rawConfig = await JsonSerializer.DeserializeAsync<HonuaConfig>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        }, cancellationToken);

        if (rawConfig == null)
        {
            throw new InvalidOperationException("Failed to parse configuration file.");
        }

        // Process environment variables and references
        var processor = new ConfigurationProcessor();
        return processor.Process(rawConfig);
    }

    private static async Task<HonuaConfig> LoadFromHclAsync(string filePath, CancellationToken cancellationToken)
    {
        var hclContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var parser = new HclParser();
        var rawConfig = parser.Parse(hclContent);

        // Process environment variables and references
        var processor = new ConfigurationProcessor();
        return processor.Process(rawConfig);
    }
}

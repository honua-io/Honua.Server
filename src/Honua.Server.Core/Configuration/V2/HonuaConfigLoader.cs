// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
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
}

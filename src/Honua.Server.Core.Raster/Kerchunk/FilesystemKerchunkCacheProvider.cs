// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Kerchunk;

/// <summary>
/// Filesystem-based cache provider for kerchunk references.
/// Stores JSON reference files in a local directory.
/// </summary>
public sealed class FilesystemKerchunkCacheProvider : IKerchunkCacheProvider
{
    private readonly string _cacheDirectory;
    private readonly ILogger<FilesystemKerchunkCacheProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FilesystemKerchunkCacheProvider(
        string cacheDirectory,
        ILogger<FilesystemKerchunkCacheProvider> logger)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            throw new ArgumentException("Cache directory cannot be null or empty", nameof(cacheDirectory));
        }

        _cacheDirectory = cacheDirectory;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<KerchunkReferences?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetCachePath(key);

        if (!File.Exists(path))
        {
            _logger.LogDebug("Cache miss for key: {Key}", key);
            return null;
        }

        try
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);

            await using var stream = File.OpenRead(path);
            var refs = await JsonSerializer.DeserializeAsync<KerchunkReferences>(
                stream,
                JsonOptions,
                cancellationToken);

            return refs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cached kerchunk references for key: {Key}", key);

            // Delete corrupted cache file
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Ignore deletion errors
            }

            return null;
        }
    }

    public async Task SetAsync(
        string key,
        KerchunkReferences references,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var path = GetCachePath(key);

        try
        {
            // Serialize to JSON
            var json = JsonSerializer.Serialize(references, JsonOptions);

            // Write atomically using temp file + rename
            var tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, cancellationToken);

            // Atomic rename (overwrites if exists)
            File.Move(tempPath, path, overwrite: true);

            _logger.LogDebug("Cached kerchunk references for key: {Key} ({Size} bytes)",
                key, json.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache kerchunk references for key: {Key}", key);
            throw;
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetCachePath(key);
        return Task.FromResult(File.Exists(path));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetCachePath(key);

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogDebug("Deleted cached kerchunk references for key: {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete cached kerchunk references for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    private string GetCachePath(string key)
    {
        // Sanitize key for filesystem (replace invalid chars)
        var sanitizedKey = key.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        return Path.Combine(_cacheDirectory, $"{sanitizedKey}.json");
    }
}

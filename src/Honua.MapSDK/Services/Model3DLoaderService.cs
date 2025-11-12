// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Honua.MapSDK.Models.Model3D;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services;

/// <summary>
/// Service for loading and caching 3D models (GLTF/GLB format).
/// Provides model metadata extraction, caching, and memory management.
/// </summary>
public class Model3DLoaderService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Model3DLoaderService> _logger;
    private readonly ConcurrentDictionary<string, Model3DInfo> _modelCache = new();
    private readonly ConcurrentDictionary<string, byte[]> _binaryCache = new();
    private readonly SemaphoreSlim _loadSemaphore = new(3); // Limit concurrent loads
    private long _totalCachedBytes = 0;
    private const long MaxCacheSizeBytes = 500 * 1024 * 1024; // 500 MB

    /// <summary>
    /// Initializes a new instance of the Model3DLoaderService
    /// </summary>
    public Model3DLoaderService(
        IHttpClientFactory httpClientFactory,
        ILogger<Model3DLoaderService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Loads a 3D model and extracts metadata.
    /// For client-side loading, this primarily validates the URL and provides caching.
    /// Actual GLTF parsing happens in JavaScript using Three.js.
    /// </summary>
    /// <param name="modelUrl">URL to GLTF or GLB file</param>
    /// <param name="preload">If true, downloads and caches the model file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Model metadata information</returns>
    public async Task<Model3DInfo> LoadModelAsync(
        string modelUrl,
        bool preload = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(modelUrl);

        // Check cache first
        if (_modelCache.TryGetValue(cacheKey, out var cachedInfo))
        {
            _logger.LogDebug("Model {ModelUrl} loaded from cache", modelUrl);
            return cachedInfo;
        }

        await _loadSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring semaphore
            if (_modelCache.TryGetValue(cacheKey, out cachedInfo))
            {
                return cachedInfo;
            }

            var startTime = DateTime.UtcNow;
            byte[]? modelData = null;

            // Preload model data if requested
            if (preload && IsHttpUrl(modelUrl))
            {
                modelData = await DownloadModelAsync(modelUrl, cancellationToken);
            }

            // Parse metadata (basic for now, full parsing happens in JS)
            var info = await ParseModelMetadataAsync(modelUrl, modelData, cancellationToken);
            info = info with
            {
                LoadTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                IsLoaded = true
            };

            // Cache the model info
            _modelCache.TryAdd(cacheKey, info);

            // Cache binary data if preloaded
            if (modelData != null && modelData.Length > 0)
            {
                await CacheModelDataAsync(cacheKey, modelData);
            }

            _logger.LogInformation(
                "Model loaded: {ModelUrl} ({FileSize}, {TriangleCount} triangles, {LoadTime}ms)",
                modelUrl, info.GetFileSizeString(), info.TriangleCount, info.LoadTimeMs);

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model {ModelUrl}", modelUrl);
            throw new Model3DLoadException($"Failed to load model from {modelUrl}", ex);
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets cached model data if available
    /// </summary>
    public byte[]? GetCachedModelData(string modelUrl)
    {
        var cacheKey = GetCacheKey(modelUrl);
        return _binaryCache.TryGetValue(cacheKey, out var data) ? data : null;
    }

    /// <summary>
    /// Gets cached model info if available
    /// </summary>
    public Model3DInfo? GetCachedModelInfo(string modelUrl)
    {
        var cacheKey = GetCacheKey(modelUrl);
        return _modelCache.TryGetValue(cacheKey, out var info) ? info : null;
    }

    /// <summary>
    /// Clears the model cache
    /// </summary>
    public void ClearCache()
    {
        _modelCache.Clear();
        _binaryCache.Clear();
        Interlocked.Exchange(ref _totalCachedBytes, 0);
        _logger.LogInformation("Model cache cleared");
    }

    /// <summary>
    /// Removes a specific model from cache
    /// </summary>
    public bool RemoveFromCache(string modelUrl)
    {
        var cacheKey = GetCacheKey(modelUrl);
        var removed = _modelCache.TryRemove(cacheKey, out _);

        if (_binaryCache.TryRemove(cacheKey, out var data))
        {
            Interlocked.Add(ref _totalCachedBytes, -data.Length);
        }

        return removed;
    }

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        return new CacheStatistics
        {
            CachedModelCount = _modelCache.Count,
            CachedBinaryCount = _binaryCache.Count,
            TotalCachedBytes = Interlocked.Read(ref _totalCachedBytes),
            MaxCacheSizeBytes = MaxCacheSizeBytes,
            CacheUtilization = (double)Interlocked.Read(ref _totalCachedBytes) / MaxCacheSizeBytes
        };
    }

    /// <summary>
    /// Validates a model URL
    /// </summary>
    public bool ValidateModelUrl(string modelUrl)
    {
        if (string.IsNullOrWhiteSpace(modelUrl))
            return false;

        var extension = Path.GetExtension(modelUrl).ToLowerInvariant();
        if (extension != ".gltf" && extension != ".glb")
            return false;

        // Validate URL format
        if (IsHttpUrl(modelUrl))
        {
            return Uri.TryCreate(modelUrl, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        return true; // Assume valid for relative paths
    }

    /// <summary>
    /// Preloads multiple models in parallel
    /// </summary>
    public async Task<List<Model3DInfo>> PreloadModelsAsync(
        IEnumerable<string> modelUrls,
        CancellationToken cancellationToken = default)
    {
        var tasks = modelUrls
            .Where(ValidateModelUrl)
            .Select(url => LoadModelAsync(url, preload: true, cancellationToken));

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    // Private helper methods

    private async Task<byte[]> DownloadModelAsync(string modelUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5); // Large models may take time

        var response = await client.GetAsync(modelUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<Model3DInfo> ParseModelMetadataAsync(
        string modelUrl,
        byte[]? modelData,
        CancellationToken cancellationToken)
    {
        // Basic metadata - full parsing happens in JavaScript
        // This provides minimal info for server-side validation and caching
        var format = Path.GetExtension(modelUrl).ToLowerInvariant() == ".glb" ? "GLB" : "GLTF";
        var modelId = GenerateModelId(modelUrl);

        var info = new Model3DInfo
        {
            ModelId = modelId,
            ModelUrl = modelUrl,
            Format = format,
            FileSizeBytes = modelData?.Length,
            IsLoaded = false,
            // These will be populated by JavaScript after actual loading
            VertexCount = 0,
            TriangleCount = 0,
            MeshCount = 0,
            MaterialCount = 0,
            TextureCount = 0
        };

        // If we have binary GLB data, we could do basic parsing here
        // For now, we rely on client-side parsing with Three.js
        if (modelData != null && format == "GLB")
        {
            // GLB header: magic (4 bytes) + version (4 bytes) + length (4 bytes)
            if (modelData.Length >= 12)
            {
                var magic = BitConverter.ToUInt32(modelData, 0);
                var version = BitConverter.ToUInt32(modelData, 4);

                // Validate GLB magic number
                if (magic == 0x46546C67) // "glTF" in little-endian
                {
                    _logger.LogDebug("Valid GLB file detected, version {Version}", version);
                }
            }
        }

        return info;
    }

    private async Task CacheModelDataAsync(string cacheKey, byte[] data)
    {
        // Check if adding this would exceed cache limit
        var newTotal = Interlocked.Add(ref _totalCachedBytes, data.Length);

        if (newTotal > MaxCacheSizeBytes)
        {
            // Evict oldest entries (simple LRU would be better, but this works)
            await EvictCacheEntriesAsync(data.Length);
        }

        _binaryCache.TryAdd(cacheKey, data);
    }

    private async Task EvictCacheEntriesAsync(long requiredSpace)
    {
        _logger.LogWarning("Cache limit reached, evicting entries to free {RequiredSpace} bytes", requiredSpace);

        // Simple eviction: remove random entries until we have space
        // A production implementation should use LRU or LFU
        var freedSpace = 0L;
        var keysToRemove = new List<string>();

        foreach (var kvp in _binaryCache)
        {
            keysToRemove.Add(kvp.Key);
            freedSpace += kvp.Value.Length;

            if (freedSpace >= requiredSpace)
                break;
        }

        foreach (var key in keysToRemove)
        {
            if (_binaryCache.TryRemove(key, out var data))
            {
                Interlocked.Add(ref _totalCachedBytes, -data.Length);
                _modelCache.TryRemove(key, out _);
            }
        }

        _logger.LogInformation("Evicted {Count} entries, freed {FreedSpace} bytes", keysToRemove.Count, freedSpace);
    }

    private static string GetCacheKey(string modelUrl)
    {
        // Use hash of URL as cache key to handle long URLs
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(modelUrl));
        return Convert.ToBase64String(hashBytes);
    }

    private static string GenerateModelId(string modelUrl)
    {
        // Generate a shorter, more readable ID
        var fileName = Path.GetFileNameWithoutExtension(modelUrl);
        var hash = GetCacheKey(modelUrl).Substring(0, 8);
        return $"{fileName}_{hash}";
    }

    private static bool IsHttpUrl(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Exception thrown when model loading fails
/// </summary>
public class Model3DLoadException : Exception
{
    public Model3DLoadException(string message) : base(message) { }
    public Model3DLoadException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Cache statistics for the model loader
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>Number of cached model info objects</summary>
    public int CachedModelCount { get; init; }

    /// <summary>Number of cached binary model files</summary>
    public int CachedBinaryCount { get; init; }

    /// <summary>Total bytes cached</summary>
    public long TotalCachedBytes { get; init; }

    /// <summary>Maximum cache size in bytes</summary>
    public long MaxCacheSizeBytes { get; init; }

    /// <summary>Cache utilization (0.0 to 1.0)</summary>
    public double CacheUtilization { get; init; }

    /// <summary>Gets total cached size as human-readable string</summary>
    public string GetTotalSizeString()
    {
        var bytes = TotalCachedBytes;
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>Gets cache utilization as percentage string</summary>
    public string GetUtilizationString() => $"{CacheUtilization * 100:F1}%";
}

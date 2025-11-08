using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;

namespace Honua.MapSDK.Services.DataLoading;

/// <summary>
/// Optimized data loader with parallel fetching, caching, and request deduplication.
/// </summary>
public class DataLoader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly DataCache _cache;
    private readonly ConcurrentDictionary<string, Task<string>> _pendingRequests = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataLoader"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests.</param>
    /// <param name="cache">Cache for storing responses.</param>
    public DataLoader(HttpClient httpClient, DataCache cache)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Loads data from a URL with caching and deduplication.
    /// </summary>
    /// <param name="url">The URL to load data from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response data as a string.</returns>
    public async Task<string> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url));

        // Check cache first
        if (_cache.TryGet<string>(url, out var cachedData) && cachedData != null)
        {
            return cachedData;
        }

        // Check if there's already a pending request for this URL (deduplication)
        if (_pendingRequests.TryGetValue(url, out var pendingTask))
        {
            return await pendingTask;
        }

        // Create a new request
        var requestTask = LoadInternalAsync(url, cancellationToken);
        _pendingRequests[url] = requestTask;

        try
        {
            var result = await requestTask;
            return result;
        }
        finally
        {
            _pendingRequests.TryRemove(url, out _);
        }
    }

    /// <summary>
    /// Loads JSON data from a URL and deserializes it.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="url">The URL to load data from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized object.</returns>
    public async Task<T?> LoadJsonAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = $"json:{url}";
        if (_cache.TryGet<T>(cacheKey, out var cachedData) && cachedData != null)
        {
            return cachedData;
        }

        var json = await LoadAsync(url, cancellationToken);
        var data = JsonSerializer.Deserialize<T>(json);

        if (data != null)
        {
            _cache.Set(cacheKey, data);
        }

        return data;
    }

    /// <summary>
    /// Loads multiple URLs in parallel.
    /// </summary>
    /// <param name="urls">The URLs to load.</param>
    /// <param name="maxParallel">Maximum number of parallel requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of URL to response data.</returns>
    public async Task<Dictionary<string, string>> LoadManyAsync(
        IEnumerable<string> urls,
        int maxParallel = 4,
        CancellationToken cancellationToken = default)
    {
        var urlList = urls.ToList();
        var results = new Dictionary<string, string>();
        var semaphore = new SemaphoreSlim(maxParallel);

        var tasks = urlList.Select(async url =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var data = await LoadAsync(url, cancellationToken);
                lock (results)
                {
                    results[url] = data;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Loads GeoJSON data from a URL.
    /// </summary>
    /// <param name="url">The URL to load GeoJSON from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The GeoJSON as a JsonDocument.</returns>
    public async Task<JsonDocument?> LoadGeoJsonAsync(string url, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"geojson:{url}";

        // For GeoJSON, we cache the raw string and parse it each time
        // This is more efficient than caching the parsed document
        if (_cache.TryGet<string>(cacheKey, out var cachedJson) && cachedJson != null)
        {
            return JsonDocument.Parse(cachedJson);
        }

        var json = await LoadAsync(url, cancellationToken);
        _cache.Set(cacheKey, json);

        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Preloads data from URLs into the cache.
    /// </summary>
    /// <param name="urls">URLs to preload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PreloadAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        await LoadManyAsync(urls, maxParallel: 8, cancellationToken);
    }

    /// <summary>
    /// Clears the cache and pending requests.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _pendingRequests.Clear();
    }

    /// <summary>
    /// Internal method to load data from a URL.
    /// </summary>
    private async Task<string> LoadInternalAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Check if response is compressed
            var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            string data;
            if (!string.IsNullOrEmpty(contentEncoding))
            {
                data = CompressionHelper.AutoDecompress(bytes, contentEncoding);
            }
            else
            {
                data = await response.Content.ReadAsStringAsync(cancellationToken);
            }

            // Cache the response
            _cache.Set(url, data);

            return data;
        }
        catch (Exception ex)
        {
            throw new DataLoadException($"Failed to load data from {url}", ex);
        }
    }

    /// <summary>
    /// Disposes the data loader and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _pendingRequests.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Exception thrown when data loading fails.
/// </summary>
public class DataLoadException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataLoadException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Inner exception.</param>
    public DataLoadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

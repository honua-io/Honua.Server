namespace Honua.MapSDK.Services.DataLoading;

/// <summary>
/// Represents a cached data entry with metadata.
/// </summary>
/// <typeparam name="T">The type of data being cached.</typeparam>
internal class CacheEntry<T>
{
    /// <summary>
    /// Gets or sets the cached data.
    /// </summary>
    public T Data { get; set; } = default!;

    /// <summary>
    /// Gets or sets the UTC timestamp when the entry was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the entry expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the last access timestamp for LRU eviction.
    /// </summary>
    public DateTime LastAccessedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of times this entry has been accessed.
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Gets or sets the estimated size of the entry in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets a value indicating whether the cache entry is expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

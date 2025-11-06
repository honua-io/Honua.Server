namespace Honua.MapSDK.Services.DataLoading;

/// <summary>
/// Configuration options for data caching.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether caching is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum cache size in megabytes.
    /// Default is 50 MB.
    /// </summary>
    public int MaxSizeMB { get; set; } = 50;

    /// <summary>
    /// Gets or sets the default time-to-live for cached items in seconds.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    public int DefaultTtlSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum number of items to cache.
    /// Default is 100 items.
    /// </summary>
    public int MaxItems { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether to compress cached data.
    /// Default is true.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable cache statistics.
    /// Default is false (for performance).
    /// </summary>
    public bool EnableStatistics { get; set; } = false;
}

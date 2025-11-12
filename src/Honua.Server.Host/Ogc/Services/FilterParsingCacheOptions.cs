// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Configuration options for the filter parsing cache.
/// </summary>
public sealed class FilterParsingCacheOptions
{
    /// <summary>
    /// Maximum number of cached filter entries. Default: 10,000.
    /// This is the primary limit enforced by the LRU eviction policy.
    /// </summary>
    public long MaxEntries { get; set; } = 10_000;

    /// <summary>
    /// Maximum total size of cached filters in bytes. Default: 50 MB (52,428,800 bytes).
    /// Individual filters exceeding this size will not be cached.
    /// </summary>
    public long MaxSizeBytes { get; set; } = 50 * 1024 * 1024; // 50 MB

    /// <summary>
    /// Sliding expiration time for cached entries in minutes. Default: 60 minutes.
    /// Entries that are not accessed within this time will be evicted.
    /// </summary>
    public int SlidingExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Whether the filter parsing cache is enabled. Default: true.
    /// Set to false to disable caching entirely (useful for debugging).
    /// </summary>
    public bool Enabled { get; set; } = true;
}

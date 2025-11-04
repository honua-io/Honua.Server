// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Standardized cache Time-To-Live (TTL) policies for consistent cache duration management across the application.
/// </summary>
/// <remarks>
/// These policies provide named, semantic cache durations that align with common data volatility patterns.
/// Using these policies instead of ad-hoc TimeSpan values improves maintainability and consistency.
/// </remarks>
public enum CacheTtlPolicy
{
    /// <summary>
    /// Very short duration (1 minute) - for frequently changing data like live metrics, active sessions, or real-time status.
    /// </summary>
    VeryShort,

    /// <summary>
    /// Short duration (5 minutes) - for dynamic content like user preferences, dashboard data, or frequently updated aggregations.
    /// </summary>
    Short,

    /// <summary>
    /// Medium duration (1 hour) - for semi-static content like raster metadata, schema validation results, or configuration snapshots.
    /// </summary>
    Medium,

    /// <summary>
    /// Long duration (24 hours) - for static content like tile cache data, catalog projections, or rarely changing reference data.
    /// </summary>
    Long,

    /// <summary>
    /// Very long duration (7 days) - for effectively immutable content like historical raster tiles or archived metadata snapshots.
    /// </summary>
    VeryLong,

    /// <summary>
    /// Permanent-like duration (30 days) - for truly immutable content like finalized catalog entries or permanent reference datasets.
    /// Use sparingly and ensure manual invalidation when data actually changes.
    /// </summary>
    Permanent
}

/// <summary>
/// Extension methods for converting <see cref="CacheTtlPolicy"/> to concrete cache options.
/// </summary>
public static class CacheTtlPolicyExtensions
{
    /// <summary>
    /// Converts a <see cref="CacheTtlPolicy"/> to its corresponding <see cref="TimeSpan"/> duration.
    /// </summary>
    /// <param name="policy">The TTL policy to convert.</param>
    /// <returns>The cache duration as a <see cref="TimeSpan"/>.</returns>
    /// <example>
    /// <code>
    /// var duration = CacheTtlPolicy.Medium.ToTimeSpan(); // Returns TimeSpan.FromHours(1)
    /// </code>
    /// </example>
    public static TimeSpan ToTimeSpan(this CacheTtlPolicy policy)
    {
        return policy switch
        {
            CacheTtlPolicy.VeryShort => TimeSpan.FromMinutes(1),
            CacheTtlPolicy.Short => TimeSpan.FromMinutes(5),
            CacheTtlPolicy.Medium => TimeSpan.FromHours(1),
            CacheTtlPolicy.Long => TimeSpan.FromHours(24),
            CacheTtlPolicy.VeryLong => TimeSpan.FromDays(7),
            CacheTtlPolicy.Permanent => TimeSpan.FromDays(30),
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown cache TTL policy")
        };
    }

    /// <summary>
    /// Converts a <see cref="CacheTtlPolicy"/> to <see cref="DistributedCacheEntryOptions"/> with absolute expiration.
    /// </summary>
    /// <param name="policy">The TTL policy to convert.</param>
    /// <returns>
    /// A <see cref="DistributedCacheEntryOptions"/> instance configured with the policy's duration as absolute expiration.
    /// </returns>
    /// <remarks>
    /// Uses absolute expiration to ensure consistent cache behavior across all nodes in a distributed environment.
    /// The cache entry will expire at a fixed time regardless of access patterns.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = CacheTtlPolicy.Medium.ToDistributedCacheOptions();
    /// await cache.SetAsync(key, value, options, cancellationToken);
    /// </code>
    /// </example>
    public static DistributedCacheEntryOptions ToDistributedCacheOptions(this CacheTtlPolicy policy)
    {
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = policy.ToTimeSpan()
        };
    }

    /// <summary>
    /// Converts a <see cref="CacheTtlPolicy"/> to <see cref="MemoryCacheEntryOptions"/> with absolute expiration.
    /// </summary>
    /// <param name="policy">The TTL policy to convert.</param>
    /// <param name="priority">
    /// The cache item priority for memory cache eviction. Defaults to <see cref="CacheItemPriority.Normal"/>.
    /// </param>
    /// <returns>
    /// A <see cref="MemoryCacheEntryOptions"/> instance configured with the policy's duration as absolute expiration.
    /// </returns>
    /// <remarks>
    /// Uses absolute expiration by default. For sliding expiration behavior, use <see cref="CacheOptionsBuilder"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = CacheTtlPolicy.Medium.ToMemoryCacheOptions(CacheItemPriority.High);
    /// cache.Set(key, value, options);
    /// </code>
    /// </example>
    public static MemoryCacheEntryOptions ToMemoryCacheOptions(
        this CacheTtlPolicy policy,
        CacheItemPriority priority = CacheItemPriority.Normal)
    {
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = policy.ToTimeSpan(),
            Priority = priority
        };
    }

    /// <summary>
    /// Converts a <see cref="CacheTtlPolicy"/> to <see cref="MemoryCacheEntryOptions"/> with sliding expiration.
    /// </summary>
    /// <param name="policy">The TTL policy to convert.</param>
    /// <param name="priority">
    /// The cache item priority for memory cache eviction. Defaults to <see cref="CacheItemPriority.Normal"/>.
    /// </param>
    /// <returns>
    /// A <see cref="MemoryCacheEntryOptions"/> instance configured with the policy's duration as sliding expiration.
    /// </returns>
    /// <remarks>
    /// Uses sliding expiration - the cache entry will remain valid as long as it's accessed within the TTL window.
    /// This is useful for frequently accessed data that should remain cached as long as it's being used.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = CacheTtlPolicy.Medium.ToMemoryCacheOptionsWithSliding();
    /// cache.Set(key, value, options); // Entry stays cached while accessed within 1 hour
    /// </code>
    /// </example>
    public static MemoryCacheEntryOptions ToMemoryCacheOptionsWithSliding(
        this CacheTtlPolicy policy,
        CacheItemPriority priority = CacheItemPriority.Normal)
    {
        return new MemoryCacheEntryOptions
        {
            SlidingExpiration = policy.ToTimeSpan(),
            Priority = priority
        };
    }
}

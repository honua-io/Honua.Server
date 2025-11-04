// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Fluent builder for creating cache entry options with a consistent, expressive API.
/// Supports both distributed cache and memory cache configurations.
/// </summary>
/// <remarks>
/// <para>
/// This builder consolidates cache configuration patterns across the codebase, eliminating
/// duplication and providing a domain-specific API for common caching scenarios.
/// </para>
/// <para>
/// <strong>Design Principles:</strong>
/// <list type="bullet">
/// <item>Fluent API for readable cache configuration</item>
/// <item>Type-safe policy-based configuration via <see cref="CacheTtlPolicy"/></item>
/// <item>Sensible defaults for production use</item>
/// <item>Support for both distributed and in-memory caching</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Using predefined policy
/// var options = new CacheOptionsBuilder()
///     .WithPolicy(CacheTtlPolicy.Medium)
///     .WithPriority(CacheItemPriority.High)
///     .BuildMemory();
///
/// // Custom configuration
/// var options = new CacheOptionsBuilder()
///     .WithAbsoluteExpiration(TimeSpan.FromMinutes(30))
///     .WithSlidingExpiration(TimeSpan.FromMinutes(10))
///     .BuildDistributed();
///
/// // Domain-specific factory
/// var options = CacheOptionsBuilder.ForRasterTiles();
/// </code>
/// </example>
public sealed class CacheOptionsBuilder
{
    private TimeSpan? _absoluteExpiration;
    private TimeSpan? _slidingExpiration;
    private DateTimeOffset? _absoluteExpirationTime;
    private CacheItemPriority _priority = CacheItemPriority.Normal;
    private long? _size;

    /// <summary>
    /// Sets the absolute expiration time relative to now.
    /// The cache entry will expire after this duration regardless of access.
    /// </summary>
    /// <param name="expiration">The duration after which the cache entry expires.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="expiration"/> is zero or negative.</exception>
    /// <remarks>
    /// Absolute expiration is recommended for:
    /// <list type="bullet">
    /// <item>Data that changes on a schedule (e.g., daily aggregations)</item>
    /// <item>Distributed cache scenarios where consistent expiration is critical</item>
    /// <item>Content that becomes stale after a fixed duration</item>
    /// </list>
    /// </remarks>
    public CacheOptionsBuilder WithAbsoluteExpiration(TimeSpan expiration)
    {
        if (expiration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiration),
                expiration,
                "Absolute expiration must be greater than zero.");
        }

        _absoluteExpiration = expiration;
        _absoluteExpirationTime = null; // Clear absolute time if set
        return this;
    }

    /// <summary>
    /// Sets the absolute expiration to a specific point in time.
    /// The cache entry will expire at the specified time.
    /// </summary>
    /// <param name="expirationTime">The point in time when the cache entry expires.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="expirationTime"/> is in the past.
    /// </exception>
    /// <remarks>
    /// Useful for data that becomes invalid at a specific time (e.g., scheduled maintenance, time-based events).
    /// </remarks>
    public CacheOptionsBuilder WithAbsoluteExpiration(DateTimeOffset expirationTime)
    {
        if (expirationTime <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expirationTime),
                expirationTime,
                "Absolute expiration time must be in the future.");
        }

        _absoluteExpirationTime = expirationTime;
        _absoluteExpiration = null; // Clear relative time if set
        return this;
    }

    /// <summary>
    /// Sets the sliding expiration window.
    /// The cache entry will expire if not accessed within this duration.
    /// </summary>
    /// <param name="expiration">The sliding expiration window.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="expiration"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// Sliding expiration is recommended for:
    /// <list type="bullet">
    /// <item>Frequently accessed data that should stay cached while in use</item>
    /// <item>Session data or user-specific caches</item>
    /// <item>Data where access patterns indicate freshness</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> Sliding expiration is only supported by <see cref="MemoryCacheEntryOptions"/>.
    /// For distributed caches, consider using absolute expiration instead.
    /// </para>
    /// </remarks>
    public CacheOptionsBuilder WithSlidingExpiration(TimeSpan expiration)
    {
        if (expiration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiration),
                expiration,
                "Sliding expiration must be greater than zero.");
        }

        _slidingExpiration = expiration;
        return this;
    }

    /// <summary>
    /// Configures cache options using a predefined <see cref="CacheTtlPolicy"/>.
    /// This is the recommended approach for common cache duration scenarios.
    /// </summary>
    /// <param name="policy">The TTL policy to apply.</param>
    /// <param name="useSliding">
    /// If <c>true</c>, applies the policy as sliding expiration; otherwise uses absolute expiration.
    /// Default is <c>false</c> (absolute expiration).
    /// </param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// Using policies improves maintainability by centralizing cache duration decisions.
    /// See <see cref="CacheTtlPolicy"/> for available policies and their semantics.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Absolute expiration (recommended for distributed cache)
    /// var options = new CacheOptionsBuilder()
    ///     .WithPolicy(CacheTtlPolicy.Medium)
    ///     .BuildDistributed();
    ///
    /// // Sliding expiration (for in-memory cache)
    /// var options = new CacheOptionsBuilder()
    ///     .WithPolicy(CacheTtlPolicy.Medium, useSliding: true)
    ///     .BuildMemory();
    /// </code>
    /// </example>
    public CacheOptionsBuilder WithPolicy(CacheTtlPolicy policy, bool useSliding = false)
    {
        var duration = policy.ToTimeSpan();

        if (useSliding)
        {
            return WithSlidingExpiration(duration);
        }

        return WithAbsoluteExpiration(duration);
    }

    /// <summary>
    /// Sets the cache item priority for memory cache eviction.
    /// </summary>
    /// <param name="priority">The cache item priority.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// Priority affects eviction behavior when memory pressure occurs:
    /// <list type="bullet">
    /// <item><see cref="CacheItemPriority.Low"/>: First to be evicted</item>
    /// <item><see cref="CacheItemPriority.Normal"/>: Standard eviction priority (default)</item>
    /// <item><see cref="CacheItemPriority.High"/>: Preserved longer under memory pressure</item>
    /// <item><see cref="CacheItemPriority.NeverRemove"/>: Only removed on expiration (use sparingly)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> This option only applies to <see cref="MemoryCacheEntryOptions"/>.
    /// It has no effect on distributed cache.
    /// </para>
    /// </remarks>
    public CacheOptionsBuilder WithPriority(CacheItemPriority priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>
    /// Sets the size of the cache entry for size-based eviction policies.
    /// </summary>
    /// <param name="size">The logical size of the cache entry.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// Size is used when the memory cache has a <see cref="MemoryCacheOptions.SizeLimit"/> configured.
    /// The cache will evict entries to stay within the size limit.
    /// </para>
    /// <para>
    /// Size units are application-defined (e.g., bytes, number of items, complexity score).
    /// Ensure consistent sizing across all cached items for predictable eviction behavior.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> This option only applies to <see cref="MemoryCacheEntryOptions"/>.
    /// </para>
    /// </remarks>
    public CacheOptionsBuilder WithSize(long size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "Cache entry size must be greater than zero.");
        }

        _size = size;
        return this;
    }

    /// <summary>
    /// Builds <see cref="DistributedCacheEntryOptions"/> based on the configured settings.
    /// </summary>
    /// <returns>A configured <see cref="DistributedCacheEntryOptions"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Distributed cache options support:
    /// <list type="bullet">
    /// <item>Absolute expiration (relative or fixed time)</item>
    /// <item>Sliding expiration (when supported by the underlying cache implementation)</item>
    /// </list>
    /// </para>
    /// <para>
    /// If no expiration is configured, the cache entry will not expire automatically.
    /// This is generally not recommended for production use.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new CacheOptionsBuilder()
    ///     .WithPolicy(CacheTtlPolicy.Long)
    ///     .BuildDistributed();
    ///
    /// await cache.SetAsync(key, data, options, cancellationToken);
    /// </code>
    /// </example>
    public DistributedCacheEntryOptions BuildDistributed()
    {
        var options = new DistributedCacheEntryOptions();

        if (_absoluteExpiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = _absoluteExpiration.Value;
        }
        else if (_absoluteExpirationTime.HasValue)
        {
            options.AbsoluteExpiration = _absoluteExpirationTime.Value;
        }

        if (_slidingExpiration.HasValue)
        {
            options.SlidingExpiration = _slidingExpiration.Value;
        }

        return options;
    }

    /// <summary>
    /// Builds <see cref="MemoryCacheEntryOptions"/> based on the configured settings.
    /// </summary>
    /// <returns>A configured <see cref="MemoryCacheEntryOptions"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Memory cache options support all configuration options:
    /// <list type="bullet">
    /// <item>Absolute expiration (relative or fixed time)</item>
    /// <item>Sliding expiration</item>
    /// <item>Cache priority</item>
    /// <item>Size-based eviction</item>
    /// </list>
    /// </para>
    /// <para>
    /// If no expiration is configured, the cache entry will not expire automatically
    /// but can still be evicted based on priority and size limits.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new CacheOptionsBuilder()
    ///     .WithPolicy(CacheTtlPolicy.Medium, useSliding: true)
    ///     .WithPriority(CacheItemPriority.High)
    ///     .WithSize(1024)
    ///     .BuildMemory();
    ///
    /// cache.Set(key, data, options);
    /// </code>
    /// </example>
    public MemoryCacheEntryOptions BuildMemory()
    {
        var options = new MemoryCacheEntryOptions
        {
            Priority = _priority
        };

        if (_absoluteExpiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = _absoluteExpiration.Value;
        }
        else if (_absoluteExpirationTime.HasValue)
        {
            options.AbsoluteExpiration = _absoluteExpirationTime.Value;
        }

        if (_slidingExpiration.HasValue)
        {
            options.SlidingExpiration = _slidingExpiration.Value;
        }

        if (_size.HasValue)
        {
            options.Size = _size.Value;
        }

        return options;
    }

    #region Domain-Specific Factory Methods

    /// <summary>
    /// Creates cache options optimized for raster tile caching.
    /// </summary>
    /// <returns>A <see cref="CacheOptionsBuilder"/> configured for raster tiles.</returns>
    /// <remarks>
    /// <para>
    /// Configuration: 24-hour absolute expiration with high priority.
    /// </para>
    /// <para>
    /// Rationale: Raster tiles are immutable and expensive to generate.
    /// Long caching duration reduces rendering load. High priority preserves
    /// frequently accessed tiles under memory pressure.
    /// </para>
    /// </remarks>
    public static CacheOptionsBuilder ForRasterTiles()
    {
        return new CacheOptionsBuilder()
            .WithPolicy(CacheTtlPolicy.Long)
            .WithPriority(CacheItemPriority.High);
    }

    /// <summary>
    /// Creates cache options optimized for vector tile caching.
    /// </summary>
    /// <returns>A <see cref="CacheOptionsBuilder"/> configured for vector tiles.</returns>
    /// <remarks>
    /// <para>
    /// Configuration: 24-hour absolute expiration with normal priority.
    /// </para>
    /// <para>
    /// Rationale: Vector tiles are generated from database queries and benefit
    /// from long caching. Normal priority allows eviction if memory constrained.
    /// </para>
    /// </remarks>
    public static CacheOptionsBuilder ForVectorTiles()
    {
        return new CacheOptionsBuilder()
            .WithPolicy(CacheTtlPolicy.Long)
            .WithPriority(CacheItemPriority.Normal);
    }

    /// <summary>
    /// Creates cache options optimized for metadata caching.
    /// </summary>
    /// <returns>A <see cref="CacheOptionsBuilder"/> configured for metadata.</returns>
    /// <remarks>
    /// <para>
    /// Configuration: 1-hour sliding expiration with high priority.
    /// </para>
    /// <para>
    /// Rationale: Metadata is frequently accessed and relatively small.
    /// Sliding expiration keeps active metadata cached. High priority
    /// preserves metadata critical for query execution.
    /// </para>
    /// </remarks>
    public static CacheOptionsBuilder ForMetadata()
    {
        return new CacheOptionsBuilder()
            .WithPolicy(CacheTtlPolicy.Medium, useSliding: true)
            .WithPriority(CacheItemPriority.High);
    }

    /// <summary>
    /// Creates cache options optimized for schema validation result caching.
    /// </summary>
    /// <returns>A <see cref="CacheOptionsBuilder"/> configured for schema validation.</returns>
    /// <remarks>
    /// <para>
    /// Configuration: 1-hour sliding expiration with normal priority and size of 1.
    /// </para>
    /// <para>
    /// Rationale: Schema validation is CPU-intensive but schemas rarely change.
    /// Sliding expiration ensures validation results stay cached during active use.
    /// Size is set to allow use with size-limited caches.
    /// </para>
    /// </remarks>
    public static CacheOptionsBuilder ForSchemaValidation()
    {
        return new CacheOptionsBuilder()
            .WithPolicy(CacheTtlPolicy.Medium, useSliding: true)
            .WithPriority(CacheItemPriority.Normal)
            .WithSize(1);
    }

    /// <summary>
    /// Creates cache options optimized for authentication token caching.
    /// </summary>
    /// <returns>A <see cref="CacheOptionsBuilder"/> configured for authentication tokens.</returns>
    /// <remarks>
    /// <para>
    /// Configuration: 5-minute absolute expiration with high priority.
    /// </para>
    /// <para>
    /// Rationale: Token validation results should not be cached long to ensure
    /// revocations take effect quickly. High priority ensures authentication
    /// doesn't slow down under memory pressure.
    /// </para>
    /// </remarks>
    public static CacheOptionsBuilder ForAuthenticationTokens()
    {
        return new CacheOptionsBuilder()
            .WithPolicy(CacheTtlPolicy.Short)
            .WithPriority(CacheItemPriority.High);
    }

    /// <summary>
    /// Creates cache options optimized for session state caching.
    /// </summary>
    /// <returns>A <see cref="CacheOptionsBuilder"/> configured for session state.</returns>
    /// <remarks>
    /// <para>
    /// Configuration: 30-minute sliding expiration with normal priority.
    /// </para>
    /// <para>
    /// Rationale: Session data should persist as long as the user is active.
    /// Sliding expiration provides automatic session timeout behavior.
    /// Normal priority allows eviction if many idle sessions accumulate.
    /// </para>
    /// </remarks>
    public static CacheOptionsBuilder ForSessionState()
    {
        return new CacheOptionsBuilder()
            .WithAbsoluteExpiration(TimeSpan.FromMinutes(30))
            .WithSlidingExpiration(TimeSpan.FromMinutes(5))
            .WithPriority(CacheItemPriority.Normal);
    }

    /// <summary>
    /// Creates cache options optimized for catalog projection caching.
    /// </summary>
    /// <returns>A <see cref="CacheOptionsBuilder"/> configured for catalog projections.</returns>
    /// <remarks>
    /// <para>
    /// Configuration: 24-hour absolute expiration with high priority.
    /// </para>
    /// <para>
    /// Rationale: Catalog projections are expensive to compute and relatively static.
    /// Long caching improves API response times. High priority preserves
    /// catalog data needed for discovery operations.
    /// </para>
    /// </remarks>
    public static CacheOptionsBuilder ForCatalogProjection()
    {
        return new CacheOptionsBuilder()
            .WithPolicy(CacheTtlPolicy.Long)
            .WithPriority(CacheItemPriority.High);
    }

    /// <summary>
    /// Creates cache options optimized for Zarr chunk caching.
    /// </summary>
    /// <returns>A <see cref="CacheOptionsBuilder"/> configured for Zarr chunks.</returns>
    /// <remarks>
    /// <para>
    /// Configuration: 1-hour absolute expiration with normal priority.
    /// </para>
    /// <para>
    /// Rationale: Zarr chunks are immutable but numerous. Medium TTL reduces
    /// remote fetches while allowing turnover for infrequently accessed chunks.
    /// Normal priority allows eviction based on access patterns.
    /// </para>
    /// </remarks>
    public static CacheOptionsBuilder ForZarrChunks()
    {
        return new CacheOptionsBuilder()
            .WithPolicy(CacheTtlPolicy.Medium)
            .WithPriority(CacheItemPriority.Normal);
    }

    /// <summary>
    /// Creates cache options optimized for connection string decryption result caching.
    /// </summary>
    /// <returns>A <see cref="CacheOptionsBuilder"/> configured for decrypted connection strings.</returns>
    /// <remarks>
    /// <para>
    /// Configuration: 1-hour absolute expiration with 30-minute sliding window, high priority.
    /// </para>
    /// <para>
    /// Rationale: Decryption is expensive and connection strings rarely change.
    /// Combined absolute and sliding expiration ensures security (max 1 hour)
    /// while optimizing for active use. High priority prevents eviction during load.
    /// </para>
    /// </remarks>
    public static CacheOptionsBuilder ForDecryptedConnectionStrings()
    {
        return new CacheOptionsBuilder()
            .WithAbsoluteExpiration(TimeSpan.FromHours(1))
            .WithSlidingExpiration(TimeSpan.FromMinutes(30))
            .WithPriority(CacheItemPriority.High);
    }

    #endregion
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Honua.Server.Core.BackgroundJobs;

/// <summary>
/// Redis-based implementation of idempotency store for background jobs.
/// Provides fast lookups and automatic TTL-based expiry.
/// </summary>
/// <remarks>
/// Implementation approach:
///
/// Key structure: "idempotency:{key}"
/// Value: JSON-serialized result
/// TTL: Configured via options (default 7 days)
///
/// Advantages:
/// - Fast lookups (sub-millisecond)
/// - Automatic expiry via Redis TTL
/// - No manual cleanup needed
/// - Atomic operations
/// - High availability with Redis Cluster/Sentinel
///
/// Disadvantages:
/// - External dependency on Redis
/// - Memory-based (limited by Redis instance size)
/// - Data loss on Redis failure (acceptable for idempotency use case)
///
/// Suitable for:
/// - All deployment tiers (Tier 1-3)
/// - High-throughput job processing
/// - Scenarios requiring fast duplicate detection
/// </remarks>
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisIdempotencyStore> _logger;
    private const string KeyPrefix = "idempotency:";
    private const string StatsKeyPrefix = "idempotency:stats:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private long _hits = 0;
    private long _misses = 0;

    public RedisIdempotencyStore(
        IConnectionMultiplexer redis,
        ILogger<RedisIdempotencyStore> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = _redis.GetDatabase();
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string idempotencyKey, CancellationToken cancellationToken = default)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be null or empty", nameof(idempotencyKey));

        try
        {
            var key = GetRedisKey(idempotencyKey);
            var value = await _database.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                Interlocked.Increment(ref _misses);
                _logger.LogDebug("Idempotency cache miss for key: {IdempotencyKey}", idempotencyKey);
                return null;
            }

            Interlocked.Increment(ref _hits);

            var result = JsonSerializer.Deserialize<T>(value!, JsonOptions);

            _logger.LogInformation(
                "Idempotency cache hit for key: {IdempotencyKey}",
                idempotencyKey);

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize cached result for key: {IdempotencyKey}",
                idempotencyKey);
            return null;
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error while getting cached result for key: {IdempotencyKey}",
                idempotencyKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StoreAsync<T>(
        string idempotencyKey,
        T result,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be null or empty", nameof(idempotencyKey));

        if (result == null)
            throw new ArgumentNullException(nameof(result));

        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive");

        try
        {
            var key = GetRedisKey(idempotencyKey);
            var json = JsonSerializer.Serialize(result, JsonOptions);

            await _database.StringSetAsync(key, json, ttl);

            _logger.LogInformation(
                "Stored idempotency cache entry for key: {IdempotencyKey}, TTL: {TTL}",
                idempotencyKey,
                ttl);
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error while storing cached result for key: {IdempotencyKey}",
                idempotencyKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be null or empty", nameof(idempotencyKey));

        try
        {
            var key = GetRedisKey(idempotencyKey);
            var exists = await _database.KeyExistsAsync(key);

            _logger.LogDebug(
                "Idempotency key existence check: {IdempotencyKey} = {Exists}",
                idempotencyKey,
                exists);

            return exists;
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error while checking key existence: {IdempotencyKey}",
                idempotencyKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be null or empty", nameof(idempotencyKey));

        try
        {
            var key = GetRedisKey(idempotencyKey);
            var deleted = await _database.KeyDeleteAsync(key);

            if (deleted)
            {
                _logger.LogDebug("Deleted idempotency cache entry for key: {IdempotencyKey}", idempotencyKey);
            }
            else
            {
                _logger.LogDebug("Idempotency cache entry not found for deletion: {IdempotencyKey}", idempotencyKey);
            }
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error while deleting key: {IdempotencyKey}",
                idempotencyKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IdempotencyStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get approximate count of keys with our prefix
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{KeyPrefix}*");
            var totalEntries = keys.LongCount();

            // Calculate hit/miss rates
            var totalRequests = _hits + _misses;
            var hitRate = totalRequests > 0 ? (_hits * 100.0m / totalRequests) : 0m;
            var missRate = totalRequests > 0 ? (_misses * 100.0m / totalRequests) : 0m;

            // Get approximate memory usage
            var info = await server.InfoAsync("memory");
            decimal? memoryUsageMB = null;

            if (info != null)
            {
                var memorySection = info.FirstOrDefault(s => s.Key == "Memory");
                if (memorySection.Key != null)
                {
                    var usedMemory = memorySection
                        .FirstOrDefault(kv => kv.Key == "used_memory")
                        .Value;

                    if (!string.IsNullOrEmpty(usedMemory) && long.TryParse(usedMemory, out var bytes))
                    {
                        memoryUsageMB = bytes / 1024m / 1024m;
                    }
                }
            }

            _logger.LogDebug(
                "Idempotency cache statistics - Entries: {Entries}, Hit Rate: {HitRate:F2}%, Miss Rate: {MissRate:F2}%",
                totalEntries,
                hitRate,
                missRate);

            return new IdempotencyStoreStatistics
            {
                TotalEntries = totalEntries,
                MemoryUsageMB = memoryUsageMB,
                HitRate = hitRate,
                MissRate = missRate
            };
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error while getting statistics");
            throw;
        }
    }

    private static string GetRedisKey(string idempotencyKey)
    {
        return $"{KeyPrefix}{idempotencyKey}";
    }
}

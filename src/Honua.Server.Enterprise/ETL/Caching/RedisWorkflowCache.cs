// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Honua.Server.Enterprise.ETL.Performance;

namespace Honua.Server.Enterprise.ETL.Caching;

/// <summary>
/// Redis cache implementation for distributed workflow data caching
/// </summary>
public class RedisWorkflowCache : IWorkflowCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisWorkflowCache> _logger;
    private readonly IPerformanceMetrics? _metrics;
    private readonly CacheOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisWorkflowCache(
        IConnectionMultiplexer redis,
        IOptions<CacheOptions> options,
        ILogger<RedisWorkflowCache> logger,
        IPerformanceMetrics? metrics = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _database = _redis.GetDatabase();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
        _options = options?.Value ?? new CacheOptions();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var fullKey = GetFullKey(key);
            var value = await _database.StringGetAsync(fullKey);

            if (value.IsNullOrEmpty)
            {
                _logger.LogTrace("Cache miss: {Key}", fullKey);
                _metrics?.RecordCacheMiss("redis", fullKey);
                return null;
            }

            _logger.LogTrace("Cache hit: {Key}", fullKey);
            _metrics?.RecordCacheHit("redis", fullKey);

            return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting from Redis cache: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var fullKey = GetFullKey(key);
            var actualTtl = ttl ?? TimeSpan.FromMinutes(_options.DefaultTtlMinutes);

            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _database.StringSetAsync(fullKey, json, actualTtl);

            _logger.LogTrace("Cache set: {Key} (TTL: {Ttl})", fullKey, actualTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Redis cache: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullKey = GetFullKey(key);
            await _database.KeyDeleteAsync(fullKey);
            _logger.LogTrace("Cache removed: {Key}", fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing from Redis cache: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullKey = GetFullKey(key);
            return await _database.KeyExistsAsync(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Redis cache key existence: {Key}", key);
            return false;
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var value = await factory();
        await SetAsync(key, value, ttl, cancellationToken);
        return value;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            var pattern = $"{_options.KeyPrefix}*";

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                await _database.KeyDeleteAsync(key);
            }

            _logger.LogInformation("Cleared all cache keys matching pattern: {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Redis cache");
        }
    }

    private string GetFullKey(string key)
    {
        return key.StartsWith(_options.KeyPrefix) ? key : $"{_options.KeyPrefix}{key}";
    }
}

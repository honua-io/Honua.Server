# Distributed Cache Usage Guide

## Overview

The Honua platform now supports Redis-based distributed caching for multi-instance deployments. This enables metadata and other data to be cached across multiple server instances.

## Configuration

### Redis Connection String

Add the Redis connection string to your `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false,connectTimeout=5000"
  }
}
```

**Production Example (AWS ElastiCache):**
```json
{
  "ConnectionStrings": {
    "Redis": "my-redis-cluster.abc123.ng.0001.use1.cache.amazonaws.com:6379,abortConnect=false,connectTimeout=5000,ssl=true"
  }
}
```

**Azure Cache for Redis:**
```json
{
  "ConnectionStrings": {
    "Redis": "my-cache.redis.cache.windows.net:6380,password=YOUR_ACCESS_KEY,ssl=true,abortConnect=false"
  }
}
```

### Fallback Behavior

If no Redis connection string is configured, the system automatically falls back to an in-memory distributed cache suitable for development and single-instance deployments.

## Using IDistributedCache

Services can inject `IDistributedCache` to cache data across multiple instances:

```csharp
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

public class MetadataRegistry
{
    private readonly IDistributedCache _cache;
    private readonly IMetadataProvider _provider;

    public MetadataRegistry(
        IDistributedCache cache,
        IMetadataProvider provider)
    {
        _cache = cache;
        _provider = provider;
    }

    public async Task<Metadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        // Try to get from distributed cache first
        var cacheKey = $"metadata:{key}";
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);

        if (cached != null)
        {
            return JsonSerializer.Deserialize<Metadata>(cached);
        }

        // Load from provider if not cached
        var metadata = await _provider.LoadMetadataAsync(key, cancellationToken);

        if (metadata != null)
        {
            // Cache for 30 minutes with sliding expiration
            var json = JsonSerializer.Serialize(metadata);
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            };

            await _cache.SetStringAsync(cacheKey, json, options, cancellationToken);
        }

        return metadata;
    }

    public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"metadata:{key}";
        await _cache.RemoveAsync(cacheKey, cancellationToken);
    }
}
```

## Cache Expiration Strategies

### Sliding Expiration
Resets the expiration timer each time the item is accessed:

```csharp
var options = new DistributedCacheEntryOptions
{
    SlidingExpiration = TimeSpan.FromMinutes(30)
};
```

### Absolute Expiration
Item expires at a specific time regardless of access:

```csharp
var options = new DistributedCacheEntryOptions
{
    AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1)
};
```

### Absolute Expiration Relative to Now
Item expires after a fixed duration from when it's cached:

```csharp
var options = new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
};
```

## Binary Data Caching

For binary data (images, tiles, etc.), use the byte array methods:

```csharp
public async Task<byte[]?> GetTileAsync(string key)
{
    var cacheKey = $"tile:{key}";
    return await _cache.GetAsync(cacheKey);
}

public async Task SetTileAsync(string key, byte[] data)
{
    var cacheKey = $"tile:{key}";
    var options = new DistributedCacheEntryOptions
    {
        SlidingExpiration = TimeSpan.FromHours(24)
    };

    await _cache.SetAsync(cacheKey, data, options);
}
```

## Cache Key Naming Conventions

Use prefixes to organize cache keys by domain:

- `metadata:{id}` - Metadata entries
- `tile:{dataset}:{z}:{x}:{y}` - Raster tiles
- `feature:{collection}:{id}` - Feature data
- `catalog:{id}` - STAC catalog items
- `config:{key}` - Configuration values

## Monitoring and Observability

### Cache Statistics

The Redis connection includes built-in metrics that can be monitored:

- Cache hit/miss ratio
- Connection health
- Memory usage
- Eviction count

### Health Checks

Add Redis health check to monitor cache availability:

```csharp
services.AddHealthChecks()
    .AddRedis(
        configuration.GetConnectionString("Redis"),
        name: "redis",
        failureStatus: HealthStatus.Degraded);
```

## Best Practices

1. **Use Appropriate Prefixes**: Always prefix cache keys with the domain/type to avoid collisions
2. **Set Expiration**: Always set an expiration policy to prevent unbounded cache growth
3. **Handle Cache Misses**: Always implement fallback logic when cache returns null
4. **Serialize Efficiently**: Use `System.Text.Json` for better performance than `Newtonsoft.Json`
5. **Use Async Methods**: Always use async cache methods to avoid blocking threads
6. **Consider Cache Size**: Don't cache very large objects; prefer caching smaller, frequently-accessed data
7. **Invalidation Strategy**: Implement cache invalidation when underlying data changes

## Migration from In-Memory Cache

If you're currently using `IMemoryCache`, migration is straightforward:

**Before (IMemoryCache):**
```csharp
var cached = _memoryCache.Get<Metadata>(key);
if (cached == null)
{
    cached = LoadData();
    _memoryCache.Set(key, cached, TimeSpan.FromMinutes(30));
}
```

**After (IDistributedCache):**
```csharp
var cacheKey = $"metadata:{key}";
var cached = await _distributedCache.GetStringAsync(cacheKey);
Metadata? data = null;

if (cached != null)
{
    data = JsonSerializer.Deserialize<Metadata>(cached);
}
else
{
    data = await LoadDataAsync();
    var json = JsonSerializer.Serialize(data);
    await _distributedCache.SetStringAsync(
        cacheKey,
        json,
        new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        });
}
```

## Troubleshooting

### Cache Not Working

1. Check Redis connection string is correct
2. Verify Redis server is running and accessible
3. Check firewall rules allow connection on Redis port (default 6379)
4. Review logs for connection errors

### Performance Issues

1. Monitor cache hit/miss ratio
2. Adjust expiration times based on data change frequency
3. Consider using compression for large cached values
4. Use Redis clustering for high-throughput scenarios

### Connection Timeouts

1. Increase `connectTimeout` in connection string
2. Use `abortConnect=false` to allow retry on connection failures
3. Implement circuit breaker pattern for resilience
4. Monitor network latency between app servers and Redis

## Related Documentation

- [Redis Configuration](https://stackexchange.github.io/StackExchange.Redis/Configuration)
- [ASP.NET Core Distributed Caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
- [Azure Cache for Redis](https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/)
- [AWS ElastiCache for Redis](https://aws.amazon.com/elasticache/redis/)

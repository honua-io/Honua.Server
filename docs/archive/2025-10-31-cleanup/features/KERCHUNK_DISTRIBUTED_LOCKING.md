# Kerchunk Distributed Locking

## Overview

The Kerchunk reference store now supports distributed locking to prevent cache stampede in multi-instance deployments. When multiple application instances request the same kerchunk references simultaneously, distributed locking ensures only one instance generates the metadata while others wait and retrieve the cached result.

## Problem Solved

**Issue #24: Cache Stampede in Multi-Instance Deployments**

Previously, `KerchunkReferenceStore` used in-memory locks (`ConcurrentDictionary<string, SemaphoreSlim>`) which only coordinated within a single process. In multi-instance deployments (e.g., Kubernetes, load-balanced environments), this led to:

- **Duplicate Generation**: Multiple instances simultaneously regenerating the same kerchunk metadata
- **Wasted Resources**: Redundant CPU/IO consumption for identical work
- **Cache Corruption**: Potential race conditions when multiple instances write to shared cache
- **Increased Latency**: Users experience slower response times during cache misses

## Solution Architecture

### Distributed Lock Abstraction

The solution introduces a clean abstraction (`IDistributedLock`) with two implementations:

1. **RedisDistributedLock**: Uses Redis SET NX EX pattern for distributed coordination
2. **InMemoryDistributedLock**: Falls back to local semaphores for single-instance deployments

### Lock Behavior

- **Lock Key Format**: `honua:kerchunk:lock:{cacheKey}`
- **Atomic Acquisition**: Redis SET with NX (not exists) + EX (expiry) options
- **Automatic Expiry**: Prevents deadlocks if a process crashes while holding a lock
- **Double-Check Pattern**: After acquiring lock, verifies cache again (another instance may have generated it)
- **Ownership Verification**: Lua script ensures only the lock owner can release it

## Configuration

### Enable Distributed Locking

Add to your `appsettings.json`:

```json
{
  "honua": {
    "rasterCache": {
      "enableDistributedLocking": true,
      "distributedLockTimeout": "00:05:00",
      "distributedLockExpiry": "00:10:00"
    }
  },
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `EnableDistributedLocking` | `true` | Enable distributed locking (recommended for production) |
| `DistributedLockTimeout` | 5 minutes | Max time to wait for lock acquisition |
| `DistributedLockExpiry` | 10 minutes | Auto-expiry time (prevents deadlocks) |

### Deployment Scenarios

#### Multi-Instance Production (Recommended)

```json
{
  "honua": {
    "rasterCache": {
      "enableDistributedLocking": true
    }
  },
  "ConnectionStrings": {
    "Redis": "redis-cluster.production.svc:6379"
  }
}
```

**Behavior**: Uses Redis-based distributed locks for global coordination

#### Single-Instance Production

```json
{
  "honua": {
    "rasterCache": {
      "enableDistributedLocking": false
    }
  }
}
```

**Behavior**: Uses in-memory locks (no Redis required)

#### Development/Testing

```json
{
  "honua": {
    "rasterCache": {
      "enableDistributedLocking": true
    }
  }
}
```

**Behavior**: Falls back to in-memory locks if Redis is not configured

## Implementation Details

### Redis Lock Pattern (SET NX EX)

```redis
SET honua:kerchunk:lock:{cacheKey} {uniqueValue} NX EX {expirySeconds}
```

- **NX (Not Exists)**: Only set if key doesn't exist (atomic check-and-set)
- **EX (Expiry)**: Automatically delete after expiry seconds
- **Unique Value**: Format: `{machineName}_{processId}_{guid}` for ownership tracking

### Lock Release (Lua Script)

```lua
if redis.call('get', KEYS[1]) == ARGV[1] then
    return redis.call('del', KEYS[1])
else
    return 0
end
```

Only deletes the lock if it still holds the correct value (prevents releasing another instance's lock).

### Double-Check Pattern

```csharp
// 1. Check cache
var cached = await _cacheProvider.GetAsync(cacheKey);
if (cached != null) return cached;

// 2. Acquire distributed lock
await using var lockHandle = await _distributedLock.TryAcquireAsync(...);
if (lockHandle == null) throw new TimeoutException(...);

// 3. Double-check cache (another instance may have generated it)
cached = await _cacheProvider.GetAsync(cacheKey);
if (cached != null) return cached;

// 4. Generate and cache
var refs = await _generator.GenerateAsync(...);
await _cacheProvider.SetAsync(cacheKey, refs);
return refs;
```

## Monitoring & Observability

### OpenTelemetry Instrumentation

Distributed lock operations emit telemetry:

```csharp
Activity: "DistributedLock.Acquire"
Tags:
  - lock.key: "kerchunk:lock:{cacheKey}"
  - lock.timeout_ms: 300000
  - lock.expiry_ms: 600000
  - lock.acquired: true|false
  - lock.wait_time_ms: 1234
```

### Log Messages

**Startup (Redis Available)**:
```
[Information] Using Redis-based distributed locking for kerchunk reference generation
[Information] Kerchunk reference store using distributed locking (LockTimeout=00:05:00, LockExpiry=00:10:00)
```

**Startup (Redis Unavailable)**:
```
[Warning] Redis is not available. Using in-memory locking for kerchunk reference generation.
          Multi-instance deployments may experience cache stampede.
          Configure Redis connection string for distributed coordination.
```

**Lock Acquisition**:
```
[Debug] Attempting to acquire distributed lock for kerchunk generation: SourceUri=s3://bucket/file.zarr
[Debug] Distributed lock acquired for kerchunk generation: SourceUri=s3://bucket/file.zarr, AcquiredAt=2025-10-26T12:34:56Z
```

**Cache Hit After Lock**:
```
[Information] Kerchunk references found in cache after acquiring distributed lock
              (generated by another instance): SourceUri=s3://bucket/file.zarr
```

**Lock Timeout**:
```
[Warning] Timeout waiting for distributed lock for kerchunk generation:
          SourceUri=s3://bucket/file.zarr, Timeout=00:05:00.
          This may indicate slow generation or contention across multiple instances.
```

## Performance Characteristics

### Lock Acquisition Overhead

- **Redis Lock Acquisition**: ~1-5ms (network latency)
- **In-Memory Lock Acquisition**: ~0.01ms (no network)
- **Lock Release**: ~1-5ms (Lua script execution)

### Retry Strategy

- **Polling Interval**: 50ms
- **Max Wait Time**: Configured via `DistributedLockTimeout` (default: 5 minutes)
- **Exponential Backoff**: Not implemented (constant 50ms interval)

### Cache Stampede Prevention

**Before (No Distributed Locking)**:
```
Instance 1: Generate kerchunk refs (60s)
Instance 2: Generate kerchunk refs (60s)  ← Duplicate work
Instance 3: Generate kerchunk refs (60s)  ← Duplicate work
Total: 180s of wasted CPU/IO
```

**After (With Distributed Locking)**:
```
Instance 1: Generate kerchunk refs (60s)
Instance 2: Wait for lock → Retrieve from cache (0.1s)
Instance 3: Wait for lock → Retrieve from cache (0.1s)
Total: 60s (3x efficiency gain)
```

## Testing

### Unit Tests

Verify distributed locking behavior:

```csharp
[Fact]
public async Task GetOrGenerateAsync_WithDistributedLocking_PreventsCacheStampede()
{
    // Simulate multiple concurrent requests
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => _store.GetOrGenerateAsync(sourceUri, options))
        .ToArray();

    var results = await Task.WhenAll(tasks);

    // Verify generator was called exactly once
    _mockGenerator.Verify(
        x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<KerchunkGenerationOptions>(), It.IsAny<CancellationToken>()),
        Times.Once);
}
```

### Integration Tests

Test with real Redis:

```csharp
[Fact]
public async Task DistributedLock_WithRedis_CoordinatesAcrossInstances()
{
    // Use RedisContainerFixture from Testcontainers
    var redis = await RedisContainer.StartAsync();

    // Create two stores with same Redis instance
    var store1 = CreateStoreWithRedis(redis);
    var store2 = CreateStoreWithRedis(redis);

    // Concurrent requests from both stores
    var task1 = store1.GetOrGenerateAsync(sourceUri, options);
    var task2 = store2.GetOrGenerateAsync(sourceUri, options);

    await Task.WhenAll(task1, task2);

    // Verify only one generation occurred
    Assert.Equal(1, generationCount);
}
```

## Migration Guide

### Upgrading from Pre-Distributed-Locking Version

1. **No Breaking Changes**: The feature is backward compatible
2. **Default Behavior**: Distributed locking is enabled by default
3. **Single-Instance Deployments**: Continue working without Redis
4. **Multi-Instance Deployments**: Configure Redis connection string to enable distributed coordination

### Configuration Migration

**Old Configuration** (no changes needed):
```json
{
  "honua": {
    "rasterCache": {
      "zarrEnabled": true
    }
  }
}
```

**New Configuration** (optional, defaults work for most scenarios):
```json
{
  "honua": {
    "rasterCache": {
      "zarrEnabled": true,
      "enableDistributedLocking": true,
      "distributedLockTimeout": "00:05:00",
      "distributedLockExpiry": "00:10:00"
    }
  },
  "ConnectionStrings": {
    "Redis": "redis-cluster:6379"
  }
}
```

## Troubleshooting

### Lock Timeouts

**Symptom**: Frequent timeout errors

**Causes**:
1. Kerchunk generation takes longer than lock expiry
2. High contention (many instances requesting same dataset)
3. Redis unavailable/slow

**Solutions**:
```json
{
  "honua": {
    "rasterCache": {
      "distributedLockTimeout": "00:10:00",  // Increase timeout
      "distributedLockExpiry": "00:15:00"    // Increase expiry
    }
  }
}
```

### Redis Connection Failures

**Symptom**: Application falls back to in-memory locking

**Solutions**:
1. Verify Redis connection string
2. Check Redis server availability
3. Review firewall rules
4. Check Redis authentication

### Performance Degradation

**Symptom**: Slower response times after enabling distributed locking

**Diagnosis**:
1. Check Redis latency: `redis-cli --latency`
2. Review lock acquisition metrics in OpenTelemetry
3. Verify cache hit rate (low cache hits = more lock contention)

**Solutions**:
1. Use Redis Sentinel/Cluster for high availability
2. Pre-warm cache during deployment
3. Increase cache TTL to reduce regeneration

## Best Practices

1. **Production Deployments**:
   - Always configure Redis for multi-instance deployments
   - Use Redis Sentinel or Cluster for high availability
   - Monitor lock acquisition latency via OpenTelemetry

2. **Lock Timeout Tuning**:
   - Set `DistributedLockExpiry` > expected generation time
   - Set `DistributedLockTimeout` > `DistributedLockExpiry`
   - Monitor timeout errors and adjust accordingly

3. **Cache Pre-warming**:
   - Generate kerchunk references during deployment
   - Reduces lock contention during production traffic
   - Use admin endpoints to force regeneration

4. **Single-Instance Deployments**:
   - Disable distributed locking (`enableDistributedLocking: false`)
   - Saves Redis dependency and network overhead

## References

- **Issue**: HIGH_IMPACT_ISSUES_BATCH3.md #24
- **Related**: WfsLockManager (similar Redis lock pattern)
- **Redis SET NX EX**: https://redis.io/commands/set/
- **RedLock Algorithm**: https://redis.io/topics/distlock

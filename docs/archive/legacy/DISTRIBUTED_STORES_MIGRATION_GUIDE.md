# Distributed Stores Migration Guide

This guide helps you migrate from in-memory stores to distributed Redis-backed stores for production deployments.

## Overview

Honua uses several state stores that can operate in two modes:

1. **In-Memory Mode** (Development)
   - Default for development environments
   - State stored in application memory
   - Lost on restart
   - Not suitable for multi-instance deployments

2. **Distributed Mode** (Production)
   - Requires Redis connection
   - State shared across all instances
   - Persists across restarts
   - Supports horizontal scaling

## Affected Components

The following stores have been upgraded with distributed alternatives:

### 1. Process State Store (Honua.Cli.AI)
- **Interface**: `IProcessStateStore`
- **In-Memory**: `InMemoryProcessStateStore`
- **Distributed**: `RedisProcessStateStore`
- **Purpose**: Stores AI process execution state
- **Key Prefix**: `honua:process:`
- **TTL**: 24 hours (configurable)

### 2. Raster Tile Cache Metadata Store (Honua.Server.Core)
- **Interface**: `IRasterTileCacheMetadataStore`
- **In-Memory**: `InMemoryRasterTileCacheMetadataStore`
- **Distributed**: `RedisRasterTileCacheMetadataStore`
- **Purpose**: Tracks raster tile cache statistics and metadata
- **Key Prefix**: `honua:raster:tile:`
- **TTL**: 30 days (configurable)

### 3. Feature Attachment Repository (Honua.Server.Core)
- **Interface**: `IFeatureAttachmentRepository`
- **In-Memory**: `InMemoryFeatureAttachmentRepository`
- **Distributed**: `RedisFeatureAttachmentRepository`
- **Purpose**: Stores feature attachment metadata
- **Key Prefix**: `honua:attachment:`
- **TTL**: 90 days (configurable)

### 4. WFS Lock Manager (Honua.Server.Host)
- **Interface**: `IWfsLockManager`
- **In-Memory**: `InMemoryWfsLockManager`
- **Distributed**: `RedisWfsLockManager`
- **Purpose**: Manages WFS feature locks for editing
- **Key Prefix**: `honua:wfs:lock:`
- **TTL**: Based on lock duration (5 minutes default)

### 5. Vector Search Provider (Honua.Cli.AI)
- **Interface**: `IVectorSearchProvider`
- **In-Memory**: `InMemoryVectorSearchProvider`
- **Distributed**: `PostgresVectorSearchProvider` (already implemented)
- **Purpose**: Vector embeddings for AI search
- **Note**: Already has PostgreSQL-backed implementation

## Migration Steps

### Step 1: Set Up Redis

#### Option A: Docker (Recommended for Testing)

```bash
docker run -d \
  --name honua-redis \
  -p 6379:6379 \
  -v redis-data:/data \
  redis:7-alpine \
  redis-server --appendonly yes
```

#### Option B: Managed Redis Service

For production, use a managed Redis service:
- **AWS**: Amazon ElastiCache for Redis
- **Azure**: Azure Cache for Redis
- **GCP**: Google Cloud Memorystore for Redis
- **Others**: Redis Cloud, Upstash, etc.

### Step 2: Configure Redis Connection

Add Redis configuration to your `appsettings.Production.json`:

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "your-redis-host:6379,password=your-password,ssl=true,abortConnect=false",
    "KeyPrefix": "honua:process:",
    "TtlSeconds": 86400,
    "ValidateConnectionOnStartup": true,
    "ConnectTimeoutMs": 5000,
    "SyncTimeoutMs": 1000
  }
}
```

#### Connection String Format

```
host:port[,host2:port2][,password=xxx][,ssl=true][,abortConnect=false][,connectTimeout=5000]
```

#### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `Enabled` | `false` | Enable Redis (if false, uses in-memory stores) |
| `ConnectionString` | `null` | Redis connection string |
| `KeyPrefix` | `"honua:process:"` | Prefix for all Redis keys |
| `TtlSeconds` | `86400` | Default time-to-live in seconds (24 hours) |
| `ValidateConnectionOnStartup` | `true` | Verify Redis connection on startup |
| `ConnectTimeoutMs` | `5000` | Connection timeout in milliseconds |
| `SyncTimeoutMs` | `1000` | Synchronous operation timeout |

### Step 3: Verify Configuration

#### Check Health Endpoint

```bash
curl http://localhost:5000/health/ready
```

Look for the `redisStores` health check:

```json
{
  "status": "Healthy",
  "checks": {
    "redisStores": {
      "status": "Healthy",
      "description": "Distributed stores using Redis",
      "data": {
        "redis.configured": true,
        "redis.connected": true,
        "redis.latency_ms": 2.5,
        "stores.mode": "distributed",
        "stores.distributed": true
      }
    }
  }
}
```

#### Check Logs

On startup, you should see:

```
info: Honua.Server.Core.Raster.Caching.RedisRasterTileCacheMetadataStore[0]
      Using Redis-backed raster tile cache metadata store

info: Honua.Server.Core.Attachments.RedisFeatureAttachmentRepository[0]
      Using Redis-backed feature attachment repository

info: Honua.Server.Host.Wfs.RedisWfsLockManager[0]
      Using Redis-backed WFS lock manager
```

### Step 4: Test Redis Connectivity

Use the provided health check to verify Redis is working:

```bash
# Check if Redis is accessible
curl http://localhost:5000/health/ready | jq '.checks.redisStores'
```

## Environment-Based Behavior

The stores automatically select the appropriate implementation based on:

1. **Environment**: Development vs. Production/Staging
2. **Redis Availability**: Connection configured and connected

### Automatic Fallback Logic

```csharp
if (!env.IsDevelopment() && redis != null && redis.IsConnected)
{
    // Use distributed Redis store
}
else
{
    // Use in-memory store (with warning in production)
}
```

### Development Environment
- Always uses in-memory stores
- No Redis configuration required
- Fast startup, no external dependencies

### Production/Staging Environment
- **With Redis**: Uses distributed stores
- **Without Redis**: Falls back to in-memory stores with warning

## Monitoring and Health Checks

### Health Check Endpoints

| Endpoint | Purpose | Tags |
|----------|---------|------|
| `/health/live` | Liveness probe | `live` |
| `/health/ready` | Readiness probe | `ready` |
| `/health/startup` | Startup probe | `startup` |

The `redisStores` health check is included in the `ready` probe with tags: `["ready", "distributed"]`

### Health Check States

| State | Condition | Impact |
|-------|-----------|--------|
| **Healthy** | Redis connected and responsive | Full distributed functionality |
| **Degraded** | Redis slow or partially connected | Performance degradation possible |
| **Degraded** | Redis not configured/connected | Using in-memory stores |

### Metrics to Monitor

1. **Redis Latency**: Should be < 10ms
2. **Connection Pool**: Monitor active connections
3. **Memory Usage**: Track Redis memory consumption
4. **Key Count**: Monitor number of keys per store

## Troubleshooting

### Issue: "Redis is not available" Warning

**Symptoms**:
```
warn: Using in-memory stores. This is not suitable for production.
```

**Solutions**:
1. Verify Redis is running: `redis-cli ping`
2. Check connection string in `appsettings.json`
3. Verify network connectivity to Redis host
4. Check Redis password and SSL settings
5. Review Redis logs for connection errors

### Issue: High Redis Latency

**Symptoms**:
```json
{
  "redisStores": {
    "status": "Degraded",
    "data": {
      "redis.latency_ms": 150
    }
  }
}
```

**Solutions**:
1. Check network latency between app and Redis
2. Use Redis in the same region/availability zone
3. Consider Redis cluster for better performance
4. Review Redis memory usage and eviction policy
5. Monitor Redis CPU and network utilization

### Issue: Connection Pool Exhausted

**Symptoms**:
```
RedisConnectionException: It was not possible to connect to the redis server(s)
```

**Solutions**:
1. Increase connection timeout: `connectTimeout=10000`
2. Use Redis connection multiplexer correctly (singleton)
3. Check Redis max clients configuration
4. Monitor concurrent request count
5. Consider Redis cluster for more connections

### Issue: State Lost After Restart

**Symptoms**: Process state or locks disappear after restart

**Causes**:
1. Using in-memory stores (check environment)
2. Redis configured with `appendonly=no` (data not persisted)
3. Redis maxmemory-policy set to volatile-lru or allkeys-lru
4. TTL expired before restart completed

**Solutions**:
1. Verify Redis AOF or RDB persistence is enabled
2. Use appropriate Redis eviction policy
3. Increase TTL for critical data
4. Use Redis in production environment
5. Check Redis persistence settings

## Breaking Changes

### None for Existing Deployments

The migration is **backward compatible**:

- **Development**: No changes required, continues using in-memory stores
- **Production without Redis**: Falls back to in-memory stores with warning
- **Production with Redis**: Automatically uses distributed stores

### Configuration Changes

New configuration section added (optional):

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "...",
    // Additional options...
  }
}
```

If not provided, defaults to in-memory stores.

## Performance Considerations

### In-Memory Stores
- **Pros**: Extremely fast (nanoseconds), no network latency
- **Cons**: Not distributed, lost on restart, memory limited

### Redis Stores
- **Pros**: Distributed, persistent, scalable
- **Cons**: Network latency (1-10ms), requires Redis infrastructure

### Latency Comparison

| Operation | In-Memory | Redis (Local) | Redis (Remote) |
|-----------|-----------|---------------|----------------|
| Get | < 1µs | 1-2ms | 5-10ms |
| Set | < 1µs | 1-2ms | 5-10ms |
| Delete | < 1µs | 0.5-1ms | 2-5ms |

### When to Use Each

**Use In-Memory When**:
- Single instance deployment
- Development/testing
- No need for persistence
- Ultra-low latency required

**Use Redis When**:
- Multi-instance deployment
- Horizontal scaling needed
- State must persist across restarts
- Distributed locks required
- Production environment

## Redis Best Practices

### 1. Connection Management

```csharp
// Correct: Use singleton ConnectionMultiplexer
services.AddSingleton<IConnectionMultiplexer>(sp => {
    var config = ConfigurationOptions.Parse(connectionString);
    return ConnectionMultiplexer.Connect(config);
});

// Incorrect: Don't create new connections per request
// var redis = ConnectionMultiplexer.Connect(connectionString); // BAD!
```

### 2. Key Naming

All stores use prefixed keys:
- `honua:process:{processId}` - Process state
- `honua:raster:tile:{datasetId}/...` - Raster metadata
- `honua:attachment:{serviceId}:{layerId}:{attachmentId}` - Attachments
- `honua:wfs:lock:{lockId}` - WFS locks

### 3. TTL Management

Set appropriate TTLs for each store:
- Short-lived data (locks): 5-60 minutes
- Medium-lived data (processes): 1-7 days
- Long-lived data (attachments): 30-90 days

### 4. Memory Management

Monitor Redis memory usage:
```bash
redis-cli INFO memory
```

Configure eviction policy:
```
maxmemory 2gb
maxmemory-policy allkeys-lru
```

### 5. High Availability

For production:
- Use Redis Sentinel or Redis Cluster
- Enable AOF persistence
- Configure automatic failover
- Use managed Redis service

## Migration Checklist

- [ ] Redis instance provisioned and accessible
- [ ] Connection string configured in `appsettings.Production.json`
- [ ] Redis persistence enabled (AOF or RDB)
- [ ] Environment variable set to `Production` or `Staging`
- [ ] Health checks passing (`/health/ready`)
- [ ] Logs confirm distributed stores in use
- [ ] Redis monitoring configured (latency, memory, connections)
- [ ] Backup and restore procedures tested
- [ ] Failover scenario tested (Redis connection lost)
- [ ] Performance benchmarks completed
- [ ] Documentation updated for operations team

## Support

For issues or questions:
1. Check health endpoint: `/health/ready`
2. Review application logs for warnings
3. Verify Redis connectivity: `redis-cli ping`
4. Check configuration in `appsettings.json`
5. Refer to Redis documentation for advanced configuration

## Additional Resources

- [Redis Documentation](https://redis.io/documentation)
- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)
- [Redis Best Practices](https://redis.io/docs/manual/patterns/)
- [Health Checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)

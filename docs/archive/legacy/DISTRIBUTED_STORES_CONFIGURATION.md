# Distributed Stores Configuration Reference

This document provides a complete configuration reference for Honua's distributed state stores.

## Configuration Overview

Honua supports two storage modes for state management:

1. **In-Memory** (default for development)
2. **Redis** (recommended for production)

The system automatically selects the appropriate store based on:
- Environment (`Development`, `Production`, `Staging`)
- Redis availability and connection status

## Redis Configuration

### Basic Configuration

Add to `appsettings.json` or `appsettings.Production.json`:

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379"
  }
}
```

### Full Configuration

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

### Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Enable Redis stores. If false, uses in-memory stores. |
| `ConnectionString` | string | `null` | Redis connection string. See format below. |
| `KeyPrefix` | string | `"honua:process:"` | Prefix for all Redis keys (used by process store). |
| `TtlSeconds` | integer | `86400` | Default TTL in seconds (24 hours). |
| `ValidateConnectionOnStartup` | boolean | `true` | Verify Redis connection on startup. |
| `ConnectTimeoutMs` | integer | `5000` | Connection timeout in milliseconds. |
| `SyncTimeoutMs` | integer | `1000` | Synchronous operation timeout in milliseconds. |

## Connection String Format

### Basic Format

```
host:port
```

Example:
```
localhost:6379
```

### With Password

```
host:port,password=your-password
```

Example:
```
redis.example.com:6379,password=mySecretPassword123
```

### With SSL/TLS

```
host:port,password=your-password,ssl=true
```

Example:
```
redis.example.com:6380,password=mySecretPassword123,ssl=true
```

### Full Options

```
host:port,password=xxx,ssl=true,abortConnect=false,connectTimeout=5000,syncTimeout=1000
```

### Common Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `password` | string | - | Redis AUTH password |
| `ssl` | boolean | `false` | Use SSL/TLS encryption |
| `abortConnect` | boolean | `true` | Abort on connection failure (set to `false` for resilience) |
| `connectTimeout` | integer | `5000` | Connection timeout in milliseconds |
| `syncTimeout` | integer | `1000` | Sync operation timeout in milliseconds |
| `connectRetry` | integer | `3` | Number of connection retry attempts |
| `keepAlive` | integer | `-1` | Keep-alive interval in seconds |
| `allowAdmin` | boolean | `false` | Allow admin commands |

### Environment-Specific Configurations

#### Development (appsettings.Development.json)

```json
{
  "Redis": {
    "Enabled": false
  }
}
```

Uses in-memory stores by default.

#### Staging (appsettings.Staging.json)

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "staging-redis.example.com:6379,password=${REDIS_PASSWORD},ssl=true",
    "TtlSeconds": 43200
  }
}
```

#### Production (appsettings.Production.json)

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "${REDIS_CONNECTION_STRING}",
    "ValidateConnectionOnStartup": true,
    "ConnectTimeoutMs": 10000,
    "SyncTimeoutMs": 2000
  }
}
```

## Store-Specific Configuration

Each store has its own configuration defaults that can be customized when registering services.

### 1. Process State Store

**Key Prefix**: `honua:process:`
**Default TTL**: 24 hours (86400 seconds)

```csharp
// Custom configuration in Program.cs or Startup.cs
services.Configure<RedisOptions>(options =>
{
    options.KeyPrefix = "honua:process:";
    options.TtlSeconds = 86400; // 24 hours
});
```

**Redis Key Pattern**:
```
honua:process:{processId}
honua:process:active (set of active process IDs)
```

### 2. Raster Tile Cache Metadata Store

**Key Prefix**: `honua:raster:tile:`
**Default TTL**: 30 days (2592000 seconds)

```csharp
// Custom registration
services.AddSingleton<IRasterTileCacheMetadataStore>(sp =>
{
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    var logger = sp.GetRequiredService<ILogger<RedisRasterTileCacheMetadataStore>>();

    return new RedisRasterTileCacheMetadataStore(
        redis,
        logger,
        keyPrefix: "honua:raster:tile:",
        defaultTtl: TimeSpan.FromDays(30)
    );
});
```

**Redis Key Pattern**:
```
honua:raster:tile:{datasetId}/{tileMatrixSetId}/{zoom}/{col}/{row}.{format}
honua:raster:tile:dataset:{datasetId} (set of tile keys)
```

### 3. Feature Attachment Repository

**Key Prefix**: `honua:attachment:`
**Default TTL**: 90 days (7776000 seconds)

```csharp
// Custom registration
services.AddSingleton<IFeatureAttachmentRepository>(sp =>
{
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    var logger = sp.GetRequiredService<ILogger<RedisFeatureAttachmentRepository>>();

    return new RedisFeatureAttachmentRepository(
        redis,
        logger,
        keyPrefix: "honua:attachment:",
        defaultTtl: TimeSpan.FromDays(90)
    );
});
```

**Redis Key Pattern**:
```
honua:attachment:{serviceId}:{layerId}:{attachmentId}
honua:attachment:feature:{serviceId}:{layerId}:{featureId} (set of attachment IDs)
honua:attachment:counter:{serviceId}:{layerId} (counter for object IDs)
```

### 4. WFS Lock Manager

**Key Prefix**: `honua:wfs:lock:`
**TTL**: Based on lock duration (default 5 minutes)

```csharp
// Custom registration
services.AddSingleton<IWfsLockManager>(sp =>
{
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    var logger = sp.GetRequiredService<ILogger<RedisWfsLockManager>>();

    return new RedisWfsLockManager(
        redis,
        logger,
        keyPrefix: "honua:wfs:lock:"
    );
});
```

**Redis Key Pattern**:
```
honua:wfs:lock:{lockId}
honua:wfs:lock:target:{serviceId}:{layerId}:{featureId}
```

## Environment Variables

Use environment variables for sensitive configuration:

### Linux/macOS

```bash
export REDIS_CONNECTION_STRING="redis.example.com:6379,password=myPassword,ssl=true"
export REDIS_ENABLED="true"
```

### Windows (PowerShell)

```powershell
$env:REDIS_CONNECTION_STRING = "redis.example.com:6379,password=myPassword,ssl=true"
$env:REDIS_ENABLED = "true"
```

### Docker

```dockerfile
ENV REDIS_CONNECTION_STRING="redis.example.com:6379,password=myPassword,ssl=true"
ENV REDIS_ENABLED="true"
```

### Kubernetes

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: honua-redis
type: Opaque
stringData:
  connection-string: "redis.example.com:6379,password=myPassword,ssl=true"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  template:
    spec:
      containers:
      - name: honua
        env:
        - name: Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: honua-redis
              key: connection-string
        - name: Redis__Enabled
          value: "true"
```

## Managed Redis Services

### AWS ElastiCache

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "master.honua-redis.abc123.use1.cache.amazonaws.com:6379,ssl=true,abortConnect=false",
    "ConnectTimeoutMs": 10000
  }
}
```

### Azure Cache for Redis

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "honua-redis.redis.cache.windows.net:6380,password=${REDIS_KEY},ssl=true,abortConnect=false",
    "ConnectTimeoutMs": 10000
  }
}
```

### Google Cloud Memorystore

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "10.0.0.3:6379",
    "ConnectTimeoutMs": 10000
  }
}
```

Note: GCP Memorystore doesn't support AUTH by default.

### Redis Cloud (Redis Labs)

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "redis-12345.c123.us-east-1-2.ec2.cloud.redislabs.com:12345,password=${REDIS_PASSWORD},ssl=true",
    "ConnectTimeoutMs": 10000
  }
}
```

## Store Selection Logic

The system automatically selects stores using this logic:

```csharp
if (!environment.IsDevelopment() && redis != null && redis.IsConnected)
{
    // Use Redis-backed distributed store
    return new RedisStore(redis, logger);
}
else
{
    // Use in-memory store (with warning in production)
    if (!environment.IsDevelopment())
    {
        logger.LogWarning("Using in-memory store in production - not suitable for multi-instance deployments");
    }
    return new InMemoryStore();
}
```

### Force In-Memory Stores

To force in-memory stores even in production:

```json
{
  "Redis": {
    "Enabled": false
  }
}
```

### Force Redis Stores in Development

To test Redis stores in development:

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379"
  }
}
```

Or set environment:
```bash
export ASPNETCORE_ENVIRONMENT=Production
```

## Monitoring Configuration

### Health Checks

Health check configuration is automatic. Access health endpoints:

- `/health/live` - Liveness probe
- `/health/ready` - Readiness probe (includes Redis check)
- `/health/startup` - Startup probe

### Logging Configuration

Control logging verbosity:

```json
{
  "Logging": {
    "LogLevel": {
      "Honua.Server.Core.Raster.Caching.RedisRasterTileCacheMetadataStore": "Information",
      "Honua.Server.Core.Attachments.RedisFeatureAttachmentRepository": "Information",
      "Honua.Server.Host.Wfs.RedisWfsLockManager": "Information",
      "Honua.Cli.AI.Services.Processes.RedisProcessStateStore": "Information",
      "StackExchange.Redis": "Warning"
    }
  }
}
```

## Performance Tuning

### Connection Pool

StackExchange.Redis uses a connection multiplexer with built-in pooling. Configure:

```csharp
var config = ConfigurationOptions.Parse(connectionString);
config.ConnectRetry = 3;
config.ConnectTimeout = 10000;
config.SyncTimeout = 2000;
config.KeepAlive = 60;

var redis = ConnectionMultiplexer.Connect(config);
```

### TTL Optimization

Adjust TTLs based on usage patterns:

- **High churn data** (locks): Short TTL (5-60 minutes)
- **Medium persistence** (processes): Medium TTL (1-7 days)
- **Long-term storage** (attachments): Long TTL (30-90 days)

### Memory Management

Configure Redis eviction policy:

```redis
maxmemory 4gb
maxmemory-policy allkeys-lru
```

Monitor memory usage and adjust as needed.

## Troubleshooting

### Enable Verbose Logging

```json
{
  "Logging": {
    "LogLevel": {
      "StackExchange.Redis": "Debug",
      "Honua.Server.Core": "Debug"
    }
  }
}
```

### Test Redis Connection

```bash
# Test connection
redis-cli -h redis.example.com -p 6379 -a myPassword ping

# Check keys
redis-cli -h redis.example.com -p 6379 -a myPassword --scan --pattern "honua:*"

# Monitor commands
redis-cli -h redis.example.com -p 6379 -a myPassword monitor
```

### Common Configuration Errors

1. **Connection timeout**: Increase `ConnectTimeoutMs`
2. **SSL errors**: Verify `ssl=true` and certificate validity
3. **Authentication failure**: Check `password` parameter
4. **Connection refused**: Verify Redis is running and accessible
5. **Memory errors**: Check Redis `maxmemory` and eviction policy

## Security Best Practices

1. **Use SSL/TLS** in production: `ssl=true`
2. **Enable AUTH**: Configure Redis with `requirepass`
3. **Network isolation**: Use VPC/private networks
4. **Rotate passwords**: Regular credential rotation
5. **Monitor access**: Enable Redis audit logging
6. **Use secrets management**: Azure Key Vault, AWS Secrets Manager, etc.

## Example Configurations

### Minimal (Development)

```json
{
  "Redis": {
    "Enabled": false
  }
}
```

### Standard (Production)

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "${REDIS_CONNECTION_STRING}",
    "ValidateConnectionOnStartup": true
  }
}
```

### High Availability (Production)

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "redis-master:6379,redis-replica1:6379,redis-replica2:6379,password=${REDIS_PASSWORD},ssl=true,abortConnect=false,connectRetry=5,connectTimeout=10000",
    "ValidateConnectionOnStartup": true,
    "ConnectTimeoutMs": 10000,
    "SyncTimeoutMs": 2000
  }
}
```

## References

- [StackExchange.Redis Configuration](https://stackexchange.github.io/StackExchange.Redis/Configuration)
- [Redis Configuration](https://redis.io/docs/manual/config/)
- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)

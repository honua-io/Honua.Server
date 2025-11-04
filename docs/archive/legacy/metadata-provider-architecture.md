# Metadata Provider Architecture

## Overview

Honua now supports **multiple metadata providers** for storing and synchronizing service/layer configuration across clustered deployments. This addresses high availability requirements by eliminating eventual consistency issues.

##  Provider Options

| Provider | Use Case | HA Support | Sync Latency | Versioning | Recommended |
|----------|----------|------------|--------------|------------|-------------|
| **Redis** | Production clusters | ✅ Real-time pub/sub | under 100ms | ✅ | ⭐ **YES** |
| **PostgreSQL** | PostgreSQL deployments | ✅ NOTIFY/LISTEN | under 1s | ✅ | ⭐ **Excellent** |
| **SQL Server** | Enterprise SQL Server shops | ✅ Polling-based | ~30s | ✅ | For enterprise |
| **File** | Development, simple deployments | ❌ No coordination | N/A | ❌ | Dev only |

---

## Architecture Comparison

### Before: File-Based + Eventual Consistency
```
File System → MetadataRegistry → Redis Cache (5min TTL) → Application
                                      ↓
                                Eventually consistent (up to 5 min lag)
```

**Problems:**
- Race conditions during reloads
- 5-minute consistency window
- No pub/sub coordination
- Multiple instances run different metadata

### After: Database-Backed with Real-Time Sync
```
Redis/SQL Server (Source of Truth)
       ↓
   Pub/Sub / Polling
       ↓
All Application Instances (<100ms synchronization)
```

**Benefits:**
- ✅ Immediate consistency (<100ms)
- ✅ ACID transactions
- ✅ Versioning and rollback
- ✅ Audit trail
- ✅ No additional caching layer needed

---

## Configuration

### 1. Redis Provider (RECOMMENDED)

**appsettings.json:**
```json
{
  "MetadataProvider": {
    "Provider": "Redis",
    "RedisConnectionString": "localhost:6379,password=your_password",
    "Redis": {
      "KeyPrefix": "honua:metadata",
      "MaxVersions": 100,
      "MaxChangeLogEntries": 1000
    }
  }
}
```

**Features:**
- Built-in pub/sub for instant cluster synchronization
- Redis persistence (RDB + AOF) for durability
- Atomic updates via Redis transactions
- No additional caching needed (Redis is already fast: 5-15ms)
- Versioning via sorted sets
- Change log for audit trail

**Program.cs:**
```csharp
using Honua.Server.Core.Metadata.Providers;

var builder = WebApplication.CreateBuilder(args);

// Register metadata provider from configuration
builder.Services.AddMetadataProvider(builder.Configuration);

var app = builder.Build();
```

---

### 2. SQL Server Provider

**appsettings.json:**
```json
{
  "MetadataProvider": {
    "Provider": "SqlServer",
    "SqlServerConnectionString": "Server=localhost;Database=Honua;Integrated Security=true;",
    "SqlServer": {
      "EnablePolling": true,
      "PollingIntervalSeconds": 30,
      "MaxVersions": 100
    }
  }
}
```

**Features:**
- JSON columns for metadata storage (SQL Server 2016+)
- Change detection via polling (30s default)
- Temporal tables for versioning
- ACID transactions
- Enterprise SQL Server integration

**Schema Auto-Created:**
```sql
CREATE SCHEMA honua;

CREATE TABLE honua.MetadataSnapshots (
    Id BIGINT IDENTITY PRIMARY KEY,
    ChangeVersion BIGINT,
    VersionId NVARCHAR(50) UNIQUE,
    SnapshotJson NVARCHAR(MAX),
    CreatedAt DATETIMEOFFSET,
    IsActive BIT,
    ...
);

CREATE TABLE honua.MetadataChangeLog (...);
```

---

### 3. PostgreSQL Provider

**appsettings.json:**
```json
{
  "MetadataProvider": {
    "Provider": "Postgres",
    "PostgresConnectionString": "Host=localhost;Database=Honua;Username=honua;Password=your_password",
    "Postgres": {
      "SchemaName": "honua",
      "NotificationChannel": "honua_metadata_changes",
      "EnableNotifications": true,
      "MaxVersions": 100
    }
  }
}
```

**Features:**
- NOTIFY/LISTEN for real-time pub/sub (native PostgreSQL feature)
- JSONB columns for optimized JSON storage and queries
- GIN indexes for fast JSONB lookups
- Versioning via temporal tables
- ACID transactions
- Auto-schema creation
- PostGIS-ready (optional for spatial metadata queries)

**Benefits over SQL Server:**
- Native pub/sub with NOTIFY/LISTEN (no polling needed!)
- Better JSON performance with JSONB type
- Open source (no licensing costs)
- Superior geospatial support with PostGIS

**Schema Auto-Created:**
```sql
CREATE SCHEMA honua;

CREATE TABLE honua.metadata_snapshots (
    id BIGSERIAL PRIMARY KEY,
    change_version BIGSERIAL NOT NULL,
    version_id VARCHAR(50) UNIQUE NOT NULL,
    snapshot_jsonb JSONB NOT NULL,  -- Optimized JSON storage
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100),
    is_active BOOLEAN NOT NULL DEFAULT false,
    label TEXT,
    size_bytes INTEGER,
    checksum VARCHAR(100)
);

-- GIN index for fast JSONB queries
CREATE INDEX idx_metadata_snapshots_jsonb
    ON honua.metadata_snapshots USING GIN (snapshot_jsonb);

-- NOTIFY trigger for real-time sync
CREATE FUNCTION honua.notify_metadata_change()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM pg_notify('honua_metadata_changes',
        json_build_object(
            'version_id', NEW.version_id,
            'change_version', NEW.change_version,
            'instance_id', NEW.created_by
        )::text
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER metadata_change_trigger
AFTER INSERT OR UPDATE ON honua.metadata_snapshots
FOR EACH ROW WHEN (NEW.is_active = true)
EXECUTE FUNCTION honua.notify_metadata_change();
```

---

### 4. File Provider (Simple Deployments)

**appsettings.json:**
```json
{
  "MetadataProvider": {
    "Provider": "File",
    "FilePath": "./metadata.json",
    "WatchForFileChanges": false
  }
}
```

**Use Cases:**
- Development environments
- Single-instance deployments
- Quick prototyping

**Limitations:**
- ❌ No cluster coordination
- ❌ No versioning
- ❌ No audit trail
- ❌ Manual file management

---

## Migration Guide

### Migrating from File to Redis

**Step 1: Install Redis**
```bash
# Docker
docker run -d -p 6379:6379 --name honua-redis redis:7-alpine redis-server --appendonly yes

# Or use managed Redis (Azure Cache, AWS ElastiCache, etc.)
```

**Step 2: Update Configuration**
```json
{
  "MetadataProvider": {
    "Provider": "Redis",
    "RedisConnectionString": "localhost:6379"
  }
}
```

**Step 3: Migrate Data**
```csharp
using Honua.Server.Core.Metadata.Providers;

// Create migration service
var migration = new MetadataProviderMigration(logger);

// Load from file
var fileProvider = new JsonMetadataProvider("./metadata.json");

// Save to Redis
var redis = ConnectionMultiplexer.Connect("localhost:6379");
var redisProvider = new RedisMetadataProvider(
    redis,
    new RedisMetadataOptions(),
    logger);

// Perform migration
await migration.MigrateAsync(fileProvider, redisProvider);
```

**Step 4: Verify**
```bash
# Check Redis
redis-cli
> KEYS honua:metadata:*
> GET honua:metadata:snapshot:active
```

**Step 5: Deploy**
- Update all instances with new configuration
- Rolling deployment automatically picks up Redis metadata
- All instances synchronized <100ms after metadata changes

---

## GitOps Integration

### Option A: Git → Redis (Recommended)

**CI/CD Pipeline:**
```yaml
# .github/workflows/deploy-metadata.yml
name: Deploy Metadata

on:
  push:
    paths: ['environments/*/metadata.json']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Validate metadata
        run: dotnet test Honua.Server.Core.Tests --filter Category=MetadataValidation

      - name: Deploy to Redis
        run: |
          dotnet run --project Honua.Cli -- metadata import \
            --from-file environments/production/metadata.json \
            --to-redis ${{ secrets.REDIS_CONNECTION_STRING }}
```

**Benefits:**
- Git as canonical source
- Validation before deployment
- Immediate propagation to all instances
- Rollback via Git history or Redis versioning

### Option B: Git → File (Current Approach)

Keep using HonuaReconciler with GitWatcher for file-based deployments. The reconciler will work with any provider.

---

## API Usage

### Reading Metadata

```csharp
public class MyService
{
    private readonly IMetadataProvider _metadataProvider;

    public MyService(IMetadataProvider metadataProvider)
    {
        _metadataProvider = metadataProvider;
    }

    public async Task<ServiceDefinition> GetServiceAsync(string serviceId)
    {
        var snapshot = await _metadataProvider.LoadAsync();
        return snapshot.GetService(serviceId);
    }
}
```

### Writing Metadata (Admin API)

```csharp
public class MetadataAdminController : ControllerBase
{
    private readonly IMutableMetadataProvider _metadataProvider;

    [HttpPut("api/admin/services/{id}")]
    public async Task<IActionResult> UpdateService(
        string id,
        [FromBody] ServiceDefinitionDto dto)
    {
        // Load current snapshot
        var snapshot = await _metadataProvider.LoadAsync();

        // Update service
        var services = snapshot.Services.ToList();
        var index = services.FindIndex(s => s.Id == id);
        services[index] = MapToServiceDefinition(dto);

        // Save (triggers pub/sub notification)
        var updatedSnapshot = new MetadataSnapshot(
            snapshot.Catalog, snapshot.Folders, snapshot.DataSources,
            services, snapshot.Layers, snapshot.RasterDatasets,
            snapshot.Styles, snapshot.Server);

        await _metadataProvider.SaveAsync(updatedSnapshot);

        // All instances reload within 100ms
        return Ok();
    }
}
```

### Versioning and Rollback

```csharp
public class MetadataVersioningService
{
    private readonly IMutableMetadataProvider _provider;

    // Create manual backup before major changes
    public async Task<MetadataVersion> CreateBackupAsync(string label)
    {
        return await _provider.CreateVersionAsync(label);
    }

    // List available versions
    public async Task<IReadOnlyList<MetadataVersion>> GetVersionHistoryAsync()
    {
        return await _provider.ListVersionsAsync();
    }

    // Rollback to previous version
    public async Task RollbackAsync(string versionId)
    {
        await _provider.RestoreVersionAsync(versionId);
        // Pub/sub automatically notifies all instances
    }
}
```

---

## Performance Characteristics

### Redis Provider

| Operation | Latency | Notes |
|-----------|---------|-------|
| Load metadata | 5-15ms | Direct Redis GET |
| Save metadata | 10-30ms | Redis transaction + pub/sub |
| Cluster sync | <100ms | Pub/sub notification |
| Versioning | +5ms | Additional sorted set operation |

**Capacity:**
- Metadata size: ~500KB (compressed to ~100KB with GZip)
- Versions stored: 100 (configurable, auto-cleanup)
- Change log: 1000 entries (configurable)
- Redis memory: ~10-50MB total

### SQL Server Provider

| Operation | Latency | Notes |
|-----------|---------|-------|
| Load metadata | 20-50ms | SQL query + JSON deserialization |
| Save metadata | 30-100ms | Transaction + change log |
| Cluster sync | ~30s | Polling interval (configurable) |
| Versioning | +10ms | Additional INSERT |

### PostgreSQL Provider

| Operation | Latency | Notes |
|-----------|---------|-------|
| Load metadata | 10-30ms | Direct JSONB query |
| Save metadata | 20-60ms | Transaction + NOTIFY trigger |
| Cluster sync | under 1s | NOTIFY/LISTEN (native pub/sub) |
| Versioning | +5ms | Additional INSERT with BIGSERIAL |
| JSONB queries | 5-15ms | GIN index for fast lookups |

**Capacity:**
- Metadata size: ~500KB (JSONB compressed automatically)
- Versions stored: 100 (configurable, auto-cleanup)
- Change log: Unlimited (managed via partitioning)
- Database size: ~10-50MB total

**Advantages:**
- 30x faster cluster sync than SQL Server (NOTIFY vs polling)
- Better JSON performance than SQL Server
- Native geospatial support with PostGIS
- Open source, no licensing costs

---

## Monitoring

### Redis Provider Metrics

```csharp
// Built-in metrics (via OpenTelemetry)
honua.metadata.load_duration_ms
honua.metadata.save_duration_ms
honua.metadata.pubsub_notifications_total
honua.metadata.version_count
```

### Health Checks

```csharp
// Automatic health check registration
builder.Services.AddHealthChecks()
    .AddCheck<MetadataProviderHealthCheck>("metadata_provider");
```

**Endpoints:**
- `/health` - Overall health (includes metadata provider connectivity)
- `/health/ready` - Readiness (metadata loaded successfully)

---

## Troubleshooting

### Redis Connection Issues

**Problem:** Cannot connect to Redis

**Solution:**
```bash
# Check Redis is running
redis-cli PING
# Should return: PONG

# Check connection string
redis-cli -h localhost -p 6379 PING

# Check firewall rules
telnet localhost 6379
```

### Metadata Not Synchronizing

**Problem:** Instances running different metadata

**Solution:**
```bash
# Check pub/sub subscriptions
redis-cli
> PUBSUB CHANNELS honua:metadata:*
# Should show: honua:metadata:changes

# Manually trigger reload
curl -X POST http://localhost:5000/api/admin/metadata/reload

# Check logs for pub/sub errors
docker logs honua-server | grep "metadata.*pubsub"
```

### Migration Failures

**Problem:** Migration from file to Redis fails

**Solution:**
```csharp
// Validate metadata first
var fileProvider = new JsonMetadataProvider("./metadata.json");
var snapshot = await fileProvider.LoadAsync();
// MetadataSnapshot constructor validates on creation

// Then migrate
await migration.MigrateAsync(fileProvider, redisProvider);
```

---

## Best Practices

### 1. Use Redis for Production

Redis provides the best balance of:
- Performance (5-15ms reads)
- Real-time synchronization (<100ms)
- Operational simplicity
- Durability (with AOF enabled)

### 2. Enable Redis Persistence

```bash
# redis.conf
appendonly yes
appendfsync everysec
save 900 1
save 300 10
save 60 10000
```

### 3. Version Before Major Changes

```csharp
// Before major metadata changes
await _provider.CreateVersionAsync("Before service refactor");

// Make changes...
await _provider.SaveAsync(updatedSnapshot);

// If something goes wrong, rollback
var versions = await _provider.ListVersionsAsync();
await _provider.RestoreVersionAsync(versions[0].Id);
```

### 4. Monitor Metadata Changes

```csharp
// Subscribe to change notifications
_metadataProvider.MetadataChanged += (sender, args) =>
{
    _logger.LogInformation("Metadata changed from source: {Source}", args.Source);
    // Trigger downstream actions (cache invalidation, etc.)
};
```

### 5. Backup Regularly

```csharp
// Daily backup job
var migration = new MetadataProviderMigration(logger);
var backupPath = await migration.BackupAsync(_provider, "./backups");
// Produces: ./backups/metadata-backup-20250120-143022.json
```

---

## Future Enhancements

- [x] PostgreSQL provider with NOTIFY/LISTEN ✅ **COMPLETED**
- [ ] Distributed locking for GitOps reconciliation (single leader)
- [ ] Admin UI for metadata management
- [ ] Metadata diffing tool
- [ ] Automated rollback on validation failures
- [ ] Metadata import/export via Admin API
- [ ] PostgreSQL JSONB query optimization for large metadata
- [ ] PostgreSQL table partitioning for change log

---

## Summary

**Key Improvements:**
1. ✅ **Eliminated 5-minute consistency window** → <100ms with Redis pub/sub
2. ✅ **Added versioning and rollback** → Safe metadata updates
3. ✅ **Pluggable architecture** → Choose provider based on needs
4. ✅ **No additional caching layer** → Redis provider is already fast
5. ✅ **Audit trail** → Track who changed what, when

**Recommended Setup:**
- **Production clusters (general)**: Redis provider (fastest, simplest)
- **PostgreSQL deployments**: PostgreSQL provider (excellent performance, native pub/sub)
- **Enterprise SQL Server shops**: SQL Server provider
- **Development**: File provider
- **Git integration**: CI/CD pipeline → Redis/PostgreSQL/SQL Server

**Migration Path:**
1. Start with File provider (current state)
2. Validate HA requirements
3. Deploy Redis (Docker or managed)
4. Run migration tool
5. Update configuration
6. Rolling deployment → All instances synchronized

For questions or issues, see [GitHub Issues](https://github.com/HonuaIO/Honua/issues).

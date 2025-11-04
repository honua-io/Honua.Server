# Honua Build Orchestrator - Migration Quick Start

## TL;DR

```bash
# 1. Set connection details
export PG_HOST=localhost
export PG_PORT=5432
export PG_DATABASE=honua
export PG_USER=postgres

# 2. Run migrations
cd src/Honua.Server.Core/Data/Migrations
./apply-migrations.sh

# 3. Verify
psql -h $PG_HOST -U $PG_USER -d $PG_DATABASE -c "SELECT version, name FROM schema_migrations ORDER BY version;"
```

## What Gets Created

### Tables (17 total)

**Core System:**
- `customers` - Customer/tenant records
- `licenses` - License management with quotas
- `registry_credentials` - Encrypted container registry credentials
- `build_cache_registry` - Shared build cache
- `build_access_log` - Cache access tracking

**Build Queue:**
- `build_queue` - Pending builds with priority
- `build_results` - Build outcomes and metrics
- `build_artifacts` - Container images, SBOMs, reports
- `build_events` - Build lifecycle events
- `build_metrics` - Time-series performance metrics

**AI Intake:**
- `conversations` - AI chat sessions
- `conversation_messages` - Individual messages
- `conversation_intents` - Detected user intents
- `build_manifests` - Generated build configurations
- `requirement_extractions` - AI extraction tracking

**System:**
- `schema_migrations` - Migration tracking

### Indexes (70+ total)

Comprehensive indexes for:
- Fast cache lookups
- Queue processing
- Time-series queries
- Analytics
- Full-text search (GIN)

### Functions (15 total)

- `update_cache_stats()` - Cache hit tracking
- `record_cache_access()` - Access logging
- `get_next_build_for_worker()` - Worker assignment
- `update_build_progress()` - Progress tracking
- `expire_old_conversations()` - Cleanup
- `revoke_expired_credentials()` - Security
- `get_cache_hit_rate()` - Analytics
- `get_license_quota_usage()` - Quota checking

### Views (8 total)

- `active_builds` - Real-time build queue
- `build_performance_summary` - Performance metrics
- `customer_build_usage` - Customer statistics
- `active_conversations` - Active AI sessions
- `conversation_performance_summary` - AI metrics
- `intent_analytics` - Intent tracking
- `extraction_accuracy` - AI accuracy

## Common Commands

### Check Current Version

```bash
psql -h localhost -U postgres -d honua -c \
  "SELECT version FROM schema_migrations ORDER BY version DESC LIMIT 1;"
```

### List Applied Migrations

```bash
psql -h localhost -U postgres -d honua -c \
  "SELECT version, name, applied_at, execution_time_ms || 'ms' as duration
   FROM schema_migrations ORDER BY version;"
```

### Verify Migrations Only (No Apply)

```bash
./apply-migrations.sh --verify-only
```

### Apply Specific Migration Manually

```bash
psql -h localhost -U postgres -d honua -f 001_InitialSchema.sql
```

## Usage Examples

### C# - Apply Migrations

```csharp
var runner = new MigrationRunner(connectionString, logger);

// Validate connection
if (!await runner.ValidateConnectionAsync())
{
    throw new Exception("Database connection failed");
}

// Apply all pending migrations
var result = await runner.ApplyMigrationsAsync();

if (!result.Success)
{
    foreach (var error in result.Errors)
    {
        logger.LogError(error);
    }
    throw new Exception("Migration failed");
}

logger.LogInformation($"Applied {result.AppliedMigrations.Count} migrations");
```

### C# - Check Status

```csharp
var currentVersion = await runner.GetCurrentVersionAsync();
var applied = await runner.GetAppliedMigrationsAsync();

Console.WriteLine($"Current version: {currentVersion}");
Console.WriteLine($"Applied migrations: {applied.Count}");

foreach (var migration in applied)
{
    Console.WriteLine($"  {migration.Version}: {migration.Name} ({migration.ExecutionTimeMs}ms)");
}
```

### PostgreSQL - Queue a Build

```sql
INSERT INTO build_queue (
    customer_id,
    manifest_path,
    manifest_hash,
    manifest_json,
    priority,
    architecture
) VALUES (
    'customer-123',
    '/manifests/honua-build.json',
    'abc123def456',
    '{"modules": ["ogc-api", "stac"], "cloud": "aws"}'::jsonb,
    8, -- high priority
    'amd64'
);
```

### PostgreSQL - Get Next Build for Worker

```sql
SELECT * FROM get_next_build_for_worker(
    'worker-001',
    'worker-host-01.example.com',
    'amd64'
);
```

### PostgreSQL - Check Cache Hit Rate

```sql
-- Overall cache hit rate
SELECT * FROM get_cache_hit_rate(NULL, 30);

-- Specific customer
SELECT * FROM get_cache_hit_rate('customer-123', 7);
```

### PostgreSQL - Monitor Active Builds

```sql
SELECT
    customer_id,
    status,
    priority,
    progress_percent,
    current_step,
    elapsed_seconds,
    remaining_seconds
FROM active_builds
ORDER BY priority DESC, queued_at ASC;
```

### PostgreSQL - Check License Quotas

```sql
SELECT * FROM get_license_quota_usage('customer-123');
```

## Monitoring Queries

### Build Queue Depth

```sql
SELECT
    status,
    COUNT(*) as count
FROM build_queue
GROUP BY status
ORDER BY status;
```

### Recent Build Performance

```sql
SELECT
    DATE(completed_at) as date,
    COUNT(*) as total_builds,
    COUNT(*) FILTER (WHERE br.success) as successful,
    ROUND(AVG(br.actual_duration_seconds)) as avg_duration_sec,
    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY br.actual_duration_seconds) as p95_duration
FROM build_queue bq
JOIN build_results br ON bq.id = br.build_queue_id
WHERE completed_at >= NOW() - INTERVAL '7 days'
GROUP BY DATE(completed_at)
ORDER BY date DESC;
```

### Top Cached Builds

```sql
SELECT
    manifest_hash,
    tier,
    cache_hit_count,
    total_customers_served,
    modules,
    cloud_provider,
    first_built_at,
    last_accessed_at
FROM build_cache_registry
WHERE deleted_at IS NULL
ORDER BY cache_hit_count DESC
LIMIT 20;
```

### Conversation Analytics

```sql
SELECT
    DATE(started_at) as date,
    COUNT(*) as total_conversations,
    COUNT(*) FILTER (WHERE status = 'completed') as completed,
    COUNT(*) FILTER (WHERE build_manifest_id IS NOT NULL) as with_manifest,
    ROUND(AVG(total_messages)) as avg_messages,
    ROUND(AVG(total_cost_usd)::numeric, 4) as avg_cost
FROM conversations
WHERE started_at >= NOW() - INTERVAL '7 days'
GROUP BY DATE(started_at)
ORDER BY date DESC;
```

## Maintenance

### Run Cleanup Functions

```sql
-- Expire old conversations (24+ hours inactive)
SELECT * FROM expire_old_conversations(24);

-- Revoke invalid credentials (7+ days)
SELECT * FROM revoke_expired_credentials();

-- Expire licenses
SELECT * FROM expire_licenses();

-- Handle build timeouts
SELECT * FROM handle_build_timeouts();

-- Cleanup old logs (90+ days)
SELECT * FROM cleanup_old_build_logs(90);

-- Purge soft-deleted records (30+ days)
SELECT * FROM purge_soft_deleted_records(30);
```

### Check Table Sizes

```sql
SELECT
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size,
    pg_total_relation_size(schemaname||'.'||tablename) AS size_bytes
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY size_bytes DESC;
```

### Vacuum Tables

```sql
VACUUM ANALYZE build_access_log;
VACUUM ANALYZE build_events;
VACUUM ANALYZE conversation_messages;
VACUUM ANALYZE build_metrics;
```

## Troubleshooting

### Migration Failed - What Now?

1. Check error message in output
2. Review PostgreSQL logs: `tail -f /var/log/postgresql/postgresql-*.log`
3. If migration partially applied, manually clean up:
   ```sql
   -- Delete failed migration record
   DELETE FROM schema_migrations WHERE version = 'XXX';

   -- Manually drop any created objects
   DROP TABLE IF EXISTS problematic_table CASCADE;
   ```
4. Fix SQL and re-run migration

### Connection Failed

```bash
# Test connection
psql -h localhost -U postgres -d honua -c "SELECT version();"

# Check PostgreSQL is running
sudo systemctl status postgresql

# Check firewall
sudo ufw status
```

### Slow Queries

```sql
-- Enable query logging
ALTER DATABASE honua SET log_min_duration_statement = 1000;

-- Check slow queries
SELECT
    query,
    calls,
    total_exec_time,
    mean_exec_time,
    max_exec_time
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 20;
```

## Next Steps

1. Review schema in detail: See `README.md`
2. Set up background jobs: See pg_cron examples in `005_Functions.sql`
3. Configure monitoring: Set up alerts for queue depth, cache hit rate, etc.
4. Tune performance: Review indexes, consider partitioning high-volume tables
5. Implement backup strategy: pg_dump, continuous archiving, or managed service

## Support

For detailed documentation, see:
- `README.md` - Comprehensive documentation
- `001_InitialSchema.sql` - Core schema with comments
- `005_Functions.sql` - Function documentation

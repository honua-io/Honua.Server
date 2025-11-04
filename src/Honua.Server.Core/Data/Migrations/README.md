# Honua Build Orchestrator - Database Migrations

This directory contains PostgreSQL database migrations for the Honua build orchestrator system.

## Overview

The migration system manages database schema for:

1. **Build Cache Registry** - Shared build cache across customers
2. **Build Access Logs** - Cache usage tracking and analytics
3. **Licenses** - Customer license management with tiers and quotas
4. **Registry Credentials** - Encrypted container registry credentials
5. **Build Queue** - Build job queue with priority and progress tracking
6. **Conversations** - AI intake conversation management
7. **Build Manifests** - Generated build configurations
8. **Customer Metadata** - Customer/tenant information

## Migration Files

Migrations are applied in order:

| File | Description |
|------|-------------|
| `001_InitialSchema.sql` | Core tables: customers, licenses, registry_credentials, build_cache_registry, build_access_log |
| `002_BuildQueue.sql` | Build queue tables: build_queue, build_results, build_artifacts, build_events, build_metrics |
| `003_IntakeSystem.sql` | AI intake tables: conversations, conversation_messages, build_manifests, requirement_extractions |
| `004_Indexes.sql` | Performance indexes for common query patterns |
| `005_Functions.sql` | Database functions, triggers, and stored procedures |

## Prerequisites

- PostgreSQL 15 or later
- Extensions: `pgcrypto`, `uuid-ossp`
- Database user with DDL permissions

## Running Migrations

### Using MigrationRunner (C#)

```csharp
using Honua.Server.Core.Data.Migrations;
using Microsoft.Extensions.Logging;

var connectionString = "Host=localhost;Database=honua;Username=honua_user;Password=...";
var logger = loggerFactory.CreateLogger<MigrationRunner>();

var runner = new MigrationRunner(connectionString, logger);

// Validate connection
var isValid = await runner.ValidateConnectionAsync();
if (!isValid)
{
    Console.WriteLine("Database connection failed!");
    return;
}

// Check current version
var currentVersion = await runner.GetCurrentVersionAsync();
Console.WriteLine($"Current schema version: {currentVersion ?? "None"}");

// Apply pending migrations
var result = await runner.ApplyMigrationsAsync();

if (result.Success)
{
    Console.WriteLine($"Successfully applied {result.AppliedMigrations.Count} migrations");
    foreach (var migration in result.AppliedMigrations)
    {
        Console.WriteLine($"  - {migration.Version}: {migration.Name} ({migration.ExecutionTimeMs}ms)");
    }
}
else
{
    Console.WriteLine("Migration failed!");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

### Manual Application (psql)

```bash
# Connect to database
psql -h localhost -U honua_user -d honua

# Apply migrations in order
\i 001_InitialSchema.sql
\i 002_BuildQueue.sql
\i 003_IntakeSystem.sql
\i 004_Indexes.sql
\i 005_Functions.sql

# Verify migrations
SELECT version, name, applied_at, execution_time_ms
FROM schema_migrations
ORDER BY version;
```

## Schema Overview

### Core Entities

#### customers
- Customer/tenant records
- Subscription status and tier tracking
- Soft delete support

#### licenses
- License key management (encrypted)
- Feature flags (JSONB)
- Quota limits (builds/month, registries, concurrent builds)
- Trial and expiration tracking

#### registry_credentials
- Encrypted container registry credentials
- Support for DockerHub, ECR, GCR, ACR, Harbor, Quay
- Credential validation tracking

#### build_cache_registry
- Shared build cache across customers
- Manifest hash-based deduplication
- Cache hit tracking and statistics
- Tier-based access control

### Build Queue System

#### build_queue
- Priority-based job queue (1-10 scale)
- Worker assignment with SKIP LOCKED
- Progress tracking (percent, current step)
- Timeout and retry logic

#### build_results
- Build outcomes (success/failure)
- Performance metrics (CPU, memory, duration)
- Container image details (digest, size, layers)
- Build logs (stdout/stderr)

#### build_artifacts
- Container images
- SBOMs (Software Bill of Materials)
- Security scan reports
- Test reports

### AI Intake System

#### conversations
- AI conversation sessions
- Token usage and cost tracking
- User satisfaction scores
- Conversation status lifecycle

#### conversation_messages
- Individual messages (user, assistant, system, function)
- Token tracking per message
- Function/tool call support

#### build_manifests
- Generated build configurations
- Validation status
- Deployment tracking

## Database Functions

### Cache Management

- `update_cache_stats()` - Automatically updates cache hit counts
- `record_cache_access()` - Records cache access with statistics

### Build Queue

- `get_next_build_for_worker()` - Atomically assigns builds to workers
- `update_build_progress()` - Updates real-time build progress
- `handle_build_timeouts()` - Marks timed-out builds as failed
- `retry_failed_build()` - Requeues failed builds

### Conversation Management

- `update_conversation_stats()` - Updates message counts and token usage
- `expire_old_conversations()` - Marks inactive conversations as abandoned

### Cleanup & Maintenance

- `expire_old_conversations()` - Abandons conversations inactive for 24+ hours
- `revoke_expired_credentials()` - Revokes invalid credentials after 7 days
- `expire_licenses()` - Marks expired licenses
- `cleanup_old_build_logs()` - Truncates logs beyond retention period (90 days)
- `purge_soft_deleted_records()` - Permanently deletes soft-deleted records (30 days)

### Analytics

- `get_cache_hit_rate()` - Calculates cache hit rate by customer
- `get_license_quota_usage()` - Returns quota usage for a customer

## Views

### Monitoring Views

- `active_builds` - Current queued and building items
- `build_performance_summary` - Daily build performance metrics
- `customer_build_usage` - Per-customer build statistics
- `active_conversations` - Currently active AI conversations
- `conversation_performance_summary` - Conversation metrics by model
- `intent_analytics` - Intent detection analytics
- `extraction_accuracy` - AI extraction accuracy metrics

## Indexes

The schema includes comprehensive indexes for:

- Fast cache lookups (manifest hash + tier)
- Queue processing (priority + queued_at)
- Time-series queries (build logs, events, metrics)
- Analytics (customer usage, performance)
- Full-text search (GIN indexes on JSONB columns)
- Partial indexes for active records only

See `004_Indexes.sql` for complete index documentation.

## Performance Considerations

### High-Volume Tables

These tables expect high insert rates and may benefit from partitioning:

- `build_access_log` - Partition by `accessed_at` (monthly)
- `build_events` - Partition by `event_timestamp` (monthly)
- `conversation_messages` - Partition by `created_at` (monthly)
- `build_metrics` - Partition by `metric_timestamp` (daily)

### Autovacuum Tuning

Consider adjusting autovacuum settings for high-churn tables:

```sql
ALTER TABLE build_access_log SET (autovacuum_vacuum_scale_factor = 0.05);
ALTER TABLE build_events SET (autovacuum_vacuum_scale_factor = 0.05);
ALTER TABLE conversation_messages SET (autovacuum_vacuum_scale_factor = 0.05);
```

### Index Maintenance

Monitor index bloat and reindex periodically:

```sql
-- Check index bloat
SELECT
    schemaname,
    tablename,
    indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
ORDER BY pg_relation_size(indexrelid) DESC
LIMIT 20;

-- Reindex (use CONCURRENTLY on production)
REINDEX INDEX CONCURRENTLY idx_build_cache_manifest_hash;
```

## Background Jobs (pg_cron)

If you have `pg_cron` installed, you can schedule maintenance tasks:

```sql
-- Install pg_cron extension
CREATE EXTENSION IF NOT EXISTS pg_cron;

-- Schedule cleanup jobs
SELECT cron.schedule('expire-conversations', '0 */6 * * *', 'SELECT expire_old_conversations(24);');
SELECT cron.schedule('revoke-credentials', '0 2 * * *', 'SELECT revoke_expired_credentials();');
SELECT cron.schedule('expire-licenses', '0 1 * * *', 'SELECT expire_licenses();');
SELECT cron.schedule('handle-timeouts', '* * * * *', 'SELECT handle_build_timeouts();');
SELECT cron.schedule('cleanup-logs', '0 3 * * 0', 'SELECT cleanup_old_build_logs(90);');
SELECT cron.schedule('purge-deleted', '0 4 * * 0', 'SELECT purge_soft_deleted_records(30);');
```

## Security

### Encryption

- License keys are encrypted and hashed (SHA-256)
- Registry credentials use AES-256 encryption
- Encryption key IDs reference external KMS/vault

### Permissions

Grant minimal permissions to application user:

```sql
-- Create application user
CREATE USER honua_app_user WITH PASSWORD 'secure_password';

-- Grant permissions
GRANT CONNECT ON DATABASE honua TO honua_app_user;
GRANT USAGE ON SCHEMA public TO honua_app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO honua_app_user;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO honua_app_user;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO honua_app_user;

-- Revoke dangerous operations
REVOKE TRUNCATE ON ALL TABLES IN SCHEMA public FROM honua_app_user;
```

## Monitoring

### Key Metrics to Monitor

- Build queue depth: `SELECT COUNT(*) FROM build_queue WHERE status = 'queued'`
- Cache hit rate: `SELECT * FROM get_cache_hit_rate(NULL, 7)`
- Active builds: `SELECT COUNT(*) FROM active_builds`
- License quota usage: `SELECT * FROM get_license_quota_usage('customer-123')`
- Database size: `SELECT pg_size_pretty(pg_database_size('honua'))`

### Health Checks

```sql
-- Check for stuck builds
SELECT id, customer_id, worker_id, started_at, current_step
FROM build_queue
WHERE status = 'building'
  AND started_at < NOW() - INTERVAL '1 hour';

-- Check license expirations (next 7 days)
SELECT customer_id, tier, expires_at
FROM licenses
WHERE status = 'active'
  AND expires_at < NOW() + INTERVAL '7 days';

-- Check invalid credentials
SELECT customer_id, registry_name, validation_status, last_validated_at
FROM registry_credentials
WHERE is_active = TRUE
  AND validation_status = 'invalid';
```

## Troubleshooting

### Migration Checksum Mismatch

If you encounter a checksum mismatch error:

```
Migration 001 checksum mismatch! Expected: abc123..., Found: def456...
```

This means the migration file was modified after it was applied. **Never modify applied migrations**. Instead:

1. Create a new migration file (e.g., `006_FixSchema.sql`)
2. Include the necessary changes in the new migration
3. Apply the new migration

### Failed Migration

If a migration fails mid-execution:

1. Check the error message in the migration result
2. Fix the SQL in the migration file
3. Manually delete the failed entry from `schema_migrations` table
4. Re-run the migration

```sql
DELETE FROM schema_migrations WHERE version = '003';
```

### Performance Issues

If queries are slow:

1. Check index usage: `EXPLAIN ANALYZE SELECT ...`
2. Review autovacuum: `SELECT * FROM pg_stat_user_tables`
3. Consider partitioning high-volume tables
4. Tune PostgreSQL settings (shared_buffers, effective_cache_size, work_mem)

## Support

For questions or issues:
- Review PostgreSQL logs: `/var/log/postgresql/`
- Check migration execution: `SELECT * FROM schema_migrations`
- Examine table sizes: `SELECT * FROM pg_stat_user_tables`

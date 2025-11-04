# Soft Delete Migration Guide

## Quick Start

This guide helps you migrate your Honua deployment to use the soft delete system for GDPR-compliant data management.

**Estimated Time**: 30-60 minutes
**Downtime Required**: None (migrations are backward compatible)

---

## Prerequisites

Before starting, ensure you have:

- [x] Database backup (CRITICAL - always backup before migrations)
- [x] Admin access to your database
- [x] Honua server stopped (recommended but not required)
- [x] Connection string for your database

---

## Step 1: Backup Your Database

### PostgreSQL
```bash
pg_dump -U honua_user -d honua_db > backup_$(date +%Y%m%d_%H%M%S).sql
```

### SQLite
```bash
cp honua.db honua_backup_$(date +%Y%m%d_%H%M%S).db
```

### MySQL
```bash
mysqldump -u honua_user -p honua_db > backup_$(date +%Y%m%d_%H%M%S).sql
```

### SQL Server
```powershell
sqlcmd -S localhost -Q "BACKUP DATABASE honua_db TO DISK = 'C:\Backups\honua_$(date +%Y%m%d).bak'"
```

---

## Step 2: Run Database Migration

### PostgreSQL

```bash
psql -U honua_user -d honua_db -f src/Honua.Server.Core/Data/Migrations/008_SoftDelete.sql
```

**Expected Output:**
```
CREATE TABLE
CREATE INDEX
CREATE INDEX
...
ALTER TABLE
CREATE FUNCTION
INSERT 0 1
```

**Verify:**
```sql
SELECT * FROM schema_migrations WHERE version = '008';
```

### SQLite

```bash
sqlite3 honua.db < src/Honua.Server.Core/Data/Migrations/008_SoftDelete_SQLite.sql
```

**Verify:**
```sql
.schema deletion_audit_log
.schema stac_collections
```

### MySQL

```bash
mysql -u honua_user -p honua_db < src/Honua.Server.Core/Data/Migrations/008_SoftDelete_MySQL.sql
```

**Verify:**
```sql
SHOW TABLES LIKE 'deletion_audit_log';
SHOW COLUMNS FROM stac_collections LIKE 'is_deleted';
```

### SQL Server

```powershell
sqlcmd -S localhost -d honua_db -i src\Honua.Server.Core\Data\Migrations\008_SoftDelete_SQLServer.sql
```

**Verify:**
```sql
SELECT * FROM sys.tables WHERE name = 'deletion_audit_log';
SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('stac_collections') AND name = 'is_deleted';
```

---

## Step 3: Verify Migration Success

Run these verification queries in your database:

### Check New Table
```sql
-- Should return 1 row
SELECT COUNT(*) FROM deletion_audit_log;
```

### Check New Columns
```sql
-- PostgreSQL/MySQL
SELECT
    table_name,
    column_name,
    data_type
FROM information_schema.columns
WHERE column_name IN ('is_deleted', 'deleted_at', 'deleted_by')
    AND table_name IN ('stac_collections', 'stac_items', 'auth_users');

-- SQLite
PRAGMA table_info(stac_collections);
PRAGMA table_info(stac_items);
PRAGMA table_info(auth_users);

-- SQL Server
SELECT
    t.name AS table_name,
    c.name AS column_name,
    ty.name AS data_type
FROM sys.columns c
JOIN sys.tables t ON c.object_id = t.object_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.name IN ('is_deleted', 'deleted_at', 'deleted_by')
    AND t.name IN ('stac_collections', 'stac_items', 'auth_users');
```

### Check Indexes
```sql
-- PostgreSQL
SELECT indexname FROM pg_indexes
WHERE indexname LIKE '%deleted%';

-- SQLite
.indexes stac_collections

-- MySQL
SHOW INDEXES FROM stac_collections WHERE Key_name LIKE '%deleted%';

-- SQL Server
SELECT name FROM sys.indexes
WHERE name LIKE '%deleted%';
```

**Expected Results:**
- ✅ `deletion_audit_log` table exists
- ✅ 3 new columns in stac_collections
- ✅ 3 new columns in stac_items
- ✅ 3 new columns in auth_users
- ✅ Multiple indexes with 'deleted' in name

---

## Step 4: Update Application Configuration

### Option A: Environment Variables

```bash
export HONUA_SOFTDELETE__ENABLED=true
export HONUA_SOFTDELETE__AUDITDELETIONS=true
export HONUA_SOFTDELETE__AUDITRESTORATIONS=true
export HONUA_SOFTDELETE__INCLUDEDELETEDBYDEFAULT=false
```

### Option B: appsettings.json

Add to your `appsettings.json` or `appsettings.Production.json`:

```json
{
  "SoftDelete": {
    "Enabled": true,
    "AutoPurgeEnabled": false,
    "RetentionPeriod": "90.00:00:00",
    "IncludeDeletedByDefault": false,
    "AuditDeletions": true,
    "AuditRestorations": true
  }
}
```

### Option C: Azure App Configuration / AWS Parameter Store

```bash
# Azure
az appconfig kv set --name honua-config \
    --key "SoftDelete:Enabled" --value "true"

# AWS
aws ssm put-parameter \
    --name "/honua/softdelete/enabled" \
    --value "true" \
    --type String
```

---

## Step 5: Test the Implementation

### Test 1: Soft Delete a Collection

```bash
# Soft delete via API
curl -X DELETE https://your-honua-instance/collections/test-collection \
    -H "Authorization: Bearer YOUR_TOKEN"

# Verify it's hidden
curl https://your-honua-instance/collections/test-collection
# Should return 404

# Check it's in database with is_deleted=true
```

SQL Query:
```sql
SELECT id, is_deleted, deleted_at, deleted_by
FROM stac_collections
WHERE id = 'test-collection';
```

### Test 2: Check Audit Log

```sql
SELECT
    entity_type,
    entity_id,
    deletion_type,
    deleted_by,
    deleted_at
FROM deletion_audit_log
WHERE entity_id = 'test-collection'
ORDER BY deleted_at DESC;
```

**Expected**: 1 row with `deletion_type = 'soft'`

### Test 3: Restore Collection

```bash
# Restore via API (requires admin role)
curl -X POST https://your-honua-instance/collections/test-collection/restore \
    -H "Authorization: Bearer ADMIN_TOKEN"

# Verify it's visible again
curl https://your-honua-instance/collections/test-collection
# Should return 200 with collection data
```

### Test 4: Verify Audit Trail

```sql
SELECT
    deletion_type,
    deleted_by,
    deleted_at
FROM deletion_audit_log
WHERE entity_id = 'test-collection'
ORDER BY deleted_at DESC;
```

**Expected**: 2 rows (soft delete + restore)

---

## Step 6: Update API Endpoints (Optional)

If you want to expose restore and hard delete endpoints:

### Add to your Program.cs or API controller:

```csharp
// Restore endpoint (admin only)
app.MapPost("/collections/{id}/restore",
    async (string id, IStacCatalogStore store, HttpContext context) =>
{
    // Get user ID from auth context
    var userId = context.User.FindFirst("sub")?.Value;

    var restored = await store.RestoreCollectionAsync(id);
    return restored ? Results.NoContent() : Results.NotFound();
})
.RequireAuthorization("Admin");

// Hard delete endpoint (super admin only)
app.MapDelete("/admin/collections/{id}/permanent",
    async (string id, IStacCatalogStore store, HttpContext context) =>
{
    var userId = context.User.FindFirst("sub")?.Value;

    var deleted = await store.HardDeleteCollectionAsync(id, userId);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.RequireAuthorization("SuperAdmin");

// View deleted collections (admin only)
app.MapGet("/admin/collections/deleted",
    async (IStacCatalogStore store) =>
{
    // This requires custom implementation to include deleted
    // For now, query database directly or add to store interface
    return Results.Ok(new { message = "Not implemented" });
})
.RequireAuthorization("Admin");
```

---

## Step 7: Set Up Monitoring

### Create Audit Log Size Alert

```sql
-- PostgreSQL
CREATE OR REPLACE FUNCTION check_audit_log_size()
RETURNS void AS $$
DECLARE
    log_size_mb numeric;
BEGIN
    SELECT pg_total_relation_size('deletion_audit_log') / 1024.0 / 1024.0
    INTO log_size_mb;

    IF log_size_mb > 1000 THEN
        RAISE WARNING 'Audit log size exceeds 1GB: % MB', log_size_mb;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- Schedule with pg_cron (if available)
SELECT cron.schedule('audit-log-check', '0 0 * * *', 'SELECT check_audit_log_size()');
```

### Create Prometheus Metrics (Optional)

```csharp
// In your metrics class
public static class SoftDeleteMetrics
{
    private static readonly Counter SoftDeletes = Metrics.CreateCounter(
        "honua_soft_deletes_total",
        "Total number of soft delete operations",
        new CounterConfiguration { LabelNames = new[] { "entity_type" } });

    private static readonly Counter HardDeletes = Metrics.CreateCounter(
        "honua_hard_deletes_total",
        "Total number of hard delete operations",
        new CounterConfiguration { LabelNames = new[] { "entity_type" } });

    private static readonly Counter Restores = Metrics.CreateCounter(
        "honua_restores_total",
        "Total number of restore operations",
        new CounterConfiguration { LabelNames = new[] { "entity_type" } });

    public static void RecordSoftDelete(string entityType) =>
        SoftDeletes.WithLabels(entityType).Inc();

    public static void RecordHardDelete(string entityType) =>
        HardDeletes.WithLabels(entityType).Inc();

    public static void RecordRestore(string entityType) =>
        Restores.WithLabels(entityType).Inc();
}
```

---

## Step 8: Set Up Automated Purge (Optional)

### Option A: Database Scheduled Job

**PostgreSQL (using pg_cron):**
```sql
-- Install pg_cron extension
CREATE EXTENSION IF NOT EXISTS pg_cron;

-- Schedule monthly purge of audit records older than 90 days
SELECT cron.schedule(
    'audit-log-purge',
    '0 2 1 * *',  -- 2 AM on 1st of each month
    $$
    DELETE FROM deletion_audit_log
    WHERE deleted_at < NOW() - INTERVAL '90 days'
    $$
);
```

**SQL Server (using SQL Agent):**
```sql
USE msdb;
GO

EXEC sp_add_job @job_name = 'AuditLogPurge';

EXEC sp_add_jobstep
    @job_name = 'AuditLogPurge',
    @step_name = 'Purge Old Records',
    @command = 'DELETE FROM deletion_audit_log WHERE deleted_at < DATEADD(day, -90, GETDATE())';

EXEC sp_add_schedule
    @schedule_name = 'Monthly',
    @freq_type = 16,  -- Monthly
    @freq_interval = 1,  -- 1st of month
    @active_start_time = 020000;  -- 2 AM

EXEC sp_attach_schedule
    @job_name = 'AuditLogPurge',
    @schedule_name = 'Monthly';
```

### Option B: Application Background Service

```csharp
public class AuditLogPurgeService : BackgroundService
{
    private readonly IDeletionAuditStore _auditStore;
    private readonly ILogger<AuditLogPurgeService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var purged = await _auditStore.PurgeOldAuditRecordsAsync(
                    TimeSpan.FromDays(90),
                    stoppingToken);

                _logger.LogInformation(
                    "Purged {Count} old audit records",
                    purged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging audit records");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}

// Register in Program.cs
services.AddHostedService<AuditLogPurgeService>();
```

---

## Rollback Procedure

If you need to rollback the migration:

### Step 1: Stop Application

```bash
systemctl stop honua-server
# or
docker stop honua-server
```

### Step 2: Restore Database Backup

```bash
# PostgreSQL
psql -U honua_user -d honua_db < backup_YYYYMMDD_HHMMSS.sql

# SQLite
cp honua_backup_YYYYMMDD_HHMMSS.db honua.db

# MySQL
mysql -u honua_user -p honua_db < backup_YYYYMMDD_HHMMSS.sql

# SQL Server
sqlcmd -S localhost -Q "RESTORE DATABASE honua_db FROM DISK = 'C:\Backups\honua_YYYYMMDD.bak'"
```

### Step 3: Remove Configuration

Remove SoftDelete section from appsettings.json

### Step 4: Restart Application

```bash
systemctl start honua-server
```

---

## Troubleshooting

### Issue: Migration Fails with "Column already exists"

**Solution**: Migration is idempotent. Safe to re-run.

For PostgreSQL:
```sql
-- Check which columns exist
SELECT column_name FROM information_schema.columns
WHERE table_name = 'stac_collections'
    AND column_name IN ('is_deleted', 'deleted_at', 'deleted_by');
```

### Issue: Queries are Slower After Migration

**Cause**: Missing indexes or statistics not updated

**Solution**:
```sql
-- PostgreSQL
ANALYZE stac_collections;
ANALYZE stac_items;
ANALYZE auth_users;

-- MySQL
ANALYZE TABLE stac_collections;
ANALYZE TABLE stac_items;
ANALYZE TABLE auth_users;

-- SQL Server
UPDATE STATISTICS stac_collections;
UPDATE STATISTICS stac_items;
UPDATE STATISTICS auth_users;
```

### Issue: Soft-Deleted Records Still Appearing

**Cause**: Application not filtering by is_deleted

**Solution**: Update queries to include:
```sql
WHERE (is_deleted IS NULL OR is_deleted = FALSE)
```

### Issue: Cannot Delete Records (Foreign Key Constraint)

**Cause**: Hard delete blocked by foreign keys

**Solution**:
1. Use soft delete instead (recommended)
2. Or cascade delete child records first
3. Or temporarily disable foreign key checks (NOT recommended for production)

---

## Post-Migration Checklist

- [ ] Database backup created
- [ ] Migration executed successfully
- [ ] All new columns present
- [ ] All indexes created
- [ ] Application configuration updated
- [ ] Soft delete tested
- [ ] Restore tested
- [ ] Audit log verified
- [ ] API endpoints updated (if needed)
- [ ] Monitoring configured
- [ ] Auto-purge configured (if desired)
- [ ] Team trained on new functionality
- [ ] Documentation updated

---

## Training Your Team

### For Developers

- Soft delete is now the default for all delete operations
- Use `HardDeleteAsync()` only for permanent removal (admin only)
- Always check audit logs when investigating deleted data
- Add `includeDeleted: true` parameter for admin queries

### For Administrators

- Deleted collections/items can be restored from admin panel
- Check audit log for who deleted what and when
- Hard delete is permanent and requires super admin rights
- Configure auto-purge based on compliance requirements

### For Users

- Deleted items go to "trash" and can be recovered (by admins)
- Contact admin to restore accidentally deleted items
- Deletion is immediate but reversible for 90 days (default)

---

## Next Steps

1. **Run Migration**: Follow steps 1-4 above
2. **Test Thoroughly**: Use test environment first
3. **Monitor Performance**: Check query times and disk usage
4. **Configure Auto-Purge**: Set up based on compliance needs
5. **Update Documentation**: Add to your runbooks

---

## Support

For issues or questions:

1. Check [SOFT_DELETE_IMPLEMENTATION_COMPLETE.md](../SOFT_DELETE_IMPLEMENTATION_COMPLETE.md)
2. Review audit logs: `SELECT * FROM deletion_audit_log`
3. Check application logs for errors
4. Verify configuration: `SELECT * FROM schema_migrations WHERE version = '008'`

---

**Last Updated**: 2025-10-30
**Version**: 1.0

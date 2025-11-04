# SQL Migration Files Comprehensive Review Report

**Generated:** 2025-10-30
**Scope:** All migration files in `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Data/Migrations/`
**Total Migrations Reviewed:** 16 files

---

## Executive Summary

**Total Issues Found:** 47
**CRITICAL Severity:** 12
**HIGH Severity:** 21
**MEDIUM Severity:** 14

### Key Findings:
- **Multiple duplicate migration version numbers** causing potential conflicts
- **Missing foreign key constraints** creating orphaned record risks
- **Security vulnerabilities** in RLS policies and encryption handling
- **Data integrity issues** from missing constraints and validation
- **Performance concerns** from missing indexes on foreign keys
- **Schema inconsistencies** across database-specific migrations

---

## Summary Statistics

| Category | Count |
|----------|-------|
| Data Integrity Issues | 18 |
| Security Issues | 8 |
| Performance Issues | 14 |
| Correctness/Migration Issues | 7 |

---

## CRITICAL Issues (Severity: CRITICAL)

### 1. **Duplicate Migration Version Numbers**
**Files:** `007_OptimisticLocking.sql`, `007_DataVersioning.sql`
**Category:** Correctness
**Severity:** CRITICAL

**Issue:** Two different migrations both use version "007", which will cause migration tracking conflicts.

**Impact:**
- One migration will silently fail to apply
- Inconsistent database schema across environments
- Potential data loss if wrong migration runs
- Migration rollback becomes impossible

**Affected SQL:**
```sql
-- 007_OptimisticLocking.sql
INSERT INTO schema_migrations (version, name, ...)
VALUES ('007', 'OptimisticLocking', ...)

-- 007_DataVersioning.sql
INSERT INTO schema_migrations (version, name, ...)
VALUES ('007', 'DataVersioning', ...)
```

**Recommended Fix:**
Renumber one migration to a unique version (e.g., rename `007_DataVersioning.sql` to `011_DataVersioning.sql`)

---

### 2. **Duplicate Migration Version "008"**
**Files:** `008_SoftDelete.sql`, `008_SamlSso.sql`, plus 3 DB-specific variants
**Category:** Correctness
**Severity:** CRITICAL

**Issue:** Five different migrations all use version "008".

**Impact:**
- Only first migration will be tracked in schema_migrations
- Critical features (soft delete OR SAML) may be completely missing
- Silent failures in production deployments

**Affected Files:**
- `008_SoftDelete.sql`
- `008_SoftDelete_SQLite.sql`
- `008_SoftDelete_MySQL.sql`
- `008_SoftDelete_SQLServer.sql`
- `008_SamlSso.sql`

**Recommended Fix:**
- Renumber SAML migration to `012_SamlSso.sql`
- Keep DB-specific soft delete variants as conditional branches, not separate migrations

---

### 3. **Missing Foreign Key on `build_cache_registry.first_built_by_customer`**
**File:** `001_InitialSchema.sql` (line 239-241)
**Category:** Data Integrity
**Severity:** CRITICAL

**Issue:** Foreign key uses `ON DELETE SET NULL` but column is `NOT NULL`, creating constraint violation.

**Affected SQL:**
```sql
first_built_by_customer VARCHAR(100) NOT NULL,
...
CONSTRAINT fk_build_cache_first_customer FOREIGN KEY (first_built_by_customer)
    REFERENCES customers(customer_id) ON DELETE SET NULL
```

**Impact:**
- Database will reject customer deletion
- Cannot delete customers who created any cached builds
- Data retention violations (GDPR compliance issue)

**Recommended Fix:**
```sql
-- Option 1: Allow NULL
first_built_by_customer VARCHAR(100),

-- Option 2: Use CASCADE or RESTRICT
ON DELETE RESTRICT  -- Prevent deletion if builds exist
```

---

### 4. **Missing Table Reference in Soft Delete Migration**
**Files:** `008_SoftDelete*.sql`
**Category:** Correctness
**Severity:** CRITICAL

**Issue:** Migration tries to add soft delete columns to `stac_collections`, `stac_items`, and `auth_users` tables that don't exist in prior migrations.

**Impact:**
- Migration will fail with "table does not exist" error
- Breaks entire migration sequence
- Manual intervention required to fix

**Recommended Fix:**
Add conditional checks or make migration idempotent:
```sql
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'stac_collections') THEN
        -- Add columns
    END IF;
END $$;
```

---

### 5. **Missing RLS Policies on Tenant-Isolated Tables**
**Files:** Multiple (`001_InitialSchema.sql`, `006_TenantUsageTracking.sql`, etc.)
**Category:** Security
**Severity:** CRITICAL

**Issue:** Multi-tenant tables with `tenant_id` columns lack Row-Level Security policies.

**Affected Tables:**
- `customers`
- `licenses`
- `build_queue`
- `tenant_usage`
- `tenant_usage_events`
- `process_runs`
- `audit_events`
- `saml_identity_providers`

**Impact:**
- **Cross-tenant data leakage**
- One tenant can read/modify another tenant's data
- Severe security and compliance violation
- Breach of tenant isolation guarantees

**Recommended Fix:**
```sql
-- Enable RLS on all tenant tables
ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
ALTER TABLE build_queue ENABLE ROW LEVEL SECURITY;
-- ... etc

-- Create policies
CREATE POLICY tenant_isolation_policy ON customers
    USING (customer_id = current_setting('app.current_tenant')::VARCHAR);

CREATE POLICY tenant_isolation_policy ON build_queue
    USING (customer_id = current_setting('app.current_tenant')::VARCHAR);
```

---

### 6. **Sensitive Data Stored Without Encryption**
**File:** `001_InitialSchema.sql`
**Category:** Security
**Severity:** CRITICAL

**Issue:** License keys stored encrypted, but encryption key ID is only referenced, not managed.

**Affected SQL (lines 82-86):**
```sql
-- License key (encrypted)
license_key TEXT NOT NULL,
license_key_hash VARCHAR(64) NOT NULL UNIQUE,
```

**Impact:**
- No key rotation mechanism
- No KMS integration documented
- Encryption key stored in application config (likely plain text)
- Compliance violation (PCI DSS, SOC2, GDPR)

**Recommended Fix:**
- Use database-level encryption (PostgreSQL pgcrypto with KMS)
- Add `encryption_key_version` column for key rotation
- Document key management procedures

---

### 7. **SQL Injection in Dynamic SQL Functions**
**Files:** `005_Functions.sql`, `008_SoftDelete.sql`
**Category:** Security
**Severity:** CRITICAL

**Issue:** Dynamic SQL constructed with `format()` without proper sanitization.

**Affected Functions:**
- `soft_delete_entity()` (line 168)
- `restore_soft_deleted_entity()` (line 221)
- `hard_delete_entity()` (line 291)

**Vulnerable Code:**
```sql
v_sql := format(
    'UPDATE %I SET is_deleted = TRUE, deleted_at = NOW(), deleted_by = $1 WHERE id = $2 AND is_deleted = FALSE',
    p_table_name  -- %I format identifier - still vulnerable if called from application
);
```

**Impact:**
- SQL injection if table names come from user input
- Privilege escalation risk
- Data exfiltration or deletion

**Recommended Fix:**
- Whitelist allowed table names
- Use schema-qualified names
- Add validation:
```sql
IF p_table_name NOT IN ('stac_collections', 'stac_items', 'auth_users') THEN
    RAISE EXCEPTION 'Invalid table name: %', p_table_name;
END IF;
```

---

### 8. **Missing Index on Foreign Key Columns**
**Files:** Multiple
**Category:** Performance
**Severity:** CRITICAL (for high-traffic tables)

**Issue:** Foreign key columns lack indexes, causing full table scans on JOIN and DELETE operations.

**Affected Columns:**
- `build_queue.target_registry_id` → `registry_credentials(id)`
- `build_manifests.deployment_id` → `build_queue(id)`
- `conversations.build_manifest_id` → `build_manifests(id)`
- `tenant_usage.tenant_id` → `customers(customer_id)` (index exists but partial)
- `saml_sessions.idp_configuration_id` → `saml_identity_providers(id)`

**Impact:**
- DELETE FROM customers scans all child tables (O(N) per customer)
- Slow JOINs on large datasets
- Lock contention during cascading deletes
- 100-1000x slower queries on multi-million row tables

**Recommended Fix:**
```sql
-- Add missing indexes
CREATE INDEX idx_build_queue_target_registry ON build_queue(target_registry_id);
CREATE INDEX idx_build_manifests_deployment ON build_manifests(deployment_id);
CREATE INDEX idx_conversations_build_manifest ON conversations(build_manifest_id);
```

---

### 9. **Race Condition in `get_next_build_for_worker()`**
**File:** `005_Functions.sql` (line 331-383)
**Category:** Data Integrity
**Severity:** HIGH

**Issue:** Timeout calculation uses `NOW()` in UPDATE, which can differ from SELECT time.

**Vulnerable Code (line 366):**
```sql
timeout_at = NOW() + (max_duration_seconds || ' seconds')::INTERVAL
```

**Impact:**
- Worker thinks timeout is at T+3600s, but DB records T+3601s
- Timeout detection function may miss stale jobs
- Jobs run longer than allowed without cleanup

**Recommended Fix:**
```sql
-- Use transaction start time
timeout_at = CLOCK_TIMESTAMP() + (max_duration_seconds || ' seconds')::INTERVAL
```

---

### 10. **Missing CHECK Constraint on Email Format**
**File:** `001_InitialSchema.sql`
**Category:** Data Integrity
**Severity:** HIGH

**Issue:** Email columns lack format validation.

**Affected Columns:**
- `customers.contact_email`
- `conversations.customer_email`
- `build_manifests.customer_email`

**Impact:**
- Invalid email addresses stored (e.g., "not-an-email")
- Email notifications fail silently
- Data quality issues for marketing/support

**Recommended Fix:**
```sql
ALTER TABLE customers ADD CONSTRAINT chk_contact_email_format
    CHECK (contact_email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$');
```

---

### 11. **Audit Log Can Be Bypassed**
**File:** `009_AuditLog.sql`
**Category:** Security
**Severity:** HIGH

**Issue:** Trigger prevents UPDATE/DELETE, but superuser can disable triggers.

**Affected Code (lines 69-92):**
```sql
CREATE TRIGGER audit_events_tamper_proof
    BEFORE UPDATE OR DELETE ON audit_events
    FOR EACH ROW
    EXECUTE FUNCTION prevent_audit_modification();
```

**Impact:**
- Superuser can `ALTER TABLE audit_events DISABLE TRIGGER ALL;`
- Audit log tampering possible
- Compliance violation (SOC2, HIPAA, PCI DSS)

**Recommended Fix:**
- Use append-only table partitioning
- Replicate to immutable storage (AWS S3 with WORM)
- Add cryptographic signatures to audit records
```sql
-- Add hash chain for tamper detection
ALTER TABLE audit_events ADD COLUMN previous_hash VARCHAR(64);
ALTER TABLE audit_events ADD COLUMN record_hash VARCHAR(64);
```

---

### 12. **Missing Unique Constraint on Cache Lookup**
**File:** `001_InitialSchema.sql`
**Category:** Data Integrity
**Severity:** HIGH

**Issue:** `manifest_hash` should be unique per tier, but can have duplicates.

**Current Constraint (line 243):**
```sql
CREATE UNIQUE INDEX idx_build_cache_manifest_hash ON build_cache_registry(manifest_hash) WHERE deleted_at IS NULL;
```

**Issue:** Same hash can exist for different tiers (e.g., core and enterprise).

**Impact:**
- Cache lookup returns wrong tier build
- License tier enforcement bypass
- Customer gets enterprise features without paying

**Recommended Fix:**
```sql
CREATE UNIQUE INDEX idx_build_cache_manifest_hash_tier
    ON build_cache_registry(manifest_hash, tier) WHERE deleted_at IS NULL;
```

---

## HIGH Severity Issues

### 13. **Orphaned Records Risk: `build_results.build_cache_id`**
**File:** `002_BuildQueue.sql` (line 146-147)
**Category:** Data Integrity
**Severity:** HIGH

**Issue:** Foreign key uses `ON DELETE SET NULL`, which can create orphaned references.

**Impact:**
- Build results point to deleted cache entries
- Broken links in UI
- Inaccurate cache hit statistics
- Audit trail incomplete

**Recommended Fix:**
```sql
CONSTRAINT fk_build_results_cache FOREIGN KEY (build_cache_id)
    REFERENCES build_cache_registry(id) ON DELETE RESTRICT
-- Or use CASCADE if build results should be deleted with cache
```

---

### 14. **Missing Index on Temporal Queries**
**File:** `001_InitialSchema.sql`
**Category:** Performance
**Severity:** HIGH

**Issue:** License expiration queries lack efficient index.

**Missing Index:**
```sql
-- Current (line 126)
CREATE INDEX idx_licenses_expires_at ON licenses(expires_at) WHERE status = 'active';

-- Should be composite for common query pattern
CREATE INDEX idx_licenses_expiring_soon
    ON licenses(status, expires_at)
    WHERE status IN ('active', 'trial') AND expires_at < NOW() + INTERVAL '30 days';
```

**Impact:**
- Slow license renewal notification queries
- Billing process delays
- Background job performance issues

---

### 15. **Unbounded Text Columns**
**Files:** Multiple
**Category:** Performance
**Severity:** HIGH

**Issue:** TEXT columns without size limits can cause memory/storage issues.

**Affected Columns:**
- `build_results.stdout_log` (line 114)
- `build_results.stderr_log` (line 115)
- `conversation_messages.content` (line 174)
- `audit_events.description` (line 19)

**Impact:**
- OOM errors when querying large logs
- Slow index rebuilds
- Backup/restore performance degradation
- Multi-GB rows possible

**Recommended Fix:**
```sql
-- Option 1: Add size limit
ALTER TABLE build_results
    ADD CONSTRAINT chk_log_size
    CHECK (LENGTH(stdout_log) <= 10485760);  -- 10MB

-- Option 2: Store large logs externally
ALTER TABLE build_results ADD COLUMN stdout_log_url TEXT;
```

---

### 16. **Missing Transaction Isolation in Queue Functions**
**File:** `005_Functions.sql`, `010_Geoprocessing.sql`
**Category:** Data Integrity
**Severity:** HIGH

**Issue:** `get_next_build_for_worker()` and `dequeue_process_run()` use `SKIP LOCKED` but don't set isolation level.

**Impact:**
- Under heavy load, workers may get same job
- Duplicate processing possible
- Race conditions in job assignment

**Recommended Fix:**
```sql
CREATE OR REPLACE FUNCTION get_next_build_for_worker(...)
RETURNS ... AS $$
BEGIN
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    -- rest of function
END;
$$ LANGUAGE plpgsql;
```

---

### 17. **Soft Delete Inconsistency Across Database Variants**
**Files:** `008_SoftDelete*.sql`
**Category:** Correctness
**Severity:** HIGH

**Issue:** Database-specific migrations have different implementations.

**SQLite Issues:**
- Uses TEXT for timestamps (line 23) instead of proper datetime
- Boolean as INTEGER (0/1) instead of native BOOLEAN
- Missing stored procedures (lines 88-89)

**SQL Server Issues:**
- Uses NVARCHAR for all strings (unnecessary overhead for ASCII)
- Different timestamp precision (DATETIMEOFFSET vs TIMESTAMPTZ)

**Impact:**
- Cross-database behavior differences
- Migration from SQLite → PostgreSQL breaks
- Different query syntax needed per database

**Recommended Fix:**
Standardize on PostgreSQL as primary, use migration framework that handles DB differences (e.g., FluentMigrator, Dapper.SimpleCRUD).

---

### 18. **Missing Rollback Scripts**
**Files:** All migrations
**Category:** Correctness
**Severity:** HIGH

**Issue:** No DOWN migrations or rollback procedures.

**Impact:**
- Cannot rollback failed deployments
- Disaster recovery requires manual SQL
- Testing migration rollback impossible
- Increased deployment risk

**Recommended Fix:**
Create matching rollback scripts:
```sql
-- 001_InitialSchema_Down.sql
DROP TABLE IF EXISTS build_access_log CASCADE;
DROP TABLE IF EXISTS build_cache_registry CASCADE;
-- ... etc
```

---

### 19. **Validation Function Has Performance Issues**
**File:** `005_Functions.sql` (line 543-579)
**Category:** Performance
**Severity:** HIGH

**Issue:** `validate_build_queue_insert()` does multiple queries per insert.

**Problematic Code:**
```sql
SELECT * INTO v_license FROM licenses ...
SELECT * INTO v_current_quota FROM get_license_quota_usage(...);
```

**Impact:**
- Every INSERT into build_queue runs 2+ queries
- O(N) license lookups
- Cannot batch insert jobs
- High-throughput workloads will fail

**Recommended Fix:**
```sql
-- Pre-cache quotas in application layer
-- Or use materialized view for quota checks
CREATE MATERIALIZED VIEW license_quota_cache AS
SELECT customer_id, builds_this_month, remaining_builds
FROM get_license_quota_usage(NULL);

REFRESH MATERIALIZED VIEW CONCURRENTLY license_quota_cache;
```

---

### 20. **Missing Constraint on `conversation_messages.sequence_number`**
**File:** `003_IntakeSystem.sql` (line 179)
**Category:** Data Integrity
**Severity:** HIGH

**Issue:** Sequence numbers can have gaps or duplicates if concurrent inserts occur.

**Current Constraint:**
```sql
CONSTRAINT uq_conversation_message_sequence UNIQUE (conversation_id, sequence_number)
```

**Issue:** Doesn't enforce contiguity (1,2,3...) - can have (1,3,7,9).

**Impact:**
- Message ordering breaks
- Conversation replay has gaps
- AI context window incomplete

**Recommended Fix:**
```sql
-- Use SERIAL or SEQUENCE to auto-assign
ALTER TABLE conversation_messages
    ALTER COLUMN sequence_number SET DEFAULT nextval('conversation_message_seq');

-- Or use trigger to enforce contiguity
CREATE FUNCTION enforce_sequence_contiguity() RETURNS TRIGGER AS $$
BEGIN
    IF NEW.sequence_number != (SELECT COALESCE(MAX(sequence_number), 0) + 1
                                FROM conversation_messages
                                WHERE conversation_id = NEW.conversation_id) THEN
        RAISE EXCEPTION 'Sequence number must be contiguous';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
```

---

### 21-33. **Additional HIGH Issues (Summary)**

**21. Missing Index on `tenant_usage_events.created_at`** - Slow time-range queries (006_TenantUsageTracking.sql)

**22. Unbounded JSONB Columns** - Can cause memory issues (multiple files)

**23. Missing Partition Strategy** - Tables like `build_events` will grow unbounded (002_BuildQueue.sql)

**24. No Cleanup Job for Expired `saml_sessions`** - Memory leak over time (008_SamlSso.sql)

**25. Missing Composite Index for License Quota Checks** - Slow quota enforcement (005_Functions.sql)

**26. Race Condition in Cache Statistics Update** - `cache_hit_count` can be inaccurate (005_Functions.sql)

**27. Missing VACUUM Strategy** - Large tables will bloat (004_Indexes.sql comments only)

**28. No Foreign Key Index on `process_runs.tenant_id`** - Slow tenant deletion (010_Geoprocessing.sql)

**29. Missing CHECK Constraint on IP Address Format** - Invalid IPs stored (multiple files)

**30. Potential Integer Overflow in `duration_ms`** - BIGINT needed for long-running jobs (010_Geoprocessing.sql)

**31. Missing Index on `audit_events.tenant_id, timestamp`** - Critical for tenant audit queries (009_AuditLog.sql)

**32. No Compression on Audit Archive** - Wastes storage (009_AuditLog.sql)

**33. Missing Validation on Process Status Transitions** - Can go from 'completed' back to 'pending' (010_Geoprocessing.sql)

---

## MEDIUM Severity Issues

### 34. **Inconsistent Timestamp Precision**
**Files:** Multiple
**Category:** Data Integrity
**Severity:** MEDIUM

**Issue:** Some tables use `TIMESTAMPTZ`, others use `TIMESTAMP`, some use `TIMESTAMP(6)`.

**Impact:**
- Timezone confusion
- Lost milliseconds precision in comparisons
- Inconsistent API responses

**Recommendation:** Standardize on `TIMESTAMPTZ` everywhere.

---

### 35. **Missing Comments on Critical Columns**
**Files:** `001_InitialSchema.sql`, `002_BuildQueue.sql`
**Category:** Maintainability
**Severity:** MEDIUM

**Issue:** Many critical columns lack COMMENT documentation.

**Recommendation:** Add comments for all columns, especially:
- Foreign keys
- Enum-like VARCHAR columns
- JSONB structure columns

---

### 36-47. **Additional MEDIUM Issues (Summary)**

**36. No Monitoring for Migration Execution Time** - Can't detect slow migrations

**37. Missing Retry Logic in Cleanup Functions** - Cleanup jobs can fail silently

**38. Hardcoded Default Values** - Should be configurable (e.g., `max_retries = 3`)

**39. No Version Constraint on Dependencies** - Migration 005 doesn't verify 001-004 applied

**40. Missing Index on GIN Columns** - Some JSONB columns lack indexes (optimization)

**41. No Circuit Breaker in Queue Functions** - Infinite loops possible

**42. Missing Tenant Quota Exceeded Event** - No audit log when quota exceeded

**43. Inconsistent Naming Convention** - Some indexes use `idx_`, others don't

**44. No Connection Pooling Guidance** - Functions may exhaust connections

**45. Missing Schema Version Validation** - No check that schema matches application version

**46. No Test Data for Migrations** - Can't verify migrations in CI/CD

**47. Missing Performance Benchmarks** - No baseline for query performance

---

## Migration-Specific Detailed Findings

### 001_InitialSchema.sql
**Lines of Interest:**
- ✅ **Good:** Extensive indexes and comments
- ⚠️ **Issue:** Missing RLS policies (lines 36-325)
- ⚠️ **Issue:** Foreign key conflict (lines 239-241)
- ⚠️ **Issue:** Encryption key management not documented (lines 82-86)

### 002_BuildQueue.sql
**Lines of Interest:**
- ✅ **Good:** Queue processing index with SKIP LOCKED support
- ⚠️ **Issue:** Missing index on `target_registry_id` (line 49)
- ⚠️ **Issue:** Unbounded log columns (lines 114-115)
- ⚠️ **Issue:** No partition strategy for `build_events` (lines 223-254)

### 003_IntakeSystem.sql
**Lines of Interest:**
- ✅ **Good:** Comprehensive conversation tracking
- ⚠️ **Issue:** Message sequence number gaps possible (line 179)
- ⚠️ **Issue:** No cleanup for abandoned conversations (line 151)
- ⚠️ **Issue:** Missing index on `build_manifest_id` in conversations (line 138)

### 004_Indexes.sql
**Lines of Interest:**
- ✅ **Good:** Many performance-optimized indexes
- ⚠️ **Issue:** No actual table modifications, only indexes
- ℹ️ **Note:** Good documentation on partitioning strategy (lines 342-346)

### 005_Functions.sql
**Lines of Interest:**
- ✅ **Good:** Comprehensive utility functions
- ⚠️ **Issue:** SQL injection risk in dynamic SQL (lines 168, 221, 291)
- ⚠️ **Issue:** Race condition in timeout calculation (line 366)
- ⚠️ **Issue:** Validation trigger disabled by default (lines 584-588)

### 006_TenantUsageTracking.sql
**Lines of Interest:**
- ✅ **Good:** Quota tracking structure
- ⚠️ **Issue:** Missing indexes on foreign keys
- ⚠️ **Issue:** Hardcoded tier quotas in function (lines 194-199)
- ⚠️ **Issue:** No audit of quota changes

### 007_OptimisticLocking.sql
**Lines of Interest:**
- ⚠️ **Issue:** Duplicate version number (line 97)
- ⚠️ **Issue:** Documentation-only migration for most databases
- ℹ️ **Note:** Requires manual application to user tables

### 007_DataVersioning.sql
**Lines of Interest:**
- ⚠️ **Issue:** Duplicate version number (line 385)
- ⚠️ **Issue:** Hard-codes table name 'collections_versioned' (line 14)
- ⚠️ **Issue:** Complex trigger may have performance impact (lines 286-348)

### 008_SoftDelete.sql (and variants)
**Lines of Interest:**
- ⚠️ **Issue:** References tables not created in prior migrations (lines 66-78)
- ⚠️ **Issue:** Dynamic SQL injection risk (lines 168-175)
- ⚠️ **Issue:** Inconsistent implementations across DB types

### 008_SamlSso.sql
**Lines of Interest:**
- ⚠️ **Issue:** Duplicate version number (all variants)
- ⚠️ **Issue:** Missing index on `idp_configuration_id` in sessions (line 45)
- ⚠️ **Issue:** No cleanup cron job configured (line 73)
- ⚠️ **Issue:** References `tenants` table not created in prior migrations (line 28)

### 009_AuditLog.sql
**Lines of Interest:**
- ✅ **Good:** Tamper-proof trigger
- ⚠️ **Issue:** Superuser can bypass trigger (lines 89-92)
- ⚠️ **Issue:** No encryption for sensitive audit data
- ⚠️ **Issue:** Archive function can block writes (lines 95-119)

### 010_Geoprocessing.sql
**Lines of Interest:**
- ✅ **Good:** Comprehensive job tracking
- ⚠️ **Issue:** Missing foreign key index on `tenant_id` (line 14)
- ⚠️ **Issue:** No validation of status transitions (line 19)
- ⚠️ **Issue:** Unbounded JSONB columns (lines 35, 37)

### 030_KeysetPaginationIndexes.sql
**Lines of Interest:**
- ✅ **Good:** Performance-focused migration
- ⚠️ **Issue:** References tables not created in prior migrations (lines 34-41)
- ℹ️ **Note:** Good documentation of performance implications

---

## Recommendations

### Immediate Action Required (CRITICAL):

1. **Renumber duplicate migrations**
   - Rename `007_DataVersioning.sql` → `011_DataVersioning.sql`
   - Rename `008_SamlSso.sql` → `012_SamlSso.sql`

2. **Fix foreign key constraint conflict**
   - Make `build_cache_registry.first_built_by_customer` nullable OR change to `ON DELETE RESTRICT`

3. **Implement Row-Level Security**
   - Add RLS policies to ALL multi-tenant tables
   - Test with multiple tenant contexts

4. **Add foreign key indexes**
   - Index all FK columns for performance
   - Priority: high-traffic tables first

5. **Sanitize dynamic SQL**
   - Add table name whitelisting in all dynamic SQL functions
   - Consider removing dynamic SQL entirely

### Short-Term (HIGH Priority):

6. **Create rollback scripts** for all migrations

7. **Add email format validation** constraints

8. **Implement audit log hash chain** for tamper detection

9. **Fix soft delete migration** to check table existence

10. **Add partition strategy** for time-series tables

### Long-Term (MEDIUM Priority):

11. **Standardize timestamp types** across all tables

12. **Add comprehensive comments** to all columns

13. **Implement migration framework** with transaction support

14. **Add monitoring** for migration execution time

15. **Create test suite** for migration validation

---

## Testing Recommendations

### For Each Migration:

```sql
-- Test in transaction (PostgreSQL)
BEGIN;
\i 001_InitialSchema.sql
-- Verify schema
\dt
\di
-- Check constraints
SELECT * FROM information_schema.table_constraints;
ROLLBACK;

-- Test idempotency
\i 001_InitialSchema.sql
\i 001_InitialSchema.sql  -- Should not error

-- Test data integrity
INSERT INTO customers (customer_id, organization_name, contact_email)
VALUES ('test', 'Test Org', 'invalid-email');  -- Should fail with validation

-- Test foreign key cascades
INSERT INTO customers (customer_id, organization_name, contact_email)
VALUES ('test', 'Test Org', 'test@example.com');
INSERT INTO licenses (customer_id, tier) VALUES ('test', 'core');
DELETE FROM customers WHERE customer_id = 'test';
-- Verify cascade worked
```

### Performance Testing:

```sql
-- Test index usage
EXPLAIN ANALYZE
SELECT * FROM build_queue
WHERE customer_id = 'test' AND status = 'queued'
ORDER BY priority DESC, queued_at ASC LIMIT 10;

-- Should show "Index Scan" not "Seq Scan"
```

---

## Files Reviewed

1. ✅ `001_InitialSchema.sql` - 325 lines
2. ✅ `002_BuildQueue.sql` - 361 lines
3. ✅ `003_IntakeSystem.sql` - 390 lines
4. ✅ `004_Indexes.sql` - 352 lines
5. ✅ `005_Functions.sql` - 629 lines
6. ✅ `006_TenantUsageTracking.sql` - 247 lines
7. ✅ `007_OptimisticLocking.sql` - 105 lines
8. ✅ `007_DataVersioning.sql` - 387 lines (DUPLICATE VERSION)
9. ✅ `008_SoftDelete.sql` - 311 lines
10. ✅ `008_SoftDelete_SQLite.sql` - 94 lines (DUPLICATE VERSION)
11. ✅ `008_SoftDelete_MySQL.sql` - 192 lines (DUPLICATE VERSION)
12. ✅ `008_SoftDelete_SQLServer.sql` - 263 lines (DUPLICATE VERSION)
13. ✅ `008_SamlSso.sql` - 107 lines (DUPLICATE VERSION)
14. ✅ `009_AuditLog.sql` - 268 lines
15. ✅ `010_Geoprocessing.sql` - 423 lines
16. ✅ `030_KeysetPaginationIndexes.sql` - 165 lines

**Total Lines Analyzed:** 4,619 lines of SQL

---

## Appendix: Quick Reference Table

| Issue ID | Severity | Category | File(s) | Line(s) | Fix Priority |
|----------|----------|----------|---------|---------|--------------|
| 1 | CRITICAL | Correctness | 007_OptimisticLocking, 007_DataVersioning | 97, 385 | P0 |
| 2 | CRITICAL | Correctness | 008_*.sql (5 files) | Multiple | P0 |
| 3 | CRITICAL | Data Integrity | 001_InitialSchema.sql | 239-241 | P0 |
| 4 | CRITICAL | Correctness | 008_SoftDelete*.sql | 66-78 | P0 |
| 5 | CRITICAL | Security | Multiple | N/A | P0 |
| 6 | CRITICAL | Security | 001_InitialSchema.sql | 82-86 | P0 |
| 7 | CRITICAL | Security | 005_Functions, 008_SoftDelete | 168, 221, 291 | P0 |
| 8 | CRITICAL | Performance | Multiple | N/A | P0 |
| 9 | HIGH | Data Integrity | 005_Functions.sql | 366 | P1 |
| 10 | HIGH | Data Integrity | 001_InitialSchema.sql | N/A | P1 |
| 11 | HIGH | Security | 009_AuditLog.sql | 89-92 | P1 |
| 12 | HIGH | Data Integrity | 001_InitialSchema.sql | 243 | P1 |

---

**Report Generated By:** SQL Migration Review Tool
**Review Scope:** All migrations in `/src/Honua.Server.Core/Data/Migrations/`
**Next Steps:** Address CRITICAL issues immediately, then proceed with HIGH and MEDIUM priorities.

# Database Review - Action Items

**Generated:** 2025-10-17
**Full Report:** See `DATABASE_DATA_LAYER_REVIEW.md`

---

## Quick Summary

**Overall Grade:** A- (Excellent architecture, minor consistency gaps)

**Issues Found:**
- 🔴 **1 Critical:** Missing MySQL auth schema
- 🟠 **2 High Priority:** Missing auth indexes in SQLite/SQL Server
- 🟡 **7 Medium Priority:** Transaction gaps, N+1 queries, missing constraints
- 🟢 **2 Low Priority:** Code quality improvements

**Strengths:**
- ✅ Zero SQL injection vulnerabilities
- ✅ Comprehensive retry policies (40+ transient error codes)
- ✅ Proper resource disposal throughout
- ✅ 40+ performance indexes added
- ✅ Clean multi-database abstraction

---

## Critical Issues (Fix Immediately)

### 1. Missing MySQL Auth Schema 🔴
**File:** `scripts/sql/auth/mysql/001_initial.sql` (doesn't exist)

**Impact:** MySQL deployments cannot use authentication at all.

**Action:**
```bash
# Create file based on PostgreSQL version
cp scripts/sql/auth/postgres/001_initial.sql scripts/sql/auth/mysql/001_initial.sql
# Adapt syntax:
# - TIMESTAMPTZ → DATETIME(6)
# - BOOLEAN → TINYINT(1)
# - JSONB → JSON
# - TEXT → VARCHAR or LONGTEXT
# - UUID → CHAR(36)
```

**Effort:** 2 hours
**Priority:** P0 - Blocks MySQL deployments

---

## High Priority Issues (This Sprint)

### 2. Missing SQLite Auth Indexes 🟠
**File:** `scripts/sql/auth/sqlite/001_initial.sql`

**Current:** 0 auth indexes
**Target:** 8 indexes (matching PostgreSQL)

**Action:** Add these indexes:
```sql
CREATE INDEX IF NOT EXISTS idx_auth_users_subject ON auth_users(subject) WHERE subject IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_auth_users_email ON auth_users(email) WHERE email IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_auth_users_username ON auth_users(username) WHERE username IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_auth_users_active ON auth_users(is_active, id);
CREATE INDEX IF NOT EXISTS idx_auth_user_roles_user ON auth_user_roles(user_id);
CREATE INDEX IF NOT EXISTS idx_auth_user_roles_role ON auth_user_roles(role_id);
CREATE INDEX IF NOT EXISTS idx_auth_credentials_audit_user ON auth_credentials_audit(user_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS idx_auth_credentials_audit_time ON auth_credentials_audit(occurred_at DESC);
```

**Effort:** 1 hour
**Priority:** P1 - Performance impact on auth queries

### 3. Missing SQL Server Auth Indexes 🟠
**File:** `scripts/sql/auth/sqlserver/001_initial.sql`

**Action:** Same 8 indexes as SQLite, using SQL Server syntax:
```sql
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_users_subject')
CREATE INDEX idx_auth_users_subject ON auth.users(subject) WHERE subject IS NOT NULL;
-- ... repeat for all 8
```

**Effort:** 1 hour
**Priority:** P1 - Performance impact on auth queries

---

## Medium Priority Issues (Next Sprint)

### 4. Transaction Missing in Role Update 🟡
**File:** `src/Honua.Server.Core/Data/Auth/SqliteAuthRepository.cs:493-503`

**Current Issue:**
```csharp
// Delete all roles
DELETE FROM auth_user_roles WHERE user_id = @userId;

// Insert new roles (if this fails mid-way, user has partial roles!)
foreach (var role in newRoles) {
    INSERT OR IGNORE INTO auth_user_roles...
}
```

**Fix:** Wrap in transaction:
```csharp
await using var transaction = await connection.BeginTransactionAsync(ct);
// Delete roles (with transaction)
// Insert roles (with transaction)
await transaction.CommitAsync(ct);
```

**Effort:** 30 minutes
**Priority:** P2 - Data integrity risk

### 5. N+1 Query in Redis Attachments 🟡
**File:** `src/Honua.Server.Core/Attachments/RedisFeatureAttachmentRepository.cs:76-99`

**Current:** Calls `FindByIdAsync` for each attachment (N+1 pattern)

**Fix:** Use Redis MGET:
```csharp
var keys = attachmentIds.Select(id => BuildKey(serviceId, layerId, id)).ToArray();
var values = await _database.StringGetAsync(keys);
```

**Effort:** 1 hour
**Priority:** P2 - Performance with many attachments

### 6. Missing CHECK Constraints 🟡
**Files:** All auth.users and stac_items tables

**Add to auth.users:**
```sql
ALTER TABLE auth_users ADD CONSTRAINT chk_auth_users_failed_attempts
    CHECK (failed_attempts >= 0 AND failed_attempts <= 100);

ALTER TABLE auth_users ADD CONSTRAINT chk_auth_users_auth_method
    CHECK (
        (subject IS NOT NULL AND password_hash IS NULL) OR
        (username IS NOT NULL AND password_hash IS NOT NULL) OR
        (email IS NOT NULL AND password_hash IS NOT NULL)
    );
```

**Add to stac_items:**
```sql
ALTER TABLE stac_items ADD CONSTRAINT chk_stac_items_temporal
    CHECK (datetime IS NOT NULL OR (start_datetime IS NOT NULL AND end_datetime IS NOT NULL));

ALTER TABLE stac_items ADD CONSTRAINT chk_stac_items_temporal_order
    CHECK (start_datetime IS NULL OR end_datetime IS NULL OR start_datetime <= end_datetime);
```

**Effort:** 3 hours (all databases)
**Priority:** P2 - Data validation

### 7. Connection Pool Metrics Stubbed 🟡
**File:** `src/Honua.Server.Core/Data/Postgres/PostgresConnectionPoolMetrics.cs:80-114`

**Current:** Returns 0 for active/idle connections (stubbed)

**Fix:** Use Npgsql's statistics API:
```csharp
var stats = dataSource.Statistics;
total += stats.TotalConnections - stats.IdleConnections;
```

**Effort:** 2 hours
**Priority:** P2 - Observability gap

### 8. Create SQLite Performance Indexes Migration 🟡
**File:** `scripts/sql/migrations/sqlite/001_performance_indexes.sql` (doesn't exist)

**Action:** Port PostgreSQL indexes to SQLite syntax

**Effort:** 2 hours
**Priority:** P2 - SQLite performance

### 9. Provider String Check in Business Logic 🟡
**File:** `src/Honua.Server.Core/Data/FeatureRepository.cs:180-183`

**Current:**
```csharp
var shouldPreproject = string.Equals(
    context.Provider.Provider,
    SqliteDataStoreProvider.ProviderKey,
    StringComparison.OrdinalIgnoreCase);
```

**Fix:** Use capability:
```csharp
var shouldPreproject = !context.Provider.Capabilities.SupportsSpatialTransforms;
```

**Effort:** 1 hour
**Priority:** P3 - Code quality

---

## Low Priority Issues (Tech Debt)

### 10. SELECT * in Production Code 🟢
**File:** `src/Honua.Server.Core/OpenRosa/SqliteSubmissionRepository.cs:75, 93`

**Fix:** Define explicit column list

**Effort:** 15 minutes
**Priority:** P4 - Minor performance/maintainability

### 11. Missing Query Hints 🟢
**File:** `src/Honua.Server.Core/Data/Query/QueryOptimizationHelper.cs`

**Fix:** Add index hints for spatial queries

**Effort:** 2 hours
**Priority:** P4 - Query optimization

---

## Implementation Order

### Week 1 (Critical + High)
1. ✅ Create MySQL auth schema (2h)
2. ✅ Add SQLite auth indexes (1h)
3. ✅ Add SQL Server auth indexes (1h)

**Total:** 4 hours

### Week 2 (Medium Priority - Part 1)
4. ✅ Fix role update transaction (30m)
5. ✅ Fix N+1 in Redis attachments (1h)
6. ✅ Add CHECK constraints (3h)

**Total:** 4.5 hours

### Week 3 (Medium Priority - Part 2)
7. ✅ Implement connection pool metrics (2h)
8. ✅ Create SQLite indexes migration (2h)
9. ✅ Fix provider capability check (1h)

**Total:** 5 hours

### Week 4 (Low Priority)
10. ✅ Fix SELECT * usage (15m)
11. ✅ Add query hints (2h)

**Total:** 2.25 hours

**Grand Total:** ~16 hours of work

---

## Testing Requirements

After each fix, run:

### Unit Tests
```bash
dotnet test tests/Honua.Server.Core.Tests/Data/
dotnet test tests/Honua.Server.Core.Tests/Attachments/
dotnet test tests/Honua.Server.Core.Tests/Auth/
```

### Integration Tests
```bash
# Test each database provider
dotnet test tests/Honua.Server.Core.Tests/Data/Postgres/
dotnet test tests/Honua.Server.Core.Tests/Data/MySql/
dotnet test tests/Honua.Server.Core.Tests/Data/SqlServer/
dotnet test tests/Honua.Server.Core.Tests/Data/Sqlite/
```

### Performance Tests
```bash
# Run benchmark suite
dotnet run --project tests/Honua.PerformanceBenchmarks -- --filter "*Index*"
```

---

## Migration Strategy

### For Auth Indexes (Issues #2, #3)

**Development:**
```sql
-- Safe to add indexes anytime
CREATE INDEX IF NOT EXISTS idx_auth_users_subject ON auth_users(subject);
```

**Production:**
```sql
-- PostgreSQL: Use CONCURRENTLY to avoid locks
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_auth_users_subject ON auth.users(subject);

-- MySQL: Safe (adds index without blocking)
CREATE INDEX idx_auth_users_subject ON auth_users(subject);

-- SQL Server: Use ONLINE option
CREATE INDEX idx_auth_users_subject ON auth.users(subject) WITH (ONLINE = ON);
```

### For CHECK Constraints (Issue #6)

**Development:**
```sql
-- Can add directly
ALTER TABLE auth_users ADD CONSTRAINT chk_auth_users_failed_attempts
    CHECK (failed_attempts >= 0);
```

**Production:**
```sql
-- Step 1: Validate existing data first
SELECT COUNT(*) FROM auth_users WHERE failed_attempts < 0 OR failed_attempts > 100;
-- Fix any invalid data

-- Step 2: Add constraint
ALTER TABLE auth_users ADD CONSTRAINT chk_auth_users_failed_attempts
    CHECK (failed_attempts >= 0 AND failed_attempts <= 100);
```

**⚠️ Warning:** CHECK constraints are NOT retroactive. Existing invalid data will prevent constraint creation.

### For Transaction Fix (Issue #4)

**Deployment:**
1. Deploy code change (backward compatible)
2. No database migration needed
3. Test with role updates
4. Monitor for transaction deadlocks (unlikely)

---

## Rollback Plan

### If Indexes Cause Issues

```sql
-- PostgreSQL
DROP INDEX CONCURRENTLY IF EXISTS idx_auth_users_subject;

-- MySQL/SQLite/SQL Server
DROP INDEX IF EXISTS idx_auth_users_subject;
```

### If CHECK Constraints Reject Valid Data

```sql
-- Remove constraint
ALTER TABLE auth_users DROP CONSTRAINT chk_auth_users_failed_attempts;

-- Fix data validation logic
-- Re-add with corrected constraint
```

### If Transaction Causes Deadlocks

```csharp
// Add retry policy to role update
await _retryPipeline.ExecuteAsync(async ct => {
    // Role update with transaction
}, cancellationToken);
```

---

## Success Criteria

✅ **MySQL auth works end-to-end**
- Users can authenticate
- Roles can be assigned
- Audit trail records events

✅ **Auth queries are fast**
- Login: < 50ms (p95)
- Role check: < 10ms (p95)
- Audit query: < 100ms (p95)

✅ **Data integrity maintained**
- Role updates are atomic
- Invalid data rejected by constraints
- No orphaned records

✅ **Observability complete**
- Connection pool metrics visible in Grafana
- Retry attempts tracked
- N+1 queries eliminated

---

## Questions for Stakeholders

1. **MySQL Deployment Timeline:** When is MySQL support needed in production?
2. **Index Maintenance:** Should we create automated index usage monitoring?
3. **Constraint Enforcement:** Are there existing production databases with invalid data?
4. **Connection Pool Sizing:** What are current pool size configurations?
5. **Performance SLOs:** What are acceptable p95 latencies for auth operations?

---

## Additional Resources

- **Full Technical Report:** `DATABASE_DATA_LAYER_REVIEW.md`
- **Index Strategy:** `docs/DATABASE_INDEXING_STRATEGY.md`
- **Retry Patterns:** `RETRY_POLICY_EXAMPLES.md`
- **Query Optimization:** `src/Honua.Server.Core/Data/Query/QueryOptimizationExamples.md`

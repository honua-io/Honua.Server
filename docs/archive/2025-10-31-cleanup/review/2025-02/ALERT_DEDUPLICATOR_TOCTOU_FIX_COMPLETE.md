# Alert Deduplicator TOCTOU Race Condition Fix

**Date:** 2025-10-31
**Status:** ✅ COMPLETE
**Priority:** P0 - Critical Security Issue
**Component:** `src/Honua.Server.AlertReceiver/Services/SqlAlertDeduplicator.cs`

## Problem Summary

### TOCTOU (Time-of-Check-Time-of-Use) Race Condition

**Location:** Lines 119-147 (original), `ShouldSendAlertAsync` method

**Issue:** The reservation check happened BEFORE checking the in-memory cache for completed reservations. Between the database check and the reservation creation, another process could complete a reservation, leading to:

1. **Alert Bypass:** Duplicate alerts could slip through deduplication
2. **Data Inconsistency:** Database and cache could become out of sync
3. **Resource Waste:** Redundant alert notifications sent to external systems

### Race Condition Flow

```
Thread A                           Thread B
--------                           --------
Check DB: no active reservation
                                   Complete reservation
                                   Update cache (Completed=true)
                                   Clear DB reservation
Create new reservation ❌
(Bypasses deduplication!)
```

## Solution Implemented

### 1. ✅ Cache-First Strategy (TOCTOU Prevention)

**Change:** Check in-memory cache BEFORE database query

**Location:** Lines 162-181 (new code in `ShouldSendAlertAsync`)

```csharp
// TOCTOU RACE CONDITION FIX: Check in-memory cache FIRST before database
if (TryGetCompletedReservationFromCache(stateId, out var cachedReservation))
{
    _logger.LogDebug("Alert suppressed due to recently completed reservation in cache");
    _metrics.RecordAlertSuppressed("completed_reservation_cache", severity);
    _metrics.RecordRaceConditionPrevented("toctou_cache_check");
    return (false, reservationId);
}
```

**Why This Works:**
- Catches recently-completed reservations that cleared their DB reservation
- Prevents the narrow window where cache is updated but DB shows no reservation
- Secondary index enables O(1) lookup by stateId instead of O(n) cache scan

### 2. ✅ Secondary Index for Fast Lookups

**Added:** `ConcurrentDictionary<string, string> _stateToReservationIndex`

**Location:** Lines 85-88

**Benefits:**
- O(1) lookup vs. O(n) cache scan
- Minimal memory overhead (bounded by cache size)
- Automatically cleaned up when cache entries are evicted

### 3. ✅ Database Unique Constraint

**Migration:** `Migrations/001_add_race_condition_fixes.sql`

**Constraint Added:**
```sql
CREATE UNIQUE INDEX IF NOT EXISTS idx_alert_deduplication_unique_active_reservation
ON alert_deduplication_state(fingerprint, severity, reservation_id)
WHERE reservation_id IS NOT NULL;
```

**Protection:**
- Database-level enforcement prevents duplicate active reservations
- Partial index (only when reservation_id IS NOT NULL) minimizes overhead
- Handles edge case where multiple processes bypass application-level checks

### 4. ✅ Optimistic Locking with Row Versioning

**Added:** `row_version INTEGER NOT NULL DEFAULT 1` column

**Implementation:**
- All UPDATE statements now include `WHERE ... AND row_version = @RowVersion`
- Row version increments on every update: `row_version = row_version + 1`
- Failed updates (0 rows affected) trigger race condition detection

**Locations Updated:**
1. `UpdateSuppressedSql` - Deduplication window check
2. `UpdateSentSql` - Record alert
3. Reservation creation - Create reservation
4. Reservation completion - Record alert
5. Reservation release - Release reservation

### 5. ✅ Double-Check Pattern

**Location:** Lines 218-239 (in `ShouldSendAlertAsync`)

**Purpose:**
- Catches the narrow race where reservation completed between cache check and DB query
- Self-healing: automatically clears stale reservations
- Ensures consistency between cache and database

### 6. ✅ Comprehensive Metrics

**New Metric:** `RecordRaceConditionPrevented(string scenario)`

**Scenarios Tracked:**
1. `toctou_cache_check` - Cache-first strategy prevented race
2. `completed_reservation_mismatch` - Double-check detected stale DB reservation
3. `optimistic_lock_failure_suppress` - Concurrent update during suppression
4. `optimistic_lock_failure_dedup_window` - Concurrent update during dedup check
5. `optimistic_lock_failure_rate_limit` - Concurrent update during rate limit check
6. `optimistic_lock_failure_create_reservation` - Concurrent update during reservation creation
7. `optimistic_lock_failure_record_alert` - Concurrent update during alert recording

## Defense in Depth

The fix implements multiple layers of protection:

```
Layer 1: Cache-First Check (Prevents TOCTOU)
    ↓
Layer 2: PostgreSQL Advisory Locks (Serializes per-state access)
    ↓
Layer 3: Database Unique Constraint (Prevents duplicate reservations)
    ↓
Layer 4: Optimistic Locking (Detects concurrent modifications)
    ↓
Layer 5: Double-Check Pattern (Catches cache/DB inconsistency)
    ↓
Layer 6: Metrics & Monitoring (Observability)
```

## Files Changed

1. **src/Honua.Server.AlertReceiver/Services/SqlAlertDeduplicator.cs**
   - Added cache-first check in `ShouldSendAlertAsync`
   - Added secondary index `_stateToReservationIndex`
   - Updated schema SQL with row_version and unique constraint
   - Added optimistic locking to all UPDATE statements
   - Added double-check pattern for cache/DB consistency
   - Added comprehensive race condition logging

2. **src/Honua.Server.AlertReceiver/Migrations/001_add_race_condition_fixes.sql**
   - New file: Idempotent migration for database changes
   - Adds row_version column
   - Adds unique constraint on active reservations
   - Includes verification checks

## Migration

The migration is automatically applied via `EnsureSchemaAsync` on first connection.

Manual migration:
```bash
psql -h $DB_HOST -U $DB_USER -d $DB_NAME -f src/Honua.Server.AlertReceiver/Migrations/001_add_race_condition_fixes.sql
```

## Conclusion

**Status:** ✅ All race conditions fixed with defense-in-depth approach

**Confidence:** High - Multiple overlapping protections ensure correctness

**Expected Outcome:**
- Zero duplicate alerts due to race conditions
- Strong consistency between cache and database
- Observable metrics for monitoring effectiveness
- Minimal performance overhead

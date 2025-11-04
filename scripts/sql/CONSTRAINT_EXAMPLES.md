# CHECK Constraint Examples

This document provides clear examples of data that will PASS and FAIL the CHECK constraints.

## Auth Users Constraints

### chk_auth_users_failed_attempts

**Constraint:**
```sql
CHECK (failed_attempts >= 0 AND failed_attempts <= 100)
```

**VALID Examples (Will be accepted):**
```sql
-- Zero attempts
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('user1', 'john', 'hash', 0);

-- Normal range
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('user2', 'jane', 'hash', 5);

-- Maximum allowed
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('user3', 'bob', 'hash', 100);
```

**INVALID Examples (Will be rejected):**
```sql
-- Negative value ❌
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('user1', 'john', 'hash', -1);
-- ERROR: violates check constraint "chk_auth_users_failed_attempts"

-- Above maximum ❌
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('user2', 'jane', 'hash', 101);
-- ERROR: violates check constraint "chk_auth_users_failed_attempts"

-- Way too high ❌
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('user3', 'bob', 'hash', 999);
-- ERROR: violates check constraint "chk_auth_users_failed_attempts"
```

---

### chk_auth_users_auth_method

**Constraint:**
```sql
CHECK (
    (subject IS NOT NULL AND password_hash IS NULL) OR
    (username IS NOT NULL AND password_hash IS NOT NULL) OR
    (email IS NOT NULL AND password_hash IS NOT NULL)
)
```

**VALID Examples (Will be accepted):**
```sql
-- OIDC user (subject only, no password) ✓
INSERT INTO auth.users (id, subject, failed_attempts)
VALUES ('user1', 'oidc|google|123456', 0);

-- Local user with username ✓
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('user2', 'john.doe', 'hashed_password_123', 0);

-- Local user with email ✓
INSERT INTO auth.users (id, email, password_hash, failed_attempts)
VALUES ('user3', 'jane@example.com', 'hashed_password_456', 0);

-- Local user with BOTH username and email (still valid) ✓
INSERT INTO auth.users (id, username, email, password_hash, failed_attempts)
VALUES ('user4', 'bob', 'bob@example.com', 'hashed_password_789', 0);
```

**INVALID Examples (Will be rejected):**
```sql
-- OIDC subject WITH password (conflicting auth methods) ❌
INSERT INTO auth.users (id, subject, password_hash, failed_attempts)
VALUES ('user1', 'oidc|google|123456', 'hash', 0);
-- ERROR: violates check constraint "chk_auth_users_auth_method"

-- Username without password (incomplete local auth) ❌
INSERT INTO auth.users (id, username, failed_attempts)
VALUES ('user2', 'john.doe', 0);
-- ERROR: violates check constraint "chk_auth_users_auth_method"

-- Email without password (incomplete local auth) ❌
INSERT INTO auth.users (id, email, failed_attempts)
VALUES ('user3', 'jane@example.com', 0);
-- ERROR: violates check constraint "chk_auth_users_auth_method"

-- No identifier at all ❌
INSERT INTO auth.users (id, failed_attempts)
VALUES ('user4', 0);
-- ERROR: violates check constraint "chk_auth_users_auth_method"

-- Password without username or email ❌
INSERT INTO auth.users (id, password_hash, failed_attempts)
VALUES ('user5', 'hash', 0);
-- ERROR: violates check constraint "chk_auth_users_auth_method"
```

---

## STAC Items Constraints

### chk_stac_items_temporal

**Constraint:**
```sql
CHECK (
    datetime IS NOT NULL OR
    (start_datetime IS NOT NULL AND end_datetime IS NOT NULL)
)
```

**VALID Examples (Will be accepted):**
```sql
-- Point-in-time item (instant) ✓
INSERT INTO stac_items (
    collection_id, id, datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'sentinel-2', 'item-001', '2024-01-15T12:00:00Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);

-- Temporal range item ✓
INSERT INTO stac_items (
    collection_id, id, start_datetime, end_datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'modis', 'item-002', '2024-01-01T00:00:00Z', '2024-01-31T23:59:59Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);

-- Both datetime AND range (redundant but valid) ✓
INSERT INTO stac_items (
    collection_id, id, datetime, start_datetime, end_datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'landsat', 'item-003', '2024-01-15T12:00:00Z',
    '2024-01-15T12:00:00Z', '2024-01-15T12:00:00Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);
```

**INVALID Examples (Will be rejected):**
```sql
-- No temporal data at all ❌
INSERT INTO stac_items (
    collection_id, id,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'sentinel-2', 'item-001',
    '{}', '[]', '[]',
    NOW(), NOW()
);
-- ERROR: violates check constraint "chk_stac_items_temporal"

-- Only start_datetime (incomplete range) ❌
INSERT INTO stac_items (
    collection_id, id, start_datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'modis', 'item-002', '2024-01-01T00:00:00Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);
-- ERROR: violates check constraint "chk_stac_items_temporal"

-- Only end_datetime (incomplete range) ❌
INSERT INTO stac_items (
    collection_id, id, end_datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'landsat', 'item-003', '2024-01-31T23:59:59Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);
-- ERROR: violates check constraint "chk_stac_items_temporal"
```

---

### chk_stac_items_temporal_order

**Constraint:**
```sql
CHECK (start_datetime IS NULL OR end_datetime IS NULL OR start_datetime <= end_datetime)
```

**VALID Examples (Will be accepted):**
```sql
-- Valid range (start before end) ✓
INSERT INTO stac_items (
    collection_id, id, start_datetime, end_datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'modis', 'item-001', '2024-01-01T00:00:00Z', '2024-01-31T23:59:59Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);

-- Same start and end (instant as range) ✓
INSERT INTO stac_items (
    collection_id, id, start_datetime, end_datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'sentinel-2', 'item-002', '2024-01-15T12:00:00Z', '2024-01-15T12:00:00Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);

-- NULL values (point-in-time item) ✓
INSERT INTO stac_items (
    collection_id, id, datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'landsat', 'item-003', '2024-01-15T12:00:00Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);
```

**INVALID Examples (Will be rejected):**
```sql
-- End before start (reversed range) ❌
INSERT INTO stac_items (
    collection_id, id, start_datetime, end_datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'modis', 'item-001', '2024-12-31T23:59:59Z', '2024-01-01T00:00:00Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);
-- ERROR: violates check constraint "chk_stac_items_temporal_order"

-- End significantly before start ❌
INSERT INTO stac_items (
    collection_id, id, start_datetime, end_datetime,
    assets_json, links_json, extensions_json,
    created_at, updated_at
)
VALUES (
    'sentinel-2', 'item-002', '2024-06-15T12:00:00Z', '2024-01-15T12:00:00Z',
    '{}', '[]', '[]',
    NOW(), NOW()
);
-- ERROR: violates check constraint "chk_stac_items_temporal_order"
```

---

## Testing These Examples

### Quick Test (PostgreSQL)

```bash
# Apply migrations first
psql -d honua -f scripts/sql/auth/postgres/002_add_check_constraints.sql
psql -d honua -f scripts/sql/stac/postgres/003_add_check_constraints.sql

# Run comprehensive tests
psql -d honua -f scripts/sql/test_check_constraints.sql
```

### Manual Testing

```sql
-- Start a transaction to test without affecting the database
BEGIN;

-- Test a valid insert (should succeed)
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('test1', 'testuser', '\x1234'::bytea, 5);

-- Test an invalid insert (should fail)
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('test2', 'testuser2', '\x1234'::bytea, -1);

-- Rollback to clean up
ROLLBACK;
```

## Summary

✅ **Valid Data Patterns:**
- Auth: OIDC users (subject only) OR local users (username/email + password)
- STAC: Items with datetime OR items with start+end datetime
- All ranges: start <= end

❌ **Invalid Data Patterns:**
- Auth: Mixed auth methods, incomplete credentials, no identifier
- STAC: Missing temporal data, incomplete ranges, reversed ranges
- All: Values outside allowed bounds

These constraints ensure data integrity at the database level, complementing application-level validation.

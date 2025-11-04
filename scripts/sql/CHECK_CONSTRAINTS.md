# Database CHECK Constraints

This document describes the CHECK constraints added to Honua database tables for data validation.

## Overview

CHECK constraints provide database-level validation to prevent invalid data from being inserted or updated. They complement application-level validation by ensuring data integrity even if application code is bypassed.

## Auth Tables

### auth.users / auth_users

#### chk_auth_users_failed_attempts

**Purpose**: Validates failed login attempts are within reasonable bounds

**Constraint**:
```sql
CHECK (failed_attempts >= 0 AND failed_attempts <= 100)
```

**Rationale**:
- Prevents negative values (invalid state)
- Prevents unreasonably high values (potential integer overflow or application bug)
- Upper limit of 100 is well above any legitimate lockout threshold

**Migration Files**:
- PostgreSQL: `/scripts/sql/auth/postgres/002_add_check_constraints.sql`
- SQL Server: `/scripts/sql/auth/sqlserver/002_add_check_constraints.sql`
- SQLite: `/scripts/sql/auth/sqlite/002_add_check_constraints.sql`
- MySQL: `/scripts/sql/auth/mysql/002_add_check_constraints.sql`

#### chk_auth_users_auth_method

**Purpose**: Ensures users have valid authentication method configuration

**Constraint**:
```sql
CHECK (
    (subject IS NOT NULL AND password_hash IS NULL) OR
    (username IS NOT NULL AND password_hash IS NOT NULL) OR
    (email IS NOT NULL AND password_hash IS NOT NULL)
)
```

**Rationale**:
- **OIDC users**: Must have `subject` (from identity provider) and no password
- **Local users**: Must have either `username` or `email` AND a password hash
- Prevents invalid states like:
  - User with subject AND password (conflicting auth methods)
  - User with neither subject nor username/email (no way to authenticate)
  - User with username/email but no password (incomplete local auth)

**Migration Files**:
- PostgreSQL: `/scripts/sql/auth/postgres/002_add_check_constraints.sql`
- SQL Server: `/scripts/sql/auth/sqlserver/002_add_check_constraints.sql`
- SQLite: `/scripts/sql/auth/sqlite/002_add_check_constraints.sql`
- MySQL: `/scripts/sql/auth/mysql/002_add_check_constraints.sql`

## STAC Tables

### stac_items

#### chk_stac_items_temporal

**Purpose**: Validates STAC temporal requirement per specification

**Constraint**:
```sql
CHECK (
    datetime IS NOT NULL OR
    (start_datetime IS NOT NULL AND end_datetime IS NOT NULL)
)
```

**Rationale**:
- STAC specification requires temporal information
- Items must have EITHER:
  - `datetime` for point-in-time (instant)
  - BOTH `start_datetime` AND `end_datetime` for temporal range
- Prevents invalid states:
  - Items with no temporal data
  - Items with only start OR end (incomplete range)

**Migration Files**:
- PostgreSQL: `/scripts/sql/stac/postgres/003_add_check_constraints.sql`
- SQL Server: `/scripts/sql/stac/sqlserver/003_add_check_constraints.sql`
- SQLite: `/scripts/sql/stac/sqlite/003_add_check_constraints.sql`
- MySQL: `/scripts/sql/stac/mysql/003_add_check_constraints.sql`

#### chk_stac_items_temporal_order

**Purpose**: Ensures temporal range validity

**Constraint**:
```sql
CHECK (start_datetime IS NULL OR end_datetime IS NULL OR start_datetime <= end_datetime)
```

**Rationale**:
- Temporal ranges must be logically valid
- Start time must be before or equal to end time
- Prevents invalid temporal ranges (end before start)
- Allows NULL values for point-in-time items

**Migration Files**:
- PostgreSQL: `/scripts/sql/stac/postgres/003_add_check_constraints.sql`
- SQL Server: `/scripts/sql/stac/sqlserver/003_add_check_constraints.sql`
- SQLite: `/scripts/sql/stac/sqlite/003_add_check_constraints.sql`
- MySQL: `/scripts/sql/stac/mysql/003_add_check_constraints.sql`

## Database-Specific Notes

### PostgreSQL
- Supports CHECK constraints natively
- Can add comments via `COMMENT ON CONSTRAINT`
- Migrations use `ALTER TABLE ADD CONSTRAINT`

### SQL Server
- Supports CHECK constraints natively
- Uses `sys.check_constraints` to check if constraint exists
- Can add extended properties for documentation
- Migrations wrapped in `IF NOT EXISTS` blocks with `GO` batch separators

### SQLite
- CHECK constraints must be defined during table creation
- Migrations recreate tables to add constraints
- Data is preserved during migration
- Triggers and indexes must be recreated after table replacement

### MySQL
- CHECK constraint support added in MySQL 8.0.16
- Syntax similar to PostgreSQL
- Simple `ALTER TABLE ADD CONSTRAINT` syntax

## Testing

### Valid Data Examples

**Auth Users**:
```sql
-- OIDC user (valid)
INSERT INTO auth_users (id, subject, failed_attempts)
VALUES ('user1', 'oidc-subject-123', 0);

-- Local user with username (valid)
INSERT INTO auth_users (id, username, password_hash, failed_attempts)
VALUES ('user2', 'john.doe', 'hashed-password', 2);

-- Local user with email (valid)
INSERT INTO auth_users (id, email, password_hash, failed_attempts)
VALUES ('user3', 'john@example.com', 'hashed-password', 5);
```

**STAC Items**:
```sql
-- Point-in-time item (valid)
INSERT INTO stac_items (collection_id, id, datetime, assets_json, links_json, extensions_json, created_at, updated_at)
VALUES ('col1', 'item1', '2024-01-15T12:00:00Z', '{}', '[]', '[]', NOW(), NOW());

-- Temporal range item (valid)
INSERT INTO stac_items (collection_id, id, start_datetime, end_datetime, assets_json, links_json, extensions_json, created_at, updated_at)
VALUES ('col1', 'item2', '2024-01-01T00:00:00Z', '2024-01-31T23:59:59Z', '{}', '[]', '[]', NOW(), NOW());
```

### Invalid Data Examples (Will be rejected)

**Auth Users**:
```sql
-- Negative failed_attempts (INVALID)
INSERT INTO auth_users (id, username, password_hash, failed_attempts)
VALUES ('user1', 'john', 'hash', -1);
-- ERROR: violates check constraint "chk_auth_users_failed_attempts"

-- Failed_attempts too high (INVALID)
INSERT INTO auth_users (id, username, password_hash, failed_attempts)
VALUES ('user2', 'jane', 'hash', 150);
-- ERROR: violates check constraint "chk_auth_users_failed_attempts"

-- Subject AND password (INVALID - conflicting auth methods)
INSERT INTO auth_users (id, subject, password_hash, failed_attempts)
VALUES ('user3', 'oidc-123', 'hash', 0);
-- ERROR: violates check constraint "chk_auth_users_auth_method"

-- Username without password (INVALID - incomplete local auth)
INSERT INTO auth_users (id, username, failed_attempts)
VALUES ('user4', 'bob', 0);
-- ERROR: violates check constraint "chk_auth_users_auth_method"

-- No identifier (INVALID - no auth method)
INSERT INTO auth_users (id, failed_attempts)
VALUES ('user5', 0);
-- ERROR: violates check constraint "chk_auth_users_auth_method"
```

**STAC Items**:
```sql
-- No temporal data (INVALID)
INSERT INTO stac_items (collection_id, id, assets_json, links_json, extensions_json, created_at, updated_at)
VALUES ('col1', 'item1', '{}', '[]', '[]', NOW(), NOW());
-- ERROR: violates check constraint "chk_stac_items_temporal"

-- Only start_datetime (INVALID - incomplete range)
INSERT INTO stac_items (collection_id, id, start_datetime, assets_json, links_json, extensions_json, created_at, updated_at)
VALUES ('col1', 'item2', '2024-01-01T00:00:00Z', '{}', '[]', '[]', NOW(), NOW());
-- ERROR: violates check constraint "chk_stac_items_temporal"

-- End before start (INVALID - reversed range)
INSERT INTO stac_items (collection_id, id, start_datetime, end_datetime, assets_json, links_json, extensions_json, created_at, updated_at)
VALUES ('col1', 'item3', '2024-12-31T23:59:59Z', '2024-01-01T00:00:00Z', '{}', '[]', '[]', NOW(), NOW());
-- ERROR: violates check constraint "chk_stac_items_temporal_order"
```

## Migration Order

These migrations should be applied after the initial table creation:

1. **Auth tables**: Migration 002 (001 is initial table creation)
2. **STAC tables**: Migration 003 (001 is initial creation, 002 is temporal indexes)

## Rollback

If you need to remove these constraints:

### PostgreSQL
```sql
ALTER TABLE auth.users DROP CONSTRAINT chk_auth_users_failed_attempts;
ALTER TABLE auth.users DROP CONSTRAINT chk_auth_users_auth_method;
ALTER TABLE stac_items DROP CONSTRAINT chk_stac_items_temporal;
ALTER TABLE stac_items DROP CONSTRAINT chk_stac_items_temporal_order;
```

### SQL Server
```sql
ALTER TABLE auth.users DROP CONSTRAINT chk_auth_users_failed_attempts;
ALTER TABLE auth.users DROP CONSTRAINT chk_auth_users_auth_method;
ALTER TABLE stac_items DROP CONSTRAINT chk_stac_items_temporal;
ALTER TABLE stac_items DROP CONSTRAINT chk_stac_items_temporal_order;
```

### MySQL
```sql
ALTER TABLE auth_users DROP CONSTRAINT chk_auth_users_failed_attempts;
ALTER TABLE auth_users DROP CONSTRAINT chk_auth_users_auth_method;
ALTER TABLE stac_items DROP CONSTRAINT chk_stac_items_temporal;
ALTER TABLE stac_items DROP CONSTRAINT chk_stac_items_temporal_order;
```

### SQLite
SQLite doesn't support dropping constraints. You would need to recreate the table without the constraints.

## Benefits

1. **Data Integrity**: Prevents invalid data at the database level
2. **Defense in Depth**: Works even if application validation is bypassed
3. **Early Error Detection**: Catches bugs during development
4. **Documentation**: Constraints serve as executable documentation of business rules
5. **Cross-Platform**: Works across all supported databases

## Performance Impact

CHECK constraints have minimal performance impact:
- Evaluated only during INSERT/UPDATE operations
- Simple boolean expressions are very fast
- No additional indexes or storage required
- Constraints are compiled and optimized by the database engine

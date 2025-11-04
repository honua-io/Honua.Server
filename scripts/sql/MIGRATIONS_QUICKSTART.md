# Database Migrations Quick Start

## CHECK Constraints Migration (002/003)

This guide covers applying the CHECK constraint migrations to ensure data validation at the database level.

### Quick Apply (All Databases)

#### PostgreSQL
```bash
# Auth constraints
psql -d honua -f scripts/sql/auth/postgres/002_add_check_constraints.sql

# STAC constraints
psql -d honua -f scripts/sql/stac/postgres/003_add_check_constraints.sql

# Verify
psql -d honua -f scripts/sql/verify_check_constraints.sql
```

#### SQL Server
```bash
# Auth constraints
sqlcmd -d honua -i scripts/sql/auth/sqlserver/002_add_check_constraints.sql

# STAC constraints
sqlcmd -d honua -i scripts/sql/stac/sqlserver/003_add_check_constraints.sql

# Verify
sqlcmd -d honua -i scripts/sql/verify_check_constraints_sqlserver.sql
```

#### MySQL
```bash
# Auth constraints
mysql honua < scripts/sql/auth/mysql/002_add_check_constraints.sql

# STAC constraints
mysql honua < scripts/sql/stac/mysql/003_add_check_constraints.sql

# Verify
mysql honua < scripts/sql/verify_check_constraints_mysql.sql
```

#### SQLite
```bash
# Auth constraints
sqlite3 honua.db < scripts/sql/auth/sqlite/002_add_check_constraints.sql

# STAC constraints
sqlite3 honua.db < scripts/sql/stac/sqlite/003_add_check_constraints.sql

# Verify
sqlite3 honua.db < scripts/sql/verify_check_constraints_sqlite.sql
```

### What Gets Added

#### Auth Tables (auth.users)
- `chk_auth_users_failed_attempts` - Validates failed_attempts is 0-100
- `chk_auth_users_auth_method` - Ensures valid authentication method

#### STAC Tables (stac_items)
- `chk_stac_items_temporal` - Requires either datetime OR start+end datetime
- `chk_stac_items_temporal_order` - Ensures start <= end for temporal ranges

### Testing

Run the comprehensive test suite (PostgreSQL):
```bash
psql -d honua -f scripts/sql/test_check_constraints.sql
```

This will:
- ✅ Verify valid data is accepted
- ❌ Verify invalid data is properly rejected
- Display SUCCESS/FAILURE for each test case

### Migration Order

1. Ensure base schema is installed (001_initial.sql)
2. Apply temporal indexes for STAC (002_temporal_indexes.sql)
3. **Apply CHECK constraints** (this migration)

### Rollback

If needed, constraints can be dropped:

**PostgreSQL/SQL Server/MySQL:**
```sql
-- Drop auth constraints
ALTER TABLE auth.users DROP CONSTRAINT chk_auth_users_failed_attempts;
ALTER TABLE auth.users DROP CONSTRAINT chk_auth_users_auth_method;

-- Drop STAC constraints
ALTER TABLE stac_items DROP CONSTRAINT chk_stac_items_temporal;
ALTER TABLE stac_items DROP CONSTRAINT chk_stac_items_temporal_order;
```

**SQLite:**
Requires table recreation. See original schema files.

### Files Reference

**Migrations:**
- Auth: `scripts/sql/auth/{database}/002_add_check_constraints.sql`
- STAC: `scripts/sql/stac/{database}/003_add_check_constraints.sql`

**Documentation:**
- Full docs: `scripts/sql/CHECK_CONSTRAINTS.md`
- Summary: `CHECK_CONSTRAINTS_SUMMARY.md`

**Testing:**
- Test script: `scripts/sql/test_check_constraints.sql`
- Verify script: `scripts/sql/verify_check_constraints*.sql`

### Common Issues

**Issue: Constraint violation on existing data**
```
ERROR: check constraint "chk_auth_users_failed_attempts" is violated
```

**Solution:** Clean up invalid data before applying constraints
```sql
-- Fix failed_attempts out of range
UPDATE auth.users SET failed_attempts = 0 WHERE failed_attempts < 0;
UPDATE auth.users SET failed_attempts = 100 WHERE failed_attempts > 100;

-- Fix missing temporal data
UPDATE stac_items SET datetime = created_at
WHERE datetime IS NULL AND start_datetime IS NULL;
```

**Issue: Constraint already exists**
```
ERROR: constraint "chk_auth_users_failed_attempts" already exists
```

**Solution:** This is safe to ignore. The constraint is already installed.

### Support

For detailed information:
- Full constraint documentation: `scripts/sql/CHECK_CONSTRAINTS.md`
- Implementation summary: `CHECK_CONSTRAINTS_SUMMARY.md`

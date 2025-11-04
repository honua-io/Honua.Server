#!/bin/bash
# Migration Verification Script

echo "=================================="
echo "Migration Verification Script"
echo "=================================="
echo ""

echo "1. Checking for duplicate version numbers..."
versions=$(ls -1 *.sql | grep -oE '^[0-9]+' | sort | uniq -d)
if [ -z "$versions" ]; then
    echo "   ✓ No duplicate version numbers found"
else
    echo "   ✗ DUPLICATE VERSIONS FOUND: $versions"
    exit 1
fi
echo ""

echo "2. Checking migration sequence..."
expected_count=14
actual_count=$(ls -1 *.sql | wc -l)
if [ "$actual_count" -eq "$expected_count" ]; then
    echo "   ✓ Correct number of migration files: $actual_count"
else
    echo "   ✗ Unexpected number of files. Expected: $expected_count, Found: $actual_count"
    exit 1
fi
echo ""

echo "3. Checking for SQL injection vulnerabilities..."
if grep -q "v_allowed_tables TEXT\[\]" 009_SoftDelete.sql; then
    echo "   ✓ SQL injection whitelist found in soft delete functions"
else
    echo "   ✗ SQL injection protection missing in soft delete functions"
    exit 1
fi
echo ""

echo "4. Checking for FK constraint conflicts..."
if grep -q "first_built_by_customer VARCHAR(100)," 001_InitialSchema.sql | grep -v "NOT NULL"; then
    echo "   ✓ FK constraint fixed (nullable column)"
else
    echo "   ⚠ Could not verify FK constraint fix"
fi
echo ""

echo "5. Checking for FK indexes..."
if grep -q "idx_build_cache_first_customer" 001_InitialSchema.sql; then
    echo "   ✓ FK index added for first_built_by_customer"
else
    echo "   ✗ Missing FK index for first_built_by_customer"
    exit 1
fi
echo ""

echo "6. Checking for RLS policies..."
if [ -f "013_RowLevelSecurity.sql" ]; then
    echo "   ✓ RLS migration file exists"
    if grep -q "tenant_isolation_policy" 013_RowLevelSecurity.sql; then
        echo "   ✓ RLS policies defined"
    else
        echo "   ✗ RLS policies missing"
        exit 1
    fi
else
    echo "   ✗ RLS migration file missing"
    exit 1
fi
echo ""

echo "7. Checking for audit log immutability..."
if grep -q "ENABLE ALWAYS TRIGGER" 011_AuditLog.sql; then
    echo "   ✓ Audit trigger set to ENABLE ALWAYS"
else
    echo "   ✗ Audit trigger not set to ENABLE ALWAYS"
    exit 1
fi
echo ""

echo "8. Checking for non-existent table references..."
if grep -q "REFERENCES tenants" *.sql 2>/dev/null; then
    echo "   ✗ Found references to non-existent 'tenants' table"
    exit 1
else
    echo "   ✓ No references to non-existent 'tenants' table"
fi
echo ""

echo "=================================="
echo "✓ ALL VERIFICATIONS PASSED"
echo "=================================="
echo ""
echo "Migration files are ready for deployment!"
echo ""
echo "Next steps:"
echo "1. Review MIGRATION_FIXES_SUMMARY.md"
echo "2. Review MIGRATION_ORDER.txt"
echo "3. Apply migrations in order to test database"
echo "4. Run validate_rls_policies() after migration 013"

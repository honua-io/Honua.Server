#!/bin/bash
# Simple test script for PostgreSQL optimization functions
# Tests the 7 optimization functions installed in the database

set -e

echo "=========================================="
echo "PostgreSQL Optimization Functions Test"
echo "=========================================="
echo

CONTAINER="honua-postgres-optimization-test"
PSQL_BASE="docker exec $CONTAINER psql -U postgres -d honua_test"

# Helper function to run SQL with search path
run_sql() {
    $PSQL_BASE -v ON_ERROR_STOP=1 -t -A -c "SET search_path TO test,public; $1" | tail -1
}

# Test 1: Verify all functions exist
echo "Test 1: Verify all 7 functions exist"
echo "--------------------------------------"
FUNC_COUNT=$(run_sql "SELECT COUNT(*) FROM pg_proc WHERE proname LIKE 'honua_%';")
echo "Found $FUNC_COUNT Honua optimization functions"
if [ "$FUNC_COUNT" -eq "7" ]; then
    echo "✓ PASSED: All 7 functions exist"
else
    echo "✗ FAILED: Expected 7 functions, found $FUNC_COUNT"
    exit 1
fi
echo

# Test 2: Verify functions are PARALLEL SAFE
echo "Test 2: Verify functions are PARALLEL SAFE"
echo "-------------------------------------------"
PARALLEL_SAFE=$(run_sql "SELECT COUNT(*) FROM pg_proc WHERE proname LIKE 'honua_%' AND proname != 'honua_validate_and_repair_geometries' AND proparallel = 's';")
echo "Found $PARALLEL_SAFE PARALLEL SAFE functions (should be 6)"
if [ "$PARALLEL_SAFE" -eq "6" ]; then
    echo "✓ PASSED: 6 functions are PARALLEL SAFE"
else
    echo "✗ FAILED: Expected 6 PARALLEL SAFE functions, found $PARALLEL_SAFE"
    exit 1
fi
echo

# Test 3: Test honua_fast_count function
echo "Test 3: Test honua_fast_count function"
echo "---------------------------------------"
COUNT=$(run_sql "SELECT honua_fast_count('spatial_features', 'geom');")
echo "Count of features in spatial_features: $COUNT"
if [ "$COUNT" -eq "4" ]; then
    echo "✓ PASSED: Fast count returned correct value"
else
    echo "✗ FAILED: Expected count of 4, got $COUNT"
    exit 1
fi
echo

# Test 4: Test honua_aggregate_features function
echo "Test 4: Test honua_aggregate_features function"
echo "-----------------------------------------------"
RESULT=$(run_sql "SELECT total_count FROM honua_aggregate_features('spatial_features', 'geom');")
echo "Aggregate total_count: $RESULT"
if [ "$RESULT" -eq "4" ]; then
    echo "✓ PASSED: Aggregate function returned correct count"
else
    echo "✗ FAILED: Expected total_count of 4, got $RESULT"
    exit 1
fi
echo

# Test 5: Test honua_get_features_optimized function
echo "Test 5: Test honua_get_features_optimized function"
echo "---------------------------------------------------"
FEATURES=$(run_sql "SELECT COUNT(*) FROM honua_get_features_optimized('spatial_features', 'geom', ST_MakeEnvelope(-180, -90, 180, 90, 4326), 10, NULL, 100);")
echo "Features returned by get_features_optimized: $FEATURES"
if [ "$FEATURES" -eq "4" ]; then
    echo "✓ PASSED: Get features returned all 4 features"
else
    echo "✗ FAILED: Expected 4 features, got $FEATURES"
    exit 1
fi
echo

# Test 6: Test honua_get_mvt_tile function
echo "Test 6: Test honua_get_mvt_tile function"
echo "-----------------------------------------"
TILE_SIZE=$(run_sql "SELECT LENGTH(honua_get_mvt_tile('spatial_features', 'geom', 5, 16, 10));")
echo "MVT tile size (bytes): $TILE_SIZE"
if [ "$TILE_SIZE" -gt "0" ]; then
    echo "✓ PASSED: MVT tile generated successfully"
else
    echo "✗ FAILED: MVT tile is empty"
    exit 1
fi
echo

# Test 7: Test honua_spatial_query function
echo "Test 7: Test honua_spatial_query function"
echo "------------------------------------------"
SPATIAL_RESULTS=$(run_sql "SELECT COUNT(*) FROM honua_spatial_query('spatial_features', 'geom', ST_MakeEnvelope(-180, -90, 180, 90, 4326), 'intersects', 4326, NULL, 100);")
echo "Spatial query results: $SPATIAL_RESULTS"
if [ "$SPATIAL_RESULTS" -eq "4" ]; then
    echo "✓ PASSED: Spatial query returned all features"
else
    echo "✗ FAILED: Expected 4 results, got $SPATIAL_RESULTS"
    exit 1
fi
echo

echo "=========================================="
echo "All tests PASSED!"
echo "=========================================="
echo
echo "Summary:"
echo "  ✓ All 7 optimization functions exist"
echo "  ✓ 6 functions are PARALLEL SAFE"
echo "  ✓ honua_fast_count works correctly"
echo "  ✓ honua_aggregate_features works correctly"
echo "  ✓ honua_get_features_optimized works correctly"
echo "  ✓ honua_get_mvt_tile works correctly"
echo "  ✓ honua_spatial_query works correctly"
echo

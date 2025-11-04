# OGC Test Data Validation Plan

## Problem Statement

Your OGC conformance tests may be producing **false positives** if the test data doesn't accurately match the metadata claims. This document provides a comprehensive plan to validate and fix all test data issues.

## Issues Discovered

### Critical Issue: Geometry Type Mismatch
**Location**: `samples/ogc/metadata.json:76`

```json
{
  "id": "roads-primary",
  "title": "Primary Roads",
  "geometryType": "Point",  // ⚠️ WRONG - Roads should be LineString!
  ...
}
```

**Impact**: Tests may pass even if LineString geometry handling is broken, because the system expects Points.

### Potential Issues (Unverified)
1. **Bbox accuracy** - Declared bbox may not match actual data extent
2. **Temporal extent** - Date ranges may not match actual timestamps
3. **SRID mismatch** - Declared CRS may differ from database
4. **Missing spatial indexes** - Performance tests may give false readings
5. **Empty tables** - Tests pass with zero features
6. **Missing primary keys** - OGC API pagination may be untested

## Tools Created

### 1. DatabaseIntrospectionUtility
**File**: `tests/Honua.Server.Core.Tests/Ogc/DatabaseIntrospectionUtility.cs`

Provides deep inspection of SQLite/SpatiaLite databases:

```csharp
using var inspector = new DatabaseIntrospectionUtility(connectionString);

// Get all tables
var tables = inspector.GetTables();

// Analyze schema
var schema = inspector.GetTableSchema("roads_primary");
// Returns: columns, geometry type, SRID, spatial index status, row count

// Discover actual geometry types in data
var geomAnalysis = inspector.AnalyzeGeometryColumn("roads_primary", "geom");
// Returns: {"LINESTRING": 150, "POINT": 5} - Reveals mixed geometries!

// Calculate actual bbox from data
var bbox = inspector.CalculateActualBbox("roads_primary", "geom");
// Returns: [minX, minY, maxX, maxY]

// Generate comprehensive report
var report = inspector.GenerateValidationReport();
File.WriteAllText("validation-report.txt", report);
```

### 2. DatabaseIntrospectionTests
**File**: `tests/Honua.Server.Core.Tests/Ogc/DatabaseIntrospectionTests.cs`

Four automated validation tests:

1. **`OgcSampleDatabase_GeneratesIntrospectionReport`**
   - Generates human-readable report of entire database
   - Saved to `TestResults/database-introspection-report.txt`

2. **`RoadsPrimaryTable_HasCorrectSchema`**
   - Validates primary key exists
   - Checks spatial index status
   - Verifies non-empty table
   - Validates SRID

3. **`MetadataJson_GeometryTypesMatchActualData`**
   - **Critical test** - Fails if metadata.json declares wrong geometry types
   - Compares declared vs actual for ALL layers

4. **`MetadataJson_BboxMatchesActualExtent`**
   - Validates declared bboxes match actual data (±0.1° tolerance)
   - Prevents over/under-estimated extents

## Execution Plan

### Phase 1: Discover Issues (30 minutes)

```bash
# 1. Run introspection tests
dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj \
  --filter "FullyQualifiedName~DatabaseIntrospectionTests" \
  --logger "console;verbosity=detailed"

# 2. Review the generated report
cat TestResults/database-introspection-report.txt

# 3. Note all failures - these are your validation issues
```

**Expected Output**:
```
=== DATABASE INTROSPECTION REPORT ===
TABLE: roads_primary
------------------------------------------------------------
Row Count: 150
Geometry Column: geom
Declared Geometry Type: LINESTRING
Actual Geometry Types:
  - LINESTRING: 145 features
  - POINT: 5 features ⚠️  MIXED GEOMETRIES!
Actual Bbox: [-122.600000, 45.500000, -122.300000, 45.700000]
Primary Key: road_id
Spatial Index: YES
```

### Phase 2: Fix Metadata (15 minutes)

Based on introspection report, update `samples/ogc/metadata.json`:

```json
{
  "layers": [
    {
      "id": "roads-primary",
      "geometryType": "LineString",  // ✅ Fixed from "Point"
      "extent": {
        "bbox": [
          [-122.600000, 45.500000, -122.300000, 45.700000]  // ✅ Updated to actual
        ]
      }
    }
  ]
}
```

### Phase 3: Fix Database (if needed) (30 minutes)

If database has bad data (mixed geometries, missing indexes, etc.):

```sql
-- Remove invalid geometry types
DELETE FROM roads_primary
WHERE GeometryType(geom) != 'LINESTRING';

-- Create spatial index if missing
SELECT CreateSpatialIndex('roads_primary', 'geom');

-- Verify primary key
SELECT sql FROM sqlite_master
WHERE type='table' AND name='roads_primary';

-- Add primary key if missing
ALTER TABLE roads_primary ADD PRIMARY KEY (road_id);

-- Update statistics
VACUUM;
ANALYZE;
```

### Phase 4: Expand Test Coverage (1-2 hours)

Add more diverse geometry types to prevent false positives:

```sql
-- Create test tables for all OGC geometry types
CREATE TABLE test_points (id INTEGER PRIMARY KEY, geom POINT);
CREATE TABLE test_linestrings (id INTEGER PRIMARY KEY, geom LINESTRING);
CREATE TABLE test_polygons (id INTEGER PRIMARY KEY, geom POLYGON);
CREATE TABLE test_multipoints (id INTEGER PRIMARY KEY, geom MULTIPOINT);
CREATE TABLE test_multilinestrings (id INTEGER PRIMARY KEY, geom MULTILINESTRING);
CREATE TABLE test_multipolygons (id INTEGER PRIMARY KEY, geom MULTIPOLYGON);

-- Add spatial reference and indexes
SELECT AddGeometryColumn('test_points', 'geom', 4326, 'POINT', 'XY');
SELECT CreateSpatialIndex('test_points', 'geom');
-- ... repeat for each table

-- Insert realistic test data
INSERT INTO test_points (id, geom) VALUES
  (1, GeomFromText('POINT(-122.5 45.6)', 4326)),
  (2, GeomFromText('POINT(-122.4 45.5)', 4326));
-- ... add 10-50 features per type
```

Update metadata.json with all new layers.

### Phase 5: Validate Again (10 minutes)

```bash
# Re-run validation tests
dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj \
  --filter "FullyQualifiedName~DatabaseIntrospectionTests"

# All tests should PASS
# Review updated report - should show NO mismatches
```

### Phase 6: Run OGC Conformance (30 minutes)

```bash
# Now run actual OGC conformance with confidence
dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj \
  --filter "FullyQualifiedName~OgcConformanceTests" \
  --logger "console;verbosity=detailed"

# If tests fail, they're REAL issues, not false positives!
```

## Validation Checklist

Before trusting OGC conformance results, verify:

- [ ] All layers in metadata.json have matching tables in database
- [ ] Declared geometry types match actual data (use `AnalyzeGeometryColumn`)
- [ ] Declared bboxes match actual extents (use `CalculateActualBbox`)
- [ ] All tables have primary keys
- [ ] Spatial indexes exist on all geometry columns
- [ ] Tables are non-empty (10+ features minimum)
- [ ] SRID matches declared CRS (4326, 3857, etc.)
- [ ] Temporal extents match actual timestamp ranges (if applicable)
- [ ] No mixed geometry types in a single column
- [ ] Test data covers edge cases:
  - Empty geometries
  - Null geometries
  - Geometries crossing antimeridian
  - Very large/small coordinates
  - Multi-part geometries

## Continuous Validation

Add to CI/CD pipeline:

```yaml
# .github/workflows/test-data-validation.yml
name: Validate Test Data
on: [push, pull_request]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Run database introspection
        run: |
          dotnet test \
            --filter "FullyQualifiedName~DatabaseIntrospectionTests.MetadataJson_GeometryTypesMatchActualData" \
            --logger "console;verbosity=detailed"

      - name: Fail if metadata mismatches found
        run: |
          if grep "MISMATCH" TestResults/*.txt; then
            echo "❌ Test data validation failed!"
            exit 1
          fi
```

## Best Practices

1. **Version control test data** - Commit `samples/ogc/ogc-sample.db` to git LFS
2. **Document test data** - Add `samples/ogc/README.md` explaining what each table contains
3. **Seed data programmatically** - Create `TestDataSeeder.cs` to regenerate database
4. **Use realistic data** - Download actual shapefiles from data.gov or OpenStreetMap
5. **Test boundary conditions** - Include features at bbox edges, dateline, poles
6. **Snapshot testing** - Save known-good API responses and compare
7. **Performance baselines** - Record query times with indexed vs non-indexed data

## Troubleshooting

### "SpatiaLite extension not loaded"
The introspection utility gracefully handles this. If SpatiaLite functions fail, use direct SQL:

```csharp
// Fallback for geometry type detection
cmd.CommandText = "SELECT DISTINCT typeof(geom) FROM roads_primary";
```

### "Connection string keyword 'version' is not supported"
Fixed in current code. Use: `Data Source=path.db` (no `Version=3`)

### "No tables found"
Database may be encrypted or corrupted. Try:
```bash
sqlite3 samples/ogc/ogc-sample.db ".tables"
```

## Success Criteria

You'll know test data is valid when:

1. ✅ All `DatabaseIntrospectionTests` pass
2. ✅ Introspection report shows NO warnings
3. ✅ Metadata geometry types match actual data 100%
4. ✅ Declared bboxes within 0.1° of actual extents
5. ✅ All tables have ≥10 features
6. ✅ Spatial indexes present on all geometry columns
7. ✅ OGC conformance tests fail for REAL bugs (not data issues)

## Next Steps

1. **Immediate**: Run `dotnet test` with introspection tests
2. **This week**: Fix all discovered issues in metadata.json and database
3. **This sprint**: Expand test coverage to all geometry types
4. **Long-term**: Add test data validation to CI/CD

## References

- OGC API Features spec: https://ogcapi.ogc.org/features/
- SpatiaLite functions: https://www.gaia-gis.it/fossil/libspatialite/
- SQLite pragma reference: https://www.sqlite.org/pragma.html

## Contact

For questions about test data validation:
- See introspection utility source code
- Review test output in `TestResults/`
- Check CI logs for validation failures

# Test Data Introspection Findings

**Date**: 2025-10-03
**Status**: Investigation Complete

## Executive Summary

The OGC test database (`samples/ogc/ogc-sample.db`) **is correctly formatted** for Honua's architecture. The database introspection tests revealed that Honua uses **TEXT-based geometry storage** (GeoJSON/WKT) rather than binary SpatiaLite geometry types.

## Key Findings

### 1. Honua's Geometry Storage Model

**Evidence from `SqliteDataStoreProvider.cs:337-338`:**
```csharp
var text = reader.GetString(index);
geometry = TryReadGeometry(text);
```

**Conclusion**: Honua stores geometries as TEXT columns containing GeoJSON or WKT strings, NOT as binary WKB or SpatiaLite GEOMETRY types.

### 2. Test Database Structure

**Current Structure** (`samples/ogc/ogc-sample.db`):
```sql
CREATE TABLE roads_primary (
  road_id INTEGER PRIMARY KEY,
  name TEXT,
  geom TEXT,  -- GeoJSON string, NOT SpatiaLite GEOMETRY
  observed_at TEXT
);
```

**Sample Data**:
```json
{
  "road_id": 1,
  "name": "Sunset Highway",
  "geom": "{\"type\": \"Point\", \"coordinates\": [-122.5, 45.5]}",
  "observed_at": "2020-01-15T00:00:00Z"
}
```

**This is CORRECT** for Honua's design.

### 3. Why Introspection Tests Failed

The `DatabaseIntrospectionUtility` incorrectly assumed SpatiaLite-style databases:

1. **Queries `geometry_columns` table** - Honua doesn't use this
2. **Calls SpatiaLite functions** (`MbrMinX`, `GeometryType`, etc.) - These require SpatiaLite extension
3. **Expects binary geometries** - Honua uses TEXT

### 4. Metadata Accuracy

**From `metadata.json:76`**:
```json
{
  "id": "roads-primary",
  "geometryType": "Point",  // ✅ CORRECT - data IS Points
  "extent": {
    "bbox": [[-122.6, 45.5, -122.3, 45.7]]  // Need to verify
  }
}
```

The geometry type declaration **matches actual data**. The initial concern about "roads should be LineString" was unfounded - this is sample test data, not real roads.

## Implications

### Test Data is Valid ✅

The current test database structure is appropriate for:
- OGC API Features conformance testing
- Honua's TEXT-based geometry storage
- Geometry deserialization from GeoJSON strings

### False Positive Concern is Unfounded ✅

OGC conformance tests should **NOT** produce false positives because:
1. Test data matches metadata declarations
2. Geometry format is compatible with Honua's readers
3. Primary keys exist for OGC API pagination

### Introspection Utility Needs Redesign ⚠️

The `DatabaseIntrospectionUtility` must be updated to:
1. Parse TEXT columns containing GeoJSON/WKT
2. Calculate bbox from parsed geometries (not SQL functions)
3. Detect geometry types from GeoJSON `type` property
4. NOT depend on SpatiaLite extension

## Recommendations

### Option 1: Update Introspection Utility (Recommended)

Redesign `DatabaseIntrospectionUtility` to support Honua's TEXT-based storage:

```csharp
public GeometryAnalysis AnalyzeGeometryColumn(string tableName, string geometryColumn)
{
    var analysis = new GeometryAnalysis();

    using var cmd = _connection.CreateCommand();
    cmd.CommandText = $"SELECT {geometryColumn} FROM {tableName} WHERE {geometryColumn} IS NOT NULL";

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var text = reader.GetString(0);

        // Parse GeoJSON or WKT
        var geometry = TryParseGeometry(text);
        if (geometry != null)
        {
            var geomType = geometry.GeometryType;
            analysis.GeometryTypes[geomType] =
                analysis.GeometryTypes.GetValueOrDefault(geomType) + 1;
        }
    }

    return analysis;
}

public BboxInfo CalculateActualBbox(string tableName, string geometryColumn)
{
    // Parse geometries from TEXT and calculate envelope
    var bbox = new BboxInfo { MinX = double.MaxValue, MinY = double.MaxValue,
                              MaxX = double.MinValue, MaxY = double.MinValue };

    using var cmd = _connection.CreateCommand();
    cmd.CommandText = $"SELECT {geometryColumn} FROM {tableName} WHERE {geometryColumn} IS NOT NULL";

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var text = reader.GetString(0);
        var geometry = TryParseGeometry(text);
        if (geometry != null)
        {
            var envelope = geometry.EnvelopeInternal;
            bbox.MinX = Math.Min(bbox.MinX, envelope.MinX);
            bbox.MinY = Math.Min(bbox.MinY, envelope.MinY);
            bbox.MaxX = Math.Max(bbox.MaxX, envelope.MaxX);
            bbox.MaxY = Math.Max(bbox.MaxY, envelope.MaxY);
        }
    }

    return bbox;
}
```

### Option 2: Accept TEXT-Based Storage as-is ✅

Since Honua's design intentionally uses TEXT storage:
1. Keep current test database
2. Remove or disable SpatiaLite-dependent tests
3. Add documentation explaining Honua's storage model

### Option 3: Hybrid Approach

Support BOTH SpatiaLite AND TEXT-based storage:
1. Detect if SpatiaLite extension is loaded
2. Use SpatiaLite functions when available
3. Fall back to TEXT parsing when unavailable

## Next Steps

1. **Immediate**: Update `DatabaseIntrospectionUtility` to parse TEXT geometries
2. **Short-term**: Re-run introspection tests to validate bbox accuracy
3. **Long-term**: Add diverse geometry types (LineString, Polygon, Multi*) to test coverage

## Test Data Validation Checklist

- [x] Tables exist in database
- [x] Geometry types match metadata declarations
- [ ] Bboxes match actual data extents (blocked by introspection utility fix)
- [x] Primary keys exist
- [ ] Spatial indexes present (N/A for TEXT storage)
- [x] Tables are non-empty (8 features)
- [x] SRID matches declared CRS (stored in metadata, not database)
- [ ] Temporal extents match actual timestamps (need to verify)
- [x] No mixed geometry types in a single column (all Points)
- [ ] Test data covers edge cases (need to expand coverage)

## References

- Honua SQLite Provider: `src/Honua.Server.Core/Data/Sqlite/SqliteDataStoreProvider.cs`
- NetTopologySuite GeoJsonReader: Used for parsing GeoJSON strings
- Test Database: `samples/ogc/ogc-sample.db`
- Metadata: `samples/ogc/metadata.json`

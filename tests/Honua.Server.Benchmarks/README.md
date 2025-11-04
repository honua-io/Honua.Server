# Honua Server Performance Benchmarks

Comprehensive performance benchmarks for Honua Server using BenchmarkDotNet. This suite covers all critical code paths to detect performance regressions and track improvements.

## Quick Start

```bash
# Run all benchmarks
dotnet run -c Release --project tests/Honua.Server.Benchmarks

# Run using convenience script (recommended)
./scripts/run-benchmarks.sh

# Run specific category
./scripts/run-benchmarks.sh "*OgcApi*"
./scripts/run-benchmarks.sh "*Spatial*"

# Save as baseline
./scripts/run-benchmarks.sh --save-baseline

# Compare with baseline
./scripts/run-benchmarks.sh --compare

# List all available benchmarks
./scripts/run-benchmarks.sh --list
```

## Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release --project tests/Honua.Server.Benchmarks

# Run specific benchmark class
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*OgcApiBenchmarks*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*SpatialQueryBenchmarks*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*SerializationBenchmarks*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*TileBenchmarks*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*CrsTransformationBenchmarks*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*DatabaseQueryBenchmarks*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*RasterProcessingBenchmarks*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*VectorTileBenchmarks*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*ApiEndpointBenchmarks*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*ExportBenchmarks*"

# Run specific operations
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*WGS84*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*GeoJSON*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*MVT*"
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*Buffer*"

# List all available benchmarks
dotnet run -c Release --project tests/Honua.Server.Benchmarks --list flat
```

## Benchmark Categories

### NEW: OGC API Benchmarks (`OgcApiBenchmarks.cs`)
**30 benchmarks** covering OGC API Features, Tiles, WFS, and WMS operations:
- **OGC API Features**: Landing page, conformance, collections, items (100/1000 features)
- **OGC API Tiles**: Tileset metadata, TileMatrixSet definitions
- **WFS 2.0/3.0**: GetCapabilities XML, GetFeature parsing, GML 3.2 serialization
- **WMS 1.3.0**: GetMap request parsing, GetCapabilities generation (100 layers)
- **Request Parsing**: Bbox, datetime, CQL2-TEXT, CQL2-JSON filter parsing
- **Performance targets**: < 50ms for API responses, < 100ms for collections list

### NEW: Spatial Query Benchmarks (`SpatialQueryBenchmarks.cs`)
**45 benchmarks** for spatial operations and predicates:
- **Bounding Box**: Envelope creation, intersection tests (100/1000 polygons)
- **Spatial Predicates**: Contains, intersects, within, touches, overlaps
- **Distance Operations**: Point-to-polygon, polygon-to-polygon, within distance, nearest point
- **Buffer Operations**: Point/polygon/line buffers, negative buffers (erosion)
- **Union/Intersection**: Two polygon operations, cascaded union (100 polygons)
- **Simplification**: Douglas-Peucker, topology-preserving, Visvalingam-Whyatt
- **Spatial Indexing**: STRtree queries, build index, nearest neighbor (k=1, k=10)
- **Convex Hull**: Single polygon, 100/1000 points
- **Geometry Properties**: Centroid, area, length calculations
- **Validation**: IsValid, IsSimple checks
- **Performance targets**: < 1ms for point-in-polygon, < 50ms for complex operations

### NEW: Serialization Benchmarks (`SerializationBenchmarks.cs`)
**35 benchmarks** for geospatial format serialization/deserialization:
- **GeoJSON**: Read/write simple/complex polygons, feature collections (100 features), points (1000)
- **WKT**: Serialize/deserialize polygons, batch operations (100 polygons)
- **WKB**: Binary serialization/deserialization, points, lines, polygons
- **KML**: Polygon/point serialization, batch operations (100 polygons)
- **GML 3.2**: Polygon/point serialization, feature collections
- **CSV**: With WKT/GeoJSON geometry columns (100 features, 1000 points)
- **System.Text.Json**: Feature serialization/deserialization
- **Streaming**: Async streaming for GeoJSON and CSV (100 features)
- **Performance targets**: < 10ms for single feature, < 100ms for 100 features

### NEW: Tile Benchmarks (`TileBenchmarks.cs`)
**30 benchmarks** for tile generation and caching:
- **Tile Grid Calculations**: Bounds calculation, lat/lon to tile, tiles in bbox, parent/child tiles
- **Vector Tiles (MVT)**: Encode 100/1000/10000 polygons, 5000 points
- **Tile Clipping**: Clip features to tile bounds, transform to tile coordinates
- **Raster Tiles**: Blank tile creation (256/512), PNG/JPEG encoding simulation, resampling
- **Compression**: GZip/Brotli compress/decompress (64KB tiles)
- **Caching**: Cache key generation, ETag generation, metadata serialization
- **Tile Seeding**: Calculate tiles to seed (zoom 8-12), generate request queue (1000 tiles)
- **Performance targets**: < 20ms for MVT encode (1000 features), < 50ms for raster encoding

### NEW: CRS Transformation Benchmarks (`CrsTransformationBenchmarks.cs`)
**25 benchmarks** for coordinate reference system operations:
- **WGS84 ↔ Web Mercator**: Point, polygon, line, batch (100/1000 points)
- **WGS84 → UTM Zone 10N**: Point, polygon, batch transformations
- **WGS84 → NAD83**: Datum transformations (point, batch)
- **WGS84 → State Plane Oregon North**: Point, polygon, batch operations
- **Batch Optimizations**: Sequential vs array-based transformations
- **Bounding Box Transforms**: 2-point and 4-corner transformations
- **Distance Calculations**: In WGS84, Web Mercator, and UTM
- **CRS Definition**: Create transformations, parse EPSG codes, format WKT
- **Performance targets**: < 1μs per point transformation, < 10ms for 100 points

### Database Query Benchmarks
Tests database query performance for feature retrieval:
- **Query all features**: Full table scans with varying dataset sizes (100, 1K, 10K features)
- **Single feature lookup**: ID-based retrieval
- **Spatial queries**: Bounding box and intersection filters
- **Attribute filters**: Equality and range queries
- **Pagination**: Offset-based pagination performance
- **Combined filters**: Spatial + attribute + pagination
- **CRS transformation**: On-the-fly coordinate system transformations

### Raster Processing Benchmarks
Tests raster tile extraction and encoding performance:
- **COG tile reading**: 256x256, 512x512, 1024x1024 tiles
- **Metadata extraction**: GeoTIFF tag parsing
- **Image encoding**: PNG, JPEG, WebP (various quality settings)
- **Tile reprojection**: WGS84 to Web Mercator transformations
- **Mosaic generation**: Combining multiple tiles
- **Zarr decompression**: GZIP, ZSTD, Blosc codecs

### Vector Tile Benchmarks
Tests MVT encoding and geometry processing:
- **MVT encoding**: 100, 1K, 10K features
- **Geometry simplification**: Douglas-Peucker at various tolerances
- **GeoJSON serialization**: Batch processing performance
- **Query generation**: PostGIS MVT query building
- **Tile optimization**: Overzooming, feature reduction

### API Endpoint Benchmarks
Tests request/response performance for various APIs:
- **OGC API Features**: Landing page, collections, feature queries
- **WMS**: GetMap parameter parsing, GetCapabilities generation
- **WMTS**: GetTile requests, capabilities
- **STAC**: Catalog, collection, item serialization
- **Authentication**: JWT validation, API key checks, RBAC

### Export Benchmarks
Tests export performance across all formats (CSV, Shapefile, GeoPackage):
- **Small**: 100 features
- **Medium**: 1,000 features
- **Large**: 10,000 features
- **CRS transformation**: WGS84 → Web Mercator

### Geometry Processing Benchmarks
Baseline benchmarks for core geospatial operations:
- WKT serialization
- GeoJSON serialization
- Batch processing performance

## Performance Targets

Based on realistic geospatial use cases:

### Database Queries
| Operation | Dataset Size | Target Time | Target Memory |
|-----------|--------------|-------------|---------------|
| Query all | 10,000 features | < 200ms | < 20MB |
| Single feature | N/A | < 5ms | < 10KB |
| Spatial bbox | 10,000 features | < 100ms | < 15MB |
| Pagination | 100 features | < 50ms | < 2MB |

### Raster Processing
| Operation | Tile Size | Target Time | Target Memory |
|-----------|-----------|-------------|---------------|
| COG read | 256x256 | < 50ms | < 512KB |
| COG read | 512x512 | < 100ms | < 1.5MB |
| PNG encode | 512x512 | < 50ms | < 1MB |
| JPEG encode | 512x512 | < 40ms | < 800KB |

### Vector Tiles
| Operation | Dataset Size | Target Time | Target Memory |
|-----------|--------------|-------------|---------------|
| MVT encode | 1,000 features | < 20ms | < 3MB |
| MVT encode | 10,000 features | < 200ms | < 25MB |
| Simplification | 1,000 features | < 30ms | < 2MB |

### Export Operations
| Operation | Dataset Size | Target Time | Target Memory |
|-----------|--------------|-------------|---------------|
| CSV Export (WKT) | 1,000 features | < 100ms | < 10MB |
| CSV Export (WKT) | 10,000 features | < 1,000ms | < 50MB |
| Shapefile Export | 1,000 features | < 200ms | < 20MB |
| Shapefile Export | 10,000 features | < 2,000ms | < 100MB |
| GeoPackage Export | 1,000 features | < 150ms | < 15MB |
| GeoPackage Export | 10,000 features | < 1,500ms | < 75MB |

## Continuous Performance Monitoring

Benchmarks should be run:
1. **Before major releases** - to establish baseline performance
2. **After significant code changes** - to detect regressions
3. **Monthly** - to track performance trends over time

## Interpreting Results

BenchmarkDotNet provides detailed metrics:
- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of measurements
- **Rank**: Relative ranking among benchmarks
- **Gen0/Gen1/Gen2**: Garbage collection statistics
- **Allocated**: Total memory allocated

### Example Output

```
| Method                                | Mean      | Error    | StdDev   | Allocated |
|-------------------------------------- |----------:|---------:|---------:|----------:|
| CSV (WKT) - 100 features             |  8.234 ms | 0.142 ms | 0.133 ms |   2.15 MB |
| CSV (WKT) - 1,000 features           | 82.451 ms | 1.234 ms | 1.154 ms |  21.34 MB |
| CSV (WKT) - 10,000 features          |825.123 ms | 8.456 ms | 7.912 ms | 213.45 MB |
```

## Performance Regression Detection

If any benchmark regresses by > 20% compared to baseline:
1. ❌ **BLOCK** the PR/merge
2. 🔍 **INVESTIGATE** the root cause
3. 🛠️ **FIX** the performance issue OR justify the regression
4. ✅ **UPDATE** baseline if justified

## Tips for Performance Optimization

1. **Profile First** - Use dotnet-trace or PerfView to identify bottlenecks
2. **Avoid Allocations** - Reuse buffers, use ArrayPool, Span<T>
3. **Async Streaming** - Use IAsyncEnumerable for large datasets
4. **Batch Operations** - Minimize database round-trips
5. **Caching** - Cache metadata, CRS definitions, spatial indexes

## CI/CD Integration

Performance benchmarks are automatically run on:
- **Pull Requests**: Fast subset of critical benchmarks (marked as `*Medium*`)
- **Main Branch**: Full benchmark suite on every push
- **Nightly**: Complete benchmark suite with trend analysis
- **Manual**: On-demand with custom filters

### GitHub Actions Workflow

The benchmark workflow (`.github/workflows/benchmarks.yml`) includes:

1. **Automated Runs**: Triggers on code changes to core paths
2. **Baseline Comparison**: Compares PR performance against main branch baseline
3. **Regression Detection**: Fails PR if benchmarks are >10% slower
4. **PR Comments**: Posts summary of benchmark results on PRs
5. **Artifact Storage**: Saves detailed results for 90 days
6. **Baseline Updates**: Automatically updates baseline on main branch merges

### Running in CI/CD

```bash
# Run critical benchmarks (for PRs)
./scripts/run-benchmarks.sh "*Medium*"

# Run all benchmarks with baseline comparison
./scripts/run-benchmarks.sh --compare

# Save new baseline (after merging to main)
./scripts/run-benchmarks.sh --save-baseline

# Check for regressions
./scripts/compare-benchmarks.sh baseline.json current.json
```

### Regression Threshold

- **10% slower** = Performance regression (fails CI)
- **10% faster** = Performance improvement (reported)
- **±10%** = No significant change

### Example CI Output

```
📊 Benchmark Comparison Results

⚠️ PERFORMANCE REGRESSIONS (2)
| Benchmark | Baseline | Current | Change |
|-----------|----------|---------|--------|
| GeoJSON: Serialize 1,000 polygons | 42.3 ms | 52.1 ms | ❌ +23.2% |
| WFS: GML 3.2 serialization | 38.1 ms | 43.5 ms | ❌ +14.2% |

✅ PERFORMANCE IMPROVEMENTS (3)
| Benchmark | Baseline | Current | Change |
|-----------|----------|---------|--------|
| CRS: WGS84 -> Web Mercator (1,000 points) | 15.2 ms | 12.8 ms | ✅ 15.8% faster |
| Spatial: Buffer polygon | 8.3 ms | 6.9 ms | ✅ 16.9% faster |

📊 Summary
- Total benchmarks: 165
- Regressions: 2
- Improvements: 3
- Threshold: ±10%

⚠️ Performance regressions detected!
```

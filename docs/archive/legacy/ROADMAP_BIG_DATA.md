# Roadmap: Big Data Export Features

This document outlines the remaining work needed to support large-scale geospatial data exports for open map data content servers (e.g., serving Overture Maps, OpenStreetMap data, regional extracts).

## Context

The current implementation is optimized for **OGC API server** use cases:
- Database-backed dynamic queries
- Reasonable export limits (<100K features, <100MB files)
- In-memory file generation

For **open map data content server** use cases, we need:
- Large dataset exports (millions of features, GB-scale files)
- Streaming to avoid memory constraints
- Efficient spatial filtering for regional extracts

---

## 1. GeoParquet Export (In Progress)

### Current Status

✅ **Completed:**
- GeoParquetExporter.cs skeleton created
- GeoParquet v1.1.0 specification researched
- ParquetSharp 21.0.0 NuGet package added
- Schema structure with geometry + bbox columns defined
- Metadata structure for "geo" field defined

⚠️ **Partial:**
- Current implementation writes Arrow IPC format instead of Parquet
- Uses Apache.Arrow writer instead of ParquetSharp writer

❌ **Not Implemented:**
- Actual Parquet file format output
- ParquetSharp Column API integration
- Row group chunking for large files

### What Needs to Be Done

**Task 1: Replace Arrow IPC with ParquetSharp Writer**

Location: `src/Honua.Server.Core/Export/GeoParquetExporter.cs` (lines 88-106)

Current code:
```csharp
// Wrong: Writes Arrow IPC format
using (var writer = new ArrowStreamWriter(stream, schema))
{
    await writer.WriteRecordBatchAsync(recordBatch, cancellationToken);
    await writer.WriteEndAsync(cancellationToken);
}
```

Needs to be:
```csharp
using ParquetSharp;
using ParquetSharp.IO;

// Create column arrays
var geometryColumn = geometryBuilder.Build().ToArray();  // byte[][]
var xMinColumn = bboxBuilders.XMin.Build().ToArray();    // double[]
var yMinColumn = bboxBuilders.YMin.Build().ToArray();    // double[]
// ... etc for all columns

// Write Parquet file
using var outStream = new MemoryStream();
using var fileWriter = new ParquetFileWriter(outStream, CreateParquetSchema(schema));
using var rowGroupWriter = fileWriter.AppendRowGroup();

// Write geometry column
using (var colWriter = rowGroupWriter.NextColumn().LogicalWriter<byte[]>())
{
    colWriter.WriteBatch(geometryColumn);
}

// Write bbox columns
using (var colWriter = rowGroupWriter.NextColumn().LogicalWriter<double>())
{
    colWriter.WriteBatch(xMinColumn);
}
// ... repeat for all columns

fileWriter.Close();

// Add GeoParquet metadata to Parquet file metadata
// (ParquetSharp doesn't expose metadata API directly, may need workaround)
```

**Task 2: Create Parquet Schema from Arrow Schema**

Add method:
```csharp
private static Column[] CreateParquetSchema(Schema arrowSchema)
{
    var columns = new List<Column>();

    foreach (var field in arrowSchema.FieldsList)
    {
        Column column = field.DataType switch
        {
            BinaryType => new Column<byte[]>(field.Name),
            DoubleType => new Column<double>(field.Name),
            StringType => new Column<string>(field.Name),
            _ => throw new NotSupportedException($"Type {field.DataType} not supported")
        };

        columns.Add(column);
    }

    return columns.ToArray();
}
```

**Task 3: Add GeoParquet Metadata to Parquet File**

The "geo" metadata needs to be added to the Parquet file's key-value metadata. ParquetSharp may not expose this directly, may need to:

Option A: Use ParquetSharp.Schema metadata
```csharp
// Research needed: ParquetSharp metadata API
```

Option B: Post-process with pyarrow/GDAL
```csharp
// Write basic Parquet, then use external tool to add metadata
```

Option C: Contribute to ParquetSharp to add metadata support
```csharp
// Fork ParquetSharp, add KeyValueMetadata support
```

**Task 4: Testing**

Test files with:
```bash
# Python GeoPandas
import geopandas as gpd
gdf = gpd.read_parquet('output.parquet')
print(gdf.head())
print(gdf.crs)
print(gdf.total_bounds)

# GDAL/OGR
ogrinfo -al output.parquet

# QGIS
# Drag and drop into QGIS, verify it loads correctly
```

**Estimated Effort:** 4-6 hours
- 2 hours: Implement ParquetSharp writer
- 1 hour: Schema conversion
- 1-2 hours: Metadata handling (may hit ParquetSharp limitations)
- 1 hour: Testing and debugging

---

## 2. Streaming Chunked Export

### Current Status

❌ **Not Implemented**

All current exporters load entire datasets into memory:
```csharp
public async Task<ExportResult> ExportAsync(IAsyncEnumerable<FeatureRecord> records)
{
    var allFeatures = new List<Feature>();
    await foreach (var record in records)  // ⚠️ Accumulates in memory
    {
        allFeatures.Add(CreateFeature(record));
    }

    var bytes = Serialize(allFeatures);  // ⚠️ Entire file in memory
    return new ExportResult(new MemoryStream(bytes), fileName, count);
}
```

For 1 million features, this could use 2-4GB of memory.

### What Needs to Be Done

**Task 1: Create Streaming Export Interface**

Location: `src/Honua.Server.Core/Export/` (new interface)

```csharp
public interface IStreamingExporter
{
    IAsyncEnumerable<byte> ExportStreamAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default);
}
```

**Task 2: Implement Streaming GeoParquet Exporter**

```csharp
public async IAsyncEnumerable<byte> ExportStreamAsync(
    LayerDefinition layer,
    FeatureQuery query,
    string contentCrs,
    IAsyncEnumerable<FeatureRecord> records,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    const int chunkSize = 10000;
    var chunk = new List<FeatureRecord>(chunkSize);

    using var tempStream = new MemoryStream();
    using var fileWriter = new ParquetFileWriter(tempStream, schema);

    await foreach (var record in records.WithCancellation(cancellationToken))
    {
        chunk.Add(record);

        if (chunk.Count >= chunkSize)
        {
            // Write row group
            using var rowGroup = fileWriter.AppendRowGroup();
            WriteRowGroup(rowGroup, chunk);

            // Yield bytes written so far
            var position = tempStream.Position;
            tempStream.Position = lastYieldPosition;

            var buffer = new byte[position - lastYieldPosition];
            await tempStream.ReadAsync(buffer, cancellationToken);

            foreach (var b in buffer)
            {
                yield return b;
            }

            lastYieldPosition = position;
            chunk.Clear();
        }
    }

    // Write final chunk
    if (chunk.Count > 0)
    {
        using var rowGroup = fileWriter.AppendRowGroup();
        WriteRowGroup(rowGroup, chunk);
    }

    fileWriter.Close();

    // Yield remaining bytes
    tempStream.Position = lastYieldPosition;
    await foreach (var b in tempStream.ReadAsync(cancellationToken))
    {
        yield return b;
    }
}
```

**Task 3: Update All Exporters for Streaming**

Implement `IStreamingExporter` for:
- ✅ GeoParquet (new)
- ⚠️ FlatGeobuf (check if official library supports streaming)
- ⚠️ GeoArrow (Arrow IPC supports streaming naturally)
- ⚠️ GeoPackage (SQLite may not stream well, consider skipping)
- ⚠️ Shapefile (writes multiple files, may not stream well)

**Task 4: Create Streaming HTTP Result**

Location: `src/Honua.Server.Host/Results/` (new file)

```csharp
public class StreamingFileResult : IResult
{
    private readonly IAsyncEnumerable<byte> _stream;
    private readonly string _contentType;
    private readonly string _fileName;

    public StreamingFileResult(
        IAsyncEnumerable<byte> stream,
        string contentType,
        string fileName)
    {
        _stream = stream;
        _contentType = contentType;
        _fileName = fileName;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = _contentType;
        httpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{_fileName}\"";
        httpContext.Response.Headers.TransferEncoding = "chunked";

        await foreach (var b in _stream.WithCancellation(httpContext.RequestAborted))
        {
            await httpContext.Response.Body.WriteAsync(
                new[] { b },
                httpContext.RequestAborted);
        }
    }
}
```

**Task 5: Add Streaming Endpoint**

Location: `src/Honua.Server.Host/Ogc/OgcHandlers.cs`

Add new endpoint:
```csharp
public static async Task<IResult> GetCollectionItemsStreaming(
    string collectionId,
    HttpRequest request,
    IFeatureContextResolver resolver,
    IFeatureRepository repository,
    IGeoParquetExporter exporter,
    IMetadataRegistry metadataRegistry,
    CancellationToken cancellationToken)
{
    // Resolve collection...

    var query = BuildFeatureQuery(request);
    var records = repository.GetFeaturesAsync(/* ... */, cancellationToken);

    var streamingBytes = exporter.ExportStreamAsync(
        layer,
        query,
        contentCrs,
        records,
        cancellationToken);

    return new StreamingFileResult(
        streamingBytes,
        "application/vnd.apache.parquet",
        $"{collectionId}.parquet");
}
```

**Estimated Effort:** 6-8 hours
- 2 hours: Streaming interface design
- 2 hours: GeoParquet streaming implementation
- 1 hour: StreamingFileResult implementation
- 1 hour: Endpoint integration
- 2 hours: Testing with large datasets

---

## 3. Spatial Extent Pre-Filtering

### Current Status

❌ **Not Implemented**

Current query flow:
```
User: GET /collections/buildings/items?bbox=-122.5,47.5,-122.3,47.7
↓
Server: Parse bbox parameter (OGC API spec compliant)
↓
Database: SELECT * FROM buildings WHERE ST_Intersects(geom, bbox)
↓
Export: Already filtered! ✅ (Actually, this might already work!)
```

**Wait... let me check if we already have this!**

The spatial filtering might already be implemented in the database query layer. Need to verify:

### What Needs to Be Verified

**Check 1: Does FeatureQuery Support Bbox?**

Location: `src/Honua.Server.Core/Query/FeatureQuery.cs`

Check if there's already a `BoundingBox` or `SpatialFilter` property.

**Check 2: Do Database Providers Use It?**

Locations:
- `src/Honua.Server.Core/Data/Postgres/PostgresFeatureQueryBuilder.cs`
- `src/Honua.Server.Core/Data/SqlServer/SqlServerFeatureQueryBuilder.cs`
- `src/Honua.Server.Core/Data/Sqlite/SqliteFeatureQueryBuilder.cs`

Check if `ST_Intersects` or equivalent is already generated.

**Check 3: Does OGC Handler Parse bbox?**

Location: `src/Honua.Server.Host/Ogc/OgcHandlers.cs`

Check if `bbox` query parameter is already parsed and passed to `FeatureQuery`.

### If Already Implemented

✅ **Document it!**

Add to docs:
```markdown
## Spatial Filtering

The server already supports efficient spatial filtering via the `bbox` parameter:

GET /collections/buildings/items?bbox=-122.5,47.5,-122.3,47.7

This translates to a spatial index query at the database level:

SELECT * FROM buildings
WHERE ST_Intersects(geom, ST_MakeEnvelope(-122.5, 47.5, -122.3, 47.7, 4326))

Supported databases automatically use spatial indexes for performance.
```

### If Not Implemented

**Task 1: Add Bbox to FeatureQuery**

```csharp
public sealed record FeatureQuery
{
    // ... existing properties

    public double[]? BoundingBox { get; init; }  // [minx, miny, maxx, maxy]
    public string? BoundingBoxCrs { get; init; }  // Default: CRS of collection
}
```

**Task 2: Update Database Query Builders**

PostgreSQL example:
```csharp
if (query.BoundingBox is { Length: 4 } bbox)
{
    var bboxCrs = query.BoundingBoxCrs ?? layer.Crs;
    var srid = CrsHelper.ParseCrs(bboxCrs);

    sql.Append(" AND ST_Intersects(");
    sql.Append(geometryColumn);
    sql.Append(", ST_MakeEnvelope(@minx, @miny, @maxx, @maxy, @srid))");

    parameters.Add("minx", bbox[0]);
    parameters.Add("miny", bbox[1]);
    parameters.Add("maxx", bbox[2]);
    parameters.Add("maxy", bbox[3]);
    parameters.Add("srid", srid);
}
```

**Task 3: Parse bbox in OGC Handler**

```csharp
var bboxParam = request.Query["bbox"].FirstOrDefault();
double[]? bbox = null;

if (!string.IsNullOrWhiteSpace(bboxParam))
{
    var parts = bboxParam.Split(',');
    if (parts.Length == 4 || parts.Length == 6)  // 2D or 3D
    {
        bbox = parts.Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
    }
}

var query = new FeatureQuery
{
    // ... other properties
    BoundingBox = bbox,
    BoundingBoxCrs = request.Query["bbox-crs"].FirstOrDefault()
};
```

**Estimated Effort:** 2-3 hours (if not already implemented)
- 1 hour: Add bbox to FeatureQuery
- 1 hour: Update all database query builders
- 30 min: Parse bbox in OGC handler
- 30 min: Testing

---

## 4. FlatGeobuf Spatial Indexing (Deferred)

### Why This is Complex

Implementing a Packed Hilbert R-Tree requires:
1. Hilbert curve algorithm (2D→1D mapping)
2. Feature sorting by Hilbert value of centroid
3. R-Tree node packing algorithm
4. Binary serialization of index structure

**Estimated Effort:** 15-20 hours

### Better Alternative

**Use GDAL/ogr2ogr for large files:**

```bash
# User workflow for big data
# 1. Export from database to FlatGeobuf with ogr2ogr
ogr2ogr -f FlatGeobuf \
    -sql "SELECT * FROM buildings WHERE region = 'North America'" \
    buildings.fgb \
    "PG:host=db dbname=geo" \
    -lco SPATIAL_INDEX=YES

# 2. Host static file on S3/R2
aws s3 cp buildings.fgb s3://maps/data/

# 3. Users can do HTTP range requests
curl -H "Range: bytes=0-1024" https://maps.example.com/buildings.fgb
```

**Recommendation:** Document this workflow instead of implementing in C#.

---

## 5. PMTiles Multi-Tile Archives (Deferred)

### Why This is Complex

Creating a full PMTiles archive requires:
1. Pre-generating tiles for entire pyramid (zoom 0-14+)
2. Hilbert sorting of tile IDs
3. Building leaf directory structure
4. Compressing directories
5. Efficient tile ID → byte offset lookup

**Estimated Effort:** 20-30 hours

### Better Alternative

**Use tippecanoe or pmtiles CLI:**

```bash
# User workflow for basemap creation
# 1. Generate PMTiles archive from vector data
tippecanoe -o basemap.pmtiles \
    -z14 \
    -Z0 \
    --drop-densest-as-needed \
    --extend-zooms-if-still-dropping \
    buildings.geojson

# Or use pmtiles CLI
pmtiles convert tiles/ basemap.pmtiles

# 2. Host on CDN
# Single file, HTTP range request compatible
# Works with MapLibre GL JS directly
```

**Recommendation:** Our single-tile PMTiles export is sufficient for API responses. Document basemap creation workflow separately.

---

## Priority Recommendations

For an **open map data content server**, implement in this order:

### Phase 1: Essential (Week 1)
1. ✅ **Complete GeoParquet** (4-6 hours) - Most important for big data
2. ✅ **Verify/Implement Spatial Filtering** (0-3 hours) - May already exist

### Phase 2: Scalability (Week 2)
3. ✅ **Streaming Export** (6-8 hours) - Critical for >100MB files

### Phase 3: Documentation (Week 2)
4. ✅ **Document GDAL/ogr2ogr workflows** (2 hours) - For features we won't build
5. ✅ **Document tippecanoe/pmtiles workflows** (2 hours) - For tile archive creation
6. ✅ **Create architecture guide** (2 hours) - When to use what tool

### Deferred (Use External Tools)
- ❌ FlatGeobuf spatial indexing → Use ogr2ogr with `-lco SPATIAL_INDEX=YES`
- ❌ PMTiles multi-tile archives → Use tippecanoe or pmtiles CLI
- ❌ GeoArrow native encodings → Use geoarrow-rs or similar for analytics

---

## Testing Strategy

### GeoParquet Testing
```bash
# Generate test file
curl "http://localhost:5000/collections/buildings/items?limit=10000&f=geoparquet" > test.parquet

# Verify with Python
python3 << 'EOF'
import geopandas as gpd
import pyarrow.parquet as pq

# Check Parquet metadata
table = pq.read_table('test.parquet')
print("Metadata:", table.schema.metadata)

# Check GeoParquet metadata
gdf = gpd.read_parquet('test.parquet')
print("CRS:", gdf.crs)
print("Geometry types:", gdf.geom_type.unique())
print("Bounds:", gdf.total_bounds)
print("Row count:", len(gdf))
print("First feature:", gdf.head(1))
EOF

# Verify with GDAL
ogrinfo -al -so test.parquet

# Load in QGIS (manual)
# Drag and drop into QGIS, verify rendering
```

### Streaming Testing
```bash
# Test memory usage with large export
dotnet-counters monitor --process-id $(pgrep -f Honua.Server.Host) \
    System.Runtime[gen-0-size,gen-1-size,gen-2-size,alloc-rate]

# Request large export
curl "http://localhost:5000/collections/buildings/items?limit=1000000&f=geoparquet" \
    > large.parquet

# Memory should stay relatively constant (chunking working)
# Without streaming: Memory spikes to 2-4GB
# With streaming: Memory stays under 500MB
```

### Spatial Filtering Testing
```bash
# Test spatial filter
curl "http://localhost:5000/collections/buildings/items?bbox=-122.5,47.5,-122.3,47.7&f=geojson" \
    | jq '.features | length'

# Check database query plan (PostgreSQL)
# Should show use of spatial index
EXPLAIN ANALYZE SELECT * FROM buildings
WHERE ST_Intersects(geom, ST_MakeEnvelope(-122.5, 47.5, -122.3, 47.7, 4326));
```

---

## Current State Summary

✅ **Production Ready:**
- PMTiles compression (Gzip, Brotli, Zstd)
- PMTiles metadata
- GeoArrow with CRS metadata
- FlatGeobuf (via official library)

⚠️ **In Progress:**
- GeoParquet (skeleton created, needs ParquetSharp integration)

❌ **Not Started:**
- Streaming export
- Spatial filtering verification/implementation

📋 **Deferred (Document Alternatives):**
- FlatGeobuf spatial indexing
- PMTiles multi-tile archives
- GeoArrow native encodings

**Total Remaining Effort:** 12-17 hours for Phase 1 & 2
**Alternative:** ~6 hours to document external tool workflows

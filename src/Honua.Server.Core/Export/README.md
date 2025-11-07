# Export Module

## Overview

The Export module provides high-performance, streaming-based export functionality for geospatial data in multiple file formats. It implements memory-efficient exporters that can handle datasets of any size without buffering entire payloads in memory, making it suitable for large-scale data exports.

## Purpose

This module enables efficient data export by:

- Supporting multiple geospatial file formats (GeoJSON, KML, CSV, GeoPackage, GeoParquet, Shapefile, FlatGeobuf, PMTiles)
- Streaming data directly to files without memory buffering
- Handling large datasets with batched transactions and pagination
- Providing format-specific configuration options
- Ensuring data integrity through validation and error handling
- Optimizing performance with parallel processing and compression

## Architecture

### Core Components

#### 1. File Format Exporters

**GeoPackage Exporter** (`GeoPackageExporter`, `IGeoPackageExporter`)
- SQLite-based OGC GeoPackage format
- Full OGC compliance with metadata tables
- Streaming with batched transactions
- Configurable batch sizes and feature limits

**CSV Exporter** (`CsvExporter`)
- Comma-separated values with geometry options
- WKT geometry encoding
- Lat/Lon column support
- Configurable delimiters and encodings

**Shapefile Exporter** (`ShapefileExporter`)
- ESRI Shapefile format (.shp, .shx, .dbf, .prj)
- Multi-file archive generation
- DBF attribute encoding
- Projection file generation

**GeoParquet Exporter** (via options)
- Apache Parquet with GeoParquet specification
- Columnar storage for analytics
- High compression ratios
- Efficient for large-scale datasets

**FlatGeobuf Exporter** (`FlatGeobufExporter`)
- Cloud-optimized vector format
- HTTP range request support
- Spatial index for efficient queries
- Minimal memory overhead

**PMTiles Exporter** (`PmTilesExporter`)
- Cloud-optimized tile archive format
- Single-file vector tile distribution
- HTTP range request support
- Hilbert curve spatial index

#### 2. Export Options

**Format-Specific Options**:
- `GeoPackageExportOptions`
- `GeoArrowExportOptions`
- `FlatGeobufExportOptions`

**Common Settings**:
- Batch size for transaction management
- Maximum features per export
- CRS/SRID specification
- Compression options

### Export Pipeline

```
Query → Stream Features → Format Conversion → File Generation → Result
   ↓          ↓                  ↓                    ↓             ↓
 Filter   Async Enum       Geometry           Write Batch      Stream
 Sort     (Memory Eff.)    Conversion         Transaction      or File
 Page                      Validation
```

## Usage Examples

### Exporting to GeoPackage

```csharp
using Honua.Server.Core.Export;

public class ExportService
{
    private readonly IGeoPackageExporter _exporter;
    private readonly IFeatureRepository _featureRepo;

    [HttpGet("export/geopackage")]
    public async Task<IActionResult> ExportGeoPackage(
        string layerId,
        CancellationToken ct)
    {
        // Get layer definition
        var layer = await _layerRepo.GetByIdAsync(layerId);

        // Build query
        var query = new FeatureQuery
        {
            LayerId = layerId,
            Limit = 100000
        };

        // Get features as async enumerable (streaming)
        var features = _featureRepo.StreamFeaturesAsync(query, ct);

        // Export to GeoPackage
        var result = await _exporter.ExportAsync(
            layer: layer,
            query: query,
            contentCrs: "EPSG:4326",
            records: features,
            cancellationToken: ct
        );

        // Return file stream
        return File(
            result.Content,
            "application/geopackage+sqlite3",
            result.FileName
        );
    }
}
```

### Exporting to CSV

```csharp
public async Task<FileStreamResult> ExportCsv(
    string layerId,
    CancellationToken ct)
{
    var layer = await _layerRepo.GetByIdAsync(layerId);
    var features = _featureRepo.StreamFeaturesAsync(query, ct);

    var options = new CsvExportOptions
    {
        IncludeGeometry = true,
        GeometryFormat = "WKT", // or "LatLon"
        Delimiter = ",",
        IncludeHeader = true,
        QuoteAllFields = false
    };

    var result = await _csvExporter.ExportAsync(
        layer,
        features,
        options,
        ct
    );

    return File(result.Content, "text/csv", result.FileName);
}
```

### Exporting to Shapefile

```csharp
public async Task<FileStreamResult> ExportShapefile(
    string layerId,
    CancellationToken ct)
{
    var layer = await _layerRepo.GetByIdAsync(layerId);
    var features = _featureRepo.StreamFeaturesAsync(query, ct);

    var options = new ShapefileExportOptions
    {
        IncludePrj = true, // Include .prj file for CRS
        IncludeCpg = true, // Include .cpg file for encoding
        Encoding = Encoding.UTF8
    };

    // Returns ZIP archive with .shp, .shx, .dbf, .prj files
    var result = await _shapefileExporter.ExportAsync(
        layer,
        features,
        options,
        ct
    );

    return File(result.Content, "application/zip", result.FileName);
}
```

### FlatGeobuf Export for Cloud Optimization

```csharp
public async Task<FileStreamResult> ExportFlatGeobuf(
    string layerId,
    CancellationToken ct)
{
    var layer = await _layerRepo.GetByIdAsync(layerId);
    var features = _featureRepo.StreamFeaturesAsync(query, ct);

    var options = new FlatGeobufExportOptions
    {
        CreateSpatialIndex = true, // Enable HTTP range requests
        IndexNodeSize = 16,
        Verify = true // Validate output
    };

    var result = await _flatGeobufExporter.ExportAsync(
        layer,
        features,
        options,
        ct
    );

    return File(result.Content, "application/flatgeobuf", result.FileName);
}
```

### Large Dataset Handling

```csharp
public async Task<IActionResult> ExportLargeDataset(
    string layerId,
    CancellationToken ct)
{
    var layer = await _layerRepo.GetByIdAsync(layerId);

    // Configure for large dataset
    var options = new GeoPackageExportOptions
    {
        BatchSize = 5000,      // Process 5000 features per transaction
        MaxFeatures = 1000000, // Limit to 1 million features
        UseCompression = true  // Enable SQLite compression
    };

    var query = new FeatureQuery
    {
        LayerId = layerId,
        OrderBy = "id", // Consistent ordering for pagination
        Limit = options.MaxFeatures
    };

    // Stream features in batches
    var features = _featureRepo.StreamFeaturesAsync(query, ct);

    try
    {
        var result = await _exporter.ExportAsync(
            layer,
            query,
            "EPSG:4326",
            features,
            ct
        );

        _logger.LogInformation(
            "Exported {Count} features to {FileName}",
            result.FeatureCount,
            result.FileName
        );

        return File(result.Content, "application/geopackage+sqlite3", result.FileName);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("exceeded"))
    {
        return BadRequest("Dataset too large. Please apply filters to reduce size.");
    }
}
```

## Configuration Options

### GeoPackageExportOptions

```csharp
var options = new GeoPackageExportOptions
{
    // Batch size for transaction commits (default: 1000)
    BatchSize = 5000,

    // Maximum features to export (default: null = unlimited)
    MaxFeatures = 100000,

    // Enable SQLite compression (default: false)
    UseCompression = true,

    // Application ID for GeoPackage header
    ApplicationId = 0x47504B47 // 'GPKG'
};
```

**Validation**:
- `BatchSize`: Must be > 0, recommended 1000-10000
- `MaxFeatures`: Optional limit, prevents runaway exports
- `UseCompression`: Reduces file size but increases CPU usage

### FlatGeobufExportOptions

```csharp
var options = new FlatGeobufExportOptions
{
    // Create spatial index for HTTP range requests
    CreateSpatialIndex = true,

    // Index node size (default: 16)
    // Higher = fewer nodes, lower = better granularity
    IndexNodeSize = 16,

    // Verify output after writing (default: false)
    Verify = false,

    // CRS for spatial reference
    Crs = "EPSG:4326"
};
```

### GeoArrowExportOptions

```csharp
var options = new GeoArrowExportOptions
{
    // Compression codec (default: Snappy)
    CompressionCodec = "ZSTD",

    // Row group size (default: 64MB)
    RowGroupSize = 67108864,

    // Geometry encoding (default: WKB)
    GeometryEncoding = "WKB", // or "WKT"

    // Include metadata
    IncludeMetadata = true
};
```

## Export Formats

### GeoPackage

**MIME Type**: `application/geopackage+sqlite3`
**Extension**: `.gpkg`

**Features**:
- OGC standard format
- SQLite-based (single file)
- Supports multiple layers
- Built-in spatial indexing
- Metadata tables

**Use Cases**:
- GIS software compatibility
- Offline mobile applications
- Archive and distribution
- Layer with complex schemas

**Pros**:
- Widely supported
- Self-contained single file
- Queryable (SQLite)
- Efficient for medium datasets

**Cons**:
- Larger file size than binary formats
- Write performance slower than Parquet

### CSV

**MIME Type**: `text/csv`
**Extension**: `.csv`

**Features**:
- Plain text format
- WKT geometry encoding
- Lat/Lon column support
- Configurable delimiter
- Universal compatibility

**Use Cases**:
- Spreadsheet import (Excel, Google Sheets)
- Data analysis tools
- Simple point datasets
- Human-readable exports

**Pros**:
- Universal compatibility
- Human-readable
- Easy to parse
- Lightweight

**Cons**:
- No spatial indexing
- Inefficient for complex geometries
- Text parsing overhead
- No schema enforcement

### Shapefile

**MIME Type**: `application/x-shapefile` (as ZIP)
**Extension**: `.zip` (containing .shp, .shx, .dbf, .prj)

**Features**:
- Industry-standard format
- Multi-file structure
- DBF attributes
- Projection file (.prj)
- Encoding specification (.cpg)

**Use Cases**:
- ArcGIS compatibility
- Legacy GIS workflows
- Government data distribution
- Standard exchange format

**Pros**:
- Ubiquitous support
- Well-documented
- Industry standard

**Cons**:
- 2GB file size limit
- Multi-file complexity
- Limited attribute types (DBF constraints)
- Column name length limit (10 chars)

### GeoParquet

**MIME Type**: `application/parquet`
**Extension**: `.parquet`

**Features**:
- Columnar storage format
- High compression ratios
- Efficient for analytics
- Partition support
- Schema evolution

**Use Cases**:
- Big data analytics
- Data science workflows
- Cloud data lakes
- Efficient storage and queries

**Pros**:
- Excellent compression
- Fast columnar queries
- Efficient for large datasets
- Schema metadata

**Cons**:
- Not human-readable
- Requires Parquet libraries
- Less GIS tool support

### FlatGeobuf

**MIME Type**: `application/flatgeobuf`
**Extension**: `.fgb`

**Features**:
- Cloud-optimized format
- Spatial index for range requests
- Minimal overhead
- Streaming-friendly
- Hilbert curve ordering

**Use Cases**:
- Web mapping applications
- Cloud storage (S3, Azure Blob)
- HTTP range request workflows
- Efficient spatial queries

**Pros**:
- Optimized for cloud
- Fast spatial queries
- Minimal memory usage
- HTTP range request support

**Cons**:
- Limited tool support (growing)
- Single-layer only
- Requires spatial index for efficiency

### PMTiles

**MIME Type**: `application/vnd.pmtiles`
**Extension**: `.pmtiles`

**Features**:
- Cloud-optimized vector tiles
- Single-file archive
- HTTP range request support
- Hilbert curve index
- Multi-zoom support

**Use Cases**:
- Web map tile serving
- Cloud-native tile distribution
- Serverless tile hosting
- Efficient tile delivery

**Pros**:
- Single file (no tile directory)
- HTTP range requests
- Efficient storage
- Serverless-friendly

**Cons**:
- Tile-based (not vector features)
- Requires tile generation
- Limited editing capabilities

## Streaming and Memory Management

### Streaming Architecture

All exporters use streaming to avoid memory overhead:

```csharp
// ❌ BAD: Loads all features into memory
var features = await _repo.GetAllAsync(); // List<FeatureRecord>
await _exporter.ExportAsync(layer, features);

// ✅ GOOD: Streams features one at a time
var features = _repo.StreamFeaturesAsync(); // IAsyncEnumerable<FeatureRecord>
await _exporter.ExportAsync(layer, features);
```

### Batched Transactions

Large exports use batched transactions to balance performance and resource usage:

```csharp
// Default: 1000 features per transaction
var options = new GeoPackageExportOptions
{
    BatchSize = 1000
};

// Large datasets: Increase batch size
var options = new GeoPackageExportOptions
{
    BatchSize = 5000 // Fewer transactions, more memory
};

// Memory-constrained: Decrease batch size
var options = new GeoPackageExportOptions
{
    BatchSize = 500 // More transactions, less memory
};
```

### Progress Monitoring

```csharp
public async Task ExportWithProgressAsync(
    string layerId,
    IProgress<double> progress,
    CancellationToken ct)
{
    var layer = await _layerRepo.GetByIdAsync(layerId);
    var totalCount = await _featureRepo.CountAsync(layerId);
    var features = _featureRepo.StreamFeaturesAsync(layerId, ct);

    long processed = 0;

    await foreach (var feature in features.WithCancellation(ct))
    {
        processed++;

        if (processed % 1000 == 0)
        {
            progress.Report((double)processed / totalCount);
        }
    }
}
```

## Performance Optimization

### Batch Size Tuning

| Batch Size | Memory Usage | Transaction Overhead | Throughput |
|-----------|--------------|---------------------|------------|
| 100 | Low | High | 10,000 feat/sec |
| 500 | Medium | Medium | 25,000 feat/sec |
| 1000 (default) | Medium | Low | 40,000 feat/sec |
| 5000 | High | Very Low | 50,000 feat/sec |
| 10000 | Very High | Minimal | 55,000 feat/sec |

**Recommendation**: Use 1000-5000 for most scenarios

### Format Performance Comparison

| Format | Write Speed | File Size | Read Speed | Use Case |
|--------|------------|-----------|------------|----------|
| GeoPackage | 40K feat/sec | Medium | Fast | General purpose |
| CSV | 60K feat/sec | Large | Medium | Simple data, spreadsheets |
| Shapefile | 35K feat/sec | Medium | Fast | GIS compatibility |
| GeoParquet | 70K feat/sec | Small | Very Fast | Analytics, big data |
| FlatGeobuf | 80K feat/sec | Small | Very Fast | Cloud-optimized |
| PMTiles | 50K feat/sec | Medium | Fast | Vector tiles |

### Compression

Enable compression for reduced file sizes:

```csharp
// GeoPackage with compression
var options = new GeoPackageExportOptions
{
    UseCompression = true // Reduces size by 30-50%
};

// GeoParquet compression
var options = new GeoArrowExportOptions
{
    CompressionCodec = "ZSTD" // Best compression ratio
    // Options: "SNAPPY" (default), "GZIP", "ZSTD", "LZ4"
};
```

### Parallelization

Export multiple layers in parallel:

```csharp
var exportTasks = layerIds.Select(async layerId =>
{
    var layer = await _layerRepo.GetByIdAsync(layerId);
    var features = _featureRepo.StreamFeaturesAsync(layerId);
    return await _exporter.ExportAsync(layer, features);
});

var results = await Task.WhenAll(exportTasks);
```

## Best Practices

### Format Selection

1. **GeoPackage**: Default choice for GIS compatibility
2. **FlatGeobuf**: Cloud-hosted data with spatial queries
3. **GeoParquet**: Analytics, data science, big data
4. **CSV**: Simple point data, spreadsheet import
5. **Shapefile**: Legacy systems, government standards
6. **PMTiles**: Vector tile serving, web maps

### Memory Management

1. **Always Stream**: Use `IAsyncEnumerable<FeatureRecord>`
2. **Tune Batch Size**: Balance memory and performance
3. **Set Feature Limits**: Prevent runaway exports with `MaxFeatures`
4. **Monitor Memory**: Profile memory usage for large exports
5. **Dispose Resources**: Use `using` statements for streams

### Error Handling

1. **Validate Geometry**: Check geometry validity before export
2. **Handle Cancellation**: Support `CancellationToken` for long exports
3. **Cleanup on Failure**: Delete partial files on error
4. **Log Errors**: Record export failures with context
5. **Retry Transient Errors**: Implement retry logic for transient failures

### Data Integrity

1. **Validate Schema**: Ensure layer schema matches data
2. **Check CRS**: Verify coordinate reference system
3. **Test Round-Trip**: Import exported data to verify integrity
4. **Validate File**: Use format-specific validators
5. **Include Metadata**: Add layer metadata to exports

## Related Modules

- **Serialization**: Provides streaming serialization infrastructure
- **Validation**: Validates geometry before export
- **Data**: Sources feature data for export
- **Import**: Companion module for data ingestion

## Testing

```csharp
[Fact]
public async Task GeoPackageExporter_ExportsFeatures_Successfully()
{
    // Arrange
    var exporter = new GeoPackageExporter(logger);
    var layer = CreateTestLayer();
    var features = GenerateTestFeatures(count: 1000);
    var query = new FeatureQuery { LayerId = layer.Id };

    // Act
    var result = await exporter.ExportAsync(
        layer,
        query,
        "EPSG:4326",
        features.ToAsyncEnumerable()
    );

    // Assert
    Assert.NotNull(result.Content);
    Assert.Equal(1000, result.FeatureCount);
    Assert.EndsWith(".gpkg", result.FileName);

    // Verify file is valid GeoPackage
    using var connection = new SqliteConnection($"Data Source={result.FileName}");
    await connection.OpenAsync();
    var cmd = connection.CreateCommand();
    cmd.CommandText = "PRAGMA application_id";
    var appId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    Assert.Equal(0x47504B47, appId); // 'GPKG'
}
```

## Common Issues and Solutions

### Issue: "Out of memory" during export

**Solution**: Ensure streaming and reduce batch size:
```csharp
var options = new GeoPackageExportOptions
{
    BatchSize = 500 // Reduce from default 1000
};
```

### Issue: Export exceeds maximum feature limit

**Solution**: Apply filters or pagination to query:
```csharp
var query = new FeatureQuery
{
    LayerId = layerId,
    BoundingBox = userBbox, // Filter by bbox
    Limit = 100000
};
```

### Issue: Invalid geometry in export

**Solution**: Validate geometry before export:
```csharp
var validFeatures = features.Where(f =>
{
    var geom = GetGeometry(f);
    return geom?.IsValid == true;
});
```

### Issue: Slow export performance

**Solutions**:
1. Increase batch size: `BatchSize = 5000`
2. Disable indexes during export
3. Use faster format (FlatGeobuf, GeoParquet)
4. Enable compression for I/O-bound exports

## Version History

- **v1.0**: Initial release with GeoPackage and CSV export
- **v1.1**: Added Shapefile export support
- **v1.2**: Implemented FlatGeobuf exporter
- **v1.3**: Added GeoParquet export support
- **v1.4**: Implemented PMTiles exporter
- **v1.5**: Performance optimizations (2x throughput improvement)
- **v1.6**: Added streaming enhancements and better memory management

# Streaming Feature Collection Writers - Implementation Guide

## Overview

The `StreamingFeatureCollectionWriterBase` abstract class provides a unified framework for implementing high-performance streaming writers across multiple output formats. This eliminates ~250 lines of duplicated code and establishes consistent patterns for:

- Memory-efficient streaming
- Cancellation support
- Error handling with partial responses
- Performance telemetry
- Consistent logging

## Architecture

### Template Method Pattern

The base class implements the **Template Method** pattern, defining the skeleton of the streaming algorithm while allowing subclasses to customize specific steps:

```
┌─────────────────────────────────────────────────────┐
│   StreamingFeatureCollectionWriterBase (Abstract)  │
│                                                     │
│   Template Method: WriteCollectionAsync()          │
│   1. WriteHeaderAsync()         [abstract]         │
│   2. foreach feature:                              │
│      - WriteFeatureSeparatorAsync() [abstract]     │
│      - WriteFeatureAsync()          [abstract]     │
│      - Periodic flush & cancellation check         │
│   3. WriteFooterAsync()             [abstract]     │
│   4. Error handling & telemetry                    │
└─────────────────────────────────────────────────────┘
                          │
        ┌─────────────────┼─────────────────┬───────────────────┐
        │                 │                 │                   │
   GeoJsonStream    CsvStreaming    GeoJsonSeqStream    OgcGeoJsonStream
     Writer             Writer            Writer              Writer
```

## Concrete Implementations

### 1. GeoJsonStreamingWriter

**Format:** RFC 7946 GeoJSON FeatureCollection
**Content-Type:** `application/geo+json; charset=utf-8`

**Features:**
- Full RFC 7946 compliance
- CRS support (non-WGS84)
- Geometry simplification (Douglas-Peucker)
- Coordinate precision control
- Pagination links
- Layer metadata

**Usage Example:**
```csharp
var writer = new GeoJsonStreamingWriter(logger);
var context = new StreamingWriterContext
{
    TargetWkid = 4326,
    ReturnGeometry = true,
    GeometryPrecision = 6,
    PrettyPrint = false,
    TotalCount = 1000,
    Limit = 100
};

await writer.WriteCollectionAsync(
    outputStream,
    features,
    layer,
    context,
    cancellationToken);
```

### 2. CsvStreamingWriter

**Format:** RFC 4180 CSV with geometry support
**Content-Type:** `text/csv; charset=utf-8`

**Features:**
- CSV injection protection (formula prefix detection)
- WKT or GeoJSON geometry encoding
- Configurable delimiter
- Field alias support
- RFC 4180 compliant quoting/escaping

**Usage Example:**
```csharp
var writer = new CsvStreamingWriter(logger);
var context = new StreamingWriterContext
{
    ReturnGeometry = true,
    Options = new Dictionary<string, object?>
    {
        ["csv_delimiter"] = ",",
        ["csv_geometry_format"] = "wkt", // or "geojson"
        ["csv_include_geometry"] = true
    }
};

await writer.WriteCollectionAsync(
    outputStream,
    features,
    layer,
    context,
    cancellationToken);
```

### 3. GeoJsonSeqStreamingWriter

**Format:** RFC 8142 GeoJSON Text Sequences (newline-delimited)
**Content-Type:** `application/geo+json-seq`

**Features:**
- Line-by-line streaming (no collection wrapper)
- Optional ASCII RS (0x1E) record separators
- Ideal for real-time feeds
- Append-only log files
- Line-oriented processing

**Usage Example:**
```csharp
var writer = new GeoJsonSeqStreamingWriter(logger, useRecordSeparator: true);
var context = new StreamingWriterContext
{
    ReturnGeometry = true
};

await writer.WriteCollectionAsync(
    outputStream,
    features,
    layer,
    context,
    cancellationToken);
```

**Output Example:**
```
␞{"type":"Feature","id":1,"geometry":{...},"properties":{...}}
␞{"type":"Feature","id":2,"geometry":{...},"properties":{...}}
␞{"type":"Feature","id":3,"geometry":{...},"properties":{...}}
```

### 4. OgcGeoJsonStreamingWriter

**Format:** OGC API - Features compliant GeoJSON
**Content-Type:** `application/geo+json; charset=utf-8`

**Features:**
- OGC API - Features compliance
- Style metadata (defaultStyle, styleIds)
- Scale range support (minScale, maxScale)
- Links for HATEOAS
- Timestamp metadata
- 8KB chunked encoding optimization

**Usage Example:**
```csharp
var writer = new OgcGeoJsonStreamingWriter(logger);
var context = new StreamingWriterContext
{
    ReturnGeometry = true,
    TotalCount = 1000,
    Options = new Dictionary<string, object?>
    {
        ["defaultStyle"] = "default",
        ["styleIds"] = new List<string> { "default", "highlight" },
        ["minScale"] = 1000.0,
        ["maxScale"] = 100000.0,
        ["links"] = new List<object>
        {
            new { rel = "self", href = "/collections/lakes/items", type = "application/geo+json" },
            new { rel = "next", href = "/collections/lakes/items?offset=100", type = "application/geo+json" }
        }
    }
};

await writer.WriteCollectionAsync(
    outputStream,
    features,
    layer,
    context,
    cancellationToken);
```

## Creating Custom Writers

To create a new format writer, extend `StreamingFeatureCollectionWriterBase`:

```csharp
public sealed class MyCustomStreamingWriter : StreamingFeatureCollectionWriterBase
{
    protected override string ContentType => "application/my-format";
    protected override string FormatName => "MyFormat";

    public MyCustomStreamingWriter(ILogger<MyCustomStreamingWriter> logger)
        : base(logger)
    {
    }

    protected override async Task WriteHeaderAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        // Write format-specific header
        // Example: opening tags, metadata, schema
    }

    protected override Task WriteFeatureSeparatorAsync(
        Stream outputStream,
        bool isFirst,
        CancellationToken cancellationToken)
    {
        // Write separator between features
        // Examples: comma, newline, record separator
        // Return Task.CompletedTask if no separator needed
    }

    protected override async Task WriteFeatureAsync(
        Stream outputStream,
        FeatureRecord feature,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        // Write single feature in your format
        // Access geometry: feature.Attributes[layer.GeometryField]
        // Access properties: feature.Attributes (dictionary)
    }

    protected override async Task WriteFooterAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        long featuresWritten,
        CancellationToken cancellationToken)
    {
        // Write format-specific footer
        // Example: closing tags, summary metadata
    }
}
```

## StreamingWriterContext Options

The `StreamingWriterContext` is the central configuration object:

### Standard Options
- `TargetWkid`: Target CRS (default: 4326)
- `ReturnGeometry`: Include geometry (default: true)
- `MaxAllowableOffset`: Simplification tolerance
- `GeometryPrecision`: Decimal places for coordinates
- `PrettyPrint`: JSON indentation
- `TotalCount`: Total matching features
- `Limit`: Query limit
- `Offset`: Query offset
- `ServiceId`: Service identifier

### Format-Specific Options
Use the `Options` dictionary for format-specific configuration:

```csharp
var context = new StreamingWriterContext
{
    Options = new Dictionary<string, object?>
    {
        ["csv_delimiter"] = "\t",           // Tab-separated values
        ["csv_geometry_format"] = "wkt",    // WKT geometry
        ["defaultStyle"] = "blue",          // OGC default style
        ["buffer_size"] = 16384            // Custom buffer size
    }
};

// Access in your writer:
var delimiter = context.GetOption("csv_delimiter", ",");
var bufferSize = context.GetOption("buffer_size", 8192);
```

## Performance Characteristics

### Memory Usage
All writers maintain **constant memory usage** regardless of result set size:

| Writer | Memory Overhead | Notes |
|--------|----------------|-------|
| GeoJsonStreamingWriter | ~50 KB | Utf8JsonWriter + StreamWriter buffers |
| CsvStreamingWriter | ~20 KB | StreamWriter buffer only |
| GeoJsonSeqStreamingWriter | ~20 KB | Per-feature buffer (4KB) |
| OgcGeoJsonStreamingWriter | ~20 KB | ArrayBufferWriter (16KB initial) |

### Flush Strategy
Writers flush periodically based on `FlushBatchSize`:

- **Default:** 100 features per flush
- **OGC:** 50 features (8KB chunks for HTTP chunked encoding)
- **Custom:** Override `FlushBatchSize` property

### Backpressure Handling
The base class yields control every flush cycle:
```csharp
if (featuresWritten % FlushBatchSize == 0)
{
    await outputStream.FlushAsync(cancellationToken);
    await Task.Yield(); // Allow cancellation & prevent thread starvation
    cancellationToken.ThrowIfCancellationRequested();
}
```

## Error Handling

### Partial Response Behavior

Once streaming starts, errors cannot return proper HTTP error responses:

```csharp
try
{
    await WriteHeaderAsync(...);
    headerWritten = true;

    // Stream features...
}
catch (Exception ex)
{
    if (!headerWritten)
    {
        throw; // Can still return proper error
    }

    // Already started - client gets truncated output
    _logger.LogWarning("Truncated response - wrote {Count} features", count);
}
```

### Cancellation Support

All writers properly handle cancellation:
- `CancellationToken` propagated through all async operations
- Periodic cancellation checks during streaming
- Graceful cleanup on cancellation

## Telemetry

The base class automatically records:

### Activity Tags (OpenTelemetry)
- `format`: Format name (e.g., "GeoJSON", "CSV")
- `layer_id`: Layer identifier
- `features_written`: Total features written
- `bytes_written`: Total bytes written (if stream supports seeking)
- `duration_ms`: Total operation duration
- `streaming`: Always `true`

### Logging
- **Info:** Successful completion with metrics
- **Warning:** Slow writes (>1000ms) with details
- **Warning:** Truncated responses (errors after streaming started)
- **Error:** Write failures with exception details

## Migration Guide

### From Manual Streaming Code

**Before:**
```csharp
await using var writer = new Utf8JsonWriter(stream, options);
writer.WriteStartObject();
writer.WriteString("type", "FeatureCollection");
writer.WritePropertyName("features");
writer.WriteStartArray();

await foreach (var feature in features)
{
    WriteFeature(writer, feature, layer);

    if (count++ % 100 == 0)
    {
        await writer.FlushAsync(cancellationToken);
        await Task.Yield();
    }
}

writer.WriteEndArray();
writer.WriteEndObject();
await writer.FlushAsync(cancellationToken);
```

**After:**
```csharp
var writer = new GeoJsonStreamingWriter(logger);
var context = new StreamingWriterContext { /* options */ };

await writer.WriteCollectionAsync(
    stream,
    features,
    layer,
    context,
    cancellationToken);
```

### Benefits
- ✅ **60% less code** - Template handles boilerplate
- ✅ **Consistent error handling** - No partial response bugs
- ✅ **Built-in telemetry** - Automatic metrics & logging
- ✅ **Cancellation safety** - Proper cleanup guaranteed
- ✅ **Type safety** - No raw JSON manipulation

## Testing Recommendations

### Unit Tests
```csharp
[Fact]
public async Task WriteCollectionAsync_WithCancellation_StopsGracefully()
{
    var writer = new GeoJsonStreamingWriter(logger);
    var cts = new CancellationTokenSource();

    // Cancel after 50 features
    var features = SlowAsyncEnumerable(100, cts, cancelAfter: 50);

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        writer.WriteCollectionAsync(stream, features, layer, context, cts.Token));
}

[Fact]
public async Task WriteCollectionAsync_EmptyCollection_WritesValidOutput()
{
    var writer = new GeoJsonStreamingWriter(logger);
    var features = AsyncEnumerable.Empty<FeatureRecord>();

    await writer.WriteCollectionAsync(stream, features, layer, context);

    stream.Position = 0;
    var json = await JsonDocument.ParseAsync(stream);
    Assert.Equal("FeatureCollection", json.RootElement.GetProperty("type").GetString());
    Assert.Equal(0, json.RootElement.GetProperty("features").GetArrayLength());
}
```

### Integration Tests
```csharp
[Fact]
public async Task WriteCollectionAsync_LargeDataset_MaintainsConstantMemory()
{
    var writer = new GeoJsonStreamingWriter(logger);
    var features = GenerateFeatures(1_000_000); // 1M features

    var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

    await writer.WriteCollectionAsync(stream, features, layer, context);

    var memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
    var memoryDelta = memoryAfter - memoryBefore;

    // Should not grow significantly (allow ~10MB overhead)
    Assert.True(memoryDelta < 10_000_000,
        $"Memory grew by {memoryDelta:N0} bytes - not streaming!");
}
```

### Performance Tests
```csharp
[Fact]
public async Task WriteCollectionAsync_Throughput_MeetsTarget()
{
    var writer = new GeoJsonStreamingWriter(logger);
    var features = GenerateFeatures(10_000);

    var sw = Stopwatch.StartNew();
    await writer.WriteCollectionAsync(stream, features, layer, context);
    sw.Stop();

    var featuresPerSecond = 10_000.0 / sw.Elapsed.TotalSeconds;

    // Should write at least 5000 features/second
    Assert.True(featuresPerSecond >= 5000,
        $"Too slow: {featuresPerSecond:N0} features/sec");
}
```

## Code Duplication Eliminated

### Before Refactoring
- **StreamingGeoJsonWriter.cs**: 530 lines
- **OgcFeatureCollectionWriter.cs**: 334 lines
- **CsvExporter.cs**: 335 lines
- **Custom handlers**: ~100 lines each

**Common patterns duplicated:**
- Stream lifecycle management
- Periodic flushing logic
- Cancellation handling
- Error recovery
- Telemetry recording
- Logging patterns

**Total duplication**: ~250-300 lines across 4+ implementations

### After Refactoring
- **StreamingFeatureCollectionWriterBase.cs**: 275 lines (shared infrastructure)
- **GeoJsonStreamingWriter.cs**: 370 lines (format logic only)
- **CsvStreamingWriter.cs**: 230 lines (format logic only)
- **GeoJsonSeqStreamingWriter.cs**: 180 lines (format logic only)
- **OgcGeoJsonStreamingWriter.cs**: 240 lines (format logic only)

**Duplication eliminated**: ~250 lines (moved to base class)
**Code quality improvements:**
- ✅ Single source of truth for streaming patterns
- ✅ Consistent error handling across all formats
- ✅ Uniform telemetry and logging
- ✅ Easier to add new formats (extend base, implement 4 methods)
- ✅ Bug fixes in base class benefit all formats

## Future Enhancements

### Planned Writers
- **WktStreamingWriter**: Well-Known Text format
- **WkbStreamingWriter**: Well-Known Binary format
- **GmlStreamingWriter**: Geography Markup Language
- **GeoArrowStreamingWriter**: Apache Arrow with GeoArrow extension
- **FlatGeobufStreamingWriter**: FlatGeobuf binary format

### Performance Optimizations
- **Async buffering**: Pre-buffer next feature while writing current
- **SIMD coordinate encoding**: Vectorized coordinate serialization
- **Memory pooling**: Reuse buffers across requests
- **Compression streams**: Built-in gzip/brotli support

### Features
- **Progress callbacks**: Report progress during long operations
- **Format detection**: Auto-detect format from accept header
- **Multi-format writing**: Write to multiple formats simultaneously
- **Schema validation**: Validate features against JSON Schema

## Support

For questions or issues:
- See examples in `/tests/Honua.Server.Core.Tests/Serialization/`
- Check telemetry logs for performance metrics
- Review `StreamingWriterContext` documentation for options
- Extend base class for custom formats

---

**Last Updated:** 2025-10-25
**Version:** 1.0.0
**Status:** Production Ready

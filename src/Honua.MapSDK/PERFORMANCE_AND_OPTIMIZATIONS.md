# MapSDK Performance Optimizations & Production Enhancements

This document outlines the comprehensive performance optimizations, developer experience improvements, and production-ready features added to Honua.MapSDK.

## Table of Contents

1. [Performance Optimizations](#performance-optimizations)
2. [Developer Experience](#developer-experience)
3. [Error Handling & Logging](#error-handling--logging)
4. [Configuration System](#configuration-system)
5. [Utilities](#utilities)
6. [Testing Support](#testing-support)
7. [Accessibility](#accessibility)
8. [Best Practices](#best-practices)

---

## Performance Optimizations

### Data Loading Optimization

**Location**: `src/Honua.MapSDK/Services/DataLoading/`

#### DataCache
In-memory LRU cache with configurable TTL and size limits.

```csharp
var cache = new DataCache(new CacheOptions
{
    MaxSizeMB = 50,
    DefaultTtlSeconds = 300,
    MaxItems = 100
});

// Cache automatically manages eviction
var data = await cache.GetOrCreateAsync("key", async () =>
{
    return await LoadExpensiveDataAsync();
});
```

**Features**:
- LRU eviction policy
- Automatic cleanup of expired entries
- Size-based and count-based limits
- Compression support
- Cache statistics

#### DataLoader
Optimized loader with parallel fetching, caching, and request deduplication.

```csharp
var loader = new DataLoader(httpClient, cache);

// Automatic deduplication - multiple requests to same URL return same promise
var data1 = loader.LoadAsync("https://api.example.com/data.json");
var data2 = loader.LoadAsync("https://api.example.com/data.json"); // Deduped!

// Parallel loading
var results = await loader.LoadManyAsync(urls, maxParallel: 4);
```

**Features**:
- Request deduplication
- Parallel loading with concurrency control
- Automatic compression handling (gzip, brotli)
- Type-safe JSON deserialization
- Preloading support

#### StreamingLoader
Chunked loading for large datasets with progressive rendering.

```csharp
var loader = new StreamingLoader(httpClient);

await loader.StreamGeoJsonFeaturesAsync(
    url: "https://api.example.com/large-dataset.geojson",
    chunkSize: 100,
    onChunk: features =>
    {
        // Process chunk of 100 features at a time
        // UI remains responsive
    }
);
```

**Features**:
- Streaming GeoJSON features
- Streaming CSV with parsing
- Streaming JSON arrays
- Configurable chunk sizes
- UI-friendly yielding

#### CompressionHelper
GZIP and Brotli compression/decompression utilities.

```csharp
// Compress data
var compressed = CompressionHelper.CompressGzip(data);

// Auto-detect and decompress
var decompressed = CompressionHelper.AutoDecompress(bytes, "gzip");

// Check if compression is worthwhile
if (CompressionHelper.ShouldCompress(data, "application/json"))
{
    // Compress large JSON
}
```

### Performance Monitoring

**Location**: `src/Honua.MapSDK/Services/Performance/PerformanceMonitor.cs`

```csharp
var monitor = new PerformanceMonitor(logger, enabled: true);

// Measure operations
using (monitor.Measure("DataLoad"))
{
    await LoadDataAsync();
}

// Or measure with return value
var result = await monitor.MeasureAsync("ProcessData", async () =>
{
    return await ProcessDataAsync();
});

// Get statistics
var stats = monitor.GetAllStatistics();
foreach (var stat in stats.Values)
{
    Console.WriteLine($"{stat.OperationName}: avg={stat.Average}ms, p95={stat.P95}ms");
}
```

**Metrics Tracked**:
- Count
- Min/Max/Average
- Median
- 95th and 99th percentiles
- Total time

---

## Developer Experience

### Configuration System

**Location**: `src/Honua.MapSDK/Configuration/MapSdkOptions.cs`

Global configuration for all MapSDK features:

```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    // Performance
    options.EnablePerformanceMonitoring = true;
    options.Cache.MaxSizeMB = 100;
    options.Cache.DefaultTtlSeconds = 600;

    // Logging
    options.LogLevel = LogLevel.Information;
    options.EnableMessageTracing = false;

    // Rendering
    options.Rendering.VirtualScrollThreshold = 1000;
    options.Rendering.ChartDownsampleThreshold = 10000;
    options.Rendering.DebounceDelayMs = 300;
    options.Rendering.EnableAnimations = true;

    // Data Loading
    options.DataLoading.MaxParallelRequests = 4;
    options.DataLoading.TimeoutMs = 30000;
    options.DataLoading.EnableCompression = true;
    options.DataLoading.EnableRetry = true;
    options.DataLoading.MaxRetries = 3;

    // Accessibility
    options.Accessibility.EnableKeyboardShortcuts = true;
    options.Accessibility.EnableScreenReaderAnnouncements = true;
    options.Accessibility.RespectReducedMotion = true;

    // Development
    options.EnableDevTools = true; // Only in development
});
```

### Memory Management

**Location**: `src/Honua.MapSDK/Core/DisposableComponentBase.cs`

Base class for components with automatic disposal:

```csharp
public class MyComponent : DisposableComponentBase
{
    protected override void OnInitialized()
    {
        // Auto-unsubscribes on disposal
        SubscribeToMessage<MapClickedMessage>(msg => HandleMapClick(msg));

        // Register cleanup actions
        RegisterCleanup(() => ClearCache());

        // Register disposable resources
        RegisterDisposable(timer);
    }

    protected override void OnDispose()
    {
        // Custom disposal logic
    }
}
```

**Features**:
- Automatic ComponentBus unsubscription
- Cleanup action registration
- Disposable resource tracking
- Exception-safe disposal

### Keyboard Shortcuts

**Location**: `src/Honua.MapSDK/Services/KeyboardShortcuts.cs`

```csharp
@inject KeyboardShortcuts Shortcuts

protected override async Task OnInitializedAsync()
{
    await Shortcuts.InitializeAsync();

    // Register default shortcuts
    Shortcuts.RegisterDefaultShortcuts(
        onSearch: () => FocusSearchAsync(),
        onToggleFilters: () => ToggleFiltersAsync(),
        onToggleLegend: () => ToggleLegendAsync(),
        onPlayPause: () => TogglePlaybackAsync(),
        onClearSelection: () => ClearSelectionAsync()
    );

    // Custom shortcuts
    Shortcuts.Register("Ctrl+Shift+E", () => ExportDataAsync());
}
```

**Default Shortcuts**:
- `Ctrl+F`: Focus search
- `Ctrl+E`: Toggle filters
- `Ctrl+L`: Toggle legend
- `Ctrl+H`: Toggle help
- `Space`: Play/pause timeline
- `Escape`: Clear selection

---

## Error Handling & Logging

### MapErrorBoundary

**Location**: `src/Honua.MapSDK/Components/ErrorBoundary/MapErrorBoundary.razor`

```razor
<MapErrorBoundary
    ShowResetButton="true"
    ShowTechnicalDetails="true"
    OnError="HandleError"
    OnRetry="HandleRetry">

    <HonuaMap Configuration="config" />

</MapErrorBoundary>
```

**Features**:
- Graceful degradation
- User-friendly error messages
- Technical details in dev mode
- Retry functionality
- Custom error formatters

### MapSdkLogger

**Location**: `src/Honua.MapSDK/Logging/MapSdkLogger.cs`

Structured logging with performance tracking:

```csharp
@inject MapSdkLogger Logger

Logger.Info("Loading map data from {Url}", url);
Logger.LogDataLoad(url, durationMs: 523, success: true);
Logger.LogFilterApplication(filterCount: 3, resultCount: 45, durationMs: 12);
Logger.LogUserAction("ZoomIn", "Map", "Zoom level: 12");

// Measure performance
using (Logger.MeasurePerformance("RenderChart"))
{
    await RenderChartAsync();
}

// Get metrics
var metrics = Logger.GetMetrics();
```

**Log Categories**:
- `[DataLoad]`: Data loading operations
- `[Render]`: Component rendering
- `[ComponentBus]`: Message passing
- `[Filter]`: Filter applications
- `[UserAction]`: User interactions

---

## Utilities

### GeometryUtils

**Location**: `src/Honua.MapSDK/Utilities/GeometryUtils.cs`

Geographic calculations and geometry operations:

```csharp
// Distance calculation
var distanceKm = GeometryUtils.CalculateDistance(lon1, lat1, lon2, lat2);

// Bounding box
var bbox = GeometryUtils.CalculateBoundingBox(coordinates);
var expanded = GeometryUtils.ExpandBoundingBox(bbox, percent: 0.1);

// Point in polygon test
var isInside = GeometryUtils.IsPointInPolygon(lon, lat, polygon);

// Line simplification (Douglas-Peucker)
var simplified = GeometryUtils.SimplifyLine(points, epsilon: 0.001);

// Buffer creation
var buffer = GeometryUtils.CreateBuffer(lon, lat, radiusKm: 5, segments: 32);

// Coordinate system conversion
var (x, y) = GeometryUtils.Wgs84ToWebMercator(lon, lat);
var (lon2, lat2) = GeometryUtils.WebMercatorToWgs84(x, y);
```

### ColorUtils

**Location**: `src/Honua.MapSDK/Utilities/ColorUtils.cs`

Color manipulation and palette generation:

```csharp
// Color interpolation
var color = ColorUtils.Interpolate("#FF0000", "#0000FF", t: 0.5);

// Generate color scale
var scale = ColorUtils.GenerateColorScale("#FF0000", "#0000FF", steps: 10);

// Diverging scale
var diverging = ColorUtils.GenerateDivergingScale("#FF0000", "#FFFF00", "#00FF00", steps: 11);

// Predefined palettes
var colors = ColorUtils.Palettes.Viridis; // Perceptually uniform
var colors2 = ColorUtils.Palettes.RdYlGn; // Diverging

// Accessibility
var contrast = ColorUtils.CalculateContrastRatio("#FFFFFF", "#000000"); // 21
var meetsWCAG = ColorUtils.MeetsWcagAA("#FFFFFF", "#767676"); // true

// Color adjustments
var lighter = ColorUtils.Lighten("#FF0000", 0.2);
var darker = ColorUtils.Darken("#FF0000", 0.2);
var complementary = ColorUtils.GetComplementary("#FF0000");
```

### TimeUtils

**Location**: `src/Honua.MapSDK/Utilities/TimeUtils.cs`

Time and date utilities:

```csharp
// Parse various formats
var date = TimeUtils.TryParseDate("2024-01-15T10:30:00Z");

// Human-readable formatting
var duration = TimeUtils.FormatDuration(TimeSpan.FromMinutes(125)); // "2 hours 5 minutes"
var relative = TimeUtils.FormatRelativeTime(yesterday); // "1 day ago"

// Date ranges
var dates = TimeUtils.GenerateDateRange(start, end, TimeSpan.FromDays(1));

// Temporal binning
var bins = TimeUtils.BinTimestamps(timestamps, TimeSpan.FromHours(1));

// Business days
var businessDays = TimeUtils.CalculateBusinessDays(start, end);

// Date boundaries
var startOfMonth = TimeUtils.StartOfMonth(DateTime.Now);
var endOfWeek = TimeUtils.StartOfWeek(DateTime.Now, DayOfWeek.Monday);
```

### DataTransform

**Location**: `src/Honua.MapSDK/Utilities/DataTransform.cs`

Data format conversions:

```csharp
// GeoJSON to CSV
var csv = DataTransform.GeoJsonToCsv(geoJson, includeGeometry: true);

// CSV to GeoJSON
var geoJson = DataTransform.CsvToGeoJson(csv, "longitude", "latitude");

// JSON array to CSV
var csv2 = DataTransform.JsonArrayToCsv(jsonArray);

// Flatten nested JSON
var flat = DataTransform.FlattenJson(nestedJson, separator: ".");
```

### ValidationUtils

**Location**: `src/Honua.MapSDK/Utilities/ValidationUtils.cs`

Input validation and sanitization:

```csharp
// URL validation
if (ValidationUtils.IsValidUrl(url))
{
    // Safe to use
}

// Coordinate validation
if (ValidationUtils.IsValidCoordinate(lon, lat))
{
    // Valid WGS84 coordinates
}

// GeoJSON validation
var result = ValidationUtils.ValidateGeoJson(geoJson);
if (!result.IsValid)
{
    Console.WriteLine($"Invalid: {result.ErrorMessage}");
}

// HTML sanitization (XSS prevention)
var safe = ValidationUtils.SanitizeHtml(userInput);

// Path validation (prevent path traversal)
var pathResult = ValidationUtils.ValidateFilePath(userPath);
```

### ResponsiveHelper

**Location**: `src/Honua.MapSDK/Utilities/ResponsiveHelper.cs`

Responsive design utilities:

```csharp
@inject ResponsiveHelper Responsive

protected override async Task OnInitializedAsync()
{
    await Responsive.InitializeAsync();

    Responsive.BreakpointChanged += OnBreakpointChanged;

    if (Responsive.IsMobile)
    {
        // Mobile layout
    }
    else if (Responsive.IsTablet)
    {
        // Tablet layout
    }
    else
    {
        // Desktop layout
    }
}

private void OnBreakpointChanged(object? sender, BreakpointChangedEventArgs e)
{
    Console.WriteLine($"Breakpoint changed: {e.OldBreakpoint} -> {e.NewBreakpoint}");
    StateHasChanged();
}
```

**Breakpoints**:
- Mobile: < 640px
- MobileLarge: 640px - 768px
- Tablet: 768px - 1024px
- Desktop: 1024px - 1280px
- DesktopLarge: 1280px - 1536px
- DesktopXLarge: >= 1536px

---

## Testing Support

### MapSdkTestContext

**Location**: `src/Honua.MapSDK/Testing/MapSdkTestContext.cs`

Pre-configured test context for bUnit:

```csharp
using var ctx = new MapSdkTestContext();

// Use mock ComponentBus
var mockBus = ctx.UseMockComponentBus();

// Render component
var cut = ctx.RenderComponent<HonuaMap>(parameters => parameters
    .Add(p => p.Configuration, config)
);

// Assert messages
mockBus.AssertMessagePublished<MapLoadedMessage>();
mockBus.AssertMessageCount<FilterAppliedMessage>(3);

// Get messages
var messages = mockBus.GetMessagesOfType<MapClickedMessage>();
```

### MockDataGenerator

**Location**: `src/Honua.MapSDK/Testing/MockDataGenerator.cs`

Generate test data easily:

```csharp
// Generate GeoJSON points
var geoJson = MockDataGenerator.GenerateGeoJsonPoints(
    count: 1000,
    bounds: (-122.5, 37.7, -122.3, 37.8),
    properties: new Dictionary<string, Func<int, object>>
    {
        ["name"] = i => $"Point {i}",
        ["temperature"] = _ => Random.Next(60, 90),
        ["category"] = _ => new[] { "A", "B", "C" }[Random.Next(3)]
    }
);

// Generate time series
var timeSeries = MockDataGenerator.GenerateSineWave(
    points: 1000,
    amplitude: 10,
    frequency: 0.1
);

// Generate CSV
var csv = MockDataGenerator.GenerateCsv(
    rows: 100,
    columns: new Dictionary<string, Func<int, object>>
    {
        ["id"] = i => i,
        ["name"] = i => $"Item {i}",
        ["value"] = _ => Random.Next(0, 100)
    }
);
```

---

## Accessibility

### Features

1. **Screen Reader Support**
   - ARIA labels on all interactive elements
   - Live region announcements for dynamic updates
   - Semantic HTML structure

2. **Keyboard Navigation**
   - Full keyboard support for all features
   - Configurable shortcuts
   - Focus management
   - Skip links

3. **High Contrast Mode**
   - Automatic detection
   - Enhanced borders and focus indicators
   - Accessible color combinations

4. **Reduced Motion**
   - Respects `prefers-reduced-motion` media query
   - Disables animations when requested
   - Configurable via options

### Configuration

```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    options.Accessibility.EnableScreenReaderAnnouncements = true;
    options.Accessibility.EnableKeyboardShortcuts = true;
    options.Accessibility.EnableHighContrastMode = true;
    options.Accessibility.EnableFocusIndicators = true;
    options.Accessibility.RespectReducedMotion = true;
});
```

---

## Best Practices

### 1. Use Configuration System

```csharp
// Good: Use centralized configuration
builder.Services.AddHonuaMapSDK(options =>
{
    options.Cache.MaxSizeMB = 100;
    options.EnablePerformanceMonitoring = true;
});

// Bad: Manual service registration
services.AddSingleton(new DataCache(new CacheOptions { MaxSizeMB = 100 }));
```

### 2. Leverage Caching

```csharp
// Good: Use DataLoader with automatic caching
var data = await dataLoader.LoadJsonAsync<MyData>(url);

// Bad: Direct HTTP requests without caching
var data = await httpClient.GetFromJsonAsync<MyData>(url);
```

### 3. Handle Errors Gracefully

```razor
<!-- Good: Use error boundary -->
<MapErrorBoundary>
    <HonuaMap Configuration="config" />
</MapErrorBoundary>

<!-- Bad: No error handling -->
<HonuaMap Configuration="config" />
```

### 4. Use Disposable Base Class

```csharp
// Good: Automatic cleanup
public class MyComponent : DisposableComponentBase
{
    protected override void OnInitialized()
    {
        SubscribeToMessage<MyMessage>(HandleMessage);
    }
}

// Bad: Manual cleanup prone to leaks
public class MyComponent : ComponentBase, IDisposable
{
    private IDisposable? _subscription;

    protected override void OnInitialized()
    {
        _subscription = ComponentBus.Subscribe<MyMessage>(HandleMessage);
    }

    public void Dispose() => _subscription?.Dispose();
}
```

### 5. Monitor Performance

```csharp
// Enable monitoring in development
builder.Services.AddHonuaMapSDK(options =>
{
    options.EnablePerformanceMonitoring = builder.Environment.IsDevelopment();
});

// Log performance metrics
@inject PerformanceMonitor Monitor

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        Monitor.LogReport();
    }
}
```

### 6. Use Streaming for Large Datasets

```csharp
// Good: Stream large datasets
await streamingLoader.StreamGeoJsonFeaturesAsync(url, 100, features =>
{
    ProcessChunk(features);
    StateHasChanged();
});

// Bad: Load entire dataset at once (blocks UI)
var allFeatures = await LoadAllFeaturesAsync(url);
```

### 7. Validate User Input

```csharp
// Good: Validate and sanitize
var result = ValidationUtils.ValidateGeoJson(userInput);
if (result.IsValid)
{
    var sanitized = ValidationUtils.SanitizeHtml(userInput);
    await ProcessInputAsync(sanitized);
}

// Bad: Use user input directly
await ProcessInputAsync(userInput);
```

---

## Performance Checklist

- [ ] Use DataCache for repeated requests
- [ ] Enable compression for large payloads
- [ ] Stream large datasets with StreamingLoader
- [ ] Use parallel loading for multiple resources
- [ ] Enable performance monitoring in development
- [ ] Implement proper disposal in components
- [ ] Use debouncing for frequent updates
- [ ] Validate input data
- [ ] Handle errors with MapErrorBoundary
- [ ] Test with mock data generators
- [ ] Configure appropriate cache sizes
- [ ] Enable keyboard shortcuts for accessibility
- [ ] Test responsive breakpoints
- [ ] Monitor cache hit rates
- [ ] Review performance metrics regularly

---

## Migration Guide

### From Manual Setup to Configuration

**Before:**
```csharp
services.AddScoped<ComponentBus>();
services.AddHttpClient<DataLoader>();
// ... many manual registrations
```

**After:**
```csharp
services.AddHonuaMapSDK(options =>
{
    // Configure once
    options.Cache.MaxSizeMB = 50;
    options.EnablePerformanceMonitoring = true;
});
```

### From Manual Cleanup to DisposableComponentBase

**Before:**
```csharp
public class MyComponent : ComponentBase, IDisposable
{
    private IDisposable? _sub1;
    private IDisposable? _sub2;
    private Timer? _timer;

    protected override void OnInitialized()
    {
        _sub1 = ComponentBus.Subscribe<Msg1>(Handle);
        _sub2 = ComponentBus.Subscribe<Msg2>(Handle);
        _timer = new Timer(OnTimer, null, 1000, 1000);
    }

    public void Dispose()
    {
        _sub1?.Dispose();
        _sub2?.Dispose();
        _timer?.Dispose();
    }
}
```

**After:**
```csharp
public class MyComponent : DisposableComponentBase
{
    protected override void OnInitialized()
    {
        SubscribeToMessage<Msg1>(Handle);
        SubscribeToMessage<Msg2>(Handle);
        RegisterDisposable(new Timer(OnTimer, null, 1000, 1000));
    }
}
```

---

## Summary

The MapSDK now includes:

- ✅ **Performance**: Caching, streaming, parallel loading, compression
- ✅ **Developer Experience**: Configuration system, keyboard shortcuts, testing utilities
- ✅ **Reliability**: Error boundaries, logging, validation, disposal patterns
- ✅ **Utilities**: Geometry, color, time, data transformation helpers
- ✅ **Accessibility**: Screen readers, keyboard nav, high contrast, reduced motion
- ✅ **Production Ready**: Security, monitoring, error handling, memory management

The SDK is now enterprise-grade with excellent performance, developer experience, and reliability.

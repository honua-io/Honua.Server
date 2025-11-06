# Performance Optimization Guide

This guide covers performance optimization techniques for building high-performance mapping applications with Honua.MapSDK.

---

## Table of Contents

1. [Large Dataset Handling](#large-dataset-handling)
2. [Virtual Scrolling](#virtual-scrolling)
3. [Layer Optimization](#layer-optimization)
4. [Bundle Size Reduction](#bundle-size-reduction)
5. [Lazy Loading](#lazy-loading)
6. [Caching Strategies](#caching-strategies)
7. [Profiling and Debugging](#profiling-and-debugging)

---

## Large Dataset Handling

### Clustering for Large Point Datasets

```csharp
// Enable clustering for large datasets
await _map.AddSourceAsync("points", new
{
    type = "geojson",
    data = "/api/features?count=10000",
    cluster = true,
    clusterMaxZoom = 14,
    clusterRadius = 50
});

// Add cluster circles
await _map.AddLayerAsync(new
{
    id = "clusters",
    type = "circle",
    source = "points",
    filter = new object[] { "has", "point_count" },
    paint = new
    {
        circle_color = new object[]
        {
            "step",
            new[] { "get", "point_count" },
            "#51bbd6", 100,
            "#f1f075", 750,
            "#f28cb1"
        },
        circle_radius = new object[]
        {
            "step",
            new[] { "get", "point_count" },
            20, 100,
            30, 750,
            40
        }
    }
});

// Add cluster count labels
await _map.AddLayerAsync(new
{
    id = "cluster-count",
    type = "symbol",
    source = "points",
    filter = new object[] { "has", "point_count" },
    layout = new
    {
        text_field = new[] { "get", "point_count_abbreviated" },
        text_size = 12
    }
});

// Add unclustered points
await _map.AddLayerAsync(new
{
    id = "unclustered-point",
    type = "circle",
    source = "points",
    filter = new object[] { "!", new object[] { "has", "point_count" } },
    paint = new
    {
        circle_color = "#11b4da",
        circle_radius = 6,
        circle_stroke_width = 1,
        circle_stroke_color = "#fff"
    }
});
```

### Server-Side Filtering and Pagination

```csharp
public interface IFeatureService
{
    Task<PagedResult<Feature>> GetFeaturesAsync(
        int page = 1,
        int pageSize = 100,
        double[]? bounds = null,
        string? filter = null);
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// Usage
private async Task LoadVisibleFeatures()
{
    var bounds = await _map.GetBoundsAsync();
    var result = await _featureService.GetFeaturesAsync(
        page: 1,
        pageSize: 1000,
        bounds: bounds);

    await RenderFeatures(result.Items);
}
```

### Viewport-Based Data Loading

```csharp
private async Task HandleExtentChanged(MapExtentChangedMessage message)
{
    // Debounce extent changes
    _extentChangeTimer?.Dispose();
    _extentChangeTimer = new Timer(async _ =>
    {
        await LoadFeaturesInViewport(message.Bounds);
    }, null, 500, Timeout.Infinite);
}

private async Task LoadFeaturesInViewport(double[] bounds)
{
    // Only load features within current viewport
    var features = await _featureService.GetFeaturesAsync(bounds: bounds);
    await UpdateMapFeatures(features);
}
```

---

## Virtual Scrolling

### Virtual Scrolling in Data Grids

```razor
<HonuaDataGrid TItem="Feature"
               Items="@_features"
               Virtualize="true"
               ItemHeight="48"
               PageSize="50"
               Height="600px">
    <Columns>
        <PropertyColumn Property="f => f.Name" />
        <PropertyColumn Property="f => f.Type" />
        <PropertyColumn Property="f => f.Status" />
    </Columns>
</HonuaDataGrid>
```

### Custom Virtual Scrolling Implementation

```razor
<MudVirtualize Items="@_features"
               Context="feature"
               OverscanCount="10">
    <MudListItem>
        <MudText>@feature.Name</MudText>
        <MudText Typo="Typo.body2">@feature.Description</MudText>
    </MudListItem>
</MudVirtualize>

@code {
    private List<Feature> _features = new();

    protected override async Task OnInitializedAsync()
    {
        // Load only initial batch
        _features = await _featureService.GetFeaturesAsync(page: 1, pageSize: 100);
    }
}
```

---

## Layer Optimization

### Simplify Geometries

```csharp
// Use Turf.js or similar for geometry simplification
public static class GeometrySimplifier
{
    public static List<double[]> Simplify(List<double[]> coordinates, double tolerance = 0.0001)
    {
        // Douglas-Peucker algorithm implementation
        if (coordinates.Count < 3)
            return coordinates;

        var simplified = new List<double[]> { coordinates[0] };
        SimplifyRecursive(coordinates, 0, coordinates.Count - 1, tolerance, simplified);
        simplified.Add(coordinates[^1]);

        return simplified;
    }

    private static void SimplifyRecursive(
        List<double[]> points,
        int first,
        int last,
        double tolerance,
        List<double[]> simplified)
    {
        double maxDistance = 0;
        int index = 0;

        for (int i = first + 1; i < last; i++)
        {
            double distance = PerpendicularDistance(points[i], points[first], points[last]);
            if (distance > maxDistance)
            {
                index = i;
                maxDistance = distance;
            }
        }

        if (maxDistance > tolerance)
        {
            SimplifyRecursive(points, first, index, tolerance, simplified);
            simplified.Add(points[index]);
            SimplifyRecursive(points, index, last, tolerance, simplified);
        }
    }

    private static double PerpendicularDistance(double[] point, double[] lineStart, double[] lineEnd)
    {
        double dx = lineEnd[0] - lineStart[0];
        double dy = lineEnd[1] - lineStart[1];

        double mag = Math.Sqrt(dx * dx + dy * dy);
        if (mag > 0.0)
        {
            dx /= mag;
            dy /= mag;
        }

        double pvx = point[0] - lineStart[0];
        double pvy = point[1] - lineStart[1];

        double pvdot = dx * pvx + dy * pvy;

        double dsx = pvdot * dx;
        double dsy = pvdot * dy;

        double ax = pvx - dsx;
        double ay = pvy - dsy;

        return Math.Sqrt(ax * ax + ay * ay);
    }
}

// Usage
var simplified = GeometrySimplifier.Simplify(feature.Geometry.Coordinates, tolerance: 0.0001);
```

### Use Appropriate Layer Types

```csharp
// For large polygons, use fill-extrusion only when needed
if (zoomLevel > 14 && showBuildings3D)
{
    await _map.AddLayerAsync(new
    {
        id = "buildings-3d",
        type = "fill-extrusion",
        source = "buildings",
        minzoom = 14,
        paint = new
        {
            fill_extrusion_color = "#aaa",
            fill_extrusion_height = new[] { "get", "height" }
        }
    });
}
else
{
    // Use simpler fill layer
    await _map.AddLayerAsync(new
    {
        id = "buildings-2d",
        type = "fill",
        source = "buildings",
        maxzoom = 14,
        paint = new
        {
            fill_color = "#aaa",
            fill_opacity = 0.8
        }
    });
}
```

---

## Bundle Size Reduction

### Tree Shaking

```xml
<!-- In .csproj -->
<PropertyGroup>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>link</TrimMode>
</PropertyGroup>
```

### Lazy Load Components

```razor
@* Instead of loading all components upfront *@
@using Honua.MapSDK.Components

@* Use lazy loading *@
<Suspense>
    <ChildContent>
        @if (_showMap)
        {
            <HonuaMap Id="lazy-map" />
        }
    </ChildContent>
    <Fallback>
        <MudProgressCircular Indeterminate="true" />
    </Fallback>
</Suspense>
```

### Minimize JavaScript Interop

```csharp
// Bad: Multiple JS interop calls
for (int i = 0; i < 1000; i++)
{
    await _map.AddMarkerAsync(markers[i]); // 1000 JS calls!
}

// Good: Batch JS interop calls
await _map.AddMarkersAsync(markers); // 1 JS call
```

---

## Lazy Loading

### Route-Based Lazy Loading

```csharp
// App.razor
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout="@typeof(MainLayout)">
            <p>Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>

// Lazy load map page
@page "/map"
@attribute [StreamRendering]

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load map resources only when needed
            await LoadMapResources();
        }
    }
}
```

### Defer Non-Critical Resources

```razor
<script src="_content/Honua.MapSDK/maplibre-gl.js" defer></script>
<script src="_content/Honua.MapSDK/chart.js" defer></script>
```

---

## Caching Strategies

### In-Memory Caching

```csharp
public class CachedFeatureService : IFeatureService
{
    private readonly IFeatureService _inner;
    private readonly IMemoryCache _cache;

    public CachedFeatureService(IFeatureService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<List<Feature>> GetFeaturesAsync(string? filter = null)
    {
        string cacheKey = $"features_{filter ?? "all"}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await _inner.GetFeaturesAsync(filter);
        }) ?? new List<Feature>();
    }
}

// Register in Program.cs
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IFeatureService, FeatureService>();
builder.Services.Decorate<IFeatureService, CachedFeatureService>();
```

### Browser Caching

```csharp
// Add cache headers to API responses
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/features"))
    {
        context.Response.Headers.Add("Cache-Control", "public, max-age=300");
    }
    await next();
});
```

### Service Worker for Offline Support

```javascript
// wwwroot/service-worker.js
const CACHE_NAME = 'mapsdk-cache-v1';
const urlsToCache = [
    '/',
    '/styles/map-style.json',
    '/data/features.geojson'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(urlsToCache))
    );
});

self.addEventListener('fetch', event => {
    event.respondWith(
        caches.match(event.request)
            .then(response => response || fetch(event.request))
    );
});
```

---

## Profiling and Debugging

### Browser Performance Tools

```javascript
// Measure map rendering time
console.time('map-render');
await _map.RenderFeaturesAsync(features);
console.timeEnd('map-render');

// Monitor memory usage
if (performance.memory) {
    console.log('Used JS Heap:', (performance.memory.usedJSHeapSize / 1048576).toFixed(2), 'MB');
    console.log('Total JS Heap:', (performance.memory.totalJSHeapSize / 1048576).toFixed(2), 'MB');
}
```

### .NET Performance Profiling

```csharp
using System.Diagnostics;

// Method timing
var sw = Stopwatch.StartNew();
var features = await _featureService.GetFeaturesAsync();
sw.Stop();
_logger.LogInformation($"Feature loading took {sw.ElapsedMilliseconds}ms");

// Memory profiling
var memoryBefore = GC.GetTotalMemory(false);
await ProcessLargeDataset();
var memoryAfter = GC.GetTotalMemory(false);
_logger.LogInformation($"Memory used: {(memoryAfter - memoryBefore) / 1024 / 1024}MB");
```

### MapLibre Performance Tips

```javascript
// Enable performance metrics
map.showCollisionBoxes = true; // Debug label collisions
map.showTileBoundaries = true; // Debug tile loading

// Monitor FPS
let lastTime = performance.now();
let frames = 0;

function measureFPS() {
    frames++;
    const currentTime = performance.now();
    if (currentTime >= lastTime + 1000) {
        const fps = Math.round((frames * 1000) / (currentTime - lastTime));
        console.log(`FPS: ${fps}`);
        frames = 0;
        lastTime = currentTime;
    }
    requestAnimationFrame(measureFPS);
}
measureFPS();
```

### Component Performance Monitoring

```razor
@implements IDisposable

@code {
    private System.Diagnostics.Stopwatch _renderTimer;

    protected override void OnInitialized()
    {
        _renderTimer = System.Diagnostics.Stopwatch.StartNew();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            _renderTimer.Stop();
            Console.WriteLine($"Component rendered in {_renderTimer.ElapsedMilliseconds}ms");
        }
    }

    public void Dispose()
    {
        _renderTimer?.Stop();
    }
}
```

---

## Performance Checklist

### Frontend Optimization

- [ ] Use clustering for large point datasets (>1000 points)
- [ ] Enable virtual scrolling for large data grids
- [ ] Implement viewport-based data loading
- [ ] Simplify complex geometries
- [ ] Lazy load non-critical components
- [ ] Minimize JavaScript interop calls
- [ ] Use appropriate layer types for zoom levels
- [ ] Cache API responses
- [ ] Optimize images and assets
- [ ] Enable compression (gzip/brotli)

### Backend Optimization

- [ ] Implement server-side pagination
- [ ] Add database indexes on frequently queried fields
- [ ] Use spatial indexes for geometric queries
- [ ] Cache frequently accessed data
- [ ] Implement rate limiting
- [ ] Optimize database queries (use EF query analysis)
- [ ] Use async/await properly
- [ ] Enable response compression

### Bundle Optimization

- [ ] Enable PublishTrimmed
- [ ] Remove unused dependencies
- [ ] Lazy load routes
- [ ] Use CDN for static assets
- [ ] Minify CSS and JavaScript
- [ ] Use tree-shaking

---

## Performance Metrics

### Target Metrics for Production

| Metric | Target | Critical |
|--------|--------|----------|
| Initial Load Time | < 3s | < 5s |
| Time to Interactive | < 5s | < 8s |
| Map Render Time | < 500ms | < 1s |
| Feature Load (1000 items) | < 1s | < 2s |
| FPS (during pan/zoom) | > 30 | > 20 |
| Memory Usage | < 100MB | < 200MB |
| Bundle Size | < 2MB | < 5MB |

### Monitoring Tools

- **Blazor**: .NET Performance Counter
- **Browser**: Chrome DevTools Performance tab
- **Network**: Chrome DevTools Network tab
- **Bundle**: webpack-bundle-analyzer
- **APM**: Application Insights, Datadog

---

## Real-World Example

```csharp
public class OptimizedMapComponent : ComponentBase
{
    [Inject] private IFeatureService FeatureService { get; set; }
    [Inject] private IMemoryCache Cache { get; set; }

    private HonuaMap? _map;
    private CancellationTokenSource? _loadCts;
    private Timer? _debounceTimer;

    protected override async Task OnInitializedAsync()
    {
        // Load minimal initial data
        await LoadInitialFeatures();
    }

    private async Task LoadInitialFeatures()
    {
        // Load from cache first
        var cached = await Cache.GetAsync<List<Feature>>("initial-features");
        if (cached != null)
        {
            await RenderFeatures(cached);
            return;
        }

        // Load and cache
        var features = await FeatureService.GetFeaturesAsync(pageSize: 100);
        await Cache.SetAsync("initial-features", features, TimeSpan.FromMinutes(5));
        await RenderFeatures(features);
    }

    private async Task HandleExtentChanged(MapExtentChangedMessage message)
    {
        // Cancel previous load
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();

        // Debounce extent changes
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async _ =>
        {
            try
            {
                await LoadFeaturesForExtent(message.Bounds, _loadCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
        }, null, 300, Timeout.Infinite);
    }

    private async Task LoadFeaturesForExtent(double[] bounds, CancellationToken ct)
    {
        var features = await FeatureService.GetFeaturesAsync(bounds: bounds);

        if (!ct.IsCancellationRequested)
        {
            await RenderFeatures(features);
        }
    }

    private async Task RenderFeatures(List<Feature> features)
    {
        // Batch render features
        await _map!.AddFeaturesAsync(features);
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _debounceTimer?.Dispose();
    }
}
```

---

## Further Reading

- [MapLibre GL JS Performance Guide](https://maplibre.org/maplibre-gl-js-docs/example/)
- [Blazor Performance Best Practices](https://docs.microsoft.com/en-us/aspnet/core/blazor/performance)
- [.NET Memory Management](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/)

---

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*

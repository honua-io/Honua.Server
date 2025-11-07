# Best Practices for Honua.MapSDK

This guide covers recommended patterns, performance optimizations, and best practices for building production applications with Honua.MapSDK.

## Table of Contents

1. [Component Usage Patterns](#component-usage-patterns)
2. [Performance Optimization](#performance-optimization)
3. [Error Handling](#error-handling)
4. [Testing Strategies](#testing-strategies)
5. [Accessibility](#accessibility)
6. [Security](#security)
7. [Deployment](#deployment)

## Component Usage Patterns

### Pattern 1: Use ComponentBus for Inter-Component Communication

**Good:**
```csharp
// Let ComponentBus handle communication
<HonuaMap Id="map1" ... />
<HonuaDataGrid SyncWith="map1" ... />
<HonuaChart SyncWith="map1" ... />
```

**Avoid:**
```csharp
// Don't pass callbacks or state between components
<HonuaMap OnExtentChanged="UpdateGrid" OnExtentChanged="UpdateChart" ... />
```

**Why:** ComponentBus provides loose coupling, automatic cleanup, and better scalability.

### Pattern 2: Provide Unique Component IDs

**Good:**
```csharp
<HonuaMap Id="mainMap" ... />
<HonuaMap Id="overviewMap" ... />
```

**Avoid:**
```csharp
// Auto-generated IDs make syncing difficult
<HonuaMap ... />
<HonuaDataGrid SyncWith="???" ... />
```

**Why:** Explicit IDs make component relationships clear and debugging easier.

### Pattern 3: Use DisposableComponentBase

**Good:**
```csharp
public class MyComponent : DisposableComponentBase
{
    protected override void OnInitialized()
    {
        // Automatically unsubscribed on disposal
        SubscribeToMessage<MapExtentChangedMessage>(HandleExtent);
    }
}
```

**Avoid:**
```csharp
public class MyComponent : ComponentBase, IDisposable
{
    private IDisposable? _subscription;

    protected override void OnInitialized()
    {
        // Manual management prone to leaks
        _subscription = Bus.Subscribe<MapExtentChangedMessage>(HandleExtent);
    }

    public void Dispose() => _subscription?.Dispose();
}
```

**Why:** DisposableComponentBase handles cleanup automatically, preventing memory leaks.

### Pattern 4: Wrap Critical Components in Error Boundaries

**Good:**
```razor
<MapErrorBoundary>
    <HonuaMap Configuration="@config" />
</MapErrorBoundary>
```

**Why:** Prevents entire application crashes and provides recovery options.

### Pattern 5: Centralize Configuration

**Good:**
```csharp
// Program.cs
builder.Services.AddHonuaMapSDK(options =>
{
    options.Cache.MaxSizeMB = 100;
    options.EnablePerformanceMonitoring = true;
    // ... all configuration in one place
});
```

**Avoid:**
```csharp
// Scattered configuration across multiple files
services.AddSingleton(new DataCache(...));
services.Configure<MapOptions>(...);
```

**Why:** Centralized configuration is easier to maintain and understand.

## Performance Optimization

### 1. Use Caching Effectively

**Good:**
```csharp
@inject DataLoader Loader

private async Task LoadData()
{
    // Automatic caching with 10-minute TTL
    var data = await Loader.LoadJsonAsync<MyData>(url);
}
```

**Configure Cache:**
```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    options.Cache.MaxSizeMB = 100;
    options.Cache.DefaultTtlSeconds = 600;
    options.Cache.MaxItems = 100;
});
```

### 2. Stream Large Datasets

**Good:**
```csharp
await StreamingLoader.StreamGeoJsonFeaturesAsync(
    url,
    chunkSize: 100,
    onChunk: features =>
    {
        ProcessChunk(features);
        StateHasChanged(); // Keep UI responsive
    }
);
```

**Avoid:**
```csharp
// Loading entire dataset blocks UI
var allFeatures = await LoadAllFeaturesAsync(url);
ProcessAllFeatures(allFeatures);
```

**Why:** Streaming keeps UI responsive and reduces memory usage.

### 3. Debounce Frequent Updates

**Good:**
```csharp
private Timer? _debounceTimer;

private void OnMapMoveInternal()
{
    _debounceTimer?.Dispose();
    _debounceTimer = new Timer(_ =>
    {
        InvokeAsync(() =>
        {
            UpdateData();
            StateHasChanged();
        });
    }, null, 300, Timeout.Infinite);
}
```

**Configure Globally:**
```csharp
options.Rendering.DebounceDelayMs = 300;
```

### 4. Enable Virtual Scrolling for Large Grids

**Good:**
```csharp
<HonuaDataGrid
    VirtualizeThreshold="1000"  // Virtual scroll for >1000 rows
    PageSize="50"
    ... />
```

**Configure Globally:**
```csharp
options.Rendering.VirtualScrollThreshold = 1000;
```

### 5. Use Parallel Loading

**Good:**
```csharp
var urls = new[] { url1, url2, url3 };
var results = await DataLoader.LoadManyAsync(urls, maxParallel: 4);
```

**Configure:**
```csharp
options.DataLoading.MaxParallelRequests = 4;
```

### 6. Monitor Performance in Development

**Enable Monitoring:**
```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    options.EnablePerformanceMonitoring = builder.Environment.IsDevelopment();
});
```

**Use PerformanceMonitor:**
```csharp
@inject PerformanceMonitor Monitor

using (Monitor.Measure("DataLoad"))
{
    await LoadDataAsync();
}

// Review metrics
var stats = Monitor.GetStatistics("DataLoad");
Logger.LogInformation("Avg: {Avg}ms, P95: {P95}ms", stats.Average, stats.P95);
```

## Error Handling

### 1. Use Error Boundaries

```razor
<MapErrorBoundary
    ShowResetButton="true"
    ShowTechnicalDetails="@IsDevelopment"
    OnError="HandleError">

    <!-- Critical components -->
    <HonuaMap ... />

</MapErrorBoundary>
```

### 2. Handle Async Errors

**Good:**
```csharp
private async Task LoadDataAsync()
{
    try
    {
        var data = await DataLoader.LoadJsonAsync<T>(url);
        ProcessData(data);
    }
    catch (HttpRequestException ex)
    {
        Logger.LogError(ex, "Network error loading data from {Url}", url);
        Snackbar.Add("Failed to load data. Check your connection.", Severity.Error);
    }
    catch (JsonException ex)
    {
        Logger.LogError(ex, "Invalid JSON from {Url}", url);
        Snackbar.Add("Invalid data format received.", Severity.Error);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Unexpected error loading data");
        Snackbar.Add("An unexpected error occurred.", Severity.Error);
    }
}
```

### 3. Validate User Input

**Good:**
```csharp
private async Task HandleImport(IBrowserFile file)
{
    // Validate file type
    if (!ValidationUtils.IsValidFileType(file.Name, allowedExtensions))
    {
        Snackbar.Add("Invalid file type", Severity.Error);
        return;
    }

    // Validate file size
    if (file.Size > maxFileSize)
    {
        Snackbar.Add("File too large", Severity.Error);
        return;
    }

    // Validate content
    var content = await ReadFileAsync(file);
    var result = ValidationUtils.ValidateGeoJson(content);
    if (!result.IsValid)
    {
        Snackbar.Add($"Invalid GeoJSON: {result.ErrorMessage}", Severity.Error);
        return;
    }

    await ProcessFileAsync(content);
}
```

### 4. Provide User Feedback

**Good:**
```csharp
private async Task SaveChangesAsync()
{
    Snackbar.Add("Saving changes...", Severity.Info);

    try
    {
        await SaveAsync();
        Snackbar.Add("Changes saved successfully", Severity.Success);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to save changes");
        Snackbar.Add("Failed to save changes", Severity.Error);
    }
}
```

## Testing Strategies

### 1. Use MapSdkTestContext

```csharp
using Honua.MapSDK.Testing;

public class MapComponentTests
{
    [Fact]
    public void Map_Publishes_ReadyMessage()
    {
        using var ctx = new MapSdkTestContext();
        var mockBus = ctx.UseMockComponentBus();

        var cut = ctx.RenderComponent<HonuaMap>(parameters => parameters
            .Add(p => p.Id, "test-map")
        );

        mockBus.AssertMessagePublished<MapReadyMessage>();
    }
}
```

### 2. Mock ComponentBus for Unit Tests

```csharp
[Fact]
public async Task Component_ReactsTo_ExtentChange()
{
    var mockBus = new Mock<ComponentBus>();
    var component = new MyComponent { Bus = mockBus.Object };

    // Simulate message
    await mockBus.Object.PublishAsync(new MapExtentChangedMessage
    {
        MapId = "test",
        Zoom = 12
    });

    // Assert behavior
    Assert.Equal(12, component.CurrentZoom);
}
```

### 3. Use Mock Data Generators

```csharp
[Fact]
public void Grid_Displays_1000_Features()
{
    var geoJson = MockDataGenerator.GenerateGeoJsonPoints(
        count: 1000,
        bounds: (-122.5, 37.7, -122.3, 37.8)
    );

    var cut = RenderComponent<HonuaDataGrid>(parameters => parameters
        .Add(p => p.Source, geoJson)
    );

    Assert.Equal(1000, cut.Instance.RowCount);
}
```

## Accessibility

### 1. Enable Accessibility Features

```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    options.Accessibility.EnableKeyboardShortcuts = true;
    options.Accessibility.EnableScreenReaderAnnouncements = true;
    options.Accessibility.RespectReducedMotion = true;
    options.Accessibility.EnableFocusIndicators = true;
});
```

### 2. Provide Alt Text and Labels

**Good:**
```razor
<HonuaMap
    AriaLabel="Main interactive map showing property locations"
    ... />

<HonuaSearch
    Placeholder="Search for an address or location"
    AriaLabel="Location search input"
    ... />
```

### 3. Support Keyboard Navigation

MapSDK provides default keyboard shortcuts:
- `Ctrl+F` - Focus search
- `Ctrl+E` - Toggle filters
- `Ctrl+L` - Toggle legend
- `Space` - Play/pause timeline
- `Escape` - Clear selection

Customize if needed:
```csharp
@inject KeyboardShortcuts Shortcuts

protected override async Task OnInitializedAsync()
{
    await Shortcuts.InitializeAsync();

    Shortcuts.Register("Ctrl+Shift+E", ExportDataAsync);
}
```

### 4. Test with Screen Readers

- Test with NVDA (Windows), JAWS, or VoiceOver (macOS)
- Ensure all interactive elements are announced
- Verify focus management
- Test keyboard-only navigation

## Security

### 1. Validate and Sanitize User Input

**Good:**
```csharp
// Sanitize HTML content
var safe = ValidationUtils.SanitizeHtml(userInput);

// Validate URLs
if (!ValidationUtils.IsValidUrl(userUrl))
{
    throw new ArgumentException("Invalid URL");
}

// Validate file paths
var pathResult = ValidationUtils.ValidateFilePath(userPath);
if (!pathResult.IsValid)
{
    throw new ArgumentException("Invalid file path");
}
```

### 2. Protect API Keys

**Good:**
```csharp
// Use user secrets in development
builder.Configuration.AddUserSecrets<Program>();

// Use environment variables in production
builder.Services.AddHonuaMapSDK(options =>
{
    options.Geocoding.ApiKey = builder.Configuration["MapboxApiKey"];
});
```

**Avoid:**
```csharp
// Never hardcode API keys
options.Geocoding.ApiKey = "pk.eyJ1IjoibXl1c2VyIiwiYSI6ImNr...";
```

### 3. Implement CORS Properly

```csharp
// Configure CORS for API endpoints
builder.Services.AddCors(options =>
{
    options.AddPolicy("MapSDKPolicy", policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

### 4. Use HTTPS

```csharp
// Enforce HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

## Deployment

### 1. Optimize for Production

```csharp
// Program.cs
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });

    builder.Services.AddHonuaMapSDK(options =>
    {
        options.EnablePerformanceMonitoring = false;
        options.EnableMessageTracing = false;
        options.Cache.MaxSizeMB = 200; // More cache in production
    });
}
```

### 2. Use CDN for Static Assets

```html
<!-- Use CDN for MapLibre -->
<link href="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.css" rel="stylesheet" />
<script src="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js"></script>
```

### 3. Configure Caching Headers

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append(
            "Cache-Control",
            "public,max-age=31536000" // 1 year for versioned assets
        );
    }
});
```

### 4. Monitor Performance

- Set up Application Insights or similar
- Track key metrics:
  - Page load time
  - Component initialization time
  - Data loading time
  - Error rates
- Set up alerts for performance degradation

### 5. Implement Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<MapSdkHealthCheck>("mapsdk");

app.MapHealthChecks("/health");
```

## Configuration Checklist

### Development
- [ ] Enable performance monitoring
- [ ] Enable message tracing (if debugging)
- [ ] Use lower cache sizes
- [ ] Show technical error details
- [ ] Enable dev tools

### Production
- [ ] Disable performance monitoring
- [ ] Disable message tracing
- [ ] Increase cache sizes
- [ ] Hide technical error details
- [ ] Disable dev tools
- [ ] Enable compression
- [ ] Use CDN for static assets
- [ ] Configure CORS properly
- [ ] Use environment variables for secrets
- [ ] Set up monitoring and alerts

## Performance Targets

Aim for these targets in production:

- **Initial Load:** < 2 seconds
- **Map Initialization:** < 500ms
- **Data Grid (1000 rows):** < 100ms
- **Chart Rendering (10K points):** < 500ms
- **ComponentBus Message:** < 5ms
- **Memory Usage:** < 100MB for typical dashboards

## Summary

Following these best practices will help you:

- Build performant applications
- Avoid common pitfalls
- Create maintainable code
- Provide excellent user experience
- Deploy with confidence

## Further Reading

- [Architecture](Architecture.md) - Understand the design
- [Troubleshooting](Troubleshooting.md) - Solve common issues
- [Performance Guide](../../src/Honua.MapSDK/PERFORMANCE_AND_OPTIMIZATIONS.md) - Deep dive into optimization

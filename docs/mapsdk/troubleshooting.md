# Troubleshooting Guide

This guide covers common issues, error messages, and solutions for Honua.MapSDK.

## Table of Contents

1. [Map Rendering Issues](#map-rendering-issues)
2. [Component Communication Issues](#component-communication-issues)
3. [Performance Problems](#performance-problems)
4. [JavaScript Errors](#javascript-errors)
5. [Data Loading Issues](#data-loading-issues)
6. [Browser Compatibility](#browser-compatibility)
7. [FAQ](#faq)

## Map Rendering Issues

### Map Container is Empty

**Symptoms:** Map container div is visible but no map content appears.

**Causes & Solutions:**

1. **Missing height on container**
   ```razor
   <!-- BAD: No height -->
   <HonuaMap Id="myMap" ... />

   <!-- GOOD: Explicit height -->
   <div style="height: 600px;">
       <HonuaMap Id="myMap" ... />
   </div>
   ```

2. **MapLibre GL CSS not loaded**
   ```html
   <!-- Add to _Host.cshtml or App.razor -->
   <link href="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.css" rel="stylesheet" />
   ```

3. **JavaScript module not loaded**
   ```html
   <!-- Add to _Host.cshtml or App.razor -->
   <script src="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js"></script>
   ```

4. **Invalid map style URL**
   ```csharp
   // Check that style URL is accessible
   MapStyle="https://demotiles.maplibre.org/style.json"
   ```

### Map Flickers or Jumps

**Symptoms:** Map viewport jumps or flickers during interaction.

**Solutions:**

1. **Avoid updating Center/Zoom parameters during interaction**
   ```csharp
   // BAD: Updates during interaction
   <HonuaMap Center="@_currentCenter" Zoom="@_currentZoom" ... />

   // GOOD: Set initial values only
   <HonuaMap Center="@_initialCenter" Zoom="@_initialZoom" ... />
   ```

2. **Use FlyToAsync for programmatic navigation**
   ```csharp
   await mapRef.FlyToAsync(center, zoom);
   ```

### Map Tiles Not Loading

**Symptoms:** Gray tiles or missing imagery.

**Solutions:**

1. **Check network connectivity**
2. **Verify tile server is accessible**
3. **Check CORS configuration**
4. **Verify API key if required**
   ```csharp
   MapStyle="https://api.maptiler.com/maps/streets/style.json?key=YOUR_KEY"
   ```

## Component Communication Issues

### Components Not Syncing

**Symptoms:** Map moves but grid/chart doesn't update.

**Diagnosis:**

1. **Check Component IDs**
   ```csharp
   // Ensure IDs match
   <HonuaMap Id="map1" ... />
   <HonuaDataGrid SyncWith="map1" ... />  <!-- Must match -->
   ```

2. **Verify ComponentBus is registered**
   ```csharp
   // In Program.cs
   builder.Services.AddHonuaMapSDK();  // Registers ComponentBus
   ```

3. **Enable message tracing**
   ```csharp
   builder.Services.AddHonuaMapSDK(options =>
   {
       options.EnableMessageTracing = true;  // See messages in console
   });
   ```

4. **Check browser console for errors**

### Messages Not Being Received

**Symptoms:** ComponentBus subscriptions not triggering.

**Solutions:**

1. **Ensure subscription happens during initialization**
   ```csharp
   protected override void OnInitialized()
   {
       // Subscribe here, not in OnAfterRender
       Bus.Subscribe<MapExtentChangedMessage>(HandleExtent);
   }
   ```

2. **Check for early disposal**
   ```csharp
   // Use DisposableComponentBase for automatic cleanup
   public class MyComponent : DisposableComponentBase
   {
       protected override void OnInitialized()
       {
           SubscribeToMessage<MapExtentChangedMessage>(HandleExtent);
       }
   }
   ```

3. **Verify message type matches**
   ```csharp
   // Publisher and subscriber must use same type
   await Bus.PublishAsync(new MapExtentChangedMessage { ... });
   Bus.Subscribe<MapExtentChangedMessage>(HandleExtent);  // Same type
   ```

## Performance Problems

### Slow Initial Load

**Symptoms:** Application takes >5 seconds to load.

**Solutions:**

1. **Reduce initial data load**
   ```csharp
   // Load data on demand, not during initialization
   protected override async Task OnAfterRenderAsync(bool firstRender)
   {
       if (firstRender)
       {
           await LoadDataAsync();  // After UI renders
       }
   }
   ```

2. **Enable caching**
   ```csharp
   builder.Services.AddHonuaMapSDK(options =>
   {
       options.Cache.MaxSizeMB = 100;
       options.DataLoading.EnableCompression = true;
   });
   ```

3. **Use CDN for static assets**

### Grid Rendering Slowly

**Symptoms:** Data grid takes >1 second to render 10,000+ rows.

**Solutions:**

1. **Enable virtualization**
   ```csharp
   <HonuaDataGrid VirtualizeThreshold="1000" ... />
   ```

2. **Implement pagination**
   ```csharp
   <HonuaDataGrid EnablePagination="true" PageSize="50" ... />
   ```

3. **Reduce visible columns**

### Chart Rendering Slowly

**Symptoms:** Chart takes several seconds to render.

**Solutions:**

1. **Enable downsampling**
   ```csharp
   builder.Services.AddHonuaMapSDK(options =>
   {
       options.Rendering.ChartDownsampleThreshold = 10000;
   });
   ```

2. **Reduce data points**
   ```csharp
   // Aggregate data before charting
   var aggregated = data.GroupBy(x => x.Category)
                        .Select(g => new { Category = g.Key, Count = g.Count() });
   ```

### High Memory Usage

**Symptoms:** Browser tab using >500MB memory.

**Solutions:**

1. **Limit cache size**
   ```csharp
   options.Cache.MaxSizeMB = 50;  // Reduce from 100MB
   ```

2. **Clean up subscriptions**
   ```csharp
   // Use DisposableComponentBase for automatic cleanup
   public class MyComponent : DisposableComponentBase { ... }
   ```

3. **Stream large datasets**
   ```csharp
   await StreamingLoader.StreamGeoJsonFeaturesAsync(url, 100, ProcessChunk);
   ```

## JavaScript Errors

### "Cannot read property 'map' of undefined"

**Cause:** Trying to access map before initialization.

**Solution:**
```csharp
private IJSObjectReference? _mapInstance;

private async Task DoSomethingWithMap()
{
    if (_mapInstance == null)
    {
        Logger.LogWarning("Map not initialized yet");
        return;
    }

    await _mapInstance.InvokeVoidAsync("someMethod");
}
```

### "Failed to execute 'postMessage' on 'DOMWindow'"

**Cause:** Trying to pass non-serializable objects to JavaScript.

**Solution:**
```csharp
// BAD: Complex object
await JS.InvokeVoidAsync("method", complexObject);

// GOOD: Serializable data
await JS.InvokeVoidAsync("method", new
{
    id = complexObject.Id,
    name = complexObject.Name
});
```

### "Maximum call stack size exceeded"

**Cause:** Infinite loop in message handling.

**Solution:**
```csharp
// BAD: Publishing in response to same message
Bus.Subscribe<MapExtentChangedMessage>(args =>
{
    await Bus.PublishAsync(new MapExtentChangedMessage { ... });  // LOOP!
});

// GOOD: Check source to prevent loops
Bus.Subscribe<MapExtentChangedMessage>(args =>
{
    if (args.Source != Id)  // Don't process own messages
    {
        await UpdateExtent(args.Message);
    }
});
```

## Data Loading Issues

### 404 Not Found

**Symptoms:** Data URL returns 404 error.

**Solutions:**

1. **Verify URL is correct**
2. **Check CORS headers**
3. **Verify authentication if required**

### Data Not Displaying

**Symptoms:** Data loads but doesn't appear on map.

**Solutions:**

1. **Verify data format**
   ```csharp
   var result = ValidationUtils.ValidateGeoJson(data);
   if (!result.IsValid)
   {
       Logger.LogError("Invalid GeoJSON: {Error}", result.ErrorMessage);
   }
   ```

2. **Check coordinate order**
   ```json
   // GeoJSON uses [longitude, latitude]
   {
     "type": "Point",
     "coordinates": [-122.4194, 37.7749]  // [lon, lat]
   }
   ```

3. **Verify map extent contains data**

### Timeout Errors

**Symptoms:** Requests timeout after 30 seconds.

**Solutions:**

1. **Increase timeout**
   ```csharp
   builder.Services.AddHonuaMapSDK(options =>
   {
       options.DataLoading.TimeoutMs = 60000;  // 60 seconds
   });
   ```

2. **Use streaming for large files**
3. **Implement pagination**

## Browser Compatibility

### Safari Issues

**Issue:** Map not rendering in Safari.

**Solutions:**

1. **Ensure Safari 13.1+**
2. **Check WebGL support**: Visit https://get.webgl.org/
3. **Disable Safari extensions** that might interfere

### Firefox Issues

**Issue:** Components rendering slowly in Firefox.

**Solutions:**

1. **Ensure Firefox 70+**
2. **Disable Firefox tracking protection** for development
3. **Check for console errors**

### Mobile Browser Issues

**Issue:** Touch events not working correctly.

**Solutions:**

1. **Ensure viewport meta tag**
   ```html
   <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
   ```

2. **Test on actual devices**, not just browser dev tools
3. **Use touch-specific event handlers** if needed

## FAQ

### Q: Why isn't my component receiving messages?

**A:** Check these items:
1. ComponentBus is registered in DI
2. Component is subscribing in `OnInitialized`
3. Component ID matches between publisher and subscriber
4. Component hasn't been disposed
5. Message type is correct

### Q: How do I debug ComponentBus messages?

**A:** Enable message tracing:
```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    options.EnableMessageTracing = true;
});
```

Check browser console for message flow.

### Q: Why is my map slow?

**A:** Common causes:
1. Loading too much data at once - use streaming
2. No caching - enable DataCache
3. Too many layers - reduce visible layers
4. No GPU acceleration - ensure EnableGPU="true"
5. Large features - simplify geometries

### Q: How do I report a bug?

**A:**
1. Check if issue is already reported: https://github.com/honua/Honua.Server/issues
2. Create minimal reproduction
3. Include browser console errors
4. Note browser and OS version
5. Submit issue with details

### Q: Can I use MapSDK with Blazor WebAssembly?

**A:** Yes, but note:
1. Some server-side features won't work
2. Adjust DI lifetime scopes appropriately
3. May require different interop patterns

### Q: How do I upgrade to the latest version?

**A:** See [Migration Guide](Migration.md) for version-specific instructions.

### Q: Why can't I see my API key working?

**A:**
1. Verify key is valid and not expired
2. Check API key has correct permissions
3. Verify domain is whitelisted (if required)
4. Check request headers include key
5. Look for CORS errors in console

### Q: How do I improve performance?

**A:** See [Best Practices](BestPractices.md) and [Performance Guide](../../src/Honua.MapSDK/PERFORMANCE_AND_OPTIMIZATIONS.md).

## Still Having Issues?

If you're still experiencing problems:

1. **Check the documentation** - Most issues are covered in docs
2. **Search GitHub issues** - Someone may have had the same problem
3. **Enable debug logging** - Get more details about what's happening
4. **Create a minimal reproduction** - Isolate the problem
5. **Ask for help**:
   - GitHub Discussions: https://github.com/honua/Honua.Server/discussions
   - Email: support@honua.io

Include in your report:
- Honua.MapSDK version
- .NET version
- Browser and version
- Operating system
- Complete error messages
- Minimal code to reproduce
- Steps to reproduce

---

**Remember:** Most issues are configuration or usage errors. Check the basics first!

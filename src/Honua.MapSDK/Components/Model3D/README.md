# Advanced 3D Model Loading (GLTF/GLB)

Comprehensive support for loading and displaying 3D models in GLTF and GLB formats on MapLibre maps using Three.js.

## Features

✅ **GLTF/GLB Loading** - Load industry-standard 3D model formats
✅ **Geographic Positioning** - Place models at precise lat/lng/altitude coordinates
✅ **Transformations** - Scale, rotate, and position models
✅ **Animation Support** - Play, pause, and control model animations
✅ **LOD (Level of Detail)** - Automatic model switching based on distance
✅ **Interactive Picking** - Click on models to get intersection data
✅ **Performance Optimized** - Handles multiple models at 60 FPS
✅ **Memory Management** - Smart caching with automatic eviction
✅ **PBR Materials** - Full support for Physically Based Rendering

## Components

### 1. Honua3DModel

Main component for loading and displaying 3D models.

```razor
<Honua3DModel MapId="my-map"
              ModelId="building-1"
              ModelUrl="models/building.glb"
              Position="@buildingPosition"
              Altitude="50"
              Scale="10"
              Rotation="@buildingRotation"
              EnableAnimation="true"
              AnimationIndex="0"
              OnModelLoaded="OnModelLoaded"
              OnModelError="OnModelError" />
```

**Parameters:**
- `ModelId` (string, required) - Unique identifier for this model instance
- `MapId` (string, required) - ID of the map to attach to
- `ModelUrl` (string, required) - URL to GLTF/GLB file
- `Position` (Coordinate3D, required) - Geographic position (lat/lng)
- `Altitude` (double) - Height above ground in meters (default: 0)
- `Scale` (double) - Model scale factor (default: 1.0)
- `Rotation` (Vector3) - Rotation in radians (x, y, z)
- `EnableAnimation` (bool) - Enable animations (default: true)
- `AnimationIndex` (int) - Which animation to play (default: 0)
- `AnimationSpeed` (double) - Playback speed (default: 1.0)
- `LoopAnimation` (bool) - Loop animation (default: true)
- `EnableLOD` (bool) - Enable Level of Detail (default: false)
- `LodLevels` (List<Model3DLodLevel>) - LOD configuration
- `EnablePicking` (bool) - Enable click interaction (default: true)
- `ShowDebugInfo` (bool) - Show debug overlay (default: false)
- `Preload` (bool) - Load model immediately (default: true)

**Events:**
- `OnModelLoaded` (EventCallback<Model3DInfo>) - Model loaded successfully
- `OnModelError` (EventCallback<string>) - Model load failed
- `OnModelClick` (EventCallback<Model3DPickResult>) - Model was clicked
- `OnAnimationComplete` (EventCallback<int>) - Animation finished
- `OnLoadProgress` (EventCallback<double>) - Loading progress (0-100)

### 2. Model3DAnimationController

UI component for controlling model animations.

```razor
<Model3DAnimationController Model="@myModel"
                           ModelInfo="@modelInfo"
                           OnAnimationPlay="OnAnimationPlay"
                           OnAnimationPause="OnAnimationPause" />
```

**Features:**
- Play/Pause/Stop controls
- Animation selection dropdown
- Timeline scrubber
- Speed control (0.1x - 3.0x)
- Loop toggle
- Real-time time display

### 3. Model3DPicker

Component for interactive model picking (raycasting).

```razor
<Model3DPicker MapId="my-map"
               ShowPickInfo="true"
               OnModelPicked="OnModelPicked"
               OnPickMiss="OnPickMiss" />
```

**Parameters:**
- `MapId` (string, required) - Map ID to attach to
- `Enabled` (bool) - Enable picking (default: true)
- `ShowPickInfo` (bool) - Show pick information panel (default: true)
- `MaxDistance` (double) - Max picking distance in meters (default: 10000)
- `HighlightOnPick` (bool) - Highlight picked model (default: true)

**Events:**
- `OnModelPicked` (EventCallback<Model3DPickResult>) - Model was picked
- `OnPickMiss` (EventCallback) - Click missed all models

## Services

### Model3DLoaderService

Server-side service for model loading and caching.

```csharp
@inject Model3DLoaderService ModelLoader

// Load and cache model
var modelInfo = await ModelLoader.LoadModelAsync(
    "https://example.com/model.glb",
    preload: true
);

// Get cache statistics
var stats = ModelLoader.GetCacheStatistics();
Console.WriteLine($"Cached: {stats.GetTotalSizeString()} ({stats.GetUtilizationString()})");

// Clear cache
ModelLoader.ClearCache();
```

**Methods:**
- `LoadModelAsync(url, preload)` - Load model metadata
- `GetCachedModelData(url)` - Get cached binary data
- `GetCachedModelInfo(url)` - Get cached model info
- `ClearCache()` - Clear all cached models
- `RemoveFromCache(url)` - Remove specific model
- `GetCacheStatistics()` - Get cache stats
- `ValidateModelUrl(url)` - Validate GLTF/GLB URL
- `PreloadModelsAsync(urls)` - Preload multiple models

## Models

### Model3DInfo

Contains metadata about a loaded model.

```csharp
public class Model3DInfo
{
    public string ModelId { get; init; }
    public string ModelUrl { get; init; }
    public string Format { get; init; } // "GLTF" or "GLB"
    public List<Model3DAnimation> Animations { get; init; }
    public BoundingBox3D BoundingBox { get; init; }
    public int VertexCount { get; init; }
    public int TriangleCount { get; init; }
    public int MeshCount { get; init; }
    public int MaterialCount { get; init; }
    public long? FileSizeBytes { get; init; }
    public double? LoadTimeMs { get; init; }
    public List<Model3DLodLevel> LodLevels { get; init; }
    public bool SupportsPBR { get; init; }
}
```

### Model3DPickResult

Result of a picking operation.

```csharp
public class Model3DPickResult
{
    public string ModelId { get; init; }
    public Vector3 Point { get; init; }
    public Coordinate3D? Coordinate { get; init; }
    public double Distance { get; init; }
    public Vector3? Normal { get; init; }
    public int? MeshIndex { get; init; }
    public string? MeshName { get; init; }
    public Vector2? UV { get; init; }
}
```

## JavaScript API

### HonuaModel3D

JavaScript module for Three.js integration.

```javascript
// Initialize
HonuaModel3D.initialize(mapId, mapLibreMap, {
    enableLighting: true,
    enablePicking: true
});

// Load model
const modelInfo = await HonuaModel3D.loadModel(mapId, modelId, modelUrl, {
    position: { latitude: 37.7749, longitude: -122.4194 },
    altitude: 100,
    scale: 5,
    rotation: [0, 0, 0],
    enableAnimation: true,
    animationIndex: 0
});

// Update animation
HonuaModel3D.updateAnimation(mapId, modelId, {
    animationIndex: 1,
    timeScale: 1.5,
    loop: true
});

// Pick model
const result = HonuaModel3D.pickModel(mapId, screenX, screenY);

// Remove model
HonuaModel3D.removeModel(mapId, modelId);

// Get FPS
const fps = HonuaModel3D.getFPS();
```

## Level of Detail (LOD)

Configure multiple model resolutions for performance:

```csharp
var lodLevels = new List<Model3DLodLevel>
{
    new() { Level = 0, ModelUrl = "model-high.glb", TriangleCount = 100000, MinDistance = 0, MaxDistance = 100 },
    new() { Level = 1, ModelUrl = "model-medium.glb", TriangleCount = 25000, MinDistance = 100, MaxDistance = 500 },
    new() { Level = 2, ModelUrl = "model-low.glb", TriangleCount = 5000, MinDistance = 500, MaxDistance = 2000 }
};

<Honua3DModel EnableLOD="true" LodLevels="@lodLevels" ... />
```

## Performance Guidelines

### Triangle Count Targets

- **Low** (< 5,000 triangles) - Mobile-friendly, 60+ FPS
- **Medium** (5,000 - 25,000) - Desktop/tablet, 60 FPS
- **High** (25,000 - 100,000) - Desktop only, 30-60 FPS
- **Very High** (> 100,000) - High-end desktop, may drop below 30 FPS

### Optimization Tips

1. **Use LOD** - Provide multiple resolutions for distance-based switching
2. **Optimize Textures** - Use compressed formats (JPEG, WebP)
3. **Reduce Geometry** - Simplify meshes in 3D software before export
4. **Limit Models** - Keep total models < 10 for best performance
5. **Preload** - Preload models during initialization
6. **Cache** - Enable caching to avoid repeated downloads
7. **GLB over GLTF** - Binary GLB is smaller and faster to parse

## Sample Models

### Khronos glTF Sample Models

Free sample models for testing:

```razor
@* Simple Box with animation *@
<Honua3DModel ModelUrl="https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Box/glTF-Binary/Box.glb" ... />

@* Damaged Helmet - PBR demo *@
<Honua3DModel ModelUrl="https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/DamagedHelmet/glTF-Binary/DamagedHelmet.glb" ... />

@* Animated Cube *@
<Honua3DModel ModelUrl="https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/AnimatedCube/glTF-Binary/AnimatedCube.glb"
              EnableAnimation="true" ... />
```

More samples: [glTF-Sample-Models](https://github.com/KhronosGroup/glTF-Sample-Models)

## Complete Example

```razor
@page "/map-with-models"
@using Honua.MapSDK.Components.Model3D
@using Honua.MapSDK.Models
@using Honua.MapSDK.Models.Model3D
@inject Model3DLoaderService ModelLoader

<HonuaMapLibre MapId="my-map"
               Center="@center"
               Zoom="16"
               Pitch="60"
               Style="width: 100%; height: 600px;">

    <Honua3DModel @ref="buildingModel"
                 MapId="my-map"
                 ModelId="building"
                 ModelUrl="/models/building.glb"
                 Position="@buildingPos"
                 Altitude="0"
                 Scale="10"
                 EnableAnimation="true"
                 ShowDebugInfo="true"
                 OnModelLoaded="OnModelLoaded"
                 OnModelClick="OnModelClicked" />

    <Model3DPicker MapId="my-map"
                  ShowPickInfo="true"
                  OnModelPicked="OnModelPicked" />
</HonuaMapLibre>

@if (modelInfo != null && modelInfo.Animations.Count > 0)
{
    <Model3DAnimationController Model="@buildingModel"
                               ModelInfo="@modelInfo" />
}

@code {
    private Honua3DModel? buildingModel;
    private Model3DInfo? modelInfo;
    private Coordinate3D center = Coordinate3D.Create2D(-122.4194, 37.7749);
    private Coordinate3D buildingPos = Coordinate3D.Create2D(-122.4194, 37.7749);

    private void OnModelLoaded(Model3DInfo info)
    {
        modelInfo = info;
        Console.WriteLine($"Loaded: {info.TriangleCount} triangles, {info.LoadTimeMs}ms");
    }

    private void OnModelClicked(Model3DPickResult result)
    {
        Console.WriteLine($"Clicked at: {result.Point}");
    }

    private void OnModelPicked(Model3DPickResult result)
    {
        Console.WriteLine($"Picked: {result.ModelId} at {result.Distance:F2}m");
    }
}
```

## Dependencies

### Required

- **Three.js** (r150+) - 3D rendering engine
- **GLTFLoader** - GLTF/GLB parser
- **MapLibre GL JS** - Map rendering

### CDN Links

```html
<!-- Three.js -->
<script src="https://unpkg.com/three@0.150.0/build/three.min.js"></script>

<!-- GLTFLoader -->
<script src="https://unpkg.com/three@0.150.0/examples/js/loaders/GLTFLoader.js"></script>
```

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (iOS Safari 14+, Chrome Android)

Requires WebGL 2.0 support.

## Troubleshooting

### Model doesn't appear

1. Check browser console for errors
2. Verify Three.js is loaded (`HonuaModel3D.isAvailable()`)
3. Confirm map is initialized before loading model
4. Check model URL is accessible (CORS)
5. Verify coordinates are within map bounds

### Poor performance

1. Check triangle count (`modelInfo.TriangleCount`)
2. Enable LOD for distance-based optimization
3. Reduce number of simultaneous models
4. Use compressed textures
5. Simplify geometry in 3D software

### Animations don't play

1. Verify model has animations (`modelInfo.Animations.Count > 0`)
2. Check `EnableAnimation="true"`
3. Ensure correct `AnimationIndex`
4. Check browser console for errors

### Picking not working

1. Verify `EnablePicking="true"` on Honua3DModel
2. Ensure Model3DPicker is added to map
3. Check `Enabled="true"` on Model3DPicker
4. Verify click events are not blocked by other elements

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0

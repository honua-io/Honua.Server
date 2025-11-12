# Live Analysis Previews for Spatial Operations

This document describes the Live Analysis Preview feature for Honua's spatial operations, which allows users to see results before executing operations with instant parameter adjustment feedback.

## Overview

The Live Analysis Preview system provides:
- **Real-time preview** of spatial operations (Buffer, Clip, Dissolve, Intersection, etc.)
- **Interactive parameter tuning** with instant visual feedback
- **Smart sampling** for large datasets (shows first 100 features by default)
- **Streaming responses** for progressive rendering
- **Parameter validation** before execution
- **Different styling** for preview vs final results (semi-transparent, dashed borders)

## Architecture

### Backend Components

#### 1. Preview Execution Service (`ProcessPreviewExecutor.cs`)

Located at: `/src/Honua.Server.Core/Processes/ProcessPreviewExecutor.cs`

**Key Features:**
- Limits preview to first N features (default: 100)
- Applies spatial sampling for representative feature distribution
- Simplifies geometries for faster rendering
- Enforces timeout (default: 5 seconds)
- Returns preview metadata (feature count, execution time, warnings)

**Example Usage:**
```csharp
var executor = new ProcessPreviewExecutor(processRegistry);
var request = new PreviewExecutionRequest
{
    ProcessId = "buffer",
    Inputs = new Dictionary<string, object>
    {
        ["geometries"] = inputGeometries,
        ["distance"] = 100,
        ["unit"] = "meters"
    },
    Options = new PreviewExecutionOptions
    {
        MaxPreviewFeatures = 100,
        UseSpatialSampling = true,
        SimplifyGeometries = true
    }
};

var result = await executor.ExecutePreviewAsync(request, cancellationToken);
```

#### 2. Preview API Endpoints

Located at: `/src/Honua.Server.Host/Processes/OgcProcessesPreviewHandlers.cs`

**Endpoints:**

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/processes/{processId}/preview` | POST | Execute process in preview mode |
| `/processes/{processId}/validate` | POST | Validate inputs without executing |

**Query Parameters:**
- `maxFeatures` (int, default: 100) - Maximum features to return
- `timeout` (int, default: 5000) - Timeout in milliseconds
- `spatialSampling` (bool, default: true) - Use spatial sampling
- `simplify` (bool, default: true) - Simplify geometries
- `stream` (bool, default: false) - Enable streaming response

**Example Request:**
```bash
POST /processes/buffer/preview?maxFeatures=100&stream=false
Content-Type: application/json

{
  "inputs": {
    "geometries": [...],
    "distance": 100,
    "unit": "meters",
    "unionResults": false
  }
}
```

**Example Response:**
```json
{
  "type": "FeatureCollection",
  "features": [...],
  "metadata": {
    "preview": true,
    "totalFeatures": 5000,
    "previewFeatures": 100,
    "spatialSampling": true,
    "simplified": true,
    "executionTimeMs": 234,
    "message": "Preview showing 100 features. Use full execution for complete results.",
    "warnings": null
  },
  "style": {
    "fillColor": "#3B82F6",
    "fillOpacity": 0.3,
    "strokeColor": "#2563EB",
    "strokeWidth": 2,
    "strokeDashArray": [5, 5]
  }
}
```

### Frontend Components

#### 1. Preview Layer Component (`HonuaAnalysisPreview.razor`)

Located at: `/src/Honua.MapSDK/Components/Analysis/HonuaAnalysisPreview.razor`

**Usage:**
```razor
<HonuaAnalysisPreview
    MapViewId="mainMap"
    ProcessId="buffer"
    Parameters="@bufferParams"
    AutoRefresh="true"
    MaxFeatures="100"
    OnExecute="HandleFullExecution">

    <ParameterControls>
        <BufferParameterControl
            @bind-Distance="bufferParams.Distance"
            @bind-Unit="bufferParams.Unit"
            @bind-UnionResults="bufferParams.UnionResults" />
    </ParameterControls>
</HonuaAnalysisPreview>

@code {
    private Dictionary<string, object> bufferParams = new()
    {
        ["distance"] = 100.0,
        ["unit"] = "meters",
        ["unionResults"] = false
    };

    private async Task HandleFullExecution(Dictionary<string, object> parameters)
    {
        // Execute full operation
        await ProcessService.ExecuteAsync("buffer", parameters);
    }
}
```

#### 2. Parameter Control Components

Located at: `/src/Honua.MapSDK/Components/Analysis/ParameterControls/`

**Available Controls:**

##### BufferParameterControl
```razor
<BufferParameterControl
    @bind-Distance="distance"
    @bind-Unit="unit"
    @bind-UnionResults="unionResults"
    MinDistance="1"
    MaxDistance="1000"
    ShowQuickPresets="true" />
```

**Features:**
- Distance slider with real-time updates
- Unit selection (meters, kilometers, feet, miles)
- Union results toggle
- Quick preset buttons (10m, 50m, 100m, 500m, 1km)

##### ClipParameterControl
```razor
<ClipParameterControl
    @bind-SelectedGeometryId="clipGeometryId"
    AvailableGeometries="@clipGeometries"
    @bind-PreserveOriginalExtent="preserveExtent"
    @bind-MaintainTopology="maintainTopology"
    ShowDrawButton="true"
    OnDrawClipGeometry="HandleDrawClip" />
```

**Features:**
- Geometry selection from available layers
- Preserve extent option
- Maintain topology option
- Draw clip geometry button

#### 3. Preview Layer JavaScript Module

Located at: `/src/Honua.MapSDK/wwwroot/Components/Analysis/preview-layer.js`

**Functions:**

```javascript
// Load preview from API
const result = await loadPreview(mapViewId, url, parameters);

// Load streaming preview
await loadStreamingPreview(mapViewId, url, parameters, (progress) => {
    console.log(`Loaded ${progress.featuresLoaded} features`);
});

// Clear preview layer
await clearPreview(mapViewId, layerId);

// Get/set preview styles
const style = getPreviewStyle('buffer');
setPreviewStyle('buffer', { fillColor: '#FF0000' });
```

## Supported Operations

### 1. Buffer Preview

**Parameters:**
- `distance` (number) - Buffer distance
- `unit` (string) - Distance unit (meters, kilometers, feet, miles)
- `unionResults` (boolean) - Union overlapping buffers

**Preview Optimizations:**
- Limits to 100 input features
- Simplifies complex geometries
- Uses spatial sampling for uniform distribution

**Example:**
```razor
@code {
    private BufferParameters bufferParams = new()
    {
        Distance = 100,
        Unit = "meters",
        UnionResults = false
    };
}

<HonuaAnalysisPreview
    ProcessId="buffer"
    Parameters="@GetBufferInputs()"
    AutoRefresh="true">

    <ParameterControls>
        <BufferParameterControl
            @bind-Distance="bufferParams.Distance"
            @bind-Unit="bufferParams.Unit"
            @bind-UnionResults="bufferParams.UnionResults" />
    </ParameterControls>
</HonuaAnalysisPreview>

@code {
    private Dictionary<string, object> GetBufferInputs() => new()
    {
        ["geometries"] = selectedGeometries,
        ["distance"] = bufferParams.Distance,
        ["unit"] = bufferParams.Unit,
        ["unionResults"] = bufferParams.UnionResults
    };
}
```

### 2. Clip Preview

**Parameters:**
- `targetGeometries` (array) - Geometries to clip
- `clipGeometry` (geometry) - Clipping boundary
- `preserveExtent` (boolean) - Keep original extent

**Preview Optimizations:**
- Samples target geometries spatially
- Simplifies clip boundary if complex
- Shows clipped results in preview style

### 3. Intersection Preview

**Parameters:**
- `geometries1` (array) - First geometry set
- `geometries2` (array) - Second geometry set

**Preview Optimizations:**
- Limits both input sets to 50 features each
- Performs pairwise intersection on samples

### 4. Dissolve Preview

**Parameters:**
- `geometries` (array) - Geometries to dissolve
- `dissolveField` (string, optional) - Field for attribute-based dissolve

**Preview Optimizations:**
- Groups geometries by spatial proximity
- Shows dissolved preview for representative groups

## Performance Considerations

### Preview Optimizations

1. **Feature Limiting**
   - Default: 100 features maximum
   - Configurable via `maxFeatures` parameter
   - Prevents browser overload with large datasets

2. **Spatial Sampling**
   - Divides extent into grid cells
   - Selects one feature per cell
   - Ensures representative distribution

3. **Geometry Simplification**
   - Applies Douglas-Peucker algorithm
   - Tolerance: 0.1% of feature extent
   - Reduces rendering complexity

4. **Execution Timeout**
   - Default: 5 seconds
   - Prevents long-running previews
   - Returns partial results on timeout

5. **Streaming Response**
   - Progressive feature rendering
   - Shows results as they're computed
   - Improves perceived performance

### Memory Management

The preview system manages memory efficiently:
- Previews use lightweight `ProcessJob` instances (no persistence)
- Preview layers are cleared when new preview is loaded
- Disposed properly on component unmount

## Styling

### Preview Layer Styles

Preview layers use distinct styling to differentiate from final results:

```javascript
{
    fillColor: '#3B82F6',     // Blue fill
    fillOpacity: 0.25,         // 25% transparent
    strokeColor: '#2563EB',    // Darker blue border
    strokeWidth: 2,            // 2px border
    strokeDashArray: [5, 5]    // Dashed line
}
```

### Operation-Specific Styles

Different operations use different colors:

| Operation | Fill Color | Stroke Color |
|-----------|------------|--------------|
| Buffer | Blue (#3B82F6) | Dark Blue (#2563EB) |
| Clip | Purple (#8B5CF6) | Dark Purple (#7C3AED) |
| Intersect | Green (#10B981) | Dark Green (#059669) |
| Dissolve | Orange (#F59E0B) | Dark Orange (#D97706) |

### Custom Styling

```javascript
import { setPreviewStyle } from './preview-layer.js';

// Customize buffer preview style
setPreviewStyle('buffer', {
    fillColor: '#FF0000',
    fillOpacity: 0.4,
    strokeWidth: 3
});
```

## Error Handling

### Validation Errors

The preview system validates inputs before execution:

```json
{
  "valid": false,
  "errors": [
    "Required input 'geometries' is missing",
    "Buffer distance must be greater than 0"
  ],
  "warnings": null
}
```

### Preview Failures

Preview failures return helpful error messages:

```json
{
  "success": false,
  "metadata": {
    "preview": true,
    "previewFeatures": 0,
    "executionTimeMs": 234,
    "warnings": [
      "Preview operation timed out. Try reducing the input size or use full execution."
    ]
  }
}
```

## Best Practices

### 1. Use Auto-Refresh Wisely

```razor
<!-- Good: Auto-refresh for simple parameters -->
<HonuaAnalysisPreview
    ProcessId="buffer"
    Parameters="@params"
    AutoRefresh="true" />

<!-- Better: Manual refresh for complex parameters -->
<HonuaAnalysisPreview
    ProcessId="clip"
    Parameters="@params"
    AutoRefresh="false"
    ShowControls="true" />
```

### 2. Provide Parameter Validation Feedback

```razor
<HonuaAnalysisPreview
    ProcessId="buffer"
    Parameters="@params"
    OnPreviewLoaded="HandlePreviewLoaded">

    @if (validationMessages.Any())
    {
        <div class="alerts">
            @foreach (var msg in validationMessages)
            {
                <div class="alert alert-@msg.Type">@msg.Text</div>
            }
        </div>
    }
</HonuaAnalysisPreview>
```

### 3. Handle Large Datasets Gracefully

```razor
@code {
    private async Task LoadPreview()
    {
        if (inputFeatureCount > 10000)
        {
            // Use streaming for large datasets
            await LoadStreamingPreview();
        }
        else
        {
            // Use regular preview
            await LoadRegularPreview();
        }
    }
}
```

### 4. Optimize Preview Settings Based on Context

```csharp
var options = new PreviewExecutionOptions();

if (isInteractiveSession)
{
    options.MaxPreviewFeatures = 50;  // Faster updates
    options.PreviewTimeoutMs = 2000;  // Quick timeout
}
else
{
    options.MaxPreviewFeatures = 200; // More accurate
    options.PreviewTimeoutMs = 10000; // Allow more time
}
```

## API Reference

### C# Classes

#### PreviewExecutionOptions
```csharp
public class PreviewExecutionOptions
{
    public int MaxPreviewFeatures { get; set; } = 100;
    public int PreviewTimeoutMs { get; set; } = 5000;
    public bool UseSpatialSampling { get; set; } = true;
    public bool EnableStreamingPreview { get; set; } = true;
    public int StreamingChunkSize { get; set; } = 10;
    public bool SimplifyGeometries { get; set; } = true;
    public double SimplificationTolerance { get; set; } = 0.001;
}
```

#### PreviewMetadata
```csharp
public class PreviewMetadata
{
    public bool IsPreview { get; init; }
    public long? TotalFeatures { get; init; }
    public int PreviewFeatures { get; init; }
    public bool SpatialSampling { get; init; }
    public bool Simplified { get; init; }
    public long ExecutionTimeMs { get; init; }
    public string? Message { get; init; }
    public List<string>? Warnings { get; init; }
}
```

### Razor Component Parameters

#### HonuaAnalysisPreview
```csharp
[Parameter] public string? MapViewId { get; set; }
[Parameter] public required string ProcessId { get; set; }
[Parameter] public Dictionary<string, object>? Parameters { get; set; }
[Parameter] public RenderFragment? ParameterControls { get; set; }
[Parameter] public string Title { get; set; } = "Analysis Preview";
[Parameter] public bool ShowControls { get; set; } = true;
[Parameter] public bool Stream { get; set; }
[Parameter] public int MaxFeatures { get; set; } = 100;
[Parameter] public bool SpatialSampling { get; set; } = true;
[Parameter] public bool Simplify { get; set; } = true;
[Parameter] public bool AutoRefresh { get; set; } = true;
[Parameter] public EventCallback<PreviewResult> OnPreviewLoaded { get; set; }
[Parameter] public EventCallback<Dictionary<string, object>> OnExecute { get; set; }
```

## Troubleshooting

### Preview Not Loading

**Problem:** Preview layer doesn't appear on map

**Solutions:**
1. Check map view ID is correct
2. Verify geometries are valid
3. Check browser console for errors
4. Ensure preview module is imported

### Slow Preview Performance

**Problem:** Preview takes too long to load

**Solutions:**
1. Reduce `maxFeatures` parameter
2. Enable geometry simplification
3. Use spatial sampling
4. Decrease timeout for faster failure

### Preview Shows Wrong Results

**Problem:** Preview doesn't match expected results

**Solutions:**
1. Check parameter values
2. Verify input geometries are correct
3. Validate operation type
4. Review preview metadata for warnings

## Future Enhancements

Planned improvements:
- [ ] 3D geometry preview support
- [ ] Advanced styling options
- [ ] Preview result caching
- [ ] Multi-step operation preview
- [ ] Preview comparison mode (before/after)
- [ ] Export preview results
- [ ] Preview history/undo

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0

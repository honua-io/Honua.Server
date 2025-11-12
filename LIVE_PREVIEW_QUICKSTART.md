# Live Analysis Preview - Quick Start Guide

Get started with Live Analysis Previews in 5 minutes!

## 1. Add the Preview Component

```razor
@page "/my-analysis"
@using Honua.MapSDK.Components.Analysis
@using Honua.MapSDK.Components.Analysis.ParameterControls

<HonuaMap @ref="mapRef" MapViewId="myMap" />

<HonuaAnalysisPreview
    MapViewId="myMap"
    ProcessId="buffer"
    Parameters="@GetParameters()"
    OnExecute="HandleExecute">

    <ParameterControls>
        <BufferParameterControl
            @bind-Distance="distance"
            @bind-Unit="unit" />
    </ParameterControls>
</HonuaAnalysisPreview>

@code {
    private HonuaMap? mapRef;
    private double distance = 100;
    private string unit = "meters";
    private List<Geometry> geometries = new();

    private Dictionary<string, object> GetParameters() => new()
    {
        ["geometries"] = geometries,
        ["distance"] = distance,
        ["unit"] = unit
    };

    private async Task HandleExecute(Dictionary<string, object> parameters)
    {
        var result = await ProcessService.ExecuteAsync("buffer", parameters);
        await mapRef.AddLayer("result", result);
    }
}
```

## 2. API Usage (Direct)

### Execute Preview
```bash
curl -X POST "http://localhost:5000/processes/buffer/preview?maxFeatures=100" \
  -H "Content-Type: application/json" \
  -d '{
    "inputs": {
      "geometries": [...],
      "distance": 100,
      "unit": "meters"
    }
  }'
```

### Validate Inputs
```bash
curl -X POST "http://localhost:5000/processes/buffer/validate" \
  -H "Content-Type: application/json" \
  -d '{
    "distance": 100,
    "unit": "meters",
    "geometries": [...]
  }'
```

## 3. JavaScript Usage

```javascript
import { loadPreview, clearPreview } from './preview-layer.js';

// Load preview
const result = await loadPreview(
  'mapViewId',
  '/processes/buffer/preview',
  {
    geometries: [...],
    distance: 100,
    unit: 'meters'
  }
);

// Clear preview
await clearPreview('mapViewId', result.layerId);
```

## 4. Streaming Preview (Large Datasets)

```javascript
import { loadStreamingPreview } from './preview-layer.js';

await loadStreamingPreview(
  'mapViewId',
  '/processes/buffer/preview',
  parameters,
  (progress) => {
    console.log(`Loaded ${progress.featuresLoaded} features`);
  }
);
```

## 5. Common Parameters

### Buffer
- `distance` (number) - Buffer distance
- `unit` (string) - meters, kilometers, feet, miles
- `unionResults` (boolean) - Union overlapping buffers

### Clip
- `targetGeometries` (array) - Features to clip
- `clipGeometry` (geometry) - Clipping boundary
- `preserveExtent` (boolean) - Keep original extent

### Intersection
- `geometries1` (array) - First geometry set
- `geometries2` (array) - Second geometry set

## 6. Preview Options

```csharp
var options = new PreviewExecutionOptions
{
    MaxPreviewFeatures = 100,        // Limit features
    PreviewTimeoutMs = 5000,         // 5 second timeout
    UseSpatialSampling = true,       // Spatial distribution
    SimplifyGeometries = true,       // Simplify for speed
    SimplificationTolerance = 0.001  // Simplification amount
};
```

## 7. Response Metadata

```json
{
  "metadata": {
    "preview": true,
    "totalFeatures": 5000,
    "previewFeatures": 100,
    "spatialSampling": true,
    "simplified": true,
    "executionTimeMs": 234,
    "message": "Preview showing 100 features...",
    "warnings": ["Large dataset sampled"]
  }
}
```

## 8. Troubleshooting

**Preview not showing?**
- Check console for errors
- Verify map view ID is correct
- Ensure geometries are valid

**Preview too slow?**
- Reduce `maxFeatures`
- Enable simplification
- Use streaming for large datasets

**Wrong results?**
- Validate parameters first
- Check preview metadata warnings
- Verify operation type

## Next Steps

- See [LIVE_ANALYSIS_PREVIEW.md](src/Honua.MapSDK/Components/Analysis/LIVE_ANALYSIS_PREVIEW.md) for detailed documentation
- Check [PREVIEW_EXAMPLES.md](src/Honua.MapSDK/Components/Analysis/PREVIEW_EXAMPLES.md) for more examples
- Review [API documentation](http://localhost:5000/swagger) for full API reference

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0

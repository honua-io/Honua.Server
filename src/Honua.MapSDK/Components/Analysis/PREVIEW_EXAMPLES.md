# Live Analysis Preview - Usage Examples

This document provides practical examples for implementing live analysis previews in Honua applications.

## Table of Contents

- [Basic Buffer Preview](#basic-buffer-preview)
- [Advanced Buffer with Custom Parameters](#advanced-buffer-with-custom-parameters)
- [Clip Operation Preview](#clip-operation-preview)
- [Intersection Preview](#intersection-preview)
- [Streaming Large Dataset Preview](#streaming-large-dataset-preview)
- [Custom Styling](#custom-styling)
- [Multiple Operation Comparison](#multiple-operation-comparison)

---

## Basic Buffer Preview

The simplest way to add a buffer preview with parameter adjustment:

```razor
@page "/analysis/buffer"
@using Honua.MapSDK.Components.Analysis
@using Honua.MapSDK.Components.Analysis.ParameterControls

<div class="analysis-container">
    <HonuaMap @ref="mapRef" MapViewId="bufferMap" />

    <HonuaAnalysisPreview
        MapViewId="bufferMap"
        ProcessId="buffer"
        Parameters="@GetBufferParameters()"
        Title="Buffer Analysis"
        AutoRefresh="true"
        OnExecute="ExecuteFullBuffer">

        <ParameterControls>
            <BufferParameterControl
                @bind-Distance="bufferDistance"
                @bind-Unit="bufferUnit"
                @bind-UnionResults="unionResults"
                MinDistance="10"
                MaxDistance="5000" />
        </ParameterControls>
    </HonuaAnalysisPreview>
</div>

@code {
    private HonuaMap? mapRef;
    private double bufferDistance = 100;
    private string bufferUnit = "meters";
    private bool unionResults = false;
    private List<Geometry> selectedGeometries = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load some sample geometries
            selectedGeometries = await LoadFeatures();
            StateHasChanged();
        }
    }

    private Dictionary<string, object> GetBufferParameters() => new()
    {
        ["geometries"] = selectedGeometries,
        ["distance"] = bufferDistance,
        ["unit"] = bufferUnit,
        ["unionResults"] = unionResults
    };

    private async Task ExecuteFullBuffer(Dictionary<string, object> parameters)
    {
        // Execute the full operation
        var result = await ProcessService.ExecuteAsync("buffer", parameters);

        // Add result to map as permanent layer
        await mapRef.AddGeoJsonLayer("buffer-result", result);

        // Show notification
        await JSRuntime.InvokeVoidAsync("showNotification",
            "Buffer complete", $"Created buffer of {bufferDistance} {bufferUnit}");
    }

    private async Task<List<Geometry>> LoadFeatures()
    {
        // Load features from your data source
        return await FeatureService.GetSelectedFeaturesAsync();
    }
}
```

---

## Advanced Buffer with Custom Parameters

More advanced example with validation and custom presets:

```razor
@page "/analysis/advanced-buffer"

<HonuaMap @ref="mapRef" MapViewId="advancedBufferMap" />

<HonuaAnalysisPreview
    MapViewId="advancedBufferMap"
    ProcessId="buffer"
    Parameters="@GetBufferParameters()"
    Title="Advanced Buffer Analysis"
    AutoRefresh="@autoRefresh"
    MaxFeatures="@maxPreviewFeatures"
    SpatialSampling="@useSpatialSampling"
    Simplify="@simplifyGeometries"
    OnPreviewLoaded="HandlePreviewLoaded"
    OnExecute="ExecuteFullBuffer">

    <ParameterControls>
        <BufferParameterControl
            @bind-Distance="bufferDistance"
            @bind-Unit="bufferUnit"
            @bind-UnionResults="unionResults"
            MinDistance="@GetMinDistance()"
            MaxDistance="@GetMaxDistance()"
            CustomPresets="@customPresets"
            ShowQuickPresets="true" />

        <div class="advanced-options">
            <h4>Preview Options</h4>

            <label>
                <input type="checkbox" @bind="autoRefresh" />
                Auto-refresh on parameter change
            </label>

            <label>
                <input type="checkbox" @bind="useSpatialSampling" />
                Use spatial sampling for large datasets
            </label>

            <label>
                <input type="checkbox" @bind="simplifyGeometries" />
                Simplify geometries for faster rendering
            </label>

            <label>
                Max preview features:
                <input type="number" @bind="maxPreviewFeatures" min="10" max="1000" />
            </label>
        </div>

        @if (selectedFeatureCount > 0)
        {
            <div class="feature-info">
                <strong>Selected Features:</strong> @selectedFeatureCount
                @if (selectedFeatureCount > maxPreviewFeatures)
                {
                    <span class="warning">
                        (will be sampled to @maxPreviewFeatures for preview)
                    </span>
                }
            </div>
        }

        @if (previewMetadata != null)
        {
            <div class="preview-stats">
                <h4>Preview Statistics</h4>
                <dl>
                    <dt>Execution Time:</dt>
                    <dd>@previewMetadata.ExecutionTimeMs ms</dd>

                    <dt>Preview Features:</dt>
                    <dd>@previewMetadata.PreviewFeatures of @(previewMetadata.TotalFeatures ?? selectedFeatureCount)</dd>

                    <dt>Spatial Sampling:</dt>
                    <dd>@(previewMetadata.SpatialSampling ? "Yes" : "No")</dd>

                    <dt>Simplified:</dt>
                    <dd>@(previewMetadata.Simplified ? "Yes" : "No")</dd>
                </dl>
            </div>
        }
    </ParameterControls>
</HonuaAnalysisPreview>

@code {
    private HonuaMap? mapRef;
    private double bufferDistance = 100;
    private string bufferUnit = "meters";
    private bool unionResults = false;
    private bool autoRefresh = true;
    private bool useSpatialSampling = true;
    private bool simplifyGeometries = true;
    private int maxPreviewFeatures = 100;
    private int selectedFeatureCount = 0;
    private PreviewMetadata? previewMetadata;

    private List<BufferParameterControl.DistancePreset> customPresets = new()
    {
        new() { Label = "Walking (5 min)", Value = 400, Unit = "meters" },
        new() { Label = "Cycling (5 min)", Value = 1500, Unit = "meters" },
        new() { Label = "Driving (5 min)", Value = 4000, Unit = "meters" },
        new() { Label = "1 mile", Value = 1, Unit = "miles" },
        new() { Label = "5 miles", Value = 5, Unit = "miles" }
    };

    private double GetMinDistance()
    {
        return bufferUnit switch
        {
            "meters" => 1,
            "kilometers" => 0.001,
            "feet" => 3,
            "miles" => 0.0001,
            _ => 1
        };
    }

    private double GetMaxDistance()
    {
        return bufferUnit switch
        {
            "meters" => 10000,
            "kilometers" => 10,
            "feet" => 33000,
            "miles" => 6,
            _ => 1000
        };
    }

    private Dictionary<string, object> GetBufferParameters() => new()
    {
        ["geometries"] = selectedGeometries,
        ["distance"] = bufferDistance,
        ["unit"] = bufferUnit,
        ["unionResults"] = unionResults
    };

    private void HandlePreviewLoaded(PreviewResult result)
    {
        previewMetadata = result.Metadata;
        StateHasChanged();
    }

    private async Task ExecuteFullBuffer(Dictionary<string, object> parameters)
    {
        try
        {
            var result = await ProcessService.ExecuteAsync("buffer", parameters);
            await mapRef.AddGeoJsonLayer("buffer-result", result);
            await ShowSuccessNotification($"Buffer created: {bufferDistance} {bufferUnit}");
        }
        catch (Exception ex)
        {
            await ShowErrorNotification($"Buffer failed: {ex.Message}");
        }
    }
}
```

---

## Clip Operation Preview

Interactive clip operation with geometry selection:

```razor
@page "/analysis/clip"

<HonuaMap @ref="mapRef" MapViewId="clipMap" />

<HonuaAnalysisPreview
    MapViewId="clipMap"
    ProcessId="clip"
    Parameters="@GetClipParameters()"
    Title="Clip Features"
    AutoRefresh="false"
    OnExecute="ExecuteFullClip">

    <ParameterControls>
        <ClipParameterControl
            @bind-SelectedGeometryId="clipGeometryId"
            AvailableGeometries="@availableClipGeometries"
            @bind-PreserveOriginalExtent="preserveExtent"
            @bind-MaintainTopology="maintainTopology"
            OnDrawClipGeometry="StartDrawingClipGeometry" />

        <div class="target-selection">
            <h4>Target Features</h4>
            <p>
                @targetFeatures.Count features selected
                <button @onclick="SelectTargetFeatures">Select Features</button>
            </p>
        </div>
    </ParameterControls>
</HonuaAnalysisPreview>

@code {
    private HonuaMap? mapRef;
    private string? clipGeometryId;
    private List<Geometry> targetFeatures = new();
    private bool preserveExtent = false;
    private bool maintainTopology = true;

    private List<ClipParameterControl.GeometryInfo> availableClipGeometries = new()
    {
        new() { Id = "boundary-1", Name = "Administrative Boundary", Type = "Polygon", FeatureCount = 1 },
        new() { Id = "study-area", Name = "Study Area", Type = "Polygon", FeatureCount = 1 },
        new() { Id = "watershed", Name = "Watershed Boundary", Type = "MultiPolygon", FeatureCount = 3 }
    };

    private Dictionary<string, object> GetClipParameters()
    {
        if (string.IsNullOrEmpty(clipGeometryId) || !targetFeatures.Any())
        {
            return new Dictionary<string, object>();
        }

        var clipGeometry = GetClipGeometry(clipGeometryId);

        return new Dictionary<string, object>
        {
            ["targetGeometries"] = targetFeatures,
            ["clipGeometry"] = clipGeometry,
            ["preserveExtent"] = preserveExtent,
            ["maintainTopology"] = maintainTopology
        };
    }

    private async Task StartDrawingClipGeometry()
    {
        // Start drawing mode on map
        var geometry = await mapRef.DrawPolygon();

        if (geometry != null)
        {
            // Add to available geometries
            var newId = $"drawn-{DateTime.Now.Ticks}";
            availableClipGeometries.Add(new()
            {
                Id = newId,
                Name = "Drawn Clip Area",
                Type = "Polygon",
                FeatureCount = 1
            });

            clipGeometryId = newId;
            StateHasChanged();
        }
    }

    private async Task SelectTargetFeatures()
    {
        // Allow user to select features on map
        targetFeatures = await mapRef.SelectFeatures();
        StateHasChanged();
    }

    private Geometry GetClipGeometry(string id)
    {
        // Retrieve actual geometry by ID
        return GeometryService.GetGeometry(id);
    }

    private async Task ExecuteFullClip(Dictionary<string, object> parameters)
    {
        var result = await ProcessService.ExecuteAsync("clip", parameters);
        await mapRef.AddGeoJsonLayer("clip-result", result);
    }
}
```

---

## Intersection Preview

Intersection of two feature sets:

```razor
@page "/analysis/intersection"

<HonuaMap @ref="mapRef" MapViewId="intersectMap" />

<HonuaAnalysisPreview
    MapViewId="intersectMap"
    ProcessId="intersect"
    Parameters="@GetIntersectParameters()"
    Title="Intersection Analysis"
    AutoRefresh="true"
    OnExecute="ExecuteFullIntersection">

    <ParameterControls>
        <div class="geometry-set">
            <h4>First Geometry Set</h4>
            <p>@geometries1.Count features selected</p>
            <button @onclick="SelectGeometries1">Select Features</button>
        </div>

        <div class="geometry-set">
            <h4>Second Geometry Set</h4>
            <p>@geometries2.Count features selected</p>
            <button @onclick="SelectGeometries2">Select Features</button>
        </div>

        <div class="operation-info">
            <p>
                Intersection will compute overlapping areas between the two geometry sets.
                Preview shows up to 50 features from each set.
            </p>
        </div>
    </ParameterControls>
</HonuaAnalysisPreview>

@code {
    private HonuaMap? mapRef;
    private List<Geometry> geometries1 = new();
    private List<Geometry> geometries2 = new();

    private Dictionary<string, object> GetIntersectParameters()
    {
        if (!geometries1.Any() || !geometries2.Any())
        {
            return new Dictionary<string, object>();
        }

        return new Dictionary<string, object>
        {
            ["geometries1"] = geometries1,
            ["geometries2"] = geometries2
        };
    }

    private async Task SelectGeometries1()
    {
        geometries1 = await mapRef.SelectFeatures("Select first geometry set");
        StateHasChanged();
    }

    private async Task SelectGeometries2()
    {
        geometries2 = await mapRef.SelectFeatures("Select second geometry set");
        StateHasChanged();
    }

    private async Task ExecuteFullIntersection(Dictionary<string, object> parameters)
    {
        var result = await ProcessService.ExecuteAsync("intersect", parameters);
        await mapRef.AddGeoJsonLayer("intersection-result", result);

        var resultCount = ((List<Geometry>)result["geometries"]).Count;
        await ShowNotification($"Intersection complete: {resultCount} features");
    }
}
```

---

## Streaming Large Dataset Preview

For very large datasets, use streaming preview:

```razor
@page "/analysis/streaming-buffer"
@inject IJSRuntime JS

<HonuaMap @ref="mapRef" MapViewId="streamingMap" />

<div class="analysis-panel">
    <h3>Large Dataset Buffer</h3>

    <BufferParameterControl
        @bind-Distance="bufferDistance"
        @bind-Unit="bufferUnit"
        @bind-UnionResults="unionResults" />

    <div class="progress-container" style="display: @(isStreaming ? "block" : "none")">
        <div class="progress-bar">
            <div class="progress-fill" style="width: @progressPercentage%"></div>
        </div>
        <p>Loaded @loadedFeatures of @totalFeatures features</p>
    </div>

    <div class="actions">
        <button @onclick="StartStreamingPreview" disabled="@isStreaming">
            Load Preview
        </button>
        <button @onclick="ExecuteFull" disabled="@isStreaming">
            Execute Full
        </button>
    </div>
</div>

@code {
    private HonuaMap? mapRef;
    private double bufferDistance = 100;
    private string bufferUnit = "meters";
    private bool unionResults = false;
    private bool isStreaming = false;
    private int loadedFeatures = 0;
    private int totalFeatures = 0;

    private double progressPercentage => totalFeatures > 0
        ? (double)loadedFeatures / totalFeatures * 100
        : 0;

    private async Task StartStreamingPreview()
    {
        isStreaming = true;
        loadedFeatures = 0;
        StateHasChanged();

        try
        {
            var previewModule = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./Components/Analysis/preview-layer.js");

            var url = $"/processes/buffer/preview?stream=true&maxFeatures=500";
            var parameters = new Dictionary<string, object>
            {
                ["geometries"] = await GetLargeDataset(),
                ["distance"] = bufferDistance,
                ["unit"] = bufferUnit,
                ["unionResults"] = unionResults
            };

            await previewModule.InvokeVoidAsync(
                "loadStreamingPreview",
                "streamingMap",
                url,
                parameters,
                DotNetObjectReference.Create(this));
        }
        finally
        {
            isStreaming = false;
            StateHasChanged();
        }
    }

    [JSInvokable]
    public void OnStreamingProgress(int featuresLoaded)
    {
        loadedFeatures = featuresLoaded;
        StateHasChanged();
    }

    private async Task<List<Geometry>> GetLargeDataset()
    {
        // Load large dataset (e.g., 10,000+ features)
        var features = await FeatureService.GetAllFeaturesAsync();
        totalFeatures = features.Count;
        return features;
    }

    private async Task ExecuteFull()
    {
        // Execute full operation with progress tracking
        var progressCallback = new Progress<int>(progress =>
        {
            loadedFeatures = progress;
            StateHasChanged();
        });

        var result = await ProcessService.ExecuteWithProgressAsync(
            "buffer",
            GetBufferParameters(),
            progressCallback);

        await mapRef.AddGeoJsonLayer("buffer-result", result);
    }
}
```

---

## Custom Styling

Customize preview layer appearance:

```razor
@page "/analysis/styled-preview"
@inject IJSRuntime JS

<HonuaMap @ref="mapRef" MapViewId="styledMap" />

<HonuaAnalysisPreview
    MapViewId="styledMap"
    ProcessId="buffer"
    Parameters="@GetBufferParameters()"
    OnPreviewLoaded="ApplyCustomStyling">

    <ParameterControls>
        <BufferParameterControl
            @bind-Distance="bufferDistance"
            @bind-Unit="bufferUnit" />

        <div class="style-controls">
            <h4>Preview Style</h4>

            <label>
                Fill Color:
                <input type="color" @bind="fillColor" @bind:event="oninput" />
            </label>

            <label>
                Fill Opacity:
                <input type="range" min="0" max="1" step="0.1"
                       @bind="fillOpacity" @bind:event="oninput" />
                (@fillOpacity)
            </label>

            <label>
                Stroke Color:
                <input type="color" @bind="strokeColor" @bind:event="oninput" />
            </label>

            <label>
                Stroke Width:
                <input type="range" min="1" max="10" step="1"
                       @bind="strokeWidth" @bind:event="oninput" />
                (@strokeWidth px)
            </label>
        </div>
    </ParameterControls>
</HonuaAnalysisPreview>

@code {
    private HonuaMap? mapRef;
    private double bufferDistance = 100;
    private string bufferUnit = "meters";

    private string fillColor = "#3B82F6";
    private double fillOpacity = 0.3;
    private string strokeColor = "#2563EB";
    private int strokeWidth = 2;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await ApplyCustomStyling(null);
        }
    }

    private async Task ApplyCustomStyling(PreviewResult? result)
    {
        var previewModule = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./Components/Analysis/preview-layer.js");

        await previewModule.InvokeVoidAsync("setPreviewStyle", "buffer", new
        {
            fillColor,
            fillOpacity,
            strokeColor,
            strokeWidth,
            strokeDashArray = new[] { 5, 5 }
        });
    }

    private Dictionary<string, object> GetBufferParameters() => new()
    {
        ["geometries"] = selectedGeometries,
        ["distance"] = bufferDistance,
        ["unit"] = bufferUnit
    };
}
```

---

## Multiple Operation Comparison

Compare results of different operations side-by-side:

```razor
@page "/analysis/compare"

<div class="comparison-layout">
    <div class="preview-panel">
        <HonuaMap MapViewId="map1" />
        <HonuaAnalysisPreview
            MapViewId="map1"
            ProcessId="buffer"
            Parameters="@bufferParams"
            Title="Buffer (100m)">
            <ParameterControls>
                <BufferParameterControl
                    Distance="100"
                    Unit="meters" />
            </ParameterControls>
        </HonuaAnalysisPreview>
    </div>

    <div class="preview-panel">
        <HonuaMap MapViewId="map2" />
        <HonuaAnalysisPreview
            MapViewId="map2"
            ProcessId="buffer"
            Parameters="@bufferParams2"
            Title="Buffer (500m)">
            <ParameterControls>
                <BufferParameterControl
                    Distance="500"
                    Unit="meters" />
            </ParameterControls>
        </HonuaAnalysisPreview>
    </div>
</div>

<style>
    .comparison-layout {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 20px;
        height: 100vh;
    }

    .preview-panel {
        position: relative;
        border: 1px solid #e5e7eb;
        border-radius: 8px;
        overflow: hidden;
    }
</style>

@code {
    private Dictionary<string, object> bufferParams => new()
    {
        ["geometries"] = selectedGeometries,
        ["distance"] = 100,
        ["unit"] = "meters"
    };

    private Dictionary<string, object> bufferParams2 => new()
    {
        ["geometries"] = selectedGeometries,
        ["distance"] = 500,
        ["unit"] = "meters"
    };
}
```

---

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0

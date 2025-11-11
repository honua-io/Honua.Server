# OGC WMS/WFS Examples

Comprehensive examples demonstrating various use cases for the OGC components.

## Example 1: Basic WMS Layer

Display a simple WMS layer from a public service:

```razor
@page "/example-wms-basic"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC

<h3>Basic WMS Layer Example</h3>

<HonuaMapLibre SyncWith="example-map"
               Style="height: 600px; width: 100%;">

    <HonuaWmsLayer SyncWith="example-map"
                   ServiceUrl="https://ows.terrestris.de/osm/service"
                   Layers='new List<string> { "OSM-WMS" }'
                   Version="1.3.0"
                   LayerTitle="OpenStreetMap WMS"
                   ShowControls="true" />
</HonuaMapLibre>
```

## Example 2: WMS with Multiple Layers

Display multiple WMS layers with individual control:

```razor
@page "/example-wms-multi"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC

<HonuaMapLibre SyncWith="example-map"
               Style="height: 600px;">

    <!-- Base layer -->
    <HonuaWmsLayer SyncWith="example-map"
                   ServiceUrl="https://example.com/wms"
                   Layers='new List<string> { "countries" }'
                   LayerTitle="Countries"
                   Opacity="0.7" />

    <!-- Overlay layer -->
    <HonuaWmsLayer SyncWith="example-map"
                   ServiceUrl="https://example.com/wms"
                   Layers='new List<string> { "cities" }'
                   LayerTitle="Cities"
                   Transparent="true"
                   Opacity="0.9" />

    <!-- Legend -->
    <HonuaLegend SyncWith="example-map"
                 Position="top-right"
                 ShowOpacity="true" />
</HonuaMapLibre>
```

## Example 3: WFS with Filtering

Query WFS features with CQL filter:

```razor
@page "/example-wfs-filter"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC
@using Honua.MapSDK.Models.OGC

<div class="container-fluid">
    <div class="row">
        <div class="col-md-3">
            <h4>Filter Options</h4>
            <MudTextField @bind-Value="_populationFilter"
                         Label="Min Population"
                         Type="InputType.Number" />
            <MudButton OnClick="ApplyFilter"
                      Variant="Variant.Filled"
                      Color="Color.Primary"
                      Class="mt-2">
                Apply Filter
            </MudButton>
        </div>

        <div class="col-md-9">
            <HonuaMapLibre @ref="_map"
                           SyncWith="example-map"
                           Style="height: 600px;">

                <HonuaWfsLayer @ref="_wfsLayer"
                               SyncWith="example-map"
                               ServiceUrl="https://demo.geo-solutions.it/geoserver/wfs"
                               FeatureType="topp:states"
                               CqlFilter="@_cqlFilter"
                               MaxFeatures="50"
                               ShowStatistics="true"
                               OnFeaturesLoaded="@HandleFeaturesLoaded" />
            </HonuaMapLibre>

            @if (_featureCount > 0)
            {
                <MudAlert Severity="Severity.Info" Class="mt-2">
                    Loaded @_featureCount features
                </MudAlert>
            }
        </div>
    </div>
</div>

@code {
    private HonuaMapLibre? _map;
    private HonuaWfsLayer? _wfsLayer;
    private string _populationFilter = "2000000";
    private string _cqlFilter = "";
    private int _featureCount = 0;

    private async Task ApplyFilter()
    {
        if (int.TryParse(_populationFilter, out var minPop))
        {
            _cqlFilter = $"PERSONS > {minPop}";
            if (_wfsLayer != null)
            {
                await _wfsLayer.RefreshAsync();
            }
        }
    }

    private void HandleFeaturesLoaded(WfsFeatureCollection features)
    {
        _featureCount = features.NumberReturned;
        StateHasChanged();
    }
}
```

## Example 4: Time-Enabled WMS

Display temporal WMS data with time controls:

```razor
@page "/example-wms-time"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC

<div class="container-fluid">
    <div class="row">
        <div class="col-12">
            <MudPaper Class="pa-4 mb-4">
                <h4>Temporal Data Viewer</h4>
                <MudSlider @bind-Value="_timeIndex"
                          Min="0"
                          Max="@(_timeSteps.Count - 1)"
                          Step="1"
                          ValueChanged="@OnTimeChanged"
                          Class="mt-2" />
                <MudText Typo="Typo.body2">
                    Current Time: @GetCurrentTime()
                </MudText>
            </MudPaper>
        </div>

        <div class="col-12">
            <HonuaMapLibre SyncWith="example-map"
                           Style="height: 600px;">

                <HonuaWmsLayer @ref="_wmsLayer"
                               SyncWith="example-map"
                               ServiceUrl="https://example.com/wms"
                               Layers='new List<string> { "temperature" }'
                               SupportsTime="true"
                               TimeDimension="@_currentTime"
                               LayerTitle="Temperature Data" />
            </HonuaMapLibre>
        </div>
    </div>
</div>

@code {
    private HonuaWmsLayer? _wmsLayer;
    private int _timeIndex = 0;
    private string _currentTime = "";
    private List<DateTime> _timeSteps = new();

    protected override void OnInitialized()
    {
        // Generate time steps (e.g., hourly for 24 hours)
        var baseTime = DateTime.UtcNow.Date;
        for (int i = 0; i < 24; i++)
        {
            _timeSteps.Add(baseTime.AddHours(i));
        }

        _currentTime = _timeSteps[0].ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    private async Task OnTimeChanged(double value)
    {
        _timeIndex = (int)value;
        _currentTime = _timeSteps[_timeIndex].ToString("yyyy-MM-ddTHH:mm:ssZ");

        if (_wmsLayer != null)
        {
            StateHasChanged();
        }
    }

    private string GetCurrentTime()
    {
        return _timeSteps[_timeIndex].ToString("yyyy-MM-dd HH:mm UTC");
    }
}
```

## Example 5: Interactive Service Browser

Complete application with OGC service browser:

```razor
@page "/example-browser"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC
@using Honua.MapSDK.Models.OGC

<div class="container-fluid vh-100">
    <div class="row h-100">
        <!-- Service Browser Panel -->
        <div class="col-md-4 h-100 overflow-auto border-end">
            <div class="p-3">
                <h3>OGC Services</h3>

                <MudTabs Elevation="2" Rounded="true" ApplyEffectsToContainer="true">
                    <MudTabPanel Text="Browse Services">
                        <HonuaOgcServiceBrowser @ref="_browser"
                                               TargetMapId="example-map"
                                               OnServiceConnected="@HandleServiceConnected"
                                               OnLayerAdded="@HandleLayerAdded" />
                    </MudTabPanel>

                    <MudTabPanel Text="Active Layers">
                        <MudList>
                            @foreach (var layer in _activeLayers)
                            {
                                <MudListItem>
                                    <div class="d-flex justify-content-between align-items-center">
                                        <MudText>@layer</MudText>
                                        <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                                      Size="Size.Small"
                                                      OnClick="@(() => RemoveLayer(layer))" />
                                    </div>
                                </MudListItem>
                            }
                        </MudList>

                        @if (_activeLayers.Count == 0)
                        {
                            <MudText Typo="Typo.body2"
                                    Color="Color.Secondary"
                                    Class="pa-4 text-center">
                                No active layers. Add layers from the Browse tab.
                            </MudText>
                        }
                    </MudTabPanel>

                    <MudTabPanel Text="Saved Services">
                        <div class="pa-4">
                            <MudText Typo="Typo.subtitle2" Class="mb-2">Quick Connect:</MudText>

                            @foreach (var service in _savedServices)
                            {
                                <MudButton OnClick="@(() => ConnectToService(service.Url))"
                                          Variant="Variant.Outlined"
                                          Color="Color.Primary"
                                          FullWidth="true"
                                          Class="mb-2">
                                    @service.Name
                                </MudButton>
                            }
                        </div>
                    </MudTabPanel>
                </MudTabs>

                @if (_serviceInfo != null)
                {
                    <MudPaper Elevation="2" Class="pa-4 mt-4">
                        <MudText Typo="Typo.subtitle1">Service Information</MudText>
                        <MudDivider Class="my-2" />
                        <MudText Typo="Typo.body2">
                            <strong>Type:</strong> @_serviceInfo.ServiceType
                        </MudText>
                        <MudText Typo="Typo.body2">
                            <strong>Version:</strong> @_serviceInfo.Version
                        </MudText>
                        <MudText Typo="Typo.body2">
                            <strong>Title:</strong> @_serviceInfo.Title
                        </MudText>
                    </MudPaper>
                }
            </div>
        </div>

        <!-- Map Panel -->
        <div class="col-md-8 h-100">
            <HonuaMapLibre @ref="_map"
                           SyncWith="example-map"
                           Style="height: 100%;">

                <HonuaLegend SyncWith="example-map"
                            Position="top-right"
                            ShowOpacity="true"
                            ShowSymbols="true" />

                <HonuaPopup SyncWith="example-map"
                           TriggerMode="PopupTrigger.Click"
                           ShowActions="true" />
            </HonuaMapLibre>
        </div>
    </div>
</div>

@code {
    private HonuaMapLibre? _map;
    private HonuaOgcServiceBrowser? _browser;
    private OgcServiceInfo? _serviceInfo;
    private List<string> _activeLayers = new();

    private List<SavedService> _savedServices = new()
    {
        new() { Name = "Terrestris OSM WMS", Url = "https://ows.terrestris.de/osm/service" },
        new() { Name = "GeoServer Demo WFS", Url = "https://demo.geo-solutions.it/geoserver/wfs" }
    };

    private void HandleServiceConnected(OgcServiceInfo info)
    {
        _serviceInfo = info;
        StateHasChanged();
    }

    private void HandleLayerAdded(string layerId)
    {
        _activeLayers.Add(layerId);
        StateHasChanged();
    }

    private void RemoveLayer(string layerId)
    {
        _activeLayers.Remove(layerId);
        // TODO: Remove layer from map
    }

    private async Task ConnectToService(string url)
    {
        // TODO: Trigger browser to connect to service
    }

    private class SavedService
    {
        public required string Name { get; set; }
        public required string Url { get; set; }
    }
}
```

## Example 6: WFS with Spatial Query

Query features within a drawn bounding box:

```razor
@page "/example-wfs-spatial"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC
@using Honua.MapSDK.Components.Draw

<div class="container-fluid">
    <div class="row">
        <div class="col-12">
            <MudPaper Class="pa-4 mb-4">
                <MudButton OnClick="StartDrawing"
                          Variant="Variant.Filled"
                          Color="Color.Primary"
                          StartIcon="@Icons.Material.Filled.CropSquare">
                    Draw Search Area
                </MudButton>

                <MudButton OnClick="ClearDrawing"
                          Variant="Variant.Outlined"
                          Color="Color.Default"
                          Class="ml-2">
                    Clear
                </MudButton>

                @if (_searchArea != null)
                {
                    <MudChip Color="Color.Info" Class="ml-2">
                        Search area defined
                    </MudChip>
                }
            </MudPaper>
        </div>

        <div class="col-12">
            <HonuaMapLibre @ref="_map"
                           SyncWith="example-map"
                           Style="height: 600px;">

                <HonuaDraw @ref="_draw"
                          SyncWith="example-map"
                          OnDrawingCompleted="@HandleDrawingCompleted" />

                <HonuaWfsLayer @ref="_wfsLayer"
                               SyncWith="example-map"
                               ServiceUrl="https://demo.geo-solutions.it/geoserver/wfs"
                               FeatureType="topp:states"
                               AutoLoad="false" />
            </HonuaMapLibre>
        </div>
    </div>
</div>

@code {
    private HonuaMapLibre? _map;
    private HonuaDraw? _draw;
    private HonuaWfsLayer? _wfsLayer;
    private double[]? _searchArea;

    private async Task StartDrawing()
    {
        if (_draw != null)
        {
            await _draw.StartDrawingAsync("rectangle");
        }
    }

    private async Task HandleDrawingCompleted(DrawingCompletedMessage message)
    {
        // Extract bounding box from drawn geometry
        // This is simplified - actual implementation would parse the geometry
        _searchArea = new double[] { -180, -90, 180, 90 };

        // Query WFS with bounding box
        if (_wfsLayer != null)
        {
            await _wfsLayer.RefreshAsync();
        }
    }

    private async Task ClearDrawing()
    {
        _searchArea = null;
        if (_draw != null)
        {
            // Clear drawing
        }
    }
}
```

## Example 7: Custom WFS Styling

Apply custom styles to WFS features based on attributes:

```razor
@page "/example-wfs-styling"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.OGC

<HonuaMapLibre SyncWith="example-map"
               Style="height: 600px;">

    <HonuaWfsLayer SyncWith="example-map"
                   ServiceUrl="https://demo.geo-solutions.it/geoserver/wfs"
                   FeatureType="topp:states"
                   StyleConfig="@_populationStyle"
                   MaxFeatures="50" />
</HonuaMapLibre>

@code {
    private object _populationStyle = new
    {
        polygon = new
        {
            fillColor = new[]
            {
                "case",
                new[] { ">", new[] { "get", "PERSONS" }, 10000000 },
                "#d32f2f", // High population - red
                new[] { ">", new[] { "get", "PERSONS" }, 5000000 },
                "#f57c00", // Medium population - orange
                "#4caf50"  // Low population - green
            },
            fillOpacity = 0.6,
            strokeColor = "#000000",
            strokeWidth = 1
        }
    };
}
```

These examples demonstrate the power and flexibility of the OGC components in Honua.MapSDK, suitable for building enterprise GIS applications with comprehensive OGC service integration.

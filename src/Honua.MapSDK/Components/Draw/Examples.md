# HonuaDraw Examples

Comprehensive examples demonstrating the HonuaDraw component's capabilities.

## Table of Contents

1. [Basic Examples](#basic-examples)
2. [Styling Examples](#styling-examples)
3. [Measurement Examples](#measurement-examples)
4. [Event Handling Examples](#event-handling-examples)
5. [Integration Examples](#integration-examples)
6. [Advanced Examples](#advanced-examples)

---

## Basic Examples

### 1. Simple Drawing Toolbar

The most basic setup with default configuration.

```razor
@page "/draw/basic"

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Basic Drawing</MudText>

    <div style="position: relative; height: 600px;">
        <HonuaMap
            Id="basicMap"
            Center="new[] { -122.4194, 37.7749 }"
            Zoom="12"
            Style="streets" />

        <HonuaDraw
            SyncWith="basicMap"
            Position="top-right" />
    </div>
</MudContainer>
```

### 2. Embedded Drawing Controls

Drawing controls embedded in your layout instead of floating.

```razor
@page "/draw/embedded"

<MudContainer>
    <MudGrid>
        <MudItem xs="12" md="3">
            <MudPaper Class="pa-4">
                <HonuaDraw
                    SyncWith="embeddedMap"
                    ShowToolbar="true"
                    ShowFeatureList="true"
                    ShowMeasurements="true" />
            </MudPaper>
        </MudItem>

        <MudItem xs="12" md="9">
            <HonuaMap
                Id="embeddedMap"
                Center="new[] { -122.4194, 37.7749 }"
                Zoom="12"
                Style="streets"
                Height="600px" />
        </MudItem>
    </MudGrid>
</MudContainer>
```

### 3. Drawing Without Toolbar

Programmatically controlled drawing without visible toolbar.

```razor
@page "/draw/programmatic"
@inject ComponentBus Bus

<MudContainer>
    <MudButtonGroup>
        <MudButton OnClick="@(() => StartDrawing("point"))">Draw Point</MudButton>
        <MudButton OnClick="@(() => StartDrawing("line_string"))">Draw Line</MudButton>
        <MudButton OnClick="@(() => StartDrawing("polygon"))">Draw Polygon</MudButton>
        <MudButton OnClick="@StopDrawing">Stop Drawing</MudButton>
    </MudButtonGroup>

    <HonuaMap Id="progMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" />

    <HonuaDraw
        SyncWith="progMap"
        ShowToolbar="false"
        ShowMeasurements="true" />
</MudContainer>

@code {
    private async Task StartDrawing(string mode)
    {
        await Bus.PublishAsync(new StartDrawingRequestMessage
        {
            MapId = "progMap",
            Mode = mode
        });
    }

    private async Task StopDrawing()
    {
        await Bus.PublishAsync(new StopDrawingRequestMessage
        {
            MapId = "progMap"
        });
    }
}
```

---

## Styling Examples

### 4. Custom Color Scheme

Drawing with custom stroke and fill colors.

```razor
@page "/draw/custom-colors"

<HonuaMap Id="colorMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" />

<HonuaDraw
    SyncWith="colorMap"
    DefaultStrokeColor="#EF4444"
    DefaultFillColor="#F87171"
    DefaultStrokeWidth="3"
    DefaultFillOpacity="0.3"
    Position="top-right" />
```

### 5. Multiple Drawing Styles

Different drawing components with different styles.

```razor
@page "/draw/multiple-styles"

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Multiple Drawing Layers</MudText>

    <MudStack Row="true" Class="mb-4">
        <MudChip Color="Color.Error">Red Features</MudChip>
        <MudChip Color="Color.Success">Green Features</MudChip>
        <MudChip Color="Color.Primary">Blue Features</MudChip>
    </MudStack>

    <HonuaMap Id="multiStyleMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="600px" />

    <!-- Red drawing layer -->
    <HonuaDraw
        Id="redDraw"
        SyncWith="multiStyleMap"
        DefaultStrokeColor="#EF4444"
        DefaultFillColor="#EF4444"
        Position="top-right" />

    <!-- Green drawing layer -->
    <HonuaDraw
        Id="greenDraw"
        SyncWith="multiStyleMap"
        DefaultStrokeColor="#10B981"
        DefaultFillColor="#10B981"
        Position="top-left"
        ShowToolbar="false" />

    <!-- Blue drawing layer -->
    <HonuaDraw
        Id="blueDraw"
        SyncWith="multiStyleMap"
        DefaultStrokeColor="#3B82F6"
        DefaultFillColor="#3B82F6"
        Position="bottom-right"
        ShowToolbar="false" />
</MudContainer>
```

### 6. Themed Drawing Component

Drawing component that matches your application theme.

```razor
@page "/draw/themed"

<style>
    .themed-draw {
        --draw-primary-color: #9333ea;
        --draw-toolbar-bg: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    }

    .themed-draw .draw-toolbar {
        background: var(--draw-toolbar-bg);
        color: white;
    }

    .themed-draw .tool-button {
        color: white;
    }
</style>

<HonuaMap Id="themedMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" />

<HonuaDraw
    SyncWith="themedMap"
    CssClass="themed-draw"
    DefaultStrokeColor="#9333ea"
    DefaultFillColor="#9333ea"
    Position="top-right" />
```

---

## Measurement Examples

### 7. Distance Measurement Tool

Specialized tool for measuring distances.

```razor
@page "/draw/distance"

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Distance Measurement Tool</MudText>

    <MudAlert Severity="Severity.Info" Class="mb-4">
        Click on the map to start measuring. Double-click to finish.
    </MudAlert>

    @if (_totalDistance > 0)
    {
        <MudCard Class="mb-4">
            <MudCardContent>
                <MudText Typo="Typo.h6">Total Distance Measured</MudText>
                <MudText Typo="Typo.h4">@FormatDistance(_totalDistance)</MudText>
            </MudCardContent>
        </MudCard>
    }

    <HonuaMap Id="distanceMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="500px" />

    <HonuaDraw
        SyncWith="distanceMap"
        OnFeatureDrawn="HandleDistanceMeasured"
        MeasurementUnit="MeasurementUnit.Imperial"
        Position="top-right" />
</MudContainer>

@code {
    private double _totalDistance = 0;

    private async Task HandleDistanceMeasured(DrawingFeature feature)
    {
        if (feature.GeometryType == "LineString" && feature.Measurements?.Distance.HasValue == true)
        {
            _totalDistance += feature.Measurements.Distance.Value;
            StateHasChanged();
        }
    }

    private string FormatDistance(double meters)
    {
        var miles = meters / 1609.34;
        return $"{miles:F2} miles ({meters:F0} meters)";
    }
}
```

### 8. Area Calculation Tool

Calculate areas of polygons.

```razor
@page "/draw/area"

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Area Calculator</MudText>

    @if (_polygons.Count > 0)
    {
        <MudTable Items="_polygons" Dense="true" Class="mb-4">
            <HeaderContent>
                <MudTh>Polygon</MudTh>
                <MudTh>Area (sq meters)</MudTh>
                <MudTh>Area (acres)</MudTh>
            </HeaderContent>
            <RowTemplate>
                <MudTd>@context.Name</MudTd>
                <MudTd>@context.AreaSqMeters.ToString("N0")</MudTd>
                <MudTd>@context.AreaAcres.ToString("F2")</MudTd>
            </RowTemplate>
        </MudTable>

        <MudText Typo="Typo.h6">
            Total Area: @_polygons.Sum(p => p.AreaSqMeters).ToString("N0") sq meters
        </MudText>
    }

    <HonuaMap Id="areaMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="500px" />

    <HonuaDraw
        SyncWith="areaMap"
        OnFeatureDrawn="HandlePolygonDrawn"
        OnFeatureDeleted="HandlePolygonDeleted"
        ShowMeasurements="true"
        Position="top-right" />
</MudContainer>

@code {
    private List<PolygonInfo> _polygons = new();

    private class PolygonInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double AreaSqMeters { get; set; }
        public double AreaAcres => AreaSqMeters / 4046.86;
    }

    private async Task HandlePolygonDrawn(DrawingFeature feature)
    {
        if (feature.GeometryType == "Polygon" && feature.Measurements?.Area.HasValue == true)
        {
            _polygons.Add(new PolygonInfo
            {
                Id = feature.Id,
                Name = $"Polygon {_polygons.Count + 1}",
                AreaSqMeters = feature.Measurements.Area.Value
            });
            StateHasChanged();
        }
    }

    private async Task HandlePolygonDeleted(string featureId)
    {
        _polygons.RemoveAll(p => p.Id == featureId);
        StateHasChanged();
    }
}
```

### 9. Multi-Unit Measurement Display

Display measurements in multiple units simultaneously.

```razor
@page "/draw/multi-unit"

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Multi-Unit Measurements</MudText>

    @if (_currentMeasurement != null)
    {
        <MudCard Class="mb-4">
            <MudCardContent>
                <MudText Typo="Typo.h6" Class="mb-3">Current Measurement</MudText>

                @if (_currentMeasurement.Distance.HasValue)
                {
                    <MudSimpleTable Dense="true">
                        <tbody>
                            <tr>
                                <td>Meters</td>
                                <td>@_currentMeasurement.Distance.Value.ToString("F2") m</td>
                            </tr>
                            <tr>
                                <td>Kilometers</td>
                                <td>@((_currentMeasurement.Distance.Value / 1000).ToString("F2")) km</td>
                            </tr>
                            <tr>
                                <td>Feet</td>
                                <td>@((_currentMeasurement.Distance.Value * 3.28084).ToString("F2")) ft</td>
                            </tr>
                            <tr>
                                <td>Miles</td>
                                <td>@((_currentMeasurement.Distance.Value / 1609.34).ToString("F2")) mi</td>
                            </tr>
                        </tbody>
                    </MudSimpleTable>
                }

                @if (_currentMeasurement.Area.HasValue)
                {
                    <MudSimpleTable Dense="true">
                        <tbody>
                            <tr>
                                <td>Square Meters</td>
                                <td>@_currentMeasurement.Area.Value.ToString("N0") m²</td>
                            </tr>
                            <tr>
                                <td>Hectares</td>
                                <td>@((_currentMeasurement.Area.Value / 10000).ToString("F2")) ha</td>
                            </tr>
                            <tr>
                                <td>Square Feet</td>
                                <td>@((_currentMeasurement.Area.Value * 10.7639).ToString("N0")) ft²</td>
                            </tr>
                            <tr>
                                <td>Acres</td>
                                <td>@((_currentMeasurement.Area.Value / 4046.86).ToString("F2")) acres</td>
                            </tr>
                        </tbody>
                    </MudSimpleTable>
                }
            </MudCardContent>
        </MudCard>
    }

    <HonuaMap Id="multiUnitMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="500px" />

    <HonuaDraw
        SyncWith="multiUnitMap"
        OnFeatureMeasured="HandleMeasurement"
        Position="top-right" />
</MudContainer>

@code {
    private FeatureMeasurements? _currentMeasurement;

    private async Task HandleMeasurement(FeatureMeasurements measurements)
    {
        _currentMeasurement = measurements;
        StateHasChanged();
    }
}
```

---

## Event Handling Examples

### 10. Feature Change Tracking

Track all changes to drawn features.

```razor
@page "/draw/tracking"

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Feature Change Tracking</MudText>

    <MudTimeline TimelinePosition="TimelinePosition.Start" Class="mb-4">
        @foreach (var log in _changeLogs.OrderByDescending(l => l.Timestamp))
        {
            <MudTimelineItem Color="@GetLogColor(log.Type)">
                <ItemOpposite>
                    <MudText Typo="Typo.caption">@log.Timestamp.ToString("HH:mm:ss")</MudText>
                </ItemOpposite>
                <ItemContent>
                    <MudText Typo="Typo.body2">@log.Message</MudText>
                </ItemContent>
            </MudTimelineItem>
        }
    </MudTimeline>

    <HonuaMap Id="trackingMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="500px" />

    <HonuaDraw
        SyncWith="trackingMap"
        OnFeatureDrawn="HandleFeatureDrawn"
        OnFeatureEdited="HandleFeatureEdited"
        OnFeatureDeleted="HandleFeatureDeleted"
        Position="top-right" />
</MudContainer>

@code {
    private List<ChangeLog> _changeLogs = new();

    private class ChangeLog
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    private async Task HandleFeatureDrawn(DrawingFeature feature)
    {
        _changeLogs.Add(new ChangeLog
        {
            Timestamp = DateTime.Now,
            Type = "Created",
            Message = $"Drew new {feature.GeometryType} (ID: {feature.Id})"
        });
        StateHasChanged();
    }

    private async Task HandleFeatureEdited(DrawingFeature feature)
    {
        _changeLogs.Add(new ChangeLog
        {
            Timestamp = DateTime.Now,
            Type = "Modified",
            Message = $"Edited {feature.GeometryType} (ID: {feature.Id})"
        });
        StateHasChanged();
    }

    private async Task HandleFeatureDeleted(string featureId)
    {
        _changeLogs.Add(new ChangeLog
        {
            Timestamp = DateTime.Now,
            Type = "Deleted",
            Message = $"Deleted feature (ID: {featureId})"
        });
        StateHasChanged();
    }

    private Color GetLogColor(string type) => type switch
    {
        "Created" => Color.Success,
        "Modified" => Color.Info,
        "Deleted" => Color.Error,
        _ => Color.Default
    };
}
```

### 11. Feature Validation

Validate features before accepting them.

```razor
@page "/draw/validation"

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Feature Validation</MudText>

    <MudAlert Severity="Severity.Info" Class="mb-4">
        Polygons must have an area greater than 1,000 sq meters.
        Lines must be longer than 100 meters.
    </MudAlert>

    @if (_validationError != null)
    {
        <MudAlert Severity="Severity.Error" Class="mb-4" CloseIconClicked="@(() => _validationError = null)">
            @_validationError
        </MudAlert>
    }

    <HonuaMap Id="validationMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="500px" />

    <HonuaDraw
        SyncWith="validationMap"
        OnFeatureDrawn="ValidateFeature"
        Position="top-right" />
</MudContainer>

@code {
    private string? _validationError;

    private async Task ValidateFeature(DrawingFeature feature)
    {
        _validationError = null;

        if (feature.GeometryType == "Polygon" && feature.Measurements?.Area.HasValue == true)
        {
            if (feature.Measurements.Area.Value < 1000)
            {
                _validationError = "Polygon too small! Must be at least 1,000 sq meters.";
                // In a real app, you might delete the feature here
            }
        }
        else if (feature.GeometryType == "LineString" && feature.Measurements?.Distance.HasValue == true)
        {
            if (feature.Measurements.Distance.Value < 100)
            {
                _validationError = "Line too short! Must be at least 100 meters.";
            }
        }

        StateHasChanged();
    }
}
```

---

## Integration Examples

### 12. Drawing with Data Grid

Combine drawing with a data grid to manage features.

```razor
@page "/draw/with-grid"

<MudContainer>
    <MudGrid>
        <MudItem xs="12">
            <MudText Typo="Typo.h4" Class="mb-4">Drawing with Data Grid</MudText>
        </MudItem>

        <MudItem xs="12" md="8">
            <HonuaMap Id="gridMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="600px" />

            <HonuaDraw
                SyncWith="gridMap"
                OnFeatureDrawn="HandleFeatureDrawn"
                OnFeatureDeleted="HandleFeatureDeleted"
                Position="top-right"
                ShowFeatureList="false" />
        </MudItem>

        <MudItem xs="12" md="4">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6" Class="mb-3">Features (@_features.Count)</MudText>

                <MudTable Items="_features" Dense="true" Hover="true">
                    <HeaderContent>
                        <MudTh>Type</MudTh>
                        <MudTh>Measurement</MudTh>
                        <MudTh>Actions</MudTh>
                    </HeaderContent>
                    <RowTemplate>
                        <MudTd>@context.GeometryType</MudTd>
                        <MudTd>@GetMeasurementSummary(context)</MudTd>
                        <MudTd>
                            <MudIconButton
                                Icon="@Icons.Material.Filled.Delete"
                                Size="Size.Small"
                                Color="Color.Error"
                                OnClick="@(() => DeleteFeature(context.Id))" />
                        </MudTd>
                    </RowTemplate>
                </MudTable>
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private List<DrawingFeature> _features = new();

    private async Task HandleFeatureDrawn(DrawingFeature feature)
    {
        _features.Add(feature);
        StateHasChanged();
    }

    private async Task HandleFeatureDeleted(string featureId)
    {
        _features.RemoveAll(f => f.Id == featureId);
        StateHasChanged();
    }

    private async Task DeleteFeature(string featureId)
    {
        // This would trigger the OnFeatureDeleted event
        await Task.CompletedTask;
    }

    private string GetMeasurementSummary(DrawingFeature feature)
    {
        if (feature.Measurements == null) return "-";

        if (feature.Measurements.Distance.HasValue)
            return $"{feature.Measurements.Distance.Value:F0} m";

        if (feature.Measurements.Area.HasValue)
            return $"{feature.Measurements.Area.Value:F0} m²";

        return "-";
    }
}
```

### 13. Drawing with Timeline

Draw features with temporal attributes.

```razor
@page "/draw/with-timeline"
@inject ComponentBus Bus

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Temporal Features</MudText>

    <HonuaMap Id="timelineMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="400px" />

    <HonuaDraw
        SyncWith="timelineMap"
        OnFeatureDrawn="HandleFeatureDrawn"
        Position="top-right" />

    <HonuaTimeline
        Id="featureTimeline"
        SyncWith="timelineMap"
        StartTime="DateTime.Now.AddDays(-7)"
        EndTime="DateTime.Now"
        OnTimeChanged="HandleTimeChanged" />
</MudContainer>

@code {
    private async Task HandleFeatureDrawn(DrawingFeature feature)
    {
        // Add timestamp to feature properties
        feature.Properties["timestamp"] = DateTime.Now.ToString("O");
        feature.Properties["visible_start"] = DateTime.Now.AddHours(-1).ToString("O");
        feature.Properties["visible_end"] = DateTime.Now.AddHours(1).ToString("O");

        StateHasChanged();
    }

    private async Task HandleTimeChanged(TimeChangedMessage message)
    {
        // Filter features based on current time
        // Implementation would go here
    }
}
```

---

## Advanced Examples

### 14. Collaborative Drawing

Multiple users drawing on the same map with real-time sync.

```razor
@page "/draw/collaborative"
@inject IHubConnection HubConnection

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Collaborative Drawing</MudText>

    <MudChip Icon="@Icons.Material.Filled.People">@_connectedUsers users online</MudChip>

    <HonuaMap Id="collabMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="600px" />

    <HonuaDraw
        SyncWith="collabMap"
        OnFeatureDrawn="BroadcastFeature"
        OnFeatureEdited="BroadcastEdit"
        OnFeatureDeleted="BroadcastDelete"
        Position="top-right" />
</MudContainer>

@code {
    private int _connectedUsers = 1;
    private IHubConnection? _hubConnection;

    protected override async Task OnInitializedAsync()
    {
        // Setup SignalR connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("https://your-server/drawhub")
            .Build();

        _hubConnection.On<string>("ReceiveFeature", async (featureJson) =>
        {
            // Add received feature to map
            await InvokeAsync(StateHasChanged);
        });

        await _hubConnection.StartAsync();
    }

    private async Task BroadcastFeature(DrawingFeature feature)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("BroadcastFeature",
                System.Text.Json.JsonSerializer.Serialize(feature));
        }
    }

    private async Task BroadcastEdit(DrawingFeature feature)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("BroadcastEdit", feature.Id,
                System.Text.Json.JsonSerializer.Serialize(feature.Geometry));
        }
    }

    private async Task BroadcastDelete(string featureId)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("BroadcastDelete", featureId);
        }
    }
}
```

### 15. Drawing with Geocoding

Draw features and automatically geocode their locations.

```razor
@page "/draw/with-geocoding"
@inject IGeocoder Geocoder

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Auto-Geocoded Features</MudText>

    <HonuaMap Id="geocodeMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="500px" />

    <HonuaDraw
        SyncWith="geocodeMap"
        OnFeatureDrawn="GeocodeFeature"
        Position="top-right" />

    @if (_geocodedFeatures.Count > 0)
    {
        <MudPaper Class="pa-4 mt-4">
            <MudText Typo="Typo.h6" Class="mb-3">Geocoded Locations</MudText>

            @foreach (var feature in _geocodedFeatures)
            {
                <MudCard Class="mb-2">
                    <MudCardContent>
                        <MudText Typo="Typo.body2">
                            <strong>@feature.GeometryType:</strong> @feature.Properties["address"]
                        </MudText>
                    </MudCardContent>
                </MudCard>
            }
        </MudPaper>
    }
</MudContainer>

@code {
    private List<DrawingFeature> _geocodedFeatures = new();

    private async Task GeocodeFeature(DrawingFeature feature)
    {
        try
        {
            double lat = 0, lon = 0;

            // Extract coordinates based on geometry type
            if (feature.GeometryType == "Point")
            {
                var coords = System.Text.Json.JsonSerializer.Deserialize<double[]>(
                    System.Text.Json.JsonSerializer.Serialize(feature.Geometry));
                if (coords != null && coords.Length >= 2)
                {
                    lon = coords[0];
                    lat = coords[1];
                }
            }

            // Reverse geocode
            var result = await Geocoder.ReverseGeocodeAsync(lat, lon);
            if (result != null)
            {
                feature.Properties["address"] = result.DisplayName;
                feature.Properties["city"] = result.City ?? "";
                feature.Properties["country"] = result.Country ?? "";

                _geocodedFeatures.Add(feature);
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Geocoding error: {ex.Message}");
        }
    }
}
```

### 16. Drawing Templates

Save and load drawing templates.

```razor
@page "/draw/templates"

<MudContainer>
    <MudText Typo="Typo.h4" Class="mb-4">Drawing Templates</MudText>

    <MudStack Row="true" Class="mb-4">
        <MudButton OnClick="SaveTemplate" Variant="Variant.Filled" Color="Color.Primary">
            Save Template
        </MudButton>
        <MudButton OnClick="LoadTemplate" Variant="Variant.Outlined">
            Load Template
        </MudButton>
    </MudStack>

    @if (_templates.Count > 0)
    {
        <MudPaper Class="pa-4 mb-4">
            <MudText Typo="Typo.h6" Class="mb-3">Saved Templates</MudText>

            @foreach (var template in _templates)
            {
                <MudChip OnClick="@(() => ApplyTemplate(template))">
                    @template.Name (@template.Features.Count features)
                </MudChip>
            }
        </MudPaper>
    }

    <HonuaMap Id="templateMap" Center="new[] { -122.4194, 37.7749 }" Zoom="12" Height="500px" />

    <HonuaDraw
        Id="templateDraw"
        SyncWith="templateMap"
        Position="top-right" />
</MudContainer>

@code {
    private List<DrawingTemplate> _templates = new();
    private List<DrawingFeature> _currentFeatures = new();

    private class DrawingTemplate
    {
        public string Name { get; set; } = string.Empty;
        public List<DrawingFeature> Features { get; set; } = new();
        public DateTime Created { get; set; }
    }

    private async Task SaveTemplate()
    {
        var template = new DrawingTemplate
        {
            Name = $"Template {_templates.Count + 1}",
            Features = new List<DrawingFeature>(_currentFeatures),
            Created = DateTime.Now
        };

        _templates.Add(template);

        // Save to localStorage or database
        StateHasChanged();
    }

    private async Task LoadTemplate()
    {
        // Load templates from localStorage or database
        StateHasChanged();
    }

    private async Task ApplyTemplate(DrawingTemplate template)
    {
        // Clear current features and load template features
        _currentFeatures = new List<DrawingFeature>(template.Features);

        // Redraw on map
        StateHasChanged();
    }
}
```

---

## Best Practices Summary

1. **Always sync with a map**: `<HonuaDraw SyncWith="mapId" />`
2. **Handle feature events**: Save, validate, or process drawn features
3. **Provide clear instructions**: Use `ShowToolbar` and measurement displays
4. **Set appropriate defaults**: Colors, units, and styles
5. **Use feature list for management**: Enable `ShowFeatureList` for better UX
6. **Consider accessibility**: Full keyboard support is built-in
7. **Validate features**: Check measurements and geometry validity
8. **Export functionality**: Enable `EnableExport` for data portability
9. **Undo/Redo support**: Enable `EnableUndo` for better user experience
10. **Test on mobile**: Component is touch-optimized

---

## Additional Resources

- [README.md](./README.md) - Complete documentation
- [MapboxGL Draw Documentation](https://github.com/mapbox/mapbox-gl-draw)
- [Turf.js Documentation](https://turfjs.org/)
- Honua.MapSDK API Reference

---

## Contributing

Found an issue or have a suggestion? Please contribute to the project!

## License

Part of Honua.MapSDK - See main project license.

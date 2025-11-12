# H3 Hexagonal Binning - Usage Examples

Practical examples demonstrating H3 hexagonal binning for various use cases.

## Example 1: Crime Density Heatmap

Visualize crime incidents in a city using H3 hexagons.

```razor
@page "/examples/h3-crime-density"
@using Honua.MapSDK.Components.H3
@using Honua.MapSDK.Components.Map

<div style="height: 100vh;">
    <HonuaMap
        Center="[-122.4194, 37.7749]"
        Zoom="11"
        Style="mapbox://styles/mapbox/dark-v10">

        <HonuaH3Hexagons
            Resolution="8"
            Aggregation="count"
            ColorScheme="YlOrRd"
            Opacity="0.8"
            SourceLayer="crime-incidents"
            OnHexagonClick="ShowCrimeDetails" />

    </HonuaMap>
</div>

@code {
    private async Task ShowCrimeDetails(HonuaH3Hexagons.H3HexagonClickEventArgs args)
    {
        Console.WriteLine($"Crime incidents in this area: {args.Count}");
        Console.WriteLine($"H3 Index: {args.H3Index}");
    }
}
```

## Example 2: Temperature Distribution

Show temperature readings across a region with average aggregation.

```razor
@page "/examples/h3-temperature"

<div style="height: 100vh;">
    <HonuaMap Center="[0, 0]" Zoom="4">

        <HonuaH3Hexagons
            Resolution="5"
            Aggregation="average"
            ValueField="temperature_celsius"
            ColorScheme="Plasma"
            SourceLayer="weather-stations"
            OnStatsUpdated="UpdateTemperatureStats" />

        <div class="temp-legend">
            <h3>Temperature Distribution</h3>
            <p>Hexagons: @_hexCount</p>
            <p>Sensors: @_sensorCount</p>
            <p>Min Temp: @_minTemp°C</p>
            <p>Max Temp: @_maxTemp°C</p>
        </div>

    </HonuaMap>
</div>

@code {
    private int _hexCount;
    private int _sensorCount;
    private double _minTemp;
    private double _maxTemp;

    private void UpdateTemperatureStats(HonuaH3Hexagons.H3Stats stats)
    {
        _hexCount = stats.HexagonCount;
        _sensorCount = stats.PointCount;
        _minTemp = stats.MinValue ?? 0;
        _maxTemp = stats.MaxValue ?? 0;
        StateHasChanged();
    }
}

<style>
    .temp-legend {
        position: absolute;
        bottom: 20px;
        left: 20px;
        background: white;
        padding: 15px;
        border-radius: 8px;
        box-shadow: 0 2px 10px rgba(0,0,0,0.2);
    }
</style>
```

## Example 3: Real Estate Price Analysis

Analyze property prices by neighborhood using median aggregation.

```razor
@page "/examples/h3-real-estate"
@inject HttpClient Http

<div style="height: 100vh;">
    <HonuaMap
        Center="[-118.2437, 34.0522]"
        Zoom="10"
        Style="mapbox://styles/mapbox/light-v10">

        <HonuaH3Hexagons @ref="_h3Component"
            Resolution="@_currentResolution"
            Aggregation="median"
            ValueField="sale_price"
            ColorScheme="Viridis"
            ShowControls="true"
            AutoRefresh="true"
            OnHexagonClick="ShowPropertyDetails" />

        <div class="controls">
            <h3>Property Price Analysis</h3>
            <button @onclick="() => SetResolution(6)">City View</button>
            <button @onclick="() => SetResolution(7)">Neighborhood</button>
            <button @onclick="() => SetResolution(8)">Block Level</button>
            <button @onclick="LoadRecentSales">Load Recent Sales</button>
        </div>

    </HonuaMap>
</div>

@code {
    private HonuaH3Hexagons _h3Component;
    private int _currentResolution = 7;

    private void SetResolution(int resolution)
    {
        _currentResolution = resolution;
    }

    private async Task LoadRecentSales()
    {
        // Load property sales data from API
        var sales = await Http.GetFromJsonAsync<PropertySale[]>("/api/properties/recent-sales");
        // Convert to GeoJSON and add to map
    }

    private void ShowPropertyDetails(HonuaH3Hexagons.H3HexagonClickEventArgs args)
    {
        // Show popup with property statistics
        var medianPrice = args.Value;
        var propertyCount = args.Count;
        Console.WriteLine($"Median Price: ${medianPrice:N0}, Properties: {propertyCount}");
    }

    public class PropertySale
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public decimal SalePrice { get; set; }
        public DateTime SaleDate { get; set; }
    }
}
```

## Example 4: Traffic Analysis with Dynamic Resolution

Show traffic incidents with zoom-based resolution adjustment.

```razor
@page "/examples/h3-traffic"

<div style="height: 100vh;">
    <HonuaMap
        Center="[-73.9857, 40.7484]"
        Zoom="12"
        OnZoomEnd="HandleZoomChange">

        <HonuaH3Hexagons @ref="_h3Component"
            Resolution="@_currentResolution"
            Aggregation="count"
            ColorScheme="Inferno"
            SourceLayer="traffic-incidents" />

    </HonuaMap>
</div>

@code {
    private HonuaH3Hexagons _h3Component;
    private int _currentResolution = 7;

    private async Task HandleZoomChange(double newZoom)
    {
        // Adjust H3 resolution based on zoom level
        int newResolution = newZoom switch
        {
            < 8 => 5,   // City-wide view
            < 11 => 7,  // Neighborhood view
            < 14 => 9,  // Street view
            _ => 10     // Building view
        };

        if (newResolution != _currentResolution)
        {
            _currentResolution = newResolution;
            await _h3Component.RefreshHexagons();
        }
    }
}
```

## Example 5: Multi-layer Comparison

Compare different datasets side-by-side using H3.

```razor
@page "/examples/h3-comparison"

<div style="display: flex; height: 100vh;">

    <!-- Map 1: Population Density -->
    <div style="flex: 1;">
        <h3>Population Density</h3>
        <HonuaMap Center="[0, 0]" Zoom="4">
            <HonuaH3Hexagons
                Resolution="6"
                Aggregation="sum"
                ValueField="population"
                ColorScheme="Blues"
                SourceLayer="census-data" />
        </HonuaMap>
    </div>

    <!-- Map 2: Income Distribution -->
    <div style="flex: 1;">
        <h3>Median Income</h3>
        <HonuaMap Center="[0, 0]" Zoom="4">
            <HonuaH3Hexagons
                Resolution="6"
                Aggregation="median"
                ValueField="income"
                ColorScheme="Greens"
                SourceLayer="census-data" />
        </HonuaMap>
    </div>

</div>
```

## Example 6: API-Driven H3 Binning

Process large datasets server-side using the H3 API.

```razor
@page "/examples/h3-api-binning"
@inject HttpClient Http

<div style="height: 100vh;">
    <HonuaMap @ref="_map" Center="[0, 0]" Zoom="4">
        <!-- Results will be added dynamically -->
    </HonuaMap>

    <div class="controls">
        <button @onclick="ProcessData">Process 1M Points</button>
        <p>Status: @_status</p>
    </div>
</div>

@code {
    private HonuaMap _map;
    private string _status = "Ready";

    private async Task ProcessData()
    {
        _status = "Processing...";
        StateHasChanged();

        var request = new H3BinRequest
        {
            Resolution = 7,
            Aggregation = "count",
            IncludeBoundaries = true,
            IncludeStatistics = true,
            Async = true,
            InputType = "collection",
            InputSource = "large-point-collection"
        };

        var response = await Http.PostAsJsonAsync("/api/analysis/h3/bin", request);
        var result = await response.Content.ReadFromJsonAsync<H3BinResponse>();

        if (result.Status == "accepted")
        {
            _status = "Job queued: " + result.JobId;
            // Poll for results
            await PollForResults(result.JobId);
        }
    }

    private async Task PollForResults(string jobId)
    {
        while (true)
        {
            await Task.Delay(2000);
            var status = await Http.GetFromJsonAsync<JobStatus>($"/processes/jobs/{jobId}");

            _status = $"Status: {status.Status} - {status.Progress}%";
            StateHasChanged();

            if (status.Status == "successful")
            {
                // Load results
                var results = await Http.GetFromJsonAsync<object>($"/processes/jobs/{jobId}/results");
                // Add to map
                break;
            }
            else if (status.Status == "failed")
            {
                _status = "Processing failed";
                break;
            }
        }
    }

    public class H3BinRequest
    {
        public int Resolution { get; set; }
        public string Aggregation { get; set; }
        public bool IncludeBoundaries { get; set; }
        public bool IncludeStatistics { get; set; }
        public bool Async { get; set; }
        public string InputType { get; set; }
        public string InputSource { get; set; }
    }

    public class H3BinResponse
    {
        public string JobId { get; set; }
        public string Status { get; set; }
    }

    public class JobStatus
    {
        public string Status { get; set; }
        public int Progress { get; set; }
    }
}
```

## Example 7: Custom Aggregation with Statistics

Use all statistical aggregations to analyze data comprehensively.

```razor
@page "/examples/h3-statistics"

<div style="height: 100vh;">
    <HonuaMap Center="[-95.7129, 37.0902]" Zoom="4">

        <HonuaH3Hexagons @ref="_h3Component"
            Resolution="6"
            Aggregation="@_selectedAggregation"
            ValueField="rainfall_mm"
            ColorScheme="Blues"
            SourceLayer="weather-data"
            OnStatsUpdated="UpdateStats" />

        <div class="stats-panel">
            <h3>Rainfall Analysis</h3>

            <label>
                Aggregation:
                <select @bind="_selectedAggregation" @bind:after="RefreshData">
                    <option value="count">Count</option>
                    <option value="sum">Total</option>
                    <option value="average">Average</option>
                    <option value="min">Minimum</option>
                    <option value="max">Maximum</option>
                    <option value="stddev">Std Deviation</option>
                    <option value="median">Median</option>
                </select>
            </label>

            @if (_stats != null)
            {
                <div class="stat-grid">
                    <div>Hexagons: <strong>@_stats.HexagonCount</strong></div>
                    <div>Stations: <strong>@_stats.PointCount</strong></div>
                    <div>Min: <strong>@_stats.MinValue?.ToString("F2") mm</strong></div>
                    <div>Max: <strong>@_stats.MaxValue?.ToString("F2") mm</strong></div>
                </div>
            }
        </div>

    </HonuaMap>
</div>

@code {
    private HonuaH3Hexagons _h3Component;
    private string _selectedAggregation = "average";
    private HonuaH3Hexagons.H3Stats _stats;

    private async Task RefreshData()
    {
        if (_h3Component != null)
        {
            await _h3Component.RefreshHexagons();
        }
    }

    private void UpdateStats(HonuaH3Hexagons.H3Stats stats)
    {
        _stats = stats;
    }
}
```

## Example 8: H3 Neighbors and Rings

Explore H3 hexagon relationships using the neighbor API.

```csharp
// C# Backend Code
using Honua.Server.Enterprise.Geoprocessing.Operations;

public class H3NeighborExample
{
    private readonly H3Service _h3Service;

    public H3NeighborExample()
    {
        _h3Service = new H3Service();
    }

    public void ExploreH3Relationships()
    {
        // Get H3 index for a location
        var h3Index = _h3Service.PointToH3(37.7749, -122.4194, 7); // San Francisco

        Console.WriteLine($"H3 Index: {h3Index}");
        Console.WriteLine($"Resolution: {_h3Service.GetH3Resolution(h3Index)}");

        // Get hexagon boundary
        var boundary = _h3Service.GetH3Boundary(h3Index);
        Console.WriteLine($"Boundary: {boundary.Coordinates.Length} vertices");

        // Get center point
        var center = _h3Service.GetH3Center(h3Index);
        Console.WriteLine($"Center: {center.Y}, {center.X}");

        // Get area
        var area = _h3Service.GetH3Area(h3Index);
        Console.WriteLine($"Area: {area:N0} m²");

        // Get immediate neighbors (ring 1)
        var neighbors = _h3Service.GetH3Neighbors(h3Index);
        Console.WriteLine($"Neighbors: {neighbors.Count}");

        // Get hexagons within 2 rings
        var ring2 = _h3Service.GetH3Ring(h3Index, 2);
        Console.WriteLine($"Hexagons within 2 rings: {ring2.Count}");
    }
}
```

## Example 9: Programmatic Color Scheme

Dynamically change color schemes based on data characteristics.

```razor
@code {
    private string DetermineColorScheme(string dataType, double? minValue, double? maxValue)
    {
        return dataType switch
        {
            "temperature" => minValue < 0 ? "Plasma" : "Inferno",
            "elevation" => "Greens",
            "precipitation" => "Blues",
            "population" => "YlOrRd",
            "pollution" => "Inferno",
            _ => "Viridis"
        };
    }

    private async Task AnalyzeDataAndVisualize(string dataType, string collectionId)
    {
        // Fetch data statistics
        var stats = await GetDataStatistics(collectionId);

        // Choose appropriate color scheme
        var colorScheme = DetermineColorScheme(dataType, stats.MinValue, stats.MaxValue);

        // Choose appropriate resolution based on data density
        var resolution = DetermineResolution(stats.PointCount, stats.BoundingBox);

        // Update visualization
        _currentColorScheme = colorScheme;
        _currentResolution = resolution;

        await _h3Component.RefreshHexagons();
    }

    private int DetermineResolution(int pointCount, BoundingBox bbox)
    {
        var area = CalculateBoundingBoxArea(bbox);
        var density = pointCount / area;

        return density switch
        {
            > 10000 => 9,  // Very dense
            > 1000 => 8,   // Dense
            > 100 => 7,    // Medium
            > 10 => 6,     // Sparse
            _ => 5         // Very sparse
        };
    }
}
```

## Example 10: Export H3 Results

Export H3 binning results to various formats.

```csharp
// Export to GeoJSON
var result = await Http.PostAsJsonAsync("/api/analysis/h3/bin", request);
var geoJson = await result.Content.ReadAsStringAsync();
await DownloadFile("h3-results.geojson", geoJson, "application/geo+json");

// Export to CSV
var hexagons = ParseH3Results(geoJson);
var csv = ConvertToCSV(hexagons);
await DownloadFile("h3-results.csv", csv, "text/csv");

private string ConvertToCSV(List<H3Hexagon> hexagons)
{
    var sb = new StringBuilder();
    sb.AppendLine("H3Index,Count,Value,CenterLat,CenterLon,Area");

    foreach (var hex in hexagons)
    {
        sb.AppendLine($"{hex.H3Index},{hex.Count},{hex.Value},{hex.CenterLat},{hex.CenterLon},{hex.Area}");
    }

    return sb.ToString();
}
```

## Tips for Production Use

1. **Cache H3 results**: Store pre-computed H3 binnings for frequently accessed datasets
2. **Use appropriate resolutions**: Match resolution to zoom level and data density
3. **Optimize large datasets**: Process server-side for datasets > 100K points
4. **Monitor performance**: Track binning time and hexagon counts
5. **Handle edge cases**: Account for poles, international dateline, and sparse data
6. **Progressive enhancement**: Start with low resolution, refine on demand
7. **Combine with filters**: Use CQL filters to subset data before binning
8. **Test color schemes**: Ensure accessibility and readability for all users

## Next Steps

- Explore [H3 Documentation](../README.md) for detailed API reference
- Review [H3 Best Practices](https://h3geo.org/docs/highlights/indexing)
- Check [Performance Optimization Guide](../../PERFORMANCE_AND_OPTIMIZATIONS.md)
- See [OGC Processes Integration](../Geoprocessing/README.md)

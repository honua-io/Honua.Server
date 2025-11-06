# HonuaHeatmap Examples

Comprehensive examples demonstrating various use cases for the HonuaHeatmap component.

## Table of Contents

1. [Crime Incident Heatmap](#1-crime-incident-heatmap)
2. [WiFi Signal Strength](#2-wifi-signal-strength)
3. [Population Density](#3-population-density)
4. [Traffic Accidents](#4-traffic-accidents)
5. [Weather Station Temperatures](#5-weather-station-temperatures)
6. [Sales by Location](#6-sales-by-location)
7. [Earthquake Magnitude Heatmap](#7-earthquake-magnitude-heatmap)
8. [Real Estate Price Density](#8-real-estate-price-density)
9. [Restaurant Ratings](#9-restaurant-ratings)
10. [Air Quality Monitoring](#10-air-quality-monitoring)

---

## 1. Crime Incident Heatmap

Visualize crime incidents to identify hot spots for law enforcement resource allocation.

### Code

```razor
@page "/examples/crime-heatmap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap
@using Honua.MapSDK.Models

<div class="example-container">
    <h1>Crime Incident Heatmap</h1>
    <p>Displaying crime density across the city to identify hot spots.</p>

    <HonuaMap
        Id="crime-map"
        Style="streets"
        Center="@(new[] { -122.4194, 37.7749 })"
        Zoom="12"
        Pitch="0" />

    <HonuaHeatmap
        SyncWith="crime-map"
        DataSource="crime-incidents"
        Radius="35"
        Intensity="1.2"
        Opacity="0.7"
        ColorGradient="HeatmapGradient.Hot"
        WeightProperty="severity"
        ShowControls="true"
        ShowStatistics="true"
        OnHeatmapUpdated="HandleHeatmapUpdate" />
</div>

@code {
    private void HandleHeatmapUpdate(HeatmapUpdatedEventArgs args)
    {
        Console.WriteLine($"Crime heatmap updated: {args.Statistics?.PointCount} incidents");
    }

    // Data would be loaded from your crime database
    // GeoJSON with properties like: { severity: 1-5, type: "theft" | "assault" | etc }
}
```

### Features

- **Weighted by severity**: More severe crimes show stronger heat
- **Hot gradient**: Red indicates highest crime density
- **Interactive controls**: Adjust radius and intensity in real-time
- **Statistics display**: Shows total incident count

### Data Format

```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Point",
        "coordinates": [-122.4194, 37.7749]
      },
      "properties": {
        "severity": 3,
        "type": "theft",
        "date": "2024-01-15"
      }
    }
  ]
}
```

---

## 2. WiFi Signal Strength

Map WiFi access point coverage and signal strength across a campus or facility.

### Code

```razor
@page "/examples/wifi-heatmap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap

<div class="example-container">
    <h1>WiFi Signal Strength</h1>
    <p>Visualizing WiFi coverage and signal strength across the campus.</p>

    <HonuaMap
        Id="wifi-map"
        Style="satellite"
        Center="@(new[] { -73.9654, 40.7829 })"
        Zoom="17"
        Pitch="45" />

    <HonuaHeatmap
        SyncWith="wifi-map"
        Data="@wifiData"
        Radius="25"
        Intensity="1.5"
        Opacity="0.6"
        ColorGradient="HeatmapGradient.Viridis"
        WeightProperty="signal_strength"
        MaxZoom="20"
        ShowControls="true" />

    <div class="legend">
        <h4>Signal Strength (dBm)</h4>
        <div class="legend-item">
            <span class="color" style="background: #253494;"></span>
            <span>Weak (&lt; -80)</span>
        </div>
        <div class="legend-item">
            <span class="color" style="background: #41b6c4;"></span>
            <span>Fair (-70 to -80)</span>
        </div>
        <div class="legend-item">
            <span class="color" style="background: #a1dab4;"></span>
            <span>Good (-60 to -70)</span>
        </div>
        <div class="legend-item">
            <span class="color" style="background: #ffffcc;"></span>
            <span>Excellent (&gt; -60)</span>
        </div>
    </div>
</div>

@code {
    private object wifiData = new
    {
        type = "FeatureCollection",
        features = new[]
        {
            new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { -73.9654, 40.7829 } },
                properties = new { signal_strength = -55, ssid = "Campus-WiFi-1" }
            },
            new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { -73.9650, 40.7831 } },
                properties = new { signal_strength = -68, ssid = "Campus-WiFi-2" }
            },
            new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { -73.9658, 40.7827 } },
                properties = new { signal_strength = -75, ssid = "Campus-WiFi-3" }
            }
            // ... more access points
        }
    };
}
```

### Use Cases

- Campus WiFi planning
- Identifying coverage gaps
- Optimizing access point placement
- Network performance monitoring

---

## 3. Population Density

Display population distribution across regions or census tracts.

### Code

```razor
@page "/examples/population-heatmap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap
@using Honua.MapSDK.Components.FilterPanel

<div class="example-container">
    <h1>Population Density Analysis</h1>

    <HonuaMap
        Id="population-map"
        Style="light"
        Center="@(new[] { -95.7129, 37.0902 })"
        Zoom="4" />

    <HonuaHeatmap
        SyncWith="population-map"
        DataSource="census-centroids"
        Radius="50"
        Intensity="1.0"
        Opacity="0.65"
        ColorGradient="HeatmapGradient.Plasma"
        WeightProperty="population"
        MinZoom="3"
        MaxZoom="10"
        ShowStatistics="true" />

    <HonuaFilterPanel
        SyncWith="population-map"
        Title="Filter Demographics">
        <FilterField
            Field="population"
            Label="Population"
            Type="FilterFieldType.Range"
            Min="0"
            Max="1000000" />
        <FilterField
            Field="density_per_sqmi"
            Label="Density (per sq mi)"
            Type="FilterFieldType.Range"
            Min="0"
            Max="10000" />
    </HonuaFilterPanel>
</div>

@code {
    // Data loaded from census API or database
    // Each point represents a census tract centroid with population weight
}
```

### Features

- **Weighted by population**: Higher population = stronger heat
- **Plasma gradient**: Professional, publication-ready colors
- **Zoom limits**: Heatmap visible only at appropriate zoom levels
- **Filter integration**: Filter by population or density ranges

---

## 4. Traffic Accidents

Identify dangerous intersections and road segments by visualizing accident frequency.

### Code

```razor
@page "/examples/traffic-accidents"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap
@using Honua.MapSDK.Components.Timeline

<div class="example-container">
    <h1>Traffic Accident Analysis</h1>

    <HonuaMap
        Id="traffic-map"
        Style="streets"
        Center="@(new[] { -118.2437, 34.0522 })"
        Zoom="11" />

    <HonuaHeatmap
        @ref="_accidentHeatmap"
        SyncWith="traffic-map"
        DataSource="traffic-accidents"
        Radius="40"
        Intensity="1.3"
        Opacity="0.75"
        ColorGradient="HeatmapGradient.Inferno"
        WeightProperty="injuries"
        ShowControls="true" />

    <HonuaTimeline
        SyncWith="traffic-map"
        DataSource="traffic-accidents"
        TimeField="accident_date"
        StartDate="@startDate"
        EndDate="@endDate"
        OnTimeChanged="HandleTimeChange" />

    <div class="stats-panel">
        <h3>Accident Statistics</h3>
        <div class="stat">
            <label>Total Accidents:</label>
            <span>@totalAccidents</span>
        </div>
        <div class="stat">
            <label>High Risk Areas:</label>
            <span>@highRiskAreas</span>
        </div>
    </div>
</div>

@code {
    private HonuaHeatmap _accidentHeatmap = null!;
    private DateTime startDate = DateTime.Now.AddYears(-1);
    private DateTime endDate = DateTime.Now;
    private int totalAccidents = 0;
    private int highRiskAreas = 0;

    private async Task HandleTimeChange(TimeChangedMessage message)
    {
        // Filter accidents by time range
        var filteredData = await GetAccidentsByDateRange(
            message.StartTime,
            message.EndTime
        );

        await _accidentHeatmap.UpdateDataAsync(filteredData);

        var stats = _accidentHeatmap.GetStatistics();
        totalAccidents = stats?.PointCount ?? 0;
    }

    private async Task<object> GetAccidentsByDateRange(DateTime start, DateTime end)
    {
        // Query database for accidents in date range
        // Return GeoJSON FeatureCollection
        return new { type = "FeatureCollection", features = new object[] { } };
    }
}
```

### Features

- **Temporal filtering**: Timeline to analyze accidents over time
- **Weighted by injuries**: More severe accidents show stronger heat
- **Inferno gradient**: Dramatic visualization for impact
- **Statistics tracking**: Real-time accident counts

---

## 5. Weather Station Temperatures

Visualize temperature readings from weather stations across a region.

### Code

```razor
@page "/examples/temperature-heatmap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap

<div class="example-container">
    <h1>Temperature Distribution</h1>
    <p>Real-time temperature readings from weather stations.</p>

    <HonuaMap
        Id="weather-map"
        Style="terrain"
        Center="@(new[] { -98.5795, 39.8283 })"
        Zoom="5" />

    <HonuaHeatmap
        @ref="_temperatureHeatmap"
        SyncWith="weather-map"
        Data="@temperatureData"
        Radius="60"
        Intensity="0.8"
        Opacity="0.5"
        ColorGradient="@currentGradient"
        WeightProperty="temperature"
        ShowControls="true" />

    <div class="control-panel">
        <h3>Temperature View</h3>
        <button @onclick="() => SetGradient(HeatmapGradient.Hot)">
            Hot (Warm → Hot)
        </button>
        <button @onclick="() => SetGradient(HeatmapGradient.Cool)">
            Cool (Cold → Warm)
        </button>
        <button @onclick="RefreshData">
            Refresh Data
        </button>
    </div>
</div>

@code {
    private HonuaHeatmap _temperatureHeatmap = null!;
    private HeatmapGradient currentGradient = HeatmapGradient.Hot;
    private object temperatureData = new { type = "FeatureCollection", features = Array.Empty<object>() };

    protected override async Task OnInitializedAsync()
    {
        temperatureData = await FetchWeatherData();
    }

    private async Task SetGradient(HeatmapGradient gradient)
    {
        currentGradient = gradient;
        var config = new HeatmapConfiguration
        {
            Gradient = gradient,
            Radius = 60,
            Intensity = 0.8,
            Opacity = 0.5
        };
        await _temperatureHeatmap.SetConfigurationAsync(config);
    }

    private async Task RefreshData()
    {
        temperatureData = await FetchWeatherData();
        await _temperatureHeatmap.UpdateDataAsync(temperatureData);
    }

    private async Task<object> FetchWeatherData()
    {
        // Fetch from weather API
        // Return GeoJSON with temperature property
        return new
        {
            type = "FeatureCollection",
            features = new[]
            {
                new
                {
                    type = "Feature",
                    geometry = new { type = "Point", coordinates = new[] { -98.5, 39.8 } },
                    properties = new { temperature = 72.5, humidity = 65, station = "STATION-001" }
                }
                // ... more stations
            }
        };
    }
}
```

### Features

- **Real-time updates**: Refresh button to get latest readings
- **Switchable gradients**: Toggle between hot and cool color schemes
- **Large radius**: Smooth interpolation between stations
- **Weighted by temperature**: Visualizes temperature distribution

---

## 6. Sales by Location

Analyze sales performance across retail locations or territories.

### Code

```razor
@page "/examples/sales-heatmap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap
@using Honua.MapSDK.Components.DataGrid

<div class="example-container">
    <h1>Sales Performance Heatmap</h1>

    <div class="layout">
        <div class="map-container">
            <HonuaMap
                Id="sales-map"
                Style="light"
                Center="@(new[] { -87.6298, 41.8781 })"
                Zoom="10" />

            <HonuaHeatmap
                SyncWith="sales-map"
                DataSource="store-locations"
                Radius="45"
                Intensity="1.1"
                Opacity="0.7"
                ColorGradient="HeatmapGradient.Viridis"
                WeightProperty="@selectedMetric"
                ShowControls="true"
                ShowStatistics="true" />
        </div>

        <div class="sidebar">
            <h3>Sales Metrics</h3>
            <select @bind="selectedMetric">
                <option value="revenue">Revenue</option>
                <option value="transactions">Transactions</option>
                <option value="customers">Customer Count</option>
                <option value="avg_sale">Average Sale</option>
            </select>

            <HonuaDataGrid
                SyncWith="sales-map"
                DataSource="store-locations"
                ShowRowNumbers="true"
                PageSize="10">
                <Column Field="store_name" Header="Store" />
                <Column Field="revenue" Header="Revenue" Format="Currency" />
                <Column Field="transactions" Header="Sales" />
                <Column Field="customers" Header="Customers" />
            </HonuaDataGrid>
        </div>
    </div>
</div>

@code {
    private string selectedMetric = "revenue";

    // Data includes store locations with sales metrics
    // GeoJSON features with properties: revenue, transactions, customers, avg_sale
}
```

### Use Cases

- Identify high-performing regions
- Optimize marketing spend
- Plan new store locations
- Allocate sales resources

---

## 7. Earthquake Magnitude Heatmap

Visualize seismic activity with magnitude-weighted heatmap.

### Code

```razor
@page "/examples/earthquake-heatmap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap
@using Honua.MapSDK.Components.Legend

<div class="example-container">
    <h1>Seismic Activity Monitor</h1>

    <HonuaMap
        Id="earthquake-map"
        Style="dark"
        Center="@(new[] { -155.5, 19.4 })"
        Zoom="7"
        Pitch="30" />

    <HonuaHeatmap
        SyncWith="earthquake-map"
        Data="@earthquakeData"
        Radius="35"
        Intensity="1.8"
        Opacity="0.8"
        ColorGradient="HeatmapGradient.Inferno"
        WeightProperty="magnitude"
        DarkMode="true"
        ShowControls="true" />

    <HonuaLegend
        SyncWith="earthquake-map"
        Title="Magnitude"
        Position="bottom-left">
        <LegendItem Color="#fcffa4" Label="8.0+ (Major)" />
        <LegendItem Color="#f79321" Label="6.0-7.9 (Strong)" />
        <LegendItem Color="#ca181d" Label="4.0-5.9 (Light)" />
        <LegendItem Color="#550b1d" Label="&lt;4.0 (Minor)" />
    </HonuaLegend>
</div>

@code {
    private object earthquakeData = new
    {
        type = "FeatureCollection",
        features = new[]
        {
            new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { -155.5, 19.4 } },
                properties = new
                {
                    magnitude = 4.2,
                    depth = 5.3,
                    time = "2024-01-15T08:23:45Z",
                    location = "5km SE of Volcano, Hawaii"
                }
            }
            // ... more earthquakes from USGS API
        }
    };

    // Data would typically come from USGS Earthquake API:
    // https://earthquake.usgs.gov/earthquakes/feed/v1.0/geojson.php
}
```

### Features

- **Magnitude weighting**: Stronger earthquakes show more intense heat
- **Dark mode**: Better visibility on dark basemap
- **Inferno gradient**: Dramatic colors for seismic visualization
- **Custom legend**: Clear magnitude scale

---

## 8. Real Estate Price Density

Analyze property prices and identify expensive neighborhoods.

### Code

```razor
@page "/examples/real-estate-heatmap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap
@using Honua.MapSDK.Components.Chart

<div class="example-container">
    <h1>Real Estate Price Analysis</h1>

    <div class="layout">
        <div class="map-section">
            <HonuaMap
                Id="realestate-map"
                Style="streets"
                Center="@(new[] { -122.2712, 37.8044 })"
                Zoom="12" />

            <HonuaHeatmap
                @ref="_priceHeatmap"
                SyncWith="realestate-map"
                DataSource="property-listings"
                Radius="30"
                Intensity="1.0"
                Opacity="0.6"
                ColorGradient="@priceGradient"
                CustomGradient="@customPriceGradient"
                WeightProperty="price_per_sqft"
                ShowControls="true" />
        </div>

        <div class="chart-section">
            <HonuaChart
                SyncWith="realestate-map"
                Field="price_per_sqft"
                Type="ChartType.Histogram"
                Bins="20"
                Title="Price Distribution"
                ValueFormat="ValueFormat.Currency" />
        </div>
    </div>
</div>

@code {
    private HonuaHeatmap _priceHeatmap = null!;
    private HeatmapGradient priceGradient = HeatmapGradient.Custom;

    private Dictionary<double, string> customPriceGradient = new()
    {
        { 0.0, "rgba(0, 255, 0, 0)" },      // Green (affordable) - transparent
        { 0.25, "rgba(255, 255, 0, 0.3)" }, // Yellow - light
        { 0.5, "rgba(255, 165, 0, 0.6)" },  // Orange - medium
        { 0.75, "rgba(255, 69, 0, 0.8)" },  // Red-orange - strong
        { 1.0, "rgba(139, 0, 0, 1)" }       // Dark red (expensive) - opaque
    };
}
```

### Features

- **Custom gradient**: Green (affordable) to red (expensive)
- **Price per sqft weighting**: Normalized by property size
- **Chart integration**: Histogram shows price distribution
- **Interactive analysis**: Click areas to see property details

---

## 9. Restaurant Ratings

Map restaurant quality using review ratings and visit frequency.

### Code

```razor
@page "/examples/restaurant-heatmap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap
@using Honua.MapSDK.Components.Popup

<div class="example-container">
    <h1>Restaurant Quality Map</h1>

    <HonuaMap
        Id="restaurant-map"
        Style="streets"
        Center="@(new[] { -73.9857, 40.7484 })"
        Zoom="14" />

    <HonuaHeatmap
        SyncWith="restaurant-map"
        DataSource="restaurants"
        Radius="25"
        Intensity="1.2"
        Opacity="0.65"
        ColorGradient="HeatmapGradient.Viridis"
        WeightProperty="@weightMetric"
        ShowControls="true" />

    <HonuaPopup
        SyncWith="restaurant-map"
        LayerId="restaurants"
        Template="@popupTemplate" />

    <div class="filter-controls">
        <label>
            Weight by:
            <select @bind="weightMetric">
                <option value="rating">Rating</option>
                <option value="review_count">Review Count</option>
                <option value="popularity">Popularity Score</option>
            </select>
        </label>

        <label>
            Cuisine:
            <select @bind="cuisineFilter">
                <option value="">All</option>
                <option value="italian">Italian</option>
                <option value="chinese">Chinese</option>
                <option value="mexican">Mexican</option>
                <option value="japanese">Japanese</option>
            </select>
        </label>
    </div>
</div>

@code {
    private string weightMetric = "rating";
    private string cuisineFilter = "";

    private string popupTemplate = @"
        <div class='restaurant-popup'>
            <h3>{{name}}</h3>
            <div class='rating'>⭐ {{rating}}/5.0</div>
            <div class='cuisine'>{{cuisine}}</div>
            <div class='reviews'>{{review_count}} reviews</div>
            <div class='price'>{{price_range}}</div>
        </div>
    ";
}
```

### Use Cases

- Find highly-rated restaurant clusters
- Identify food deserts
- Plan restaurant locations
- Compare cuisine popularity by area

---

## 10. Air Quality Monitoring

Real-time air quality visualization from sensor network.

### Code

```razor
@page "/examples/air-quality-heatmap"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Heatmap

<div class="example-container">
    <h1>Air Quality Monitor</h1>
    <p class="subtitle">PM2.5 Particulate Matter Concentration</p>

    <HonuaMap
        Id="airquality-map"
        Style="light"
        Center="@(new[] { 121.5654, 25.0330 })"
        Zoom="11" />

    <HonuaHeatmap
        @ref="_aqiHeatmap"
        SyncWith="airquality-map"
        Data="@sensorData"
        Radius="50"
        Intensity="1.1"
        Opacity="0.7"
        ColorGradient="HeatmapGradient.Custom"
        CustomGradient="@aqiGradient"
        WeightProperty="pm25"
        ShowControls="true"
        ShowStatistics="true"
        OnHeatmapUpdated="HandleAQIUpdate" />

    <div class="aqi-legend">
        <h3>Air Quality Index</h3>
        <div class="aqi-item good">
            <span class="dot"></span>
            <span>Good (0-50)</span>
        </div>
        <div class="aqi-item moderate">
            <span class="dot"></span>
            <span>Moderate (51-100)</span>
        </div>
        <div class="aqi-item unhealthy-sensitive">
            <span class="dot"></span>
            <span>Unhealthy for Sensitive (101-150)</span>
        </div>
        <div class="aqi-item unhealthy">
            <span class="dot"></span>
            <span>Unhealthy (151-200)</span>
        </div>
        <div class="aqi-item very-unhealthy">
            <span class="dot"></span>
            <span>Very Unhealthy (201-300)</span>
        </div>
        <div class="aqi-item hazardous">
            <span class="dot"></span>
            <span>Hazardous (301+)</span>
        </div>
    </div>
</div>

@code {
    private HonuaHeatmap _aqiHeatmap = null!;
    private object sensorData = new { type = "FeatureCollection", features = Array.Empty<object>() };

    // AQI-based color gradient (EPA standard colors)
    private Dictionary<double, string> aqiGradient = new()
    {
        { 0.0, "rgba(0, 228, 0, 0)" },      // Good - transparent green
        { 0.2, "rgba(255, 255, 0, 0.4)" },  // Moderate - yellow
        { 0.4, "rgba(255, 126, 0, 0.6)" },  // Unhealthy for Sensitive - orange
        { 0.6, "rgba(255, 0, 0, 0.8)" },    // Unhealthy - red
        { 0.8, "rgba(143, 63, 151, 0.9)" }, // Very Unhealthy - purple
        { 1.0, "rgba(126, 0, 35, 1)" }      // Hazardous - maroon
    };

    protected override async Task OnInitializedAsync()
    {
        sensorData = await FetchAirQualityData();

        // Set up periodic refresh (every 5 minutes)
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                await RefreshAirQuality();
            }
        });
    }

    private async Task RefreshAirQuality()
    {
        sensorData = await FetchAirQualityData();
        await InvokeAsync(async () =>
        {
            await _aqiHeatmap.UpdateDataAsync(sensorData);
            StateHasChanged();
        });
    }

    private void HandleAQIUpdate(HeatmapUpdatedEventArgs args)
    {
        var stats = args.Statistics;
        if (stats != null)
        {
            Console.WriteLine($"Monitoring {stats.PointCount} sensors");
            if (stats.MaxWeight.HasValue)
            {
                Console.WriteLine($"Max PM2.5: {stats.MaxWeight.Value:F1} μg/m³");
            }
        }
    }

    private async Task<object> FetchAirQualityData()
    {
        // Fetch from air quality API (e.g., PurpleAir, IQAir)
        return new
        {
            type = "FeatureCollection",
            features = new[]
            {
                new
                {
                    type = "Feature",
                    geometry = new { type = "Point", coordinates = new[] { 121.5654, 25.0330 } },
                    properties = new
                    {
                        pm25 = 35.2,
                        pm10 = 48.7,
                        aqi = 101,
                        sensor_id = "SENSOR-001",
                        timestamp = DateTime.UtcNow
                    }
                }
                // ... more sensors
            }
        };
    }
}

<style>
    .aqi-legend {
        position: absolute;
        bottom: 20px;
        left: 20px;
        background: white;
        padding: 15px;
        border-radius: 8px;
        box-shadow: 0 2px 10px rgba(0,0,0,0.15);
    }

    .aqi-item {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 4px 0;
    }

    .aqi-item .dot {
        width: 12px;
        height: 12px;
        border-radius: 50%;
    }

    .aqi-item.good .dot { background: #00e400; }
    .aqi-item.moderate .dot { background: #ffff00; }
    .aqi-item.unhealthy-sensitive .dot { background: #ff7e00; }
    .aqi-item.unhealthy .dot { background: #ff0000; }
    .aqi-item.very-unhealthy .dot { background: #8f3f97; }
    .aqi-item.hazardous .dot { background: #7e0023; }
</style>
```

### Features

- **EPA-standard colors**: Official AQI color scheme
- **Real-time updates**: Auto-refresh every 5 minutes
- **Weighted by PM2.5**: Particulate matter concentration
- **Health advisory**: Color-coded legend with health implications
- **Sensor network**: Visualizes data from multiple monitoring stations

### Use Cases

- Public health monitoring
- Pollution source identification
- Urban planning
- Real-time alerts
- Historical trend analysis

---

## Performance Tips for Large Datasets

When working with large point datasets (10,000+ points):

1. **Use vector tiles**: Pre-render heatmaps server-side for massive datasets
2. **Implement clustering**: Switch between heatmap and clusters based on zoom
3. **Optimize radius**: Larger radius = faster but less detail
4. **Lazy loading**: Load data only for visible map extent
5. **Debounce updates**: Don't update on every map move
6. **Progressive rendering**: Load coarse data first, refine on idle

## Additional Resources

- [HonuaHeatmap README](README.md) - Full documentation
- [MapLibre Heatmap Spec](https://maplibre.org/maplibre-style-spec/layers/#heatmap) - Layer specification
- [ComponentBus Guide](../../Core/README.md) - Inter-component communication

## Contributing

Have a great heatmap example? Submit a PR to add it to this collection!

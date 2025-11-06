# Honua MapSDK Demo Application

A comprehensive demonstration application showcasing the capabilities of Honua.MapSDK - a powerful mapping library for Blazor applications.

## Overview

This demo application features **6 real-world scenarios** that demonstrate how to build interactive mapping dashboards using Honua MapSDK components. Each demo showcases different use cases and component combinations, providing both inspiration and practical examples for your own applications.

## Features

### Components Demonstrated

- **HonuaMap** - Interactive maps with MapLibre GL
- **HonuaDataGrid** - Data tables with auto-sync and export
- **HonuaChart** - Multiple chart types (histogram, bar, pie, line)
- **HonuaLegend** - Layer management and visibility control
- **HonuaFilterPanel** - Spatial and attribute filtering

### Key Capabilities

- **Zero-Config Auto-Sync** - Components automatically synchronize through ComponentBus
- **Click-to-Filter** - Click on map features, grid rows, or chart segments to filter across all components
- **Export** - Export data to JSON, CSV, or GeoJSON formats
- **Dark Mode** - Full dark mode support with theme switching
- **Responsive Design** - Works on desktop, tablet, and mobile devices
- **Real-World Data** - Realistic sample datasets for each scenario

## Demo Scenarios

### 1. Property Analysis Dashboard

**Route:** `/property-dashboard`

**Purpose:** Real estate analysis with property parcels, value analysis, and filtering

**Components Used:**
- HonuaMap with property parcel polygons
- HonuaDataGrid with property details
- HonuaChart (histogram) for value distribution
- HonuaChart (pie) for land use breakdown

**Key Features:**
- Search properties by address
- Filter by price range and land use
- Click parcels to view detailed information
- Export filtered properties to CSV
- Property value analysis

**Code Pattern:**
```razor
<HonuaMap Id="property-map" ... />

<HonuaDataGrid TItem="PropertyFeature"
               SyncWith="property-map"
               DataSource="data/parcels.geojson"
               ShowExport="true" />

<HonuaChart SyncWith="property-map"
            Type="ChartType.Histogram"
            Field="assessedValue"
            ValueFormat="ValueFormat.Currency" />
```

### 2. Environmental Monitoring

**Route:** `/environmental-monitoring`

**Purpose:** Sensor network dashboard with air quality and temperature monitoring

**Components Used:**
- HonuaMap with sensor point locations
- HonuaDataGrid with sensor readings
- HonuaChart (line) for temperature trends
- HonuaChart (bar) for air quality by location
- HonuaFilterPanel for sensor filtering

**Key Features:**
- Real-time sensor monitoring (simulated)
- Color-coded sensors by air quality index
- Temperature and humidity trends
- Filter by sensor type and status
- Alert threshold visualization

**Data Structure:**
```json
{
  "type": "Feature",
  "properties": {
    "id": "S001",
    "temperature": 68.5,
    "airQuality": 42,
    "status": "Active",
    "readings": [...]
  },
  "geometry": { "type": "Point", "coordinates": [-122.40, 37.78] }
}
```

### 3. Vehicle Tracking

**Route:** `/vehicle-tracking`

**Purpose:** Fleet management with real-time vehicle locations and status

**Components Used:**
- HonuaMap with vehicle markers
- HonuaDataGrid with fleet details
- HonuaChart (bar) for status distribution
- HonuaChart (doughnut) for fleet composition

**Key Features:**
- Real-time vehicle location tracking
- Status monitoring (active, idle, maintenance)
- Speed analysis with color coding
- Driver and route information
- Fleet utilization metrics

**Use Cases:**
- Delivery fleet management
- Service vehicle tracking
- Route optimization analysis
- Maintenance scheduling

### 4. Emergency Response

**Route:** `/emergency-response`

**Purpose:** Incident management with multi-layer infrastructure

**Components Used:**
- HonuaMap with multiple POI layers
- HonuaDataGrid with facility details
- HonuaLegend for layer management
- HonuaFilterPanel for facility filtering
- Multiple HonuaChart components

**Layers:**
- Hospitals (capacity, emergency services)
- Fire Stations (units, coverage)
- Police Stations (officers, jurisdiction)
- Schools (students, capacity)
- Parks (recreation areas)

**Key Features:**
- Multi-layer visualization
- Toggle layers on/off
- Filter by facility type
- Emergency service coverage analysis
- Infrastructure capacity metrics

### 5. Urban Planning

**Route:** `/urban-planning`

**Purpose:** City planning analysis with zoning and demographics

**Components Used:**
- HonuaMap with multiple basemaps
- HonuaDataGrid with parcel data
- Multiple HonuaChart components for analysis
- HonuaLegend for zoning colors

**Key Features:**
- Multiple basemap options (streets, satellite, terrain)
- Zoning layer visualization
- Population density analysis
- Land use distribution
- Building age and value trends
- Infrastructure overlay

**Analysis Types:**
- Land use distribution (residential, commercial, mixed-use)
- Property value analysis by zone
- Building age distribution
- Square footage analysis

### 6. Component Showcase

**Route:** `/component-showcase`

**Purpose:** Comprehensive reference for all MapSDK components

**Sections:**
- Overview of all components
- Individual component demonstrations
- Integration examples
- Best practices and tips
- Code samples

**Features:**
- Tabbed interface for easy navigation
- Live examples of each component
- Property documentation
- Integration patterns
- Performance tips

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- A modern web browser (Chrome, Firefox, Edge, Safari)

### Running the Demo

1. Clone the repository:
```bash
cd Honua.Server/examples/Honua.MapSDK.DemoApp
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Run the application:
```bash
dotnet run
```

4. Open your browser to `https://localhost:5001` (or the URL shown in the console)

### Building for Production

```bash
dotnet publish -c Release -o ./publish
```

The published files will be in the `./publish/wwwroot` directory and can be deployed to any static hosting service.

## Sample Data

The demo includes four sample GeoJSON datasets:

### parcels.geojson
Property parcel data for real estate analysis
- **Features:** 10 sample parcels
- **Properties:** address, assessedValue, landUse, sqft, bedrooms, bathrooms, yearBuilt
- **Geometry:** Polygons representing parcel boundaries
- **Location:** San Francisco neighborhoods

### sensors.geojson
Environmental sensor network data
- **Features:** 10 monitoring sensors
- **Properties:** temperature, airQuality, humidity, status, time-series readings
- **Geometry:** Points representing sensor locations
- **Data:** 24-hour reading history per sensor

### vehicles.geojson
Fleet tracking data
- **Features:** 12 vehicles
- **Properties:** vehicleType, status, speed, driver, route
- **Geometry:** Points representing current vehicle positions
- **Types:** Delivery trucks, service vans, sedans

### pois.geojson
Points of interest for emergency and urban planning
- **Features:** 20 facilities
- **Types:** Hospitals, fire stations, police stations, schools, parks
- **Properties:** capacity, address, type-specific metadata
- **Geometry:** Points representing facility locations

## Project Structure

```
Honua.MapSDK.DemoApp/
├── Pages/
│   ├── Index.razor                    # Landing page
│   ├── PropertyDashboard.razor        # Real estate demo
│   ├── EnvironmentalMonitoring.razor  # Sensor network demo
│   ├── VehicleTracking.razor          # Fleet management demo
│   ├── EmergencyResponse.razor        # Emergency services demo
│   ├── UrbanPlanning.razor            # City planning demo
│   └── ComponentShowcase.razor        # Component reference
├── Components/
│   └── Layout/
│       └── DemoLayout.razor           # Main layout with navigation
├── wwwroot/
│   ├── data/                          # Sample GeoJSON datasets
│   ├── css/
│   │   └── app.css                    # Application styles
│   └── index.html                     # HTML entry point
├── Program.cs                          # Application entry point
├── App.razor                           # Root component
└── _Imports.razor                      # Global using statements
```

## Code Patterns

### Basic Map with DataGrid

```razor
<!-- Map displays spatial data -->
<HonuaMap Id="my-map"
          MapStyle="https://basemaps.cartocdn.com/gl/voyager-gl-style/style.json"
          Center="new[] { -122.4194, 37.7749 }"
          Zoom="13" />

<!-- DataGrid automatically syncs with map -->
<HonuaDataGrid TItem="MyFeature"
               SyncWith="my-map"
               DataSource="data/features.geojson"
               ShowExport="true"
               Filterable="true"
               Sortable="true" />
```

### Map with Charts

```razor
<!-- Map as sync source -->
<HonuaMap Id="analysis-map" ... />

<!-- Histogram for numeric distribution -->
<HonuaChart SyncWith="analysis-map"
            Type="ChartType.Histogram"
            Field="price"
            Title="Price Distribution"
            Bins="10"
            ValueFormat="ValueFormat.Currency"
            EnableFilter="true" />

<!-- Pie chart for categorical data -->
<HonuaChart SyncWith="analysis-map"
            Type="ChartType.Pie"
            Field="category"
            Title="Categories"
            EnableFilter="true" />
```

### Feature Click Handling

```razor
<HonuaMap Id="my-map"
          OnFeatureClicked="@OnFeatureClicked" />

@code {
    private void OnFeatureClicked(FeatureClickedMessage message)
    {
        // Access clicked feature properties
        var id = message.Properties["id"];
        var name = message.Properties["name"];

        // Show details dialog, highlight feature, etc.
    }
}
```

### Multi-Component Dashboard

```razor
<div class="dashboard-layout">
    <!-- Single map as the sync source -->
    <HonuaMap Id="dashboard-map" ... />

    <!-- All components sync with the map -->
    <HonuaDataGrid SyncWith="dashboard-map" ... />

    <HonuaChart SyncWith="dashboard-map"
                Type="ChartType.Histogram" ... />

    <HonuaChart SyncWith="dashboard-map"
                Type="ChartType.Pie" ... />

    <HonuaFilterPanel SyncWith="dashboard-map" ... />

    <HonuaLegend SyncWith="dashboard-map" ... />
</div>
```

## Customization

### Changing Basemaps

```razor
@code {
    private string _mapStyle = "https://basemaps.cartocdn.com/gl/voyager-gl-style/style.json";

    private void ChangeBasemap(string style)
    {
        _mapStyle = style switch
        {
            "streets" => "https://basemaps.cartocdn.com/gl/voyager-gl-style/style.json",
            "dark" => "https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json",
            "satellite" => "https://api.maptiler.com/maps/hybrid/style.json?key=YOUR_KEY",
            _ => _mapStyle
        };
    }
}
```

### Custom Column Definitions

```razor
<HonuaDataGrid TItem="MyFeature" ...>
    <Columns>
        <PropertyColumn Property="x => x.Properties.Name"
                        Title="Name"
                        Sortable="true" />

        <PropertyColumn Property="x => x.Properties.Value"
                        Title="Value">
            <CellTemplate>
                <MudChip Color="@GetValueColor(context.Properties.Value)">
                    @context.Properties.Value.ToString("C0")
                </MudChip>
            </CellTemplate>
        </PropertyColumn>
    </Columns>
</HonuaDataGrid>
```

### Dark Mode Implementation

The demo includes full dark mode support. The theme is managed in `DemoLayout.razor`:

```razor
@code {
    private bool _isDarkMode = false;

    private void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
    }
}
```

CSS variables automatically adjust:
```css
:root {
    --bg-primary: #FFFFFF;
    --text-primary: #212121;
}

.dark-mode {
    --bg-primary: #1E1E1E;
    --text-primary: #E0E0E0;
}
```

## Performance Tips

1. **Use Pagination** - For large datasets, enable pagination on data grids
2. **Limit Categories** - Set `MaxCategories` on charts to prevent overcrowding
3. **Optimize Bins** - Use 8-12 bins for histograms
4. **Dense Mode** - Enable `Dense="true"` on grids to show more data
5. **Lazy Loading** - Load data on demand rather than all at once

## Browser Support

- Chrome/Edge (recommended)
- Firefox
- Safari
- Opera

## Resources

- **Honua MapSDK Documentation** - `/component-showcase` in the demo
- **GitHub Repository** - https://github.com/honua-io/Honua.Server
- **MapLibre GL Documentation** - https://maplibre.org/
- **MudBlazor Documentation** - https://mudblazor.com/

## License

This demo application is part of the Honua.Server project.

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

## Support

For questions, issues, or feature requests, please visit the GitHub repository.

---

**Built with Honua MapSDK** - Zero-config mapping for Blazor applications

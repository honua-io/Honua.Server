# Your First Dashboard

Build a complete, production-ready geospatial dashboard with synchronized components in under 30 minutes.

---

## What We'll Build

In this tutorial, you'll create a **Property Management Dashboard** with:

- Interactive map showing property locations
- Data grid with search, filter, and export
- Charts visualizing property data
- Filter panel for advanced queries
- Legend showing map layers
- Full synchronization between all components

**Final Result:** A dashboard that allows users to explore property data visually, filter by attributes, export reports, and analyze trends—all without writing synchronization code.

---

## Prerequisites

- Completed [Installation](installation.md)
- Completed [Quick Start](quick-start.md) or [Your First Map](first-map.md)
- Basic understanding of Blazor and C#

---

## Step 1: Create the Project Structure

### Create the Page

Create `Pages/PropertyDashboard.razor`:

```razor
@page "/property-dashboard"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Models

<PageTitle>Property Management Dashboard</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-0" Style="height: 100vh;">
    <MudAppBar Elevation="1">
        <MudIcon Icon="@Icons.Material.Filled.Dashboard" Class="mr-2" />
        <MudText Typo="Typo.h6">Property Management Dashboard</MudText>
        <MudSpacer />
        <MudText Typo="Typo.body2">@_propertyCount properties loaded</MudText>
    </MudAppBar>

    <!-- Content will go here -->
</MudContainer>

@code {
    private int _propertyCount = 0;
}
```

---

## Step 2: Define the Data Model

Add a property data model:

```csharp
@code {
    private List<PropertyData> _properties = new();
    private int _propertyCount = 0;

    public class PropertyData
    {
        public int Id { get; set; }
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string PropertyType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public int SquareFeet { get; set; }
        public int YearBuilt { get; set; }
        public DateTime ListedDate { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    protected override void OnInitialized()
    {
        LoadSampleData();
    }

    private void LoadSampleData()
    {
        _properties = new List<PropertyData>
        {
            new() { Id = 1, Address = "123 Main St", City = "San Francisco", State = "CA", ZipCode = "94102", PropertyType = "Condo", Status = "For Sale", Price = 850000, Bedrooms = 2, Bathrooms = 2, SquareFeet = 1200, YearBuilt = 2015, ListedDate = DateTime.Now.AddDays(-10), Latitude = 37.7749, Longitude = -122.4194 },
            new() { Id = 2, Address = "456 Market St", City = "San Francisco", State = "CA", ZipCode = "94103", PropertyType = "House", Status = "For Sale", Price = 1250000, Bedrooms = 3, Bathrooms = 2, SquareFeet = 1800, YearBuilt = 2010, ListedDate = DateTime.Now.AddDays(-5), Latitude = 37.7899, Longitude = -122.3959 },
            new() { Id = 3, Address = "789 Valencia St", City = "San Francisco", State = "CA", ZipCode = "94110", PropertyType = "Condo", Status = "Sold", Price = 720000, Bedrooms = 1, Bathrooms = 1, SquareFeet = 900, YearBuilt = 2018, ListedDate = DateTime.Now.AddDays(-30), Latitude = 37.7599, Longitude = -122.4214 },
            new() { Id = 4, Address = "321 Hayes St", City = "San Francisco", State = "CA", ZipCode = "94102", PropertyType = "Apartment", Status = "For Rent", Price = 3500, Bedrooms = 2, Bathrooms = 1, SquareFeet = 1000, YearBuilt = 2012, ListedDate = DateTime.Now.AddDays(-2), Latitude = 37.7765, Longitude = -122.4241 },
            new() { Id = 5, Address = "654 Divisadero St", City = "San Francisco", State = "CA", ZipCode = "94117", PropertyType = "House", Status = "For Sale", Price = 1750000, Bedrooms = 4, Bathrooms = 3, SquareFeet = 2500, YearBuilt = 2005, ListedDate = DateTime.Now.AddDays(-15), Latitude = 37.7733, Longitude = -122.4375 }
        };

        _propertyCount = _properties.Count;
    }
}
```

---

## Step 3: Create the Dashboard Layout

Set up a responsive grid layout:

```razor
<MudContainer MaxWidth="MaxWidth.False" Class="pa-0" Style="height: 100vh; display: flex; flex-direction: column;">
    <MudAppBar Elevation="1" Dense="true">
        <MudIcon Icon="@Icons.Material.Filled.Dashboard" Class="mr-2" />
        <MudText Typo="Typo.h6">Property Management Dashboard</MudText>
        <MudSpacer />
        <MudChip Size="Size.Small" Color="Color.Info">@_propertyCount properties</MudChip>
    </MudAppBar>

    <div style="flex: 1; overflow: hidden;">
        <MudGrid Spacing="0" Style="height: 100%;">
            <!-- Left Panel: Filters and Charts -->
            <MudItem xs="12" md="3" Style="height: 100%; overflow-y: auto; border-right: 1px solid #e0e0e0;">
                <div class="pa-3">
                    <!-- Filters will go here -->
                </div>
            </MudItem>

            <!-- Center: Map -->
            <MudItem xs="12" md="6" Style="height: 100%; position: relative;">
                <!-- Map will go here -->
            </MudItem>

            <!-- Right Panel: Data Grid -->
            <MudItem xs="12" md="3" Style="height: 100%; border-left: 1px solid #e0e0e0;">
                <!-- Data grid will go here -->
            </MudItem>
        </MudGrid>
    </div>
</MudContainer>
```

---

## Step 4: Add the Map

Add the `HonuaMap` component:

```razor
<!-- Center: Map -->
<MudItem xs="12" md="6" Style="height: 100%; position: relative;">
    <HonuaMap Id="property-map"
              Center="@(new[] { -122.4194, 37.7749 })"
              Zoom="12"
              MapStyle="https://demotiles.maplibre.org/style.json"
              OnMapReady="@HandleMapReady"
              OnFeatureClicked="@HandleFeatureClick"
              Style="width: 100%; height: 100%;" />

    <!-- Map Overlay: Legend -->
    <div style="position: absolute; top: 10px; right: 10px; z-index: 1000;">
        <HonuaLegend SyncWith="property-map"
                     Collapsible="true"
                     ShowOpacity="true" />
    </div>

    <!-- Map Overlay: Statistics -->
    <div style="position: absolute; bottom: 10px; left: 10px; z-index: 1000;">
        <MudPaper Elevation="3" Class="pa-2">
            <MudText Typo="Typo.caption">
                Viewing @_visibleCount of @_propertyCount properties
            </MudText>
        </MudPaper>
    </div>
</MudItem>

@code {
    private int _visibleCount = 0;

    private void HandleMapReady(MapReadyMessage message)
    {
        Console.WriteLine("Map ready");
        _visibleCount = _propertyCount;
    }

    private void HandleFeatureClick(FeatureClickedMessage message)
    {
        Console.WriteLine($"Clicked property: {message.FeatureId}");
        // Show property details
    }
}
```

---

## Step 5: Add the Data Grid

Add `HonuaDataGrid` for property listings:

```razor
<!-- Right Panel: Data Grid -->
<MudItem xs="12" md="3" Style="height: 100%; border-left: 1px solid #e0e0e0;">
    <HonuaDataGrid TItem="PropertyData"
                   Items="@_properties"
                   SyncWith="property-map"
                   Title="Property Listings"
                   ShowSearch="true"
                   ShowExport="true"
                   ShowRefresh="true"
                   Filterable="true"
                   Sortable="true"
                   Dense="true"
                   PageSize="10"
                   Height="100%">
        <Columns>
            <PropertyColumn Property="x => x.Address" Title="Address">
                <CellTemplate>
                    <MudText Typo="Typo.body2" Style="font-weight: 500;">@context.Address</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">@context.City, @context.State</MudText>
                </CellTemplate>
            </PropertyColumn>

            <PropertyColumn Property="x => x.PropertyType" Title="Type" />

            <PropertyColumn Property="x => x.Status" Title="Status">
                <CellTemplate>
                    <MudChip Size="Size.Small"
                             Color="@GetStatusColor(context.Status)">
                        @context.Status
                    </MudChip>
                </CellTemplate>
            </PropertyColumn>

            <PropertyColumn Property="x => x.Price" Title="Price" Format="C0" />

            <PropertyColumn Property="x => x.Bedrooms" Title="Beds" />
            <PropertyColumn Property="x => x.Bathrooms" Title="Baths" />
            <PropertyColumn Property="x => x.SquareFeet" Title="Sq Ft" Format="N0" />
        </Columns>
    </HonuaDataGrid>
</MudItem>

@code {
    private Color GetStatusColor(string status) => status switch
    {
        "For Sale" => Color.Success,
        "For Rent" => Color.Info,
        "Sold" => Color.Default,
        "Pending" => Color.Warning,
        _ => Color.Default
    };
}
```

---

## Step 6: Add Charts

Add visualizations to the left panel:

```razor
<!-- Left Panel: Filters and Charts -->
<MudItem xs="12" md="3" Style="height: 100%; overflow-y: auto; border-right: 1px solid #e0e0e0;">
    <div class="pa-3">
        <!-- Summary Stats -->
        <MudPaper Elevation="2" Class="pa-3 mb-3">
            <MudText Typo="Typo.h6" Class="mb-2">Overview</MudText>
            <MudGrid Spacing="2">
                <MudItem xs="6">
                    <MudText Typo="Typo.caption" Color="Color.Secondary">Total Value</MudText>
                    <MudText Typo="Typo.h6">@_totalValue.ToString("C0")</MudText>
                </MudItem>
                <MudItem xs="6">
                    <MudText Typo="Typo.caption" Color="Color.Secondary">Avg Price</MudText>
                    <MudText Typo="Typo.h6">@_avgPrice.ToString("C0")</MudText>
                </MudItem>
            </MudGrid>
        </MudPaper>

        <!-- Property Type Chart -->
        <MudPaper Elevation="2" Class="pa-3 mb-3" Style="height: 300px;">
            <HonuaChart Id="type-chart"
                        Type="ChartType.Doughnut"
                        Field="PropertyType"
                        SyncWith="property-map"
                        Title="By Type"
                        ColorScheme="cool"
                        EnableFilter="true"
                        ShowLegend="true"
                        LegendPosition="bottom" />
        </MudPaper>

        <!-- Status Chart -->
        <MudPaper Elevation="2" Class="pa-3 mb-3" Style="height: 300px;">
            <HonuaChart Id="status-chart"
                        Type="ChartType.Pie"
                        Field="Status"
                        SyncWith="property-map"
                        Title="By Status"
                        ColorScheme="warm"
                        EnableFilter="true"
                        ShowLegend="true"
                        LegendPosition="bottom" />
        </MudPaper>

        <!-- Price Distribution -->
        <MudPaper Elevation="2" Class="pa-3 mb-3" Style="height: 300px;">
            <HonuaChart Id="price-chart"
                        Type="ChartType.Histogram"
                        Field="Price"
                        SyncWith="property-map"
                        Title="Price Distribution"
                        Bins="10"
                        ColorScheme="earth"
                        ValueFormat="ValueFormat.Currency" />
        </MudPaper>
    </div>
</MudItem>

@code {
    private decimal _totalValue => _properties.Sum(p => p.Price);
    private decimal _avgPrice => _properties.Any() ? _properties.Average(p => p.Price) : 0;
}
```

---

## Step 7: Add Filter Panel

Add advanced filtering capabilities:

```razor
<div class="pa-3">
    <!-- Filter Panel -->
    <MudPaper Elevation="2" Class="pa-3 mb-3">
        <HonuaFilterPanel SyncWith="property-map"
                          Title="Filters"
                          ShowSpatial="true"
                          ShowAttribute="true"
                          ShowTemporal="true"
                          AttributeFields="@_filterFields"
                          DefaultDateField="ListedDate" />
    </MudPaper>

    <!-- Summary Stats -->
    <!-- Charts -->
    <!-- ... (rest of the left panel content) -->
</div>

@code {
    private List<FilterFieldConfig> _filterFields = new()
    {
        new FilterFieldConfig
        {
            Field = "PropertyType",
            Label = "Property Type",
            Type = FieldType.String
        },
        new FilterFieldConfig
        {
            Field = "Status",
            Label = "Status",
            Type = FieldType.String
        },
        new FilterFieldConfig
        {
            Field = "Price",
            Label = "Price",
            Type = FieldType.Number
        },
        new FilterFieldConfig
        {
            Field = "Bedrooms",
            Label = "Bedrooms",
            Type = FieldType.Number
        },
        new FilterFieldConfig
        {
            Field = "Bathrooms",
            Label = "Bathrooms",
            Type = FieldType.Number
        },
        new FilterFieldConfig
        {
            Field = "SquareFeet",
            Label = "Square Feet",
            Type = FieldType.Number
        },
        new FilterFieldConfig
        {
            Field = "YearBuilt",
            Label = "Year Built",
            Type = FieldType.Number
        }
    };
}
```

---

## Complete Dashboard Code

Here's the complete `PropertyDashboard.razor`:

```razor
@page "/property-dashboard"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.DataGrid
@using Honua.MapSDK.Components.Chart
@using Honua.MapSDK.Components.Legend
@using Honua.MapSDK.Components.FilterPanel
@using Honua.MapSDK.Core.Messages
@using Honua.MapSDK.Models

<PageTitle>Property Management Dashboard</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-0" Style="height: 100vh; display: flex; flex-direction: column;">
    <MudAppBar Elevation="1" Dense="true">
        <MudIcon Icon="@Icons.Material.Filled.Dashboard" Class="mr-2" />
        <MudText Typo="Typo.h6">Property Management Dashboard</MudText>
        <MudSpacer />
        <MudChip Size="Size.Small" Color="Color.Info" Icon="@Icons.Material.Filled.Home">
            @_propertyCount properties
        </MudChip>
    </MudAppBar>

    <div style="flex: 1; overflow: hidden;">
        <MudGrid Spacing="0" Style="height: 100%;">
            <!-- Left Panel -->
            <MudItem xs="12" md="3" Style="height: 100%; overflow-y: auto; border-right: 1px solid var(--mud-palette-divider);">
                <div class="pa-3">
                    <!-- Filters -->
                    <MudPaper Elevation="2" Class="pa-3 mb-3">
                        <HonuaFilterPanel SyncWith="property-map"
                                          Title="Filters"
                                          ShowSpatial="true"
                                          ShowAttribute="true"
                                          ShowTemporal="true"
                                          AttributeFields="@_filterFields"
                                          DefaultDateField="ListedDate"
                                          OnFiltersApplied="@HandleFiltersApplied" />
                    </MudPaper>

                    <!-- Summary -->
                    <MudPaper Elevation="2" Class="pa-3 mb-3">
                        <MudText Typo="Typo.h6" Class="mb-3">Overview</MudText>
                        <MudGrid Spacing="2">
                            <MudItem xs="6">
                                <div class="stat-card">
                                    <MudText Typo="Typo.caption" Color="Color.Secondary">Total Value</MudText>
                                    <MudText Typo="Typo.h5">@_totalValue.ToString("C0")</MudText>
                                </div>
                            </MudItem>
                            <MudItem xs="6">
                                <div class="stat-card">
                                    <MudText Typo="Typo.caption" Color="Color.Secondary">Avg Price</MudText>
                                    <MudText Typo="Typo.h5">@_avgPrice.ToString("C0")</MudText>
                                </div>
                            </MudItem>
                            <MudItem xs="6">
                                <div class="stat-card">
                                    <MudText Typo="Typo.caption" Color="Color.Secondary">For Sale</MudText>
                                    <MudText Typo="Typo.h5">@_forSaleCount</MudText>
                                </div>
                            </MudItem>
                            <MudItem xs="6">
                                <div class="stat-card">
                                    <MudText Typo="Typo.caption" Color="Color.Secondary">Sold</MudText>
                                    <MudText Typo="Typo.h5">@_soldCount</MudText>
                                </div>
                            </MudItem>
                        </MudGrid>
                    </MudPaper>

                    <!-- Charts -->
                    <MudPaper Elevation="2" Class="mb-3" Style="height: 280px;">
                        <HonuaChart Id="type-chart"
                                    Type="ChartType.Doughnut"
                                    Field="PropertyType"
                                    SyncWith="property-map"
                                    Title="By Type"
                                    ColorScheme="cool"
                                    EnableFilter="true"
                                    ShowLegend="true"
                                    LegendPosition="bottom" />
                    </MudPaper>

                    <MudPaper Elevation="2" Class="mb-3" Style="height: 280px;">
                        <HonuaChart Id="status-chart"
                                    Type="ChartType.Pie"
                                    Field="Status"
                                    SyncWith="property-map"
                                    Title="By Status"
                                    ColorScheme="warm"
                                    EnableFilter="true"
                                    ShowLegend="true"
                                    LegendPosition="bottom" />
                    </MudPaper>

                    <MudPaper Elevation="2" Class="mb-3" Style="height: 280px;">
                        <HonuaChart Id="price-chart"
                                    Type="ChartType.Histogram"
                                    Field="Price"
                                    SyncWith="property-map"
                                    Title="Price Distribution"
                                    Bins="8"
                                    ColorScheme="earth"
                                    ValueFormat="ValueFormat.Currency" />
                    </MudPaper>
                </div>
            </MudItem>

            <!-- Center: Map -->
            <MudItem xs="12" md="6" Style="height: 100%; position: relative;">
                <HonuaMap Id="property-map"
                          Center="@(new[] { -122.4194, 37.7749 })"
                          Zoom="12"
                          MapStyle="https://demotiles.maplibre.org/style.json"
                          OnMapReady="@HandleMapReady"
                          OnExtentChanged="@HandleExtentChanged"
                          OnFeatureClicked="@HandleFeatureClick"
                          Style="width: 100%; height: 100%;" />

                <div style="position: absolute; top: 10px; right: 10px; z-index: 1000;">
                    <HonuaLegend SyncWith="property-map"
                                 Collapsible="true"
                                 ShowOpacity="true"
                                 ShowGroups="true" />
                </div>

                <div style="position: absolute; bottom: 10px; left: 10px; z-index: 1000;">
                    <MudPaper Elevation="3" Class="pa-2">
                        <MudText Typo="Typo.caption">
                            <MudIcon Icon="@Icons.Material.Filled.Visibility" Size="Size.Small" />
                            Viewing @_visibleCount of @_propertyCount properties
                        </MudText>
                    </MudPaper>
                </div>
            </MudItem>

            <!-- Right Panel: Data Grid -->
            <MudItem xs="12" md="3" Style="height: 100%; border-left: 1px solid var(--mud-palette-divider);">
                <HonuaDataGrid TItem="PropertyData"
                               Items="@_properties"
                               SyncWith="property-map"
                               Title="Property Listings"
                               ShowSearch="true"
                               ShowExport="true"
                               ShowRefresh="true"
                               Filterable="true"
                               Sortable="true"
                               Dense="true"
                               PageSize="10"
                               Height="100%">
                    <Columns>
                        <PropertyColumn Property="x => x.Address" Title="Address" Sortable="true">
                            <CellTemplate>
                                <MudText Typo="Typo.body2" Style="font-weight: 500;">@context.Address</MudText>
                                <MudText Typo="Typo.caption" Color="Color.Secondary">@context.City, @context.State</MudText>
                            </CellTemplate>
                        </PropertyColumn>

                        <PropertyColumn Property="x => x.PropertyType" Title="Type" Sortable="true" />

                        <PropertyColumn Property="x => x.Status" Title="Status" Sortable="true">
                            <CellTemplate>
                                <MudChip Size="Size.Small" Color="@GetStatusColor(context.Status)">
                                    @context.Status
                                </MudChip>
                            </CellTemplate>
                        </PropertyColumn>

                        <PropertyColumn Property="x => x.Price" Title="Price" Format="C0" Sortable="true" />
                        <PropertyColumn Property="x => x.Bedrooms" Title="Beds" Sortable="true" />
                        <PropertyColumn Property="x => x.Bathrooms" Title="Baths" Sortable="true" />
                        <PropertyColumn Property="x => x.SquareFeet" Title="Sq Ft" Format="N0" Sortable="true" />
                    </Columns>
                </HonuaDataGrid>
            </MudItem>
        </MudGrid>
    </div>
</MudContainer>

@code {
    private List<PropertyData> _properties = new();
    private int _propertyCount = 0;
    private int _visibleCount = 0;

    // Summary stats
    private decimal _totalValue => _properties.Sum(p => p.Price);
    private decimal _avgPrice => _properties.Any() ? _properties.Average(p => p.Price) : 0;
    private int _forSaleCount => _properties.Count(p => p.Status == "For Sale");
    private int _soldCount => _properties.Count(p => p.Status == "Sold");

    private List<FilterFieldConfig> _filterFields = new()
    {
        new() { Field = "PropertyType", Label = "Property Type", Type = FieldType.String },
        new() { Field = "Status", Label = "Status", Type = FieldType.String },
        new() { Field = "Price", Label = "Price", Type = FieldType.Number },
        new() { Field = "Bedrooms", Label = "Bedrooms", Type = FieldType.Number },
        new() { Field = "Bathrooms", Label = "Bathrooms", Type = FieldType.Number },
        new() { Field = "SquareFeet", Label = "Square Feet", Type = FieldType.Number },
        new() { Field = "YearBuilt", Label = "Year Built", Type = FieldType.Number }
    };

    protected override void OnInitialized()
    {
        LoadSampleData();
    }

    private void LoadSampleData()
    {
        _properties = new List<PropertyData>
        {
            new() { Id = 1, Address = "123 Main St", City = "San Francisco", State = "CA", ZipCode = "94102", PropertyType = "Condo", Status = "For Sale", Price = 850000, Bedrooms = 2, Bathrooms = 2, SquareFeet = 1200, YearBuilt = 2015, ListedDate = DateTime.Now.AddDays(-10), Latitude = 37.7749, Longitude = -122.4194 },
            new() { Id = 2, Address = "456 Market St", City = "San Francisco", State = "CA", ZipCode = "94103", PropertyType = "House", Status = "For Sale", Price = 1250000, Bedrooms = 3, Bathrooms = 2, SquareFeet = 1800, YearBuilt = 2010, ListedDate = DateTime.Now.AddDays(-5), Latitude = 37.7899, Longitude = -122.3959 },
            new() { Id = 3, Address = "789 Valencia St", City = "San Francisco", State = "CA", ZipCode = "94110", PropertyType = "Condo", Status = "Sold", Price = 720000, Bedrooms = 1, Bathrooms = 1, SquareFeet = 900, YearBuilt = 2018, ListedDate = DateTime.Now.AddDays(-30), Latitude = 37.7599, Longitude = -122.4214 },
            new() { Id = 4, Address = "321 Hayes St", City = "San Francisco", State = "CA", ZipCode = "94102", PropertyType = "Apartment", Status = "For Rent", Price = 3500, Bedrooms = 2, Bathrooms = 1, SquareFeet = 1000, YearBuilt = 2012, ListedDate = DateTime.Now.AddDays(-2), Latitude = 37.7765, Longitude = -122.4241 },
            new() { Id = 5, Address = "654 Divisadero St", City = "San Francisco", State = "CA", ZipCode = "94117", PropertyType = "House", Status = "For Sale", Price = 1750000, Bedrooms = 4, Bathrooms = 3, SquareFeet = 2500, YearBuilt = 2005, ListedDate = DateTime.Now.AddDays(-15), Latitude = 37.7733, Longitude = -122.4375 },
            new() { Id = 6, Address = "987 Castro St", City = "San Francisco", State = "CA", ZipCode = "94114", PropertyType = "Condo", Status = "For Sale", Price = 950000, Bedrooms = 2, Bathrooms = 2, SquareFeet = 1300, YearBuilt = 2016, ListedDate = DateTime.Now.AddDays(-7), Latitude = 37.7609, Longitude = -122.4351 }
        };

        _propertyCount = _properties.Count;
        _visibleCount = _propertyCount;
    }

    private void HandleMapReady(MapReadyMessage message)
    {
        Console.WriteLine("Property map ready");
    }

    private void HandleExtentChanged(MapExtentChangedMessage message)
    {
        // Update visible count based on extent
        _visibleCount = _propertyCount; // In real app, filter by bounds
        StateHasChanged();
    }

    private void HandleFeatureClick(FeatureClickedMessage message)
    {
        Console.WriteLine($"Clicked property: {message.FeatureId}");
    }

    private void HandleFiltersApplied(List<FilterDefinition> filters)
    {
        Console.WriteLine($"Filters applied: {filters.Count}");
    }

    private Color GetStatusColor(string status) => status switch
    {
        "For Sale" => Color.Success,
        "For Rent" => Color.Info,
        "Sold" => Color.Default,
        "Pending" => Color.Warning,
        _ => Color.Default
    };

    public class PropertyData
    {
        public int Id { get; set; }
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string PropertyType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public int SquareFeet { get; set; }
        public int YearBuilt { get; set; }
        public DateTime ListedDate { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}

<style>
    .stat-card {
        padding: 8px;
        border-radius: 4px;
        background: var(--mud-palette-background-grey);
    }
</style>
```

---

## Testing Your Dashboard

Run the application and test these features:

1. **Filter by property type** - Click a chart segment
2. **Search properties** - Use the data grid search
3. **Spatial filter** - Use current map extent filter
4. **Attribute filter** - Filter by price, bedrooms, etc.
5. **Export data** - Export to CSV or JSON
6. **Row selection** - Click a row to highlight on map

---

## Next Steps

### Enhance Your Dashboard

1. **Load Real Data** - Connect to an API
2. **Add Real-Time Updates** - Use SignalR for live data
3. **Custom Styling** - Apply your brand colors
4. **User Preferences** - Save filter and view state
5. **Advanced Analytics** - Add more charts and KPIs

### Learn More

- [Working with Data](../guides/working-with-data.md)
- [Advanced Filtering](../guides/advanced-filtering.md)
- [Custom Styling](../guides/custom-styling.md)
- [Performance Tips](../recipes/performance-tips.md)

---

**Congratulations!** You've built a production-ready geospatial dashboard with Honua.MapSDK.

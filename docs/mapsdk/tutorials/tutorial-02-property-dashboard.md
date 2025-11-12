# Tutorial 02: Building a Property Management Dashboard

> **Learning Objectives**: Build a complete property management dashboard with map visualization, data grid, filtering, attribute tables, and export functionality. This tutorial demonstrates real-world application development with Honua.MapSDK.

---

## Prerequisites

- Completed [Tutorial 01: First Map](Tutorial_01_FirstMap.md) OR
- Basic knowledge of Blazor and C#
- .NET 8.0 SDK installed
- Visual Studio 2022 or VS Code

**Estimated Time**: 45 minutes

---

## Table of Contents

1. [Overview of Final Result](#overview-of-final-result)
2. [Setup Data Models](#step-1-setup-data-models)
3. [Create Property Service](#step-2-create-property-service)
4. [Build the Dashboard Layout](#step-3-build-the-dashboard-layout)
5. [Add Map with Parcel Layer](#step-4-add-map-with-parcel-layer)
6. [Add Layer List](#step-5-add-layer-list)
7. [Add Popup for Property Info](#step-6-add-popup-for-property-info)
8. [Add Attribute Table](#step-7-add-attribute-table)
9. [Add Filtering](#step-8-add-filtering)
10. [Add Export Functionality](#step-9-add-export-functionality)
11. [Complete Working Example](#complete-working-example)

---

## Overview of Final Result

By the end of this tutorial, you'll have a production-ready property management dashboard with:

- 🗺️ **Interactive Map** - Display properties on a map with custom styling
- 📊 **Data Grid** - Sortable, filterable table of properties
- 📋 **Attribute Table** - Detailed property information
- 🎛️ **Layer Controls** - Toggle visibility of different layers
- 💬 **Popups** - Click properties to see details
- 🔍 **Advanced Filtering** - Filter by price, type, status, location
- 📤 **Export** - Export data to CSV, JSON, or GeoJSON
- 📈 **Statistics** - Real-time statistics based on filters

**Screenshot Preview:**
```
┌─────────────────────────────────────────────────────────┐
│  Property Management Dashboard        [Export] [Filter] │
├──────────────────┬──────────────────────────────────────┤
│                  │  Statistics:                         │
│                  │  Total Properties: 127               │
│      MAP         │  Average Price: $625,000             │
│                  │  Active Listings: 89                 │
│   (Properties    │                                      │
│    displayed)    ├──────────────────────────────────────┤
│                  │  [Data Grid - Property List]         │
│                  │  Address | Price | Type | Status     │
│                  │  ...                                 │
└──────────────────┴──────────────────────────────────────┘
```

---

## Step 1: Setup Data Models

Create a `Models` folder and add `Property.cs`:

```csharp
using System.Text.Json.Serialization;

namespace PropertyDashboard.Models
{
    public class Property
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("zipCode")]
        public string ZipCode { get; set; } = string.Empty;

        [JsonPropertyName("propertyType")]
        public PropertyType PropertyType { get; set; }

        [JsonPropertyName("status")]
        public PropertyStatus Status { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("bedrooms")]
        public int Bedrooms { get; set; }

        [JsonPropertyName("bathrooms")]
        public double Bathrooms { get; set; }

        [JsonPropertyName("squareFeet")]
        public int SquareFeet { get; set; }

        [JsonPropertyName("yearBuilt")]
        public int YearBuilt { get; set; }

        [JsonPropertyName("lotSize")]
        public double LotSize { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("listingDate")]
        public DateTime ListingDate { get; set; } = DateTime.Now;

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // Computed properties
        [JsonIgnore]
        public string FullAddress => $"{Address}, {City}, {State} {ZipCode}";

        [JsonIgnore]
        public string FormattedPrice => Price.ToString("C0");

        [JsonIgnore]
        public double PricePerSquareFoot => SquareFeet > 0 ? (double)(Price / SquareFeet) : 0;

        [JsonIgnore]
        public string PropertyTypeDisplay => PropertyType.ToString().Replace("_", " ");

        [JsonIgnore]
        public string StatusDisplay => Status.ToString();
    }

    public enum PropertyType
    {
        Single_Family,
        Condo,
        Townhouse,
        Multi_Family,
        Land,
        Commercial
    }

    public enum PropertyStatus
    {
        Active,
        Pending,
        Sold,
        Off_Market
    }

    public class PropertyStatistics
    {
        public int TotalProperties { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal MedianPrice { get; set; }
        public int ActiveListings { get; set; }
        public int PendingListings { get; set; }
        public int SoldListings { get; set; }
        public double AverageSquareFeet { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class PropertyFilter
    {
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public PropertyType? PropertyType { get; set; }
        public PropertyStatus? Status { get; set; }
        public int? MinBedrooms { get; set; }
        public int? MaxBedrooms { get; set; }
        public double? MinBathrooms { get; set; }
        public int? MinSquareFeet { get; set; }
        public int? MaxSquareFeet { get; set; }
        public string? City { get; set; }
        public double[]? BoundingBox { get; set; } // [west, south, east, north]
    }
}
```

**What you just did:**
- Created a comprehensive Property model with all relevant fields
- Added enums for PropertyType and PropertyStatus
- Created helper classes for statistics and filtering
- Added computed properties for display

---

## Step 2: Create Property Service

Create `Services/PropertyService.cs`:

```csharp
using PropertyDashboard.Models;
using System.Text.Json;

namespace PropertyDashboard.Services
{
    public interface IPropertyService
    {
        Task<List<Property>> GetPropertiesAsync();
        Task<Property?> GetPropertyByIdAsync(string id);
        Task<PropertyStatistics> GetStatisticsAsync(List<Property> properties);
        Task<List<Property>> FilterPropertiesAsync(List<Property> properties, PropertyFilter filter);
        Task<string> ExportToGeoJsonAsync(List<Property> properties);
        Task<string> ExportToCsvAsync(List<Property> properties);
    }

    public class PropertyService : IPropertyService
    {
        private List<Property>? _cachedProperties;

        public async Task<List<Property>> GetPropertiesAsync()
        {
            if (_cachedProperties != null)
                return _cachedProperties;

            // In a real application, this would fetch from a database or API
            // For this demo, we'll generate sample data
            _cachedProperties = GenerateSampleProperties();
            await Task.Delay(100); // Simulate network delay
            return _cachedProperties;
        }

        public async Task<Property?> GetPropertyByIdAsync(string id)
        {
            var properties = await GetPropertiesAsync();
            return properties.FirstOrDefault(p => p.Id == id);
        }

        public Task<PropertyStatistics> GetStatisticsAsync(List<Property> properties)
        {
            if (!properties.Any())
            {
                return Task.FromResult(new PropertyStatistics());
            }

            var stats = new PropertyStatistics
            {
                TotalProperties = properties.Count,
                AveragePrice = properties.Average(p => p.Price),
                MedianPrice = CalculateMedian(properties.Select(p => p.Price).ToList()),
                ActiveListings = properties.Count(p => p.Status == PropertyStatus.Active),
                PendingListings = properties.Count(p => p.Status == PropertyStatus.Pending),
                SoldListings = properties.Count(p => p.Status == PropertyStatus.Sold),
                AverageSquareFeet = properties.Average(p => p.SquareFeet),
                TotalValue = properties.Sum(p => p.Price)
            };

            return Task.FromResult(stats);
        }

        public Task<List<Property>> FilterPropertiesAsync(List<Property> properties, PropertyFilter filter)
        {
            var filtered = properties.AsEnumerable();

            if (filter.MinPrice.HasValue)
                filtered = filtered.Where(p => p.Price >= filter.MinPrice.Value);

            if (filter.MaxPrice.HasValue)
                filtered = filtered.Where(p => p.Price <= filter.MaxPrice.Value);

            if (filter.PropertyType.HasValue)
                filtered = filtered.Where(p => p.PropertyType == filter.PropertyType.Value);

            if (filter.Status.HasValue)
                filtered = filtered.Where(p => p.Status == filter.Status.Value);

            if (filter.MinBedrooms.HasValue)
                filtered = filtered.Where(p => p.Bedrooms >= filter.MinBedrooms.Value);

            if (filter.MaxBedrooms.HasValue)
                filtered = filtered.Where(p => p.Bedrooms <= filter.MaxBedrooms.Value);

            if (filter.MinBathrooms.HasValue)
                filtered = filtered.Where(p => p.Bathrooms >= filter.MinBathrooms.Value);

            if (filter.MinSquareFeet.HasValue)
                filtered = filtered.Where(p => p.SquareFeet >= filter.MinSquareFeet.Value);

            if (filter.MaxSquareFeet.HasValue)
                filtered = filtered.Where(p => p.SquareFeet <= filter.MaxSquareFeet.Value);

            if (!string.IsNullOrEmpty(filter.City))
                filtered = filtered.Where(p => p.City.Contains(filter.City, StringComparison.OrdinalIgnoreCase));

            if (filter.BoundingBox != null && filter.BoundingBox.Length == 4)
            {
                var (west, south, east, north) = (filter.BoundingBox[0], filter.BoundingBox[1],
                                                   filter.BoundingBox[2], filter.BoundingBox[3]);
                filtered = filtered.Where(p =>
                    p.Longitude >= west && p.Longitude <= east &&
                    p.Latitude >= south && p.Latitude <= north);
            }

            return Task.FromResult(filtered.ToList());
        }

        public Task<string> ExportToGeoJsonAsync(List<Property> properties)
        {
            var features = properties.Select(p => new
            {
                type = "Feature",
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { p.Longitude, p.Latitude }
                },
                properties = new
                {
                    id = p.Id,
                    address = p.Address,
                    city = p.City,
                    state = p.State,
                    zipCode = p.ZipCode,
                    propertyType = p.PropertyType.ToString(),
                    status = p.Status.ToString(),
                    price = p.Price,
                    bedrooms = p.Bedrooms,
                    bathrooms = p.Bathrooms,
                    squareFeet = p.SquareFeet,
                    yearBuilt = p.YearBuilt
                }
            });

            var geoJson = new
            {
                type = "FeatureCollection",
                features = features
            };

            return Task.FromResult(JsonSerializer.Serialize(geoJson, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        public Task<string> ExportToCsvAsync(List<Property> properties)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,Address,City,State,ZipCode,PropertyType,Status,Price,Bedrooms,Bathrooms,SquareFeet,YearBuilt,Latitude,Longitude");

            foreach (var p in properties)
            {
                csv.AppendLine($"{p.Id},{p.Address},{p.City},{p.State},{p.ZipCode},{p.PropertyType},{p.Status},{p.Price},{p.Bedrooms},{p.Bathrooms},{p.SquareFeet},{p.YearBuilt},{p.Latitude},{p.Longitude}");
            }

            return Task.FromResult(csv.ToString());
        }

        private decimal CalculateMedian(List<decimal> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int count = sorted.Count;
            if (count == 0) return 0;
            if (count % 2 == 0)
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
            return sorted[count / 2];
        }

        private List<Property> GenerateSampleProperties()
        {
            var random = new Random(42); // Fixed seed for consistent data
            var properties = new List<Property>();

            // San Francisco area coordinates
            var cities = new[]
            {
                new { Name = "San Francisco", Lat = 37.7749, Lon = -122.4194, Bounds = 0.1 },
                new { Name = "Oakland", Lat = 37.8044, Lon = -122.2712, Bounds = 0.08 },
                new { Name = "San Jose", Lat = 37.3382, Lon = -121.8863, Bounds = 0.12 },
                new { Name = "Berkeley", Lat = 37.8715, Lon = -122.2730, Bounds = 0.05 },
            };

            for (int i = 0; i < 150; i++)
            {
                var city = cities[random.Next(cities.Length)];
                var propertyType = (PropertyType)random.Next(6);
                var status = (PropertyStatus)random.Next(4);

                var property = new Property
                {
                    Id = Guid.NewGuid().ToString(),
                    Address = $"{random.Next(1, 9999)} {GetRandomStreetName(random)} {GetRandomStreetType(random)}",
                    City = city.Name,
                    State = "CA",
                    ZipCode = $"94{random.Next(100, 999)}",
                    PropertyType = propertyType,
                    Status = status,
                    Price = GeneratePrice(propertyType, random),
                    Bedrooms = random.Next(1, 6),
                    Bathrooms = random.Next(1, 5) + (random.Next(2) * 0.5),
                    SquareFeet = random.Next(800, 4000),
                    YearBuilt = random.Next(1950, 2024),
                    LotSize = random.Next(2000, 10000) / 100.0,
                    Latitude = city.Lat + (random.NextDouble() - 0.5) * city.Bounds,
                    Longitude = city.Lon + (random.NextDouble() - 0.5) * city.Bounds,
                    Description = GenerateDescription(propertyType),
                    ImageUrl = $"https://picsum.photos/400/300?random={i}",
                    ListingDate = DateTime.Now.AddDays(-random.Next(1, 180)),
                    LastUpdated = DateTime.Now.AddDays(-random.Next(0, 30))
                };

                properties.Add(property);
            }

            return properties;
        }

        private decimal GeneratePrice(PropertyType type, Random random)
        {
            return type switch
            {
                PropertyType.Single_Family => random.Next(500000, 2000000),
                PropertyType.Condo => random.Next(300000, 1200000),
                PropertyType.Townhouse => random.Next(400000, 1500000),
                PropertyType.Multi_Family => random.Next(800000, 3000000),
                PropertyType.Land => random.Next(200000, 1000000),
                PropertyType.Commercial => random.Next(1000000, 5000000),
                _ => random.Next(300000, 2000000)
            };
        }

        private string GetRandomStreetName(Random random)
        {
            var names = new[] { "Main", "Oak", "Pine", "Maple", "Cedar", "Elm", "Washington", "Lincoln", "Jefferson", "Madison" };
            return names[random.Next(names.Length)];
        }

        private string GetRandomStreetType(Random random)
        {
            var types = new[] { "St", "Ave", "Blvd", "Dr", "Ln", "Way", "Ct" };
            return types[random.Next(types.Length)];
        }

        private string GenerateDescription(PropertyType type)
        {
            return type switch
            {
                PropertyType.Single_Family => "Beautiful single-family home with spacious rooms and modern amenities.",
                PropertyType.Condo => "Modern condo with stunning city views and premium finishes.",
                PropertyType.Townhouse => "Charming townhouse in desirable neighborhood with easy access to transit.",
                PropertyType.Multi_Family => "Investment opportunity with multiple units and strong rental income.",
                PropertyType.Land => "Prime development opportunity in growing area.",
                PropertyType.Commercial => "Commercial property with high visibility and excellent location.",
                _ => "Exceptional property with great potential."
            };
        }
    }
}
```

**Register the service in `Program.cs`:**

```csharp
builder.Services.AddScoped<IPropertyService, PropertyService>();
```

---

## Step 3: Build the Dashboard Layout

Create `Pages/PropertyDashboard.razor`:

```razor
@page "/properties"
@using PropertyDashboard.Models
@using PropertyDashboard.Services
@using Honua.MapSDK.Components
@inject IPropertyService PropertyService
@inject ISnackbar Snackbar

<PageTitle>Property Management Dashboard</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-0" Style="height: 100vh;">
    <!-- Header -->
    <MudPaper Elevation="4" Class="pa-3 mb-2">
        <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
            <MudStack Row="true" Spacing="2" AlignItems="AlignItems.Center">
                <MudIcon Icon="@Icons.Material.Filled.Home" Size="Size.Large" Color="Color.Primary" />
                <MudText Typo="Typo.h4">Property Management Dashboard</MudText>
            </MudStack>
            <MudStack Row="true" Spacing="2">
                <MudButton Variant="Variant.Filled"
                           Color="Color.Primary"
                           StartIcon="@Icons.Material.Filled.FilterList"
                           OnClick="@(() => _showFilters = !_showFilters)">
                    @(_showFilters ? "Hide" : "Show") Filters
                </MudButton>
                <MudButton Variant="Variant.Filled"
                           Color="Color.Secondary"
                           StartIcon="@Icons.Material.Filled.FileDownload"
                           OnClick="@ShowExportDialog">
                    Export
                </MudButton>
                <MudButton Variant="Variant.Outlined"
                           StartIcon="@Icons.Material.Filled.Refresh"
                           OnClick="@RefreshData">
                    Refresh
                </MudButton>
            </MudStack>
        </MudStack>
    </MudPaper>

    <!-- Statistics Cards -->
    <MudGrid Class="mb-2 px-3">
        <MudItem xs="12" sm="6" md="3">
            <MudCard Elevation="2">
                <MudCardContent>
                    <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
                        <div>
                            <MudText Typo="Typo.body2" Color="Color.Secondary">Total Properties</MudText>
                            <MudText Typo="Typo.h4">@_statistics.TotalProperties</MudText>
                        </div>
                        <MudIcon Icon="@Icons.Material.Filled.Home" Size="Size.Large" Color="Color.Primary" />
                    </MudStack>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard Elevation="2">
                <MudCardContent>
                    <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
                        <div>
                            <MudText Typo="Typo.body2" Color="Color.Secondary">Average Price</MudText>
                            <MudText Typo="Typo.h4">@_statistics.AveragePrice.ToString("C0")</MudText>
                        </div>
                        <MudIcon Icon="@Icons.Material.Filled.AttachMoney" Size="Size.Large" Color="Color.Success" />
                    </MudStack>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard Elevation="2">
                <MudCardContent>
                    <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
                        <div>
                            <MudText Typo="Typo.body2" Color="Color.Secondary">Active Listings</MudText>
                            <MudText Typo="Typo.h4">@_statistics.ActiveListings</MudText>
                        </div>
                        <MudIcon Icon="@Icons.Material.Filled.CheckCircle" Size="Size.Large" Color="Color.Info" />
                    </MudStack>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard Elevation="2">
                <MudCardContent>
                    <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
                        <div>
                            <MudText Typo="Typo.body2" Color="Color.Secondary">Sold</MudText>
                            <MudText Typo="Typo.h4">@_statistics.SoldListings</MudText>
                        </div>
                        <MudIcon Icon="@Icons.Material.Filled.Sell" Size="Size.Large" Color="Color.Warning" />
                    </MudStack>
                </MudCardContent>
            </MudCard>
        </MudItem>
    </MudGrid>

    <!-- Main Content Area -->
    <MudGrid Class="px-3" Style="height: calc(100vh - 280px);">
        <!-- Map Section -->
        <MudItem xs="12" md="7" Style="height: 100%;">
            <MudPaper Elevation="3" Style="height: 100%; position: relative;">
                @if (_isLoading)
                {
                    <div class="loading-overlay">
                        <MudProgressCircular Indeterminate="true" Size="Size.Large" Color="Color.Primary" />
                        <MudText Typo="Typo.h6" Class="mt-3">Loading properties...</MudText>
                    </div>
                }
                else
                {
                    <!-- Map will be added in Step 4 -->
                    <div style="height: 100%; display: flex; align-items: center; justify-content: center;">
                        <MudText Typo="Typo.h5" Color="Color.Secondary">Map will be added here</MudText>
                    </div>
                }
            </MudPaper>
        </MudItem>

        <!-- Data Grid Section -->
        <MudItem xs="12" md="5" Style="height: 100%;">
            <MudPaper Elevation="3" Style="height: 100%; display: flex; flex-direction: column;">
                <!-- Data Grid will be added in Step 7 -->
                <div style="flex: 1; display: flex; align-items: center; justify-content: center;">
                    <MudText Typo="Typo.h5" Color="Color.Secondary">Data Grid will be added here</MudText>
                </div>
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

<style>
    .loading-overlay {
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        background: rgba(255, 255, 255, 0.9);
        z-index: 1000;
    }
</style>

@code {
    private List<Property> _allProperties = new();
    private List<Property> _filteredProperties = new();
    private PropertyStatistics _statistics = new();
    private PropertyFilter _filter = new();
    private bool _isLoading = true;
    private bool _showFilters = false;
    private Property? _selectedProperty;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        _allProperties = await PropertyService.GetPropertiesAsync();
        _filteredProperties = _allProperties;
        _statistics = await PropertyService.GetStatisticsAsync(_filteredProperties);
        _isLoading = false;
    }

    private async Task RefreshData()
    {
        await LoadData();
        Snackbar.Add("Data refreshed successfully", Severity.Success);
    }

    private void ShowExportDialog()
    {
        // Export dialog will be implemented in Step 9
        Snackbar.Add("Export functionality will be added in Step 9", Severity.Info);
    }
}
```

**What you just did:**
- Created the dashboard layout with header and statistics
- Added action buttons for filter, export, and refresh
- Created statistics cards showing key metrics
- Set up a responsive grid layout for map and data sections

---

## Step 4: Add Map with Parcel Layer

Replace the map placeholder with a real map:

```razor
<!-- Replace the map placeholder div with this -->
<HonuaMap @ref="_map"
          Id="property-map"
          Center="@(new[] { -122.4194, 37.7749 })"
          Zoom="10"
          MapStyle="https://demotiles.maplibre.org/style.json"
          OnMapReady="@HandleMapReady"
          OnFeatureClicked="@HandleFeatureClicked"
          Style="height: 100%; width: 100%;" />

<!-- Layer Controls Overlay -->
<div style="position: absolute; top: 10px; right: 10px; z-index: 1000;">
    <HonuaLayerList SyncWith="property-map"
                    Collapsible="true"
                    ShowOpacity="true" />
</div>

<!-- Basemap Switcher -->
<div style="position: absolute; bottom: 10px; right: 10px; z-index: 1000;">
    <HonuaBasemapGallery SyncWith="property-map"
                         ShowThumbnails="true" />
</div>
```

**Add code to render properties as GeoJSON:**

```csharp
@code {
    private HonuaMap? _map;
    private string _propertiesGeoJson = string.Empty;

    private async Task HandleMapReady(MapReadyMessage message)
    {
        Console.WriteLine($"Map ready: {message.MapId}");
        await RenderPropertiesOnMap();
    }

    private async Task RenderPropertiesOnMap()
    {
        if (_map == null) return;

        // Convert properties to GeoJSON
        _propertiesGeoJson = await PropertyService.ExportToGeoJsonAsync(_filteredProperties);

        // Add source and layers to map
        await _map.AddSourceAsync("properties", new
        {
            type = "geojson",
            data = System.Text.Json.JsonSerializer.Deserialize<object>(_propertiesGeoJson)
        });

        // Add circle layer for properties
        await _map.AddLayerAsync(new
        {
            id = "property-points",
            type = "circle",
            source = "properties",
            paint = new
            {
                circle_radius = new object[] { "interpolate", new[] { "linear" }, new[] { "zoom" },
                    10, 6,
                    15, 12
                },
                circle_color = new object[] { "match", new[] { "get", "status" },
                    "Active", "#4CAF50",
                    "Pending", "#FF9800",
                    "Sold", "#9E9E9E",
                    "#2196F3"
                },
                circle_opacity = 0.8,
                circle_stroke_width = 2,
                circle_stroke_color = "#FFFFFF"
            }
        });

        // Add labels for high zoom levels
        await _map.AddLayerAsync(new
        {
            id = "property-labels",
            type = "symbol",
            source = "properties",
            minzoom = 14,
            layout = new
            {
                text_field = new[] { "get", "price" },
                text_size = 12,
                text_offset = new[] { 0, 1.5 }
            },
            paint = new
            {
                text_color = "#000000",
                text_halo_color = "#FFFFFF",
                text_halo_width = 2
            }
        });
    }

    private async Task HandleFeatureClicked(FeatureClickedMessage message)
    {
        if (message.Properties.ContainsKey("id"))
        {
            var propertyId = message.Properties["id"].ToString();
            _selectedProperty = await PropertyService.GetPropertyByIdAsync(propertyId!);
            StateHasChanged();
        }
    }
}
```

---

## Step 5: Add Layer List

The layer list was already added in Step 4. Here's how to customize it:

```razor
<HonuaLayerList SyncWith="property-map"
                Collapsible="true"
                ShowOpacity="true"
                ShowGroups="true"
                Position="top-right">
    <LayerGroup Name="Properties">
        <LayerItem LayerId="property-points" Name="Property Locations" Visible="true" />
        <LayerItem LayerId="property-labels" Name="Price Labels" Visible="true" />
    </LayerGroup>
    <LayerGroup Name="Basemaps">
        <LayerItem LayerId="background" Name="Background" Visible="true" />
    </LayerGroup>
</HonuaLayerList>
```

---

## Step 6: Add Popup for Property Info

Add a popup component that shows property details:

```razor
<!-- Add this inside the map container -->
<HonuaPopup SyncWith="property-map"
            ShowOnClick="true"
            Position="top"
            CloseButton="true"
            MaxWidth="400px">
    @if (_selectedProperty != null)
    {
        <MudCard>
            @if (!string.IsNullOrEmpty(_selectedProperty.ImageUrl))
            {
                <MudCardMedia Image="@_selectedProperty.ImageUrl" Height="200" />
            }
            <MudCardContent>
                <MudText Typo="Typo.h6">@_selectedProperty.Address</MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-2">
                    @_selectedProperty.City, @_selectedProperty.State @_selectedProperty.ZipCode
                </MudText>
                <MudDivider Class="my-2" />
                <MudGrid Spacing="1">
                    <MudItem xs="6">
                        <MudText Typo="Typo.body2"><strong>Price:</strong></MudText>
                        <MudText Typo="Typo.h6" Color="Color.Success">@_selectedProperty.FormattedPrice</MudText>
                    </MudItem>
                    <MudItem xs="6">
                        <MudText Typo="Typo.body2"><strong>Status:</strong></MudText>
                        <MudChip Size="Size.Small" Color="@GetStatusColor(_selectedProperty.Status)">
                            @_selectedProperty.StatusDisplay
                        </MudChip>
                    </MudItem>
                    <MudItem xs="4">
                        <MudText Typo="Typo.caption" Color="Color.Secondary">Bedrooms</MudText>
                        <MudText Typo="Typo.body1">@_selectedProperty.Bedrooms</MudText>
                    </MudItem>
                    <MudItem xs="4">
                        <MudText Typo="Typo.caption" Color="Color.Secondary">Bathrooms</MudText>
                        <MudText Typo="Typo.body1">@_selectedProperty.Bathrooms</MudText>
                    </MudItem>
                    <MudItem xs="4">
                        <MudText Typo="Typo.caption" Color="Color.Secondary">Sq Ft</MudText>
                        <MudText Typo="Typo.body1">@_selectedProperty.SquareFeet.ToString("N0")</MudText>
                    </MudItem>
                </MudGrid>
                <MudText Typo="Typo.body2" Class="mt-2">@_selectedProperty.Description</MudText>
            </MudCardContent>
            <MudCardActions>
                <MudButton Variant="Variant.Text" Color="Color.Primary">View Details</MudButton>
                <MudButton Variant="Variant.Text" Color="Color.Secondary">Schedule Tour</MudButton>
            </MudCardActions>
        </MudCard>
    }
</HonuaPopup>
```

```csharp
@code {
    private Color GetStatusColor(PropertyStatus status)
    {
        return status switch
        {
            PropertyStatus.Active => Color.Success,
            PropertyStatus.Pending => Color.Warning,
            PropertyStatus.Sold => Color.Default,
            _ => Color.Info
        };
    }
}
```

---

## Step 7: Add Attribute Table

Replace the data grid placeholder:

```razor
<HonuaAttributeTable TItem="Property"
                     Items="@_filteredProperties"
                     SyncWith="property-map"
                     Dense="true"
                     Hover="true"
                     Striped="true"
                     Height="100%"
                     OnRowClick="@HandleRowClick">
    <Columns>
        <PropertyColumn Property="p => p.Address" Title="Address" />
        <PropertyColumn Property="p => p.City" Title="City" />
        <PropertyColumn Property="p => p.FormattedPrice" Title="Price" />
        <PropertyColumn Property="p => p.PropertyTypeDisplay" Title="Type" />
        <PropertyColumn Property="p => p.StatusDisplay" Title="Status">
            <CellTemplate>
                <MudChip Size="Size.Small" Color="@GetStatusColor(context.Status)">
                    @context.StatusDisplay
                </MudChip>
            </CellTemplate>
        </PropertyColumn>
        <PropertyColumn Property="p => p.Bedrooms" Title="Beds" />
        <PropertyColumn Property="p => p.Bathrooms" Title="Baths" />
        <PropertyColumn Property="p => p.SquareFeet" Title="Sq Ft" Format="N0" />
    </Columns>
</HonuaAttributeTable>
```

```csharp
@code {
    private async Task HandleRowClick(Property property)
    {
        _selectedProperty = property;

        // Fly to property on map
        if (_map != null)
        {
            await _map.FlyToAsync(new[] { property.Longitude, property.Latitude }, 16);
        }
    }
}
```

---

## Step 8: Add Filtering

Add a filter panel:

```razor
<!-- Add after the header -->
@if (_showFilters)
{
    <MudPaper Elevation="2" Class="pa-3 mb-2 mx-3">
        <MudText Typo="Typo.h6" Class="mb-3">Filters</MudText>
        <MudGrid>
            <MudItem xs="12" sm="6" md="3">
                <MudNumericField @bind-Value="_filter.MinPrice"
                                 Label="Min Price"
                                 Format="C0"
                                 Variant="Variant.Outlined"
                                 Adornment="Adornment.Start"
                                 AdornmentIcon="@Icons.Material.Filled.AttachMoney" />
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudNumericField @bind-Value="_filter.MaxPrice"
                                 Label="Max Price"
                                 Format="C0"
                                 Variant="Variant.Outlined"
                                 Adornment="Adornment.Start"
                                 AdornmentIcon="@Icons.Material.Filled.AttachMoney" />
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudSelect @bind-Value="_filter.PropertyType"
                           Label="Property Type"
                           Variant="Variant.Outlined"
                           Clearable="true">
                    @foreach (PropertyType type in Enum.GetValues(typeof(PropertyType)))
                    {
                        <MudSelectItem Value="@type">@type.ToString().Replace("_", " ")</MudSelectItem>
                    }
                </MudSelect>
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudSelect @bind-Value="_filter.Status"
                           Label="Status"
                           Variant="Variant.Outlined"
                           Clearable="true">
                    @foreach (PropertyStatus status in Enum.GetValues(typeof(PropertyStatus)))
                    {
                        <MudSelectItem Value="@status">@status.ToString()</MudSelectItem>
                    }
                </MudSelect>
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudNumericField @bind-Value="_filter.MinBedrooms"
                                 Label="Min Bedrooms"
                                 Variant="Variant.Outlined" />
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudNumericField @bind-Value="_filter.MinBathrooms"
                                 Label="Min Bathrooms"
                                 Variant="Variant.Outlined" />
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudNumericField @bind-Value="_filter.MinSquareFeet"
                                 Label="Min Square Feet"
                                 Variant="Variant.Outlined" />
            </MudItem>
            <MudItem xs="12" sm="6" md="3">
                <MudTextField @bind-Value="_filter.City"
                              Label="City"
                              Variant="Variant.Outlined" />
            </MudItem>
        </MudGrid>
        <MudStack Row="true" Class="mt-3" Justify="Justify.FlexEnd">
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.Search"
                       OnClick="@ApplyFilters">
                Apply Filters
            </MudButton>
            <MudButton Variant="Variant.Outlined"
                       StartIcon="@Icons.Material.Filled.Clear"
                       OnClick="@ClearFilters">
                Clear All
            </MudButton>
        </MudStack>
    </MudPaper>
}
```

```csharp
@code {
    private async Task ApplyFilters()
    {
        _filteredProperties = await PropertyService.FilterPropertiesAsync(_allProperties, _filter);
        _statistics = await PropertyService.GetStatisticsAsync(_filteredProperties);
        await RenderPropertiesOnMap();
        Snackbar.Add($"Filters applied: {_filteredProperties.Count} properties found", Severity.Success);
    }

    private async Task ClearFilters()
    {
        _filter = new PropertyFilter();
        _filteredProperties = _allProperties;
        _statistics = await PropertyService.GetStatisticsAsync(_filteredProperties);
        await RenderPropertiesOnMap();
        Snackbar.Add("Filters cleared", Severity.Info);
    }
}
```

---

## Step 9: Add Export Functionality

Add export dialog and methods:

```razor
<!-- Add export dialog -->
<MudDialog @bind-IsVisible="_showExportDialog" Options="@_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.FileDownload" Class="mr-3" /> Export Properties
        </MudText>
    </TitleContent>
    <DialogContent>
        <MudText Class="mb-3">
            Export @_filteredProperties.Count properties in your preferred format:
        </MudText>
        <MudRadioGroup @bind-SelectedOption="_exportFormat">
            <MudRadio Option="@("csv")" Color="Color.Primary">
                <MudText><strong>CSV</strong> - Comma-separated values for Excel</MudText>
            </MudRadio>
            <MudRadio Option="@("geojson")" Color="Color.Primary">
                <MudText><strong>GeoJSON</strong> - Geographic data format for GIS</MudText>
            </MudRadio>
            <MudRadio Option="@("json")" Color="Color.Primary">
                <MudText><strong>JSON</strong> - Structured data format</MudText>
            </MudRadio>
        </MudRadioGroup>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showExportDialog = false)">Cancel</MudButton>
        <MudButton Color="Color.Primary"
                   Variant="Variant.Filled"
                   StartIcon="@Icons.Material.Filled.Download"
                   OnClick="@ExportData">
            Export
        </MudButton>
    </DialogActions>
</MudDialog>
```

```csharp
@code {
    private bool _showExportDialog = false;
    private string _exportFormat = "csv";
    private DialogOptions _dialogOptions = new() { MaxWidth = MaxWidth.Small, FullWidth = true };

    private void ShowExportDialog()
    {
        _showExportDialog = true;
    }

    private async Task ExportData()
    {
        string data;
        string filename;
        string mimeType;

        switch (_exportFormat)
        {
            case "csv":
                data = await PropertyService.ExportToCsvAsync(_filteredProperties);
                filename = $"properties_{DateTime.Now:yyyyMMdd}.csv";
                mimeType = "text/csv";
                break;
            case "geojson":
                data = await PropertyService.ExportToGeoJsonAsync(_filteredProperties);
                filename = $"properties_{DateTime.Now:yyyyMMdd}.geojson";
                mimeType = "application/geo+json";
                break;
            case "json":
                data = System.Text.Json.JsonSerializer.Serialize(_filteredProperties,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                filename = $"properties_{DateTime.Now:yyyyMMdd}.json";
                mimeType = "application/json";
                break;
            default:
                return;
        }

        // Trigger download
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        var base64 = Convert.ToBase64String(bytes);
        var dataUrl = $"data:{mimeType};base64,{base64}";

        await JS.InvokeVoidAsync("downloadFile", filename, dataUrl);

        _showExportDialog = false;
        Snackbar.Add($"Exported {_filteredProperties.Count} properties as {_exportFormat.ToUpper()}", Severity.Success);
    }
}
```

**Add JavaScript function to `wwwroot/index.html` or `_Host.cshtml`:**

```javascript
<script>
    window.downloadFile = function(filename, dataUrl) {
        const link = document.createElement('a');
        link.download = filename;
        link.href = dataUrl;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    };
</script>
```

---

## Complete Working Example

See the full consolidated code at: [Complete PropertyDashboard.razor](https://github.com/honua-io/Honua.Server/examples/PropertyDashboard.razor)

**To run the complete example:**

1. Create all the files as shown above
2. Register services in `Program.cs`
3. Run the application: `dotnet run`
4. Navigate to `/properties`

---

## What You Learned

In this tutorial, you built a production-ready property management dashboard with:

✅ **Data Models** - Comprehensive property models with enums and filters
✅ **Service Layer** - PropertyService with filtering, statistics, and export
✅ **Interactive Map** - Properties displayed with color-coded status
✅ **Layer Controls** - Toggle visibility and adjust opacity
✅ **Popups** - Rich property details on click
✅ **Attribute Table** - Sortable, filterable data grid
✅ **Advanced Filtering** - Multiple filter criteria
✅ **Export** - CSV, JSON, and GeoJSON export
✅ **Statistics** - Real-time metrics and aggregations
✅ **Responsive Layout** - Works on desktop and mobile

---

## Next Steps

### Enhance the Dashboard

1. **Add Charts**
   ```razor
   <HonuaChart Type="ChartType.Bar"
               Field="propertyType"
               SyncWith="property-map" />
   ```

2. **Add Timeline** for market trends
3. **Implement Real-time Updates** with SignalR
4. **Add User Authentication** and favorites
5. **Connect to Real Database** (SQL Server, PostgreSQL)

### Continue Learning

- 📖 [Tutorial 03: Environmental Monitoring](Tutorial_03_EnvironmentalMonitoring.md)
- 📖 [Advanced Filtering Guide](../guides/advanced-filtering.md)
- 📖 [Performance Optimization](../guides/PerformanceOptimization.md)

---

**Congratulations!** You've built a sophisticated property management dashboard!

**Tutorial Duration**: 45 minutes
**Lines of Code**: ~1,000
**Components Used**: HonuaMap, HonuaLayerList, HonuaBasemapGallery, HonuaPopup, HonuaAttributeTable
**Difficulty**: Intermediate

---

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*

# HonuaSearch Examples

Comprehensive examples demonstrating various use cases for the HonuaSearch component.

## Table of Contents

- [Basic Examples](#basic-examples)
- [Provider Configuration](#provider-configuration)
- [Layout Examples](#layout-examples)
- [Event Handling](#event-handling)
- [Integration Examples](#integration-examples)
- [Advanced Examples](#advanced-examples)
- [Full Application Examples](#full-application-examples)

---

## Basic Examples

### 1. Minimal Setup

The simplest possible search component using free Nominatim:

```razor
@page "/search-basic"

<HonuaMap Id="map1" />
<HonuaSearch SyncWith="map1" />
```

### 2. With Custom Placeholder

```razor
<HonuaSearch SyncWith="map1"
             Placeholder="Where would you like to go?" />
```

### 3. Compact Dense Mode

```razor
<HonuaSearch SyncWith="map1"
             Dense="true"
             Width="300px" />
```

### 4. Without Recent Searches

```razor
<HonuaSearch SyncWith="map1"
             ShowRecentSearches="false" />
```

### 5. Search Only (No Geolocation)

```razor
<HonuaSearch SyncWith="map1"
             ShowLocateButton="false" />
```

---

## Provider Configuration

### 1. Nominatim (Free - Default)

```razor
@page "/search-nominatim"

<HonuaMap Id="map1" />

<HonuaSearch SyncWith="map1"
             Provider="GeocodeProvider.Nominatim"
             Placeholder="Search with OpenStreetMap..." />
```

### 2. Mapbox Geocoding

```razor
@page "/search-mapbox"
@inject IConfiguration Config

<HonuaMap Id="map1" />

<HonuaSearch SyncWith="map1"
             Provider="GeocodeProvider.Mapbox"
             ApiKey="@Config["Mapbox:ApiKey"]"
             Placeholder="Search with Mapbox..."
             MaxResults="15" />
```

**appsettings.json:**
```json
{
  "Mapbox": {
    "ApiKey": "pk.your-mapbox-api-key-here"
  }
}
```

### 3. Custom Geocoding Provider

```razor
@page "/search-custom"

<HonuaMap Id="map1" />

<HonuaSearch SyncWith="map1"
             Provider="GeocodeProvider.Custom"
             CustomGeocoder="@_customGeocoder" />

@code {
    private MyCustomGeocoder _customGeocoder = new();
}
```

**MyCustomGeocoder.cs:**
```csharp
using Honua.MapSDK.Services.Geocoding;

public class MyCustomGeocoder : IGeocoder
{
    public string ProviderName => "My Internal Database";

    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        // Search your internal database or API
        var results = await _database.SearchLocations(query, limit);

        return results.Select(r => new SearchResult
        {
            Id = r.Id,
            DisplayName = r.Name,
            Latitude = r.Lat,
            Longitude = r.Lon,
            Type = "custom",
            Category = SearchResultCategory.PointOfInterest
        }).ToList();
    }

    public async Task<SearchResult?> ReverseGeocodeAsync(double lat, double lon, CancellationToken cancellationToken = default)
    {
        var result = await _database.FindNearest(lat, lon);
        if (result == null) return null;

        return new SearchResult
        {
            Id = result.Id,
            DisplayName = result.Name,
            Latitude = result.Lat,
            Longitude = result.Lon,
            Type = "custom"
        };
    }

    private InternalDatabase _database = new();
}
```

---

## Layout Examples

### 1. Floating Top-Left

```razor
<div style="position: relative; height: 100vh;">
    <HonuaMap Id="map1" />

    <HonuaSearch SyncWith="map1"
                 Position="top-left"
                 Width="400px" />
</div>
```

### 2. Floating Top-Right

```razor
<div style="position: relative; height: 100vh;">
    <HonuaMap Id="map1" />

    <HonuaSearch SyncWith="map1"
                 Position="top-right"
                 Width="350px" />
</div>
```

### 3. Floating Bottom-Right (with Legend)

```razor
<div style="position: relative; height: 100vh;">
    <HonuaMap Id="map1" />

    <HonuaLegend SyncWith="map1" Position="top-right" />

    <HonuaSearch SyncWith="map1"
                 Position="bottom-right"
                 Width="350px" />
</div>
```

### 4. Embedded in Sidebar

```razor
<MudLayout>
    <MudDrawer Open="true" Variant="DrawerVariant.Persistent" Width="350px">
        <MudPaper Class="pa-4" Elevation="0">
            <MudText Typo="Typo.h6" Class="mb-4">Search & Filter</MudText>

            <HonuaSearch SyncWith="map1"
                         Position="@null"
                         Width="100%"
                         Variant="Variant.Filled" />

            <MudDivider Class="my-4" />

            <HonuaFilterPanel SyncWith="map1" />
        </MudPaper>
    </MudDrawer>

    <MudMainContent>
        <HonuaMap Id="map1" Style="height: 100vh;" />
    </MudMainContent>
</MudLayout>
```

### 5. In Toolbar

```razor
<MudAppBar>
    <MudText Typo="Typo.h6">My Geospatial Dashboard</MudText>
    <MudSpacer />

    <div style="width: 400px; margin-right: 16px;">
        <HonuaSearch SyncWith="map1"
                     Position="@null"
                     Width="100%"
                     Dense="true"
                     Variant="Variant.Outlined" />
    </div>

    <MudIconButton Icon="@Icons.Material.Filled.Settings" Color="Color.Inherit" />
</MudAppBar>

<HonuaMap Id="map1" Style="height: calc(100vh - 64px);" />
```

---

## Event Handling

### 1. Handle Result Selection

```razor
<HonuaSearch SyncWith="map1"
             OnResultSelected="@OnSearchResultSelected" />

<MudText>Selected: @_selectedLocation</MudText>

@code {
    private string _selectedLocation = "None";

    private void OnSearchResultSelected(SearchResult result)
    {
        _selectedLocation = result.DisplayName;
        Console.WriteLine($"User searched for: {result.DisplayName} ({result.Latitude}, {result.Longitude})");
    }
}
```

### 2. Handle Geolocation

```razor
<HonuaSearch SyncWith="map1"
             ShowLocateButton="true"
             OnLocationFound="@OnUserLocationFound" />

<MudAlert Severity="Severity.Info" Class="mt-4">
    @_locationMessage
</MudAlert>

@code {
    private string _locationMessage = "Click 'Locate Me' to find your position";

    private void OnUserLocationFound((double Lat, double Lon) location)
    {
        _locationMessage = $"Your location: {location.Lat:F6}, {location.Lon:F6}";
        Console.WriteLine($"User location obtained: {location.Lat}, {location.Lon}");
    }
}
```

### 3. Listen to ComponentBus Messages

```razor
@inject ComponentBus Bus

<HonuaSearch Id="search1" SyncWith="map1" />

@code {
    protected override void OnInitialized()
    {
        Bus.Subscribe<SearchResultSelectedMessage>(async args =>
        {
            var msg = args.Message;
            Console.WriteLine($"Search: {msg.DisplayName}");
            Console.WriteLine($"Type: {msg.Type}");
            Console.WriteLine($"Coords: {msg.Latitude}, {msg.Longitude}");

            // Do something with the search result
            await ProcessSearchResult(msg);
        });
    }

    private async Task ProcessSearchResult(SearchResultSelectedMessage result)
    {
        // Analytics tracking
        await _analytics.TrackSearch(result.DisplayName);

        // Load data for selected location
        await LoadLocationData(result.Latitude, result.Longitude);
    }
}
```

---

## Integration Examples

### 1. Search + Map + DataGrid

```razor
@page "/search-grid-map"
@inject ComponentBus Bus

<MudGrid>
    <MudItem xs="12">
        <HonuaSearch SyncWith="map1" Position="@null" Width="100%" />
    </MudItem>

    <MudItem xs="12" md="8">
        <HonuaMap Id="map1" Height="600px" />
    </MudItem>

    <MudItem xs="12" md="4">
        <HonuaDataGrid SyncWith="map1"
                       Data="@_features"
                       Height="600px" />
    </MudItem>
</MudGrid>

@code {
    private List<Feature> _features = new();

    protected override void OnInitialized()
    {
        Bus.Subscribe<SearchResultSelectedMessage>(async args =>
        {
            // Load features near search result
            _features = await LoadFeaturesNear(
                args.Message.Latitude,
                args.Message.Longitude,
                5000 // 5km radius
            );
            StateHasChanged();
        });
    }
}
```

### 2. Search + Map + Chart

```razor
@page "/search-chart-map"

<MudGrid>
    <MudItem xs="12">
        <MudPaper Class="pa-4">
            <HonuaSearch SyncWith="map1" Position="@null" Width="100%" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="8">
        <HonuaMap Id="map1" Height="500px" />
    </MudItem>

    <MudItem xs="12" md="4">
        <HonuaChart SyncWith="map1"
                    ChartType="ChartType.Bar"
                    Title="Statistics by Region" />
    </MudItem>
</MudGrid>
```

### 3. Multi-Map Search

```razor
@page "/multi-map-search"

<MudGrid>
    <MudItem xs="12">
        <HonuaSearch SyncWith="@null"
                     OnResultSelected="@SyncBothMaps" />
    </MudItem>

    <MudItem xs="12" md="6">
        <MudText Typo="Typo.h6">Street View</MudText>
        <HonuaMap Id="map1" Height="500px" />
    </MudItem>

    <MudItem xs="12" md="6">
        <MudText Typo="Typo.h6">Satellite View</MudText>
        <HonuaMap Id="map2" Height="500px" MapStyle="satellite" />
    </MudItem>
</MudGrid>

@code {
    [Inject] ComponentBus Bus { get; set; } = default!;

    private async Task SyncBothMaps(SearchResult result)
    {
        // Fly both maps to the same location
        var flyToMessage = new FlyToRequestMessage
        {
            MapId = "map1",
            Center = new[] { result.Longitude, result.Latitude },
            Zoom = 15
        };

        await Bus.PublishAsync(flyToMessage with { MapId = "map1" });
        await Bus.PublishAsync(flyToMessage with { MapId = "map2" });
    }
}
```

### 4. Search with Filter Panel

```razor
@page "/search-filter"

<MudDrawer Open="true" Width="350px">
    <MudPaper Class="pa-4" Elevation="0">
        <HonuaSearch SyncWith="map1" Position="@null" Width="100%" />

        <MudDivider Class="my-4" />

        <HonuaFilterPanel SyncWith="map1" />

        <MudDivider Class="my-4" />

        <HonuaLegend SyncWith="map1" Position="@null" />
    </MudPaper>
</MudDrawer>

<MudMainContent>
    <HonuaMap Id="map1" Style="height: 100vh;" />
</MudMainContent>
```

---

## Advanced Examples

### 1. Search with Result Preview

```razor
<HonuaSearch SyncWith="map1"
             OnResultSelected="@ShowResultPreview" />

@if (_previewResult != null)
{
    <MudCard Class="mt-4">
        <MudCardHeader>
            <CardHeaderAvatar>
                <MudIcon Icon="@Icons.Material.Filled.LocationOn" />
            </CardHeaderAvatar>
            <CardHeaderContent>
                <MudText Typo="Typo.h6">@_previewResult.DisplayName</MudText>
                <MudText Typo="Typo.body2">@_previewResult.Type</MudText>
            </CardHeaderContent>
        </MudCardHeader>
        <MudCardContent>
            <MudText><strong>Coordinates:</strong> @_previewResult.Latitude, @_previewResult.Longitude</MudText>
            @if (!string.IsNullOrEmpty(_previewResult.City))
            {
                <MudText><strong>City:</strong> @_previewResult.City</MudText>
            }
            @if (!string.IsNullOrEmpty(_previewResult.Country))
            {
                <MudText><strong>Country:</strong> @_previewResult.Country</MudText>
            }
        </MudCardContent>
        <MudCardActions>
            <MudButton StartIcon="@Icons.Material.Filled.Directions" Color="Color.Primary">
                Get Directions
            </MudButton>
            <MudButton StartIcon="@Icons.Material.Filled.Share">
                Share
            </MudButton>
        </MudCardActions>
    </MudCard>
}

@code {
    private SearchResult? _previewResult;

    private void ShowResultPreview(SearchResult result)
    {
        _previewResult = result;
    }
}
```

### 2. Search History Panel

```razor
@inject IJSRuntime JS

<HonuaSearch Id="search1" SyncWith="map1" />

<MudPaper Class="pa-4 mt-4">
    <MudText Typo="Typo.h6">Search History</MudText>
    <MudList>
        @foreach (var search in _searchHistory)
        {
            <MudListItem OnClick="@(() => ReplaySearch(search))">
                <div class="d-flex justify-space-between align-center">
                    <div>
                        <MudText>@search.DisplayName</MudText>
                        <MudText Typo="Typo.caption">@search.Timestamp.ToLocalTime()</MudText>
                    </div>
                    <MudIconButton Icon="@Icons.Material.Filled.Replay" Size="Size.Small" />
                </div>
            </MudListItem>
        }
    </MudList>
</MudPaper>

@code {
    [Inject] ComponentBus Bus { get; set; } = default!;
    private List<SearchHistoryItem> _searchHistory = new();

    protected override void OnInitialized()
    {
        Bus.Subscribe<SearchResultSelectedMessage>(args =>
        {
            _searchHistory.Insert(0, new SearchHistoryItem
            {
                DisplayName = args.Message.DisplayName,
                Latitude = args.Message.Latitude,
                Longitude = args.Message.Longitude,
                Timestamp = DateTime.Now
            });

            // Keep last 20
            if (_searchHistory.Count > 20)
                _searchHistory = _searchHistory.Take(20).ToList();

            InvokeAsync(StateHasChanged);
        });
    }

    private async Task ReplaySearch(SearchHistoryItem item)
    {
        await Bus.PublishAsync(new FlyToRequestMessage
        {
            MapId = "map1",
            Center = new[] { item.Longitude, item.Latitude },
            Zoom = 14
        });
    }

    private class SearchHistoryItem
    {
        public string DisplayName { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
```

### 3. Search with Auto-Complete Suggestions

```razor
<MudAutocomplete T="string"
                 Label="Quick Search"
                 SearchFunc="@GetQuickSearchSuggestions"
                 ValueChanged="@SearchLocation"
                 Immediate="true"
                 ResetValueOnEmptyText="true" />

<HonuaSearch Id="mainSearch" SyncWith="map1" />

@code {
    private readonly string[] _popularLocations = new[]
    {
        "New York, USA",
        "London, UK",
        "Tokyo, Japan",
        "Paris, France",
        "Sydney, Australia"
    };

    private async Task<IEnumerable<string>> GetQuickSearchSuggestions(string value)
    {
        await Task.Delay(100); // Simulate search

        if (string.IsNullOrEmpty(value))
            return _popularLocations;

        return _popularLocations.Where(x =>
            x.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SearchLocation(string location)
    {
        // Trigger search component programmatically
        // This would require adding a public SearchAsync method to HonuaSearch
    }
}
```

### 4. Bounded Search (Search Within Area)

```razor
@inject ComponentBus Bus

<MudSwitch @bind-Checked="@_searchInViewport" Color="Color.Primary">
    Search only in current map view
</MudSwitch>

<HonuaSearch SyncWith="map1" />

@code {
    private bool _searchInViewport = false;
    private double[]? _currentBounds;

    protected override void OnInitialized()
    {
        Bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            if (args.Message.MapId == "map1")
            {
                _currentBounds = args.Message.Bounds;
            }
        });
    }

    // Note: Would require extending HonuaSearch to support bounded searches
}
```

---

## Full Application Examples

### 1. Real Estate Search Application

```razor
@page "/real-estate"
@inject ComponentBus Bus

<MudLayout>
    <MudAppBar>
        <MudText Typo="Typo.h5">Property Search</MudText>
        <MudSpacer />
        <div style="width: 400px; margin-right: 16px;">
            <HonuaSearch SyncWith="map1"
                         Position="@null"
                         Width="100%"
                         Dense="true"
                         Variant="Variant.Outlined"
                         Placeholder="Search neighborhoods, cities..."
                         OnResultSelected="@SearchProperties" />
        </div>
    </MudAppBar>

    <MudDrawer Open="true" Width="350px" Variant="DrawerVariant.Persistent">
        <MudPaper Class="pa-4" Elevation="0">
            <MudText Typo="Typo.h6" Class="mb-4">Filters</MudText>

            <MudNumericField @bind-Value="_minPrice" Label="Min Price" Variant="Variant.Outlined" />
            <MudNumericField @bind-Value="_maxPrice" Label="Max Price" Variant="Variant.Outlined" />
            <MudSlider @bind-Value="_bedrooms" Min="1" Max="5" Label="Bedrooms" />

            <MudButton Variant="Variant.Filled" Color="Color.Primary" FullWidth="true" OnClick="@ApplyFilters">
                Apply Filters
            </MudButton>
        </MudPaper>
    </MudDrawer>

    <MudMainContent>
        <div style="display: flex; height: calc(100vh - 64px);">
            <div style="flex: 1;">
                <HonuaMap Id="map1" Style="height: 100%;" />
            </div>
            <div style="width: 400px; overflow-y: auto; border-left: 1px solid #e0e0e0;">
                <MudList>
                    @foreach (var property in _properties)
                    {
                        <MudListItem OnClick="@(() => ShowProperty(property))">
                            <div class="d-flex">
                                <MudImage Src="@property.ImageUrl" Width="100" Height="75" ObjectFit="ObjectFit.Cover" Class="mr-3" />
                                <div>
                                    <MudText Typo="Typo.body1">@property.Address</MudText>
                                    <MudText Typo="Typo.h6">$@property.Price.ToString("N0")</MudText>
                                    <MudText Typo="Typo.caption">@property.Bedrooms bd | @property.Bathrooms ba</MudText>
                                </div>
                            </div>
                        </MudListItem>
                    }
                </MudList>
            </div>
        </div>
    </MudMainContent>
</MudLayout>

@code {
    private List<Property> _properties = new();
    private decimal _minPrice = 0;
    private decimal _maxPrice = 1000000;
    private int _bedrooms = 2;

    private async Task SearchProperties(SearchResult location)
    {
        // Search properties near the selected location
        _properties = await LoadPropertiesNear(location.Latitude, location.Longitude);
        StateHasChanged();
    }

    private async Task ApplyFilters()
    {
        // Apply filters to property search
    }

    private async Task ShowProperty(Property property)
    {
        await Bus.PublishAsync(new FlyToRequestMessage
        {
            MapId = "map1",
            Center = new[] { property.Longitude, property.Latitude },
            Zoom = 16
        });
    }

    private class Property
    {
        public string Address { get; set; } = "";
        public decimal Price { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string ImageUrl { get; set; } = "";
    }
}
```

### 2. Delivery Route Planner

```razor
@page "/delivery-planner"

<MudGrid>
    <MudItem xs="12">
        <MudPaper Class="pa-4">
            <MudText Typo="Typo.h5">Delivery Route Planner</MudText>
            <HonuaSearch SyncWith="map1"
                         Position="@null"
                         Width="400px"
                         Placeholder="Add delivery stop..."
                         OnResultSelected="@AddDeliveryStop" />
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="8">
        <HonuaMap Id="map1" Height="600px" />
    </MudItem>

    <MudItem xs="12" md="4">
        <MudPaper Class="pa-4">
            <MudText Typo="Typo.h6">Delivery Stops (@_stops.Count)</MudText>
            <MudList>
                @for (int i = 0; i < _stops.Count; i++)
                {
                    var index = i;
                    var stop = _stops[i];
                    <MudListItem>
                        <div class="d-flex justify-space-between align-center">
                            <div>
                                <MudText>@((index + 1)). @stop.DisplayName</MudText>
                                <MudText Typo="Typo.caption">@stop.Latitude, @stop.Longitude</MudText>
                            </div>
                            <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                          OnClick="@(() => RemoveStop(index))"
                                          Size="Size.Small" />
                        </div>
                    </MudListItem>
                }
            </MudList>

            @if (_stops.Count >= 2)
            {
                <MudButton Variant="Variant.Filled"
                          Color="Color.Primary"
                          FullWidth="true"
                          StartIcon="@Icons.Material.Filled.Route"
                          OnClick="@OptimizeRoute">
                    Optimize Route
                </MudButton>
            }
        </MudPaper>
    </MudItem>
</MudGrid>

@code {
    [Inject] ComponentBus Bus { get; set; } = default!;
    private List<SearchResult> _stops = new();

    private void AddDeliveryStop(SearchResult result)
    {
        _stops.Add(result);
        // Add marker to map at result location
    }

    private void RemoveStop(int index)
    {
        _stops.RemoveAt(index);
    }

    private async Task OptimizeRoute()
    {
        // Calculate optimal route through all stops
        var optimizedRoute = CalculateOptimalRoute(_stops);

        // Draw route on map
        await DrawRouteOnMap(optimizedRoute);
    }
}
```

### 3. Weather Dashboard

```razor
@page "/weather-dashboard"

<MudGrid>
    <MudItem xs="12">
        <MudPaper Class="pa-4">
            <div class="d-flex align-center gap-4">
                <HonuaSearch SyncWith="map1"
                             Position="@null"
                             Width="400px"
                             ShowLocateButton="true"
                             OnResultSelected="@LoadWeatherData"
                             OnLocationFound="@LoadWeatherForLocation"
                             Placeholder="Search location for weather..." />

                @if (_currentWeather != null)
                {
                    <div class="d-flex align-center gap-3">
                        <MudIcon Icon="@GetWeatherIcon(_currentWeather.Condition)" Size="Size.Large" />
                        <div>
                            <MudText Typo="Typo.h4">@_currentWeather.Temperature°C</MudText>
                            <MudText>@_currentWeather.Location</MudText>
                        </div>
                    </div>
                }
            </div>
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="8">
        <HonuaMap Id="map1" Height="500px" />
    </MudItem>

    <MudItem xs="12" md="4">
        <HonuaChart SyncWith="map1"
                    ChartType="ChartType.Line"
                    Title="7-Day Forecast"
                    Data="@_forecastData" />
    </MudItem>
</MudGrid>

@code {
    private WeatherData? _currentWeather;
    private List<ForecastData> _forecastData = new();

    private async Task LoadWeatherData(SearchResult result)
    {
        _currentWeather = await FetchWeather(result.Latitude, result.Longitude, result.DisplayName);
        _forecastData = await FetchForecast(result.Latitude, result.Longitude);
        StateHasChanged();
    }

    private async Task LoadWeatherForLocation((double Lat, double Lon) location)
    {
        _currentWeather = await FetchWeather(location.Lat, location.Lon, "Your Location");
        _forecastData = await FetchForecast(location.Lat, location.Lon);
        StateHasChanged();
    }

    private class WeatherData
    {
        public string Location { get; set; } = "";
        public double Temperature { get; set; }
        public string Condition { get; set; } = "";
    }
}
```

---

## Testing Examples

### 1. Unit Testing Custom Geocoder

```csharp
[TestClass]
public class CustomGeocoderTests
{
    [TestMethod]
    public async Task SearchAsync_ReturnsResults()
    {
        // Arrange
        var geocoder = new MyCustomGeocoder();

        // Act
        var results = await geocoder.SearchAsync("San Francisco");

        // Assert
        Assert.IsTrue(results.Count > 0);
        Assert.AreEqual("San Francisco", results[0].City);
    }
}
```

### 2. Integration Testing with Component Bus

```csharp
[TestClass]
public class SearchIntegrationTests
{
    [TestMethod]
    public async Task SearchResultSelected_PublishesMessage()
    {
        // Arrange
        var bus = new ComponentBus();
        SearchResultSelectedMessage? receivedMessage = null;

        bus.Subscribe<SearchResultSelectedMessage>(args =>
        {
            receivedMessage = args.Message;
        });

        // Act
        await bus.PublishAsync(new SearchResultSelectedMessage
        {
            SearchId = "test",
            DisplayName = "Test Location",
            Latitude = 37.7749,
            Longitude = -122.4194
        });

        // Assert
        Assert.IsNotNull(receivedMessage);
        Assert.AreEqual("Test Location", receivedMessage.DisplayName);
    }
}
```

---

## Performance Tips

1. **Use appropriate DebounceInterval** - Higher values reduce API calls
2. **Limit MaxResults** - Fewer results = faster rendering
3. **Cache frequent searches** - Implement caching layer for popular locations
4. **Use appropriate provider** - Mapbox is faster than Nominatim for high-volume applications

## Need More Help?

- See [README.md](./README.md) for detailed documentation
- Check the inline XML documentation in `HonuaSearch.razor`
- Visit Honua.MapSDK documentation
- Report issues on GitHub

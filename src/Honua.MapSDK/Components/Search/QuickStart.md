# HonuaSearch Quick Start Guide

Get started with HonuaSearch in 5 minutes!

## 1. Basic Setup (Free - No API Key)

The simplest way to add search to your map:

```razor
@page "/my-map"

<div style="position: relative; height: 100vh;">
    <HonuaMap Id="map1"
              Center="new[] { -122.4194, 37.7749 }"
              Zoom="12" />

    <HonuaSearch SyncWith="map1"
                 Position="top-left" />
</div>
```

That's it! You now have a fully functional search with:
- ✓ Autocomplete suggestions
- ✓ Geolocation "Locate Me" button
- ✓ Recent searches history
- ✓ Free geocoding (OpenStreetMap Nominatim)

## 2. Embedded in Sidebar

```razor
@page "/dashboard"

<MudLayout>
    <MudDrawer Open="true" Width="350px" Variant="DrawerVariant.Persistent">
        <MudPaper Class="pa-4" Elevation="0">
            <MudText Typo="Typo.h6" Class="mb-4">Search</MudText>

            <HonuaSearch SyncWith="map1"
                         Position="@null"
                         Width="100%"
                         ShowRecentSearches="true" />
        </MudPaper>
    </MudDrawer>

    <MudMainContent>
        <HonuaMap Id="map1" Style="height: 100vh;" />
    </MudMainContent>
</MudLayout>
```

## 3. With Mapbox (Better Performance)

First, get a free Mapbox API key at https://account.mapbox.com/

**appsettings.json:**
```json
{
  "Mapbox": {
    "ApiKey": "pk.your-mapbox-api-key-here"
  }
}
```

**YourPage.razor:**
```razor
@page "/mapbox-search"
@inject IConfiguration Config

<div style="position: relative; height: 100vh;">
    <HonuaMap Id="map1" />

    <HonuaSearch SyncWith="map1"
                 Provider="GeocodeProvider.Mapbox"
                 ApiKey="@Config["Mapbox:ApiKey"]"
                 Position="top-right" />
</div>
```

## 4. Handle Search Events

```razor
@page "/search-events"

<HonuaSearch SyncWith="map1"
             OnResultSelected="@OnLocationSelected"
             OnLocationFound="@OnUserLocationFound" />

<HonuaMap Id="map1" />

<MudAlert Severity="Severity.Info" Class="mt-4">
    @_statusMessage
</MudAlert>

@code {
    private string _statusMessage = "Search for a location...";

    private void OnLocationSelected(SearchResult result)
    {
        _statusMessage = $"Selected: {result.DisplayName}";
    }

    private void OnUserLocationFound((double Lat, double Lon) location)
    {
        _statusMessage = $"Your location: {location.Lat:F4}, {location.Lon:F4}";
    }
}
```

## 5. Customization

```razor
<HonuaSearch SyncWith="map1"
             Position="top-left"
             Width="400px"
             Placeholder="Where do you want to go?"
             Dense="true"
             Variant="Variant.Filled"
             MinSearchLength="3"
             DebounceInterval="500"
             MaxResults="15"
             DefaultZoom="16"
             ShowRecentSearches="true"
             ShowLocateButton="true"
             ReverseGeocodeOnLocate="true" />
```

## Common Patterns

### Search + DataGrid + Chart

```razor
<MudGrid>
    <MudItem xs="12">
        <HonuaSearch SyncWith="map1" Position="@null" Width="100%" />
    </MudItem>

    <MudItem xs="12" md="6">
        <HonuaMap Id="map1" Height="600px" />
    </MudItem>

    <MudItem xs="12" md="3">
        <HonuaDataGrid SyncWith="map1" Height="600px" />
    </MudItem>

    <MudItem xs="12" md="3">
        <HonuaChart SyncWith="map1" />
    </MudItem>
</MudGrid>
```

### Multiple Positioning Options

```razor
<!-- Top Left (common for search) -->
<HonuaSearch Position="top-left" />

<!-- Top Right (with legend on left) -->
<HonuaSearch Position="top-right" />

<!-- Bottom Right -->
<HonuaSearch Position="bottom-right" />

<!-- Embedded (in sidebar or panel) -->
<HonuaSearch Position="@null" Width="100%" />
```

## Troubleshooting

### Search not working?
1. Check browser console for errors
2. Verify internet connection
3. For Mapbox: verify API key is correct

### Geolocation not working?
1. Ensure you're using HTTPS (required for geolocation)
2. Check browser permissions
3. Try in a different browser

### No results found?
1. Try different search terms
2. Check spelling
3. Try searching for a well-known location first

## Next Steps

- Read the [full documentation](./README.md)
- Explore [comprehensive examples](./Examples.md)
- Implement a custom geocoding provider
- Integrate with your existing components

## Need Help?

- Check the inline XML documentation in the component
- Review the examples in Examples.md
- Check the main Honua.MapSDK documentation
- Report issues on GitHub

---

**Pro Tip:** The default Nominatim provider is free but rate-limited. For production applications with high search volume, consider using Mapbox (100,000 free searches/month).

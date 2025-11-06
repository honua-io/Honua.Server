# Quick Start Guide - HonuaBasemapGallery

Get up and running with the HonuaBasemapGallery component in 5 minutes.

## Installation

The HonuaBasemapGallery is part of Honua.MapSDK. If you already have the SDK installed, you're ready to go!

## Step 1: Add Using Statements

In your Razor page, add these using statements:

```razor
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.BasemapGallery
```

Or add them to `_Imports.razor`:

```razor
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.BasemapGallery
@using Honua.MapSDK.Models
@using Honua.MapSDK.Services
```

## Step 2: Register Services

In your `Program.cs`, ensure the ComponentBus is registered:

```csharp
builder.Services.AddScoped<ComponentBus>();
builder.Services.AddScoped<BasemapService>();
```

## Step 3: Add to Your Page

### Simple Example

```razor
@page "/my-map"

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />
    <HonuaBasemapGallery SyncWith="map1" Position="top-right" />
</div>
```

That's it! You now have a working basemap gallery with 16+ built-in basemaps.

## Step 4: Customize (Optional)

### Change Layout

```razor
<HonuaBasemapGallery SyncWith="map1" Layout="floating" Position="bottom-right" />
```

Available layouts: `grid`, `list`, `dropdown`, `floating`, `modal`

### Add Custom Basemaps

```razor
@code {
    private List<Basemap> _basemaps = new()
    {
        new Basemap
        {
            Id = "my-basemap",
            Name = "My Custom Basemap",
            Category = BasemapCategories.Streets,
            StyleUrl = "https://example.com/style.json",
            ThumbnailUrl = "/assets/my-basemap.png",
            Provider = "My Company"
        }
    };
}

<HonuaBasemapGallery SyncWith="map1" Basemaps="@_basemaps" />
```

### Filter Categories

```razor
<!-- Only show Streets and Satellite -->
<HonuaBasemapGallery
    SyncWith="map1"
    Categories="@(new[] { "Streets", "Satellite" })" />
```

### Enable Advanced Features

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    Position="top-right"
    ShowSearch="true"
    ShowFavorites="true"
    ShowOpacitySlider="true"
    EnablePreview="true" />
```

## Common Layouts

### Desktop App (Grid)

```razor
<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />
    <HonuaBasemapGallery SyncWith="map1" Layout="grid" Position="top-right" />
</div>
```

### Mobile App (Dropdown)

```razor
<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />
    <HonuaBasemapGallery SyncWith="map1" Layout="dropdown" Position="top-left" />
</div>
```

### Dashboard (Floating)

```razor
<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />
    <HonuaBasemapGallery SyncWith="map1" Layout="floating" Position="bottom-right" />
</div>
```

### Sidebar (List)

```razor
<MudDrawer Open="true" Width="300px">
    <HonuaBasemapGallery SyncWith="map1" Layout="list" />
</MudDrawer>
<div style="width: 100%; height: 100vh;">
    <HonuaMap Id="map1" />
</div>
```

## Next Steps

- Read the [full documentation](README.md)
- Browse [complete examples](Examples.md)
- Customize [basemap styles](../../Services/BasemapService.cs)
- Add [custom thumbnails](../../wwwroot/basemap-thumbnails/README.md)

## Troubleshooting

**Basemap not changing?**
- Verify `SyncWith` matches your map's `Id`
- Check browser console for errors

**Thumbnails not showing?**
- Check that thumbnail files exist in `wwwroot/basemap-thumbnails/`
- Verify the path in `ThumbnailBasePath` parameter

**Need help?**
- Check the [examples](Examples.md)
- Review the [full API docs](README.md)

## Complete Working Example

```razor
@page "/complete-example"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.BasemapGallery
@using Honua.MapSDK.Models

<PageTitle>Basemap Gallery Example</PageTitle>

<MudText Typo="Typo.h3" Class="mb-4">Interactive Map with Basemap Gallery</MudText>

<MudPaper Elevation="3" Style="width: 100%; height: 70vh; position: relative;">
    <HonuaMap
        Id="myMap"
        Center="@(new[] { -122.4194, 37.7749 })"
        Zoom="12"
        MapStyle="https://demotiles.maplibre.org/style.json" />

    <HonuaBasemapGallery
        SyncWith="myMap"
        Position="top-right"
        Layout="floating"
        ShowSearch="true"
        ShowCategories="true"
        OnBasemapChanged="@HandleBasemapChanged" />
</MudPaper>

<MudAlert Severity="Severity.Info" Class="mt-4">
    Current Basemap: <strong>@_currentBasemapName</strong>
</MudAlert>

@code {
    private string _currentBasemapName = "Default";

    private void HandleBasemapChanged(Basemap basemap)
    {
        _currentBasemapName = basemap.Name;
    }
}
```

## Resources

- 📖 [Full Documentation](README.md)
- 🎯 [Complete Examples](Examples.md)
- 🎨 [Custom Styling Guide](HonuaBasemapGallery.razor.css)
- 🖼️ [Thumbnail Guide](../../wwwroot/basemap-thumbnails/README.md)

Happy mapping! 🗺️

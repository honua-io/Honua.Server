# HonuaBasemapGallery Component

A comprehensive basemap/background layer switcher component for Honua.MapSDK that allows users to choose between different map styles with thumbnail previews, categories, and smooth transitions.

## Features

- Multiple layout modes (grid, list, dropdown, floating, modal)
- 16+ built-in basemaps across 4 categories
- Thumbnail preview of each basemap
- Category-based filtering
- Search functionality
- Favorites system
- Recently used tracking
- Custom basemap support
- Opacity control
- Hover preview (optional)
- Smooth loading transitions
- Responsive design
- Full keyboard accessibility
- Dark mode support

## Quick Start

### Basic Usage

```razor
@page "/map-with-basemaps"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.BasemapGallery

<div style="width: 100%; height: 100vh; position: relative;">
    <HonuaMap Id="map1" />
    <HonuaBasemapGallery SyncWith="map1" Position="top-right" />
</div>
```

## Layouts

### 1. Grid Layout (Default)

Displays basemaps in a responsive grid with thumbnails.

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    Layout="grid"
    Position="top-right" />
```

**Best for**: Desktop applications, sidebars, full-featured galleries

### 2. List Layout

Vertical list with thumbnails and descriptions.

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    Layout="list"
    Position="top-left" />
```

**Best for**: Narrow sidebars, mobile views

### 3. Dropdown Layout

Compact dropdown selector.

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    Layout="dropdown"
    Position="top-right" />
```

**Best for**: Toolbars, compact interfaces, embedded controls

### 4. Floating Layout

Expandable floating panel with toggle button.

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    Layout="floating"
    Position="bottom-right" />
```

**Best for**: Map overlays, minimal UI, togglable panels

### 5. Modal Layout

Opens in a modal dialog on button click.

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    Layout="modal"
    Position="top-right" />
```

**Best for**: Large galleries, occasional basemap switching

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | auto-generated | Unique identifier for the component |
| `SyncWith` | string? | null | Map ID to synchronize with |
| `Title` | string | "Basemap Gallery" | Title displayed in header |
| `Layout` | string | "grid" | Layout mode: grid, list, dropdown, floating, modal |
| `Position` | string? | null | Position: top-right, top-left, bottom-right, bottom-left |
| `Basemaps` | List<Basemap>? | null | Custom basemaps (overrides built-in) |
| `Categories` | string[]? | null | Categories to show (null = all) |
| `DefaultBasemap` | string? | null | Default basemap ID to activate |
| `ShowOpacitySlider` | bool | false | Show opacity slider for basemap |
| `ShowCategories` | bool | true | Show category tabs/filter |
| `ShowSearch` | bool | true | Show search bar |
| `ShowFavorites` | bool | false | Show favorites section |
| `EnablePreview` | bool | false | Enable hover preview |
| `ThumbnailBasePath` | string | "/_content/..." | Base path for thumbnails |
| `CssClass` | string? | null | Custom CSS class |
| `Style` | string? | null | Custom inline styles |
| `OnBasemapChanged` | EventCallback | - | Event fired when basemap changes |

## Built-in Basemaps

### Streets Category

1. **OpenStreetMap** - Standard OSM with bright colors
2. **Carto Positron** - Light, minimalist style for data visualization
3. **Carto Dark Matter** - Dark theme for stunning visualizations
4. **OSM Liberty** - Classic OpenStreetMap style

### Satellite Category

1. **ESRI World Imagery** - High-resolution satellite imagery
2. **Mapbox Satellite** - Global satellite imagery (requires API key)
3. **Satellite Streets** - Satellite with street labels (requires API key)

### Terrain Category

1. **OpenTopoMap** - Topographic with contour lines
2. **Stamen Terrain** - Beautiful hillshade terrain
3. **MapTiler Outdoor** - Detailed outdoor/hiking map (requires API key)
4. **ESRI World Terrain** - Physical terrain with elevation

### Specialty Category

1. **Watercolor** - Artistic watercolor style
2. **Black & White** - High contrast monochrome
3. **Blueprint** - Blueprint-style technical map
4. **Vintage** - Vintage/retro style with muted colors

## Custom Basemaps

### Add Custom Basemap

```csharp
var customBasemaps = new List<Basemap>
{
    new Basemap
    {
        Id = "my-custom-style",
        Name = "Custom Streets",
        Category = BasemapCategories.Streets,
        StyleUrl = "https://api.example.com/style.json",
        ThumbnailUrl = "/assets/custom-thumb.png",
        Provider = "My Company",
        Description = "Custom branded basemap",
        RequiresApiKey = true,
        DefaultOpacity = 1.0,
        Tags = new List<string> { "custom", "branded" }
    }
};
```

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    Basemaps="@customBasemaps" />
```

### Mix Custom with Built-in

```csharp
@code {
    private List<Basemap> _allBasemaps = new();
    private BasemapService _service = new();

    protected override void OnInitialized()
    {
        // Start with built-in basemaps
        _allBasemaps = _service.GetBasemaps();

        // Add custom basemaps
        _allBasemaps.Add(new Basemap
        {
            Id = "company-basemap",
            Name = "Company Map",
            Category = BasemapCategories.Custom,
            StyleUrl = "https://tiles.company.com/style.json",
            ThumbnailUrl = "/assets/company-map.png",
            Provider = "Company Name"
        });
    }
}
```

## Advanced Features

### Search Basemaps

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    ShowSearch="true" />
```

Users can search by:
- Basemap name
- Provider
- Description
- Tags

### Filter by Category

```razor
<!-- Only show Streets and Satellite -->
<HonuaBasemapGallery
    SyncWith="map1"
    Categories="@(new[] { "Streets", "Satellite" })" />
```

### Favorites

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    ShowFavorites="true" />
```

Users can:
- Star favorite basemaps
- Quick access to favorites
- Persistent across sessions (with local storage)

### Hover Preview

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    EnablePreview="true" />
```

Hovering over a thumbnail shows a preview on the map before clicking.

### Opacity Control

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    ShowOpacitySlider="true" />
```

Allows blending basemap with overlays or adjusting transparency.

## ComponentBus Integration

The component communicates via the ComponentBus using these messages:

### Published Messages

**BasemapChangedMessage**
```csharp
public class BasemapChangedMessage
{
    public string MapId { get; init; }
    public string Style { get; init; }
    public string? BasemapId { get; init; }
    public string? BasemapName { get; init; }
    public string? ComponentId { get; init; }
}
```

**BasemapLoadingMessage**
```csharp
public class BasemapLoadingMessage
{
    public string MapId { get; init; }
    public bool IsLoading { get; init; }
    public string? BasemapId { get; init; }
    public string? ComponentId { get; init; }
}
```

### Subscribed Messages

- **MapReadyMessage** - Sets initial basemap when map loads

## Styling

### Custom CSS

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    CssClass="my-custom-gallery"
    Style="max-width: 500px;" />
```

### CSS Variables

Override these CSS variables for theming:

```css
.my-custom-gallery {
    --basemap-card-hover-shadow: 0 12px 24px rgba(0, 0, 0, 0.2);
    --basemap-active-border: var(--mud-palette-success);
    --basemap-card-border-radius: 12px;
}
```

## Responsive Design

The component automatically adapts to different screen sizes:

- **Desktop** (>1200px): 3-4 columns in grid
- **Tablet** (768-1200px): 2-3 columns in grid
- **Mobile** (<768px): 1-2 columns in grid, compact layout

## Accessibility

- Full keyboard navigation (Tab, Enter, Arrow keys)
- ARIA labels on all interactive elements
- Screen reader descriptions
- Focus indicators
- Alt text for thumbnails
- High contrast support

### Keyboard Shortcuts

- **Tab**: Navigate between basemaps
- **Enter/Space**: Select basemap
- **Arrow Keys**: Navigate in grid
- **Escape**: Close modal/floating panel

## Performance

### Lazy Loading

Thumbnails are loaded lazily for better performance.

### Preloading

Common basemaps can be preloaded for instant switching.

### Caching

Recently used basemaps are cached for faster access.

## Examples

See [Examples.md](./Examples.md) for complete usage examples.

## API Reference

### Methods

The component exposes these methods via `@ref`:

```csharp
// Programmatically select a basemap
await gallery.SelectBasemapAsync("osm-standard");

// Get current basemap
var currentBasemap = gallery.GetActiveBasemap();

// Refresh basemaps list
gallery.RefreshBasemaps();
```

### Events

```razor
<HonuaBasemapGallery
    SyncWith="map1"
    OnBasemapChanged="@HandleBasemapChanged" />

@code {
    private void HandleBasemapChanged(Basemap basemap)
    {
        Console.WriteLine($"Basemap changed to: {basemap.Name}");
    }
}
```

## Troubleshooting

### Basemap not changing

1. Ensure `SyncWith` matches your map's `Id`
2. Check that the map is initialized before gallery
3. Verify StyleUrl is valid and accessible

### Thumbnails not showing

1. Check `ThumbnailBasePath` is correct
2. Ensure thumbnail files exist in wwwroot
3. Verify file permissions and paths

### API Key required

Some basemaps (Mapbox, MapTiler) require API keys:

```csharp
// Configure API keys in your basemap StyleUrl
StyleUrl = $"https://api.mapbox.com/styles/v1/mapbox/satellite-v9?access_token={apiKey}"
```

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

## License

Part of Honua.MapSDK - See main project license.

## Contributing

Contributions welcome! See the main project's contribution guidelines.

## Related Components

- **HonuaMap** - Main map component
- **HonuaLegend** - Layer legend and controls
- **HonuaSearch** - Geocoding and search
- **HonuaTimeline** - Temporal data visualization

# HonuaSearch Component

A powerful geocoding search component for Honua.MapSDK that provides autocomplete search, geolocation support, and integration with multiple geocoding providers.

## Features

- **Autocomplete Search**: Real-time search with debounced requests
- **Multiple Providers**: Support for Nominatim (free), Mapbox, Google Maps, and custom providers
- **Geolocation**: "Locate Me" button using browser's geolocation API
- **Recent Searches**: Stores last 10 searches in localStorage
- **Reverse Geocoding**: Convert coordinates to addresses
- **ComponentBus Integration**: Seamless integration with HonuaMap and other components
- **Responsive Design**: Works on desktop, tablet, and mobile
- **Accessibility**: Full keyboard navigation and screen reader support
- **Dark Mode**: Automatic dark mode support

## Installation

The HonuaSearch component is included in the Honua.MapSDK package. No additional installation required.

## Basic Usage

### Simple Search with Nominatim (Free)

```razor
<HonuaSearch SyncWith="map1"
             Placeholder="Search for a location..." />
```

This uses the free Nominatim geocoding service from OpenStreetMap. No API key required!

### With Mapbox Provider

```razor
<HonuaSearch SyncWith="map1"
             Provider="GeocodeProvider.Mapbox"
             ApiKey="pk.your-mapbox-key-here"
             Placeholder="Search locations..." />
```

### Embedded in Sidebar

```razor
<div class="sidebar">
    <HonuaSearch SyncWith="map1"
                 Position="@null"
                 Width="100%"
                 ShowRecentSearches="true" />
</div>
```

### Floating on Map

```razor
<HonuaSearch SyncWith="map1"
             Position="top-left"
             Width="400px"
             ShowLocateButton="true" />
```

## Parameters

### Core Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | Auto-generated | Unique identifier for the component |
| `SyncWith` | string? | null | Map ID to synchronize with (null syncs with all maps) |
| `Provider` | GeocodeProvider | Nominatim | Geocoding provider to use |
| `ApiKey` | string? | null | API key for provider (required for Mapbox, Google) |
| `CustomGeocoder` | IGeocoder? | null | Custom geocoding implementation |

### UI Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Placeholder` | string | "Search for a location..." | Placeholder text for input |
| `Position` | string? | null | Position: "top-right", "top-left", "bottom-right", "bottom-left", or null for embedded |
| `Width` | string | "350px" | Width of search component |
| `Variant` | Variant | Outlined | MudBlazor variant (Text, Filled, Outlined) |
| `Dense` | bool | false | Compact display mode |
| `CssClass` | string? | null | Additional CSS classes |

### Search Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `MinSearchLength` | int | 2 | Minimum characters before search starts |
| `DebounceInterval` | int | 300 | Debounce delay in milliseconds |
| `MaxResults` | int | 10 | Maximum number of results to display |
| `DefaultZoom` | double | 14 | Zoom level when flying to result |

### Feature Flags

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowRecentSearches` | bool | true | Show recent searches list |
| `ShowLocateButton` | bool | true | Show "Locate Me" button |
| `ReverseGeocodeOnLocate` | bool | true | Reverse geocode when using geolocation |

### Event Callbacks

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnResultSelected` | EventCallback<SearchResult> | Fired when user selects a search result |
| `OnLocationFound` | EventCallback<(double, double)> | Fired when geolocation succeeds |

## Geocoding Providers

### Nominatim (OpenStreetMap)

**Free, no API key required**

```razor
<HonuaSearch Provider="GeocodeProvider.Nominatim" />
```

**Pros:**
- Completely free
- No API key required
- Good global coverage
- Open source

**Cons:**
- Rate limited to 1 request per second
- Less detailed than commercial providers
- No commercial support

**Usage Policy:** https://operations.osmfoundation.org/policies/nominatim/

### Mapbox Geocoding API

**Requires API key**

```razor
<HonuaSearch Provider="GeocodeProvider.Mapbox"
             ApiKey="pk.your-mapbox-key-here" />
```

**Pros:**
- Fast and reliable
- Excellent global coverage
- Good autocomplete suggestions
- Generous free tier (100,000 requests/month)

**Cons:**
- Requires API key
- Costs money after free tier

**Get API Key:** https://account.mapbox.com/

### Google Maps Geocoding API

**Requires API key (coming soon)**

```razor
<HonuaSearch Provider="GeocodeProvider.Google"
             ApiKey="your-google-key-here" />
```

**Note:** Google provider implementation coming soon!

### Custom Provider

Implement your own geocoding provider:

```csharp
public class MyCustomGeocoder : IGeocoder
{
    public string ProviderName => "My Custom Provider";

    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        // Your implementation
        return new List<SearchResult>();
    }

    public async Task<SearchResult?> ReverseGeocodeAsync(double lat, double lon, CancellationToken cancellationToken = default)
    {
        // Your implementation
        return null;
    }
}
```

```razor
<HonuaSearch Provider="GeocodeProvider.Custom"
             CustomGeocoder="@_myGeocoder" />

@code {
    private MyCustomGeocoder _myGeocoder = new();
}
```

## ComponentBus Messages

### Messages Published

#### SearchResultSelectedMessage

Published when user selects a search result:

```csharp
public class SearchResultSelectedMessage
{
    public string SearchId { get; init; }
    public string DisplayName { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double[]? BoundingBox { get; init; }
    public string? Type { get; init; }
    public Dictionary<string, string> Metadata { get; init; }
}
```

#### FlyToRequestMessage

Published to navigate map to selected location:

```csharp
await Bus.PublishAsync(new FlyToRequestMessage
{
    MapId = "map1",
    Center = new[] { lon, lat },
    Zoom = 14
});
```

#### FitBoundsRequestMessage

Published when result has a bounding box:

```csharp
await Bus.PublishAsync(new FitBoundsRequestMessage
{
    MapId = "map1",
    Bounds = new[] { west, south, east, north },
    Padding = 50
});
```

### Messages Subscribed

#### MapReadyMessage

Subscribes to map ready events to enable location button.

## Search Result Categories

Results are automatically categorized for appropriate icon display:

- **City** - Major cities
- **Town** - Towns and smaller municipalities
- **Village** - Small villages
- **Street** - Streets and roads
- **Building** - Buildings and structures
- **PointOfInterest** - POIs, landmarks
- **Park** - Parks and natural areas
- **Water** - Lakes, rivers, oceans
- **Mountain** - Mountains and peaks
- **Administrative** - Countries, states, regions
- **Transportation** - Stations, airports
- **Restaurant** - Restaurants
- **Hotel** - Hotels
- **Shop** - Shops and stores
- **Other** - Other types

## Recent Searches

Recent searches are automatically stored in browser's localStorage:

```json
{
  "honua-search-recent": [
    {
      "id": "123",
      "displayName": "San Francisco, California, United States",
      "latitude": 37.7749,
      "longitude": -122.4194,
      "boundingBox": null,
      "type": "city",
      "timestamp": "2025-11-06T10:30:00Z"
    }
  ]
}
```

Maximum 10 recent searches are stored. Users can clear recent searches by clicking the clear button.

## Geolocation

The "Locate Me" button uses the browser's Geolocation API:

```javascript
navigator.geolocation.getCurrentPosition(
    (position) => {
        // Success - fly to user's location
    },
    (error) => {
        // Handle errors (permission denied, unavailable, timeout)
    },
    {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 0
    }
);
```

### Browser Permissions

Users must grant location permission. Handle permission denial gracefully:

```razor
<HonuaSearch OnLocationFound="@HandleLocationFound" />

@code {
    private async Task HandleLocationFound((double Lat, double Lon) location)
    {
        Console.WriteLine($"User location: {location.Lat}, {location.Lon}");
    }
}
```

## Styling

### Custom Styling

```razor
<HonuaSearch CssClass="my-custom-search"
             Style="width: 100%; max-width: 500px;" />
```

```css
.my-custom-search {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    border-radius: 12px;
}

.my-custom-search .result-name {
    font-weight: 600;
}
```

### Dark Mode

Dark mode is automatically detected using `prefers-color-scheme`:

```css
@media (prefers-color-scheme: dark) {
    .honua-search {
        background: #2d2d2d;
        color: #e0e0e0;
    }
}
```

## Keyboard Navigation

Full keyboard support:

- **Tab** - Navigate between elements
- **Arrow Up/Down** - Navigate search results
- **Enter** - Select highlighted result
- **Escape** - Close dropdown
- **Type** - Search automatically

## Accessibility

The component follows WCAG 2.1 Level AA guidelines:

- Semantic HTML
- ARIA labels and roles
- Keyboard navigation
- Screen reader support
- Focus management
- High contrast mode support
- Reduced motion support

## Performance

### Debouncing

Search requests are automatically debounced (default 300ms):

```razor
<HonuaSearch DebounceInterval="500" />
```

### Rate Limiting

Nominatim provider includes automatic rate limiting (1 req/sec) to comply with usage policy.

### Caching

Consider implementing caching for frequently searched locations:

```csharp
public class CachedGeocoder : IGeocoder
{
    private readonly IGeocoder _innerGeocoder;
    private readonly Dictionary<string, List<SearchResult>> _cache = new();

    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(query, out var cached))
            return cached;

        var results = await _innerGeocoder.SearchAsync(query, limit, cancellationToken);
        _cache[query] = results;
        return results;
    }
}
```

## Error Handling

The component handles errors gracefully:

- Network failures
- API errors
- Invalid API keys
- Permission denied
- Timeout errors

Errors are displayed in an alert below the search box.

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

Requires JavaScript enabled for full functionality.

## Security

### API Keys

Never commit API keys to source control. Use environment variables or configuration:

```csharp
@inject IConfiguration Configuration

<HonuaSearch ApiKey="@Configuration["Mapbox:ApiKey"]" />
```

### GDPR Compliance

When using geolocation:

1. Inform users before requesting location
2. Explain why you need location data
3. Provide clear opt-out mechanism
4. Don't store location without consent
5. Include in privacy policy

## Troubleshooting

### Search Not Working

1. Check provider API key
2. Check browser console for errors
3. Verify network connectivity
4. Check provider rate limits

### Geolocation Not Working

1. Verify HTTPS (geolocation requires secure context)
2. Check browser permissions
3. Test in different browser
4. Check browser console for errors

### No Results

1. Try different search terms
2. Check provider coverage
3. Verify MinSearchLength setting
4. Check provider rate limits

## Examples

See [Examples.md](./Examples.md) for comprehensive usage examples.

## API Reference

See the inline XML documentation in `HonuaSearch.razor` for complete API reference.

## License

Part of Honua.MapSDK - See main project license.

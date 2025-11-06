# HonuaBookmarks Component

A comprehensive bookmarks/saved views component for the Honua.MapSDK library that allows users to save, organize, and restore map views with thumbnails, folders, and sharing capabilities.

## Features

- **Save Current View**: Capture current map state (center, zoom, bearing, pitch) as a bookmark
- **Thumbnail Preview**: Auto-generate thumbnail images of saved views
- **Folder Organization**: Organize bookmarks into folders with custom icons and colors
- **Search & Filter**: Quickly find bookmarks by name, description, or tags
- **Multiple Layouts**: List, grid, compact, and dropdown view modes
- **Sharing**: Generate shareable URLs for bookmarks
- **Import/Export**: Backup and restore bookmarks as JSON
- **Responsive Design**: Works on desktop, tablet, and mobile devices
- **Dark Mode**: Automatic dark mode support
- **Accessibility**: Full keyboard navigation and screen reader support

## Basic Usage

### Simple Bookmarks Panel

```razor
<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1" />
```

### Floating Panel

```razor
<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1"
                Position="top-right" />
```

### Dropdown Selector

```razor
<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1"
                Layout="dropdown"
                Title="Go to saved location" />
```

### Grid View

```razor
<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1"
                ViewMode="grid"
                EnableThumbnails="true" />
```

## Parameters

### Core Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | auto-generated | Unique identifier for the component |
| `SyncWith` | string? | null | Map ID to synchronize with |
| `Title` | string | "Bookmarks" | Title displayed in header |
| `Layout` | string | "list" | Layout mode: "list", "grid", "compact", "dropdown" |
| `ViewMode` | string | "list" | View mode for non-folder layouts: "list" or "grid" |
| `Position` | string? | null | Floating position: "top-right", "top-left", "bottom-right", "bottom-left" |

### Feature Toggles

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowFolders` | bool | true | Show folder organization |
| `ShowSearch` | bool | true | Show search box (when >3 bookmarks) |
| `EnableThumbnails` | bool | true | Enable thumbnail capture and display |
| `AllowAdd` | bool | true | Allow adding new bookmarks |
| `AllowEdit` | bool | true | Allow editing bookmarks |
| `AllowDelete` | bool | true | Allow deleting bookmarks |
| `AllowShare` | bool | true | Allow sharing bookmarks |
| `AllowImportExport` | bool | true | Allow import/export operations |

### Advanced Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Storage` | IBookmarkStorage? | null | Custom storage provider (defaults to LocalStorage) |
| `CssClass` | string? | null | Custom CSS class |
| `Style` | string? | null | Custom inline styles |

### Event Callbacks

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnBookmarkSelected` | EventCallback&lt;Bookmark&gt; | Fired when user navigates to a bookmark |
| `OnBookmarkCreated` | EventCallback&lt;Bookmark&gt; | Fired when new bookmark is created |
| `OnBookmarkDeleted` | EventCallback&lt;Bookmark&gt; | Fired when bookmark is deleted |
| `OnBookmarkUpdated` | EventCallback&lt;Bookmark&gt; | Fired when bookmark is updated |

## Storage Options

### LocalStorage (Default)

Bookmarks are stored in the browser's localStorage, persisting across sessions but limited to the device.

```razor
<HonuaBookmarks SyncWith="map1" />
```

### Custom Storage Provider

Implement the `IBookmarkStorage` interface for custom storage backends:

```csharp
public class ApiBookmarkStorage : IBookmarkStorage
{
    private readonly HttpClient _http;

    public ApiBookmarkStorage(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Bookmark>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Bookmark>>("/api/bookmarks");
    }

    public async Task<string> SaveAsync(Bookmark bookmark)
    {
        var response = await _http.PostAsJsonAsync("/api/bookmarks", bookmark);
        var result = await response.Content.ReadFromJsonAsync<SaveResult>();
        return result.Id;
    }

    // Implement other methods...
}
```

Then use it:

```razor
@inject ApiBookmarkStorage CustomStorage

<HonuaBookmarks SyncWith="map1"
                Storage="@CustomStorage" />
```

## ComponentBus Integration

The component integrates with the MapSDK's ComponentBus for loosely-coupled communication:

### Published Messages

#### BookmarkSelectedMessage
Published when user navigates to a bookmark:
```csharp
public record BookmarkSelectedMessage(
    string BookmarkId,
    string BookmarkName,
    double[] Center,
    double Zoom,
    double Bearing,
    double Pitch,
    string ComponentId
);
```

#### BookmarkCreatedMessage
Published when new bookmark is created:
```csharp
public record BookmarkCreatedMessage(
    string BookmarkId,
    string BookmarkName,
    string ComponentId,
    double[] Center,
    double Zoom
);
```

#### BookmarkDeletedMessage
Published when bookmark is deleted:
```csharp
public record BookmarkDeletedMessage(
    string BookmarkId,
    string ComponentId
);
```

### Subscribed Messages

- **MapReadyMessage**: Enables bookmark creation when map is ready
- **MapExtentChangedMessage**: Tracks current map state for saving

### Listening to Events

```razor
@inject ComponentBus Bus

<HonuaBookmarks SyncWith="map1" />

@code {
    protected override void OnInitialized()
    {
        Bus.Subscribe<BookmarkSelectedMessage>(args =>
        {
            Console.WriteLine($"Navigated to: {args.Message.BookmarkName}");
        });

        Bus.Subscribe<BookmarkCreatedMessage>(args =>
        {
            Console.WriteLine($"New bookmark: {args.Message.BookmarkName}");
        });
    }
}
```

## Bookmark Model

```csharp
public class Bookmark
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public double[] Center { get; set; } // [lon, lat]
    public double Zoom { get; set; }
    public double Bearing { get; set; }
    public double Pitch { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? FolderId { get; set; }
    public List<string> Tags { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public bool IsPublic { get; set; }
    public string? ShareUrl { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public int AccessCount { get; set; }
}
```

## Folder Model

```csharp
public class BookmarkFolder
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string? ParentFolderId { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## Thumbnail Capture

The component uses the `bookmark-capture.js` module to capture map thumbnails:

### Automatic Capture
Thumbnails are automatically captured when saving a bookmark (if `EnableThumbnails="true"`).

### Manual Capture
```javascript
import { captureMapThumbnail } from './bookmark-capture.js';

const thumbnail = await captureMapThumbnail('map1', 200, 150);
// Returns base64 data URL
```

### Thumbnail Options
- **Width**: Default 200px
- **Height**: Default 150px
- **Format**: JPEG (0.8 quality) or PNG
- **Aspect Ratio**: Maintains map aspect ratio with cropping

## URL Sharing

Generate shareable URLs with map state:

```
https://app.example.com/map?bookmark=abc123
https://app.example.com/map?center=-122.4,37.7&zoom=12&bearing=45&pitch=30
```

### Parsing URL Parameters

```razor
@inject NavigationManager Navigation

@code {
    protected override async Task OnInitializedAsync()
    {
        var uri = new Uri(Navigation.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        if (query["center"] != null)
        {
            var coords = query["center"].Split(',');
            var center = new[] {
                double.Parse(coords[0]),
                double.Parse(coords[1])
            };
            var zoom = double.Parse(query["zoom"] ?? "10");

            // Navigate to bookmark
            await Bus.PublishAsync(new FlyToRequestMessage
            {
                MapId = "map1",
                Center = center,
                Zoom = zoom
            });
        }
    }
}
```

## Import/Export

### Export Bookmarks

```razor
<HonuaBookmarks SyncWith="map1"
                AllowImportExport="true" />
```

Click the menu (⋮) → Export to download JSON file:

```json
{
  "bookmarks": [
    {
      "id": "abc123",
      "name": "Downtown Office",
      "center": [-122.4, 37.7],
      "zoom": 15,
      "bearing": 0,
      "pitch": 0,
      "thumbnailUrl": "data:image/jpeg;base64,...",
      "createdAt": "2024-01-15T10:30:00Z",
      "tags": ["work", "office"]
    }
  ],
  "folders": [
    {
      "id": "folder1",
      "name": "Work",
      "icon": "folder",
      "color": "#2196F3"
    }
  ],
  "exportedAt": "2024-01-15T12:00:00Z",
  "version": "1.0"
}
```

### Import Bookmarks

Click the menu (⋮) → Import to select a JSON file for import.

## Styling

### Custom Styles

```razor
<HonuaBookmarks SyncWith="map1"
                CssClass="my-custom-bookmarks"
                Style="max-height: 500px;" />
```

### CSS Variables

Override default styles with CSS variables:

```css
.honua-bookmarks {
    --bookmarks-primary-color: #667eea;
    --bookmarks-header-gradient: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    --bookmarks-card-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    --bookmarks-card-hover-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}
```

## Accessibility

### Keyboard Navigation
- **Tab**: Navigate between bookmarks
- **Enter/Space**: Activate bookmark
- **Escape**: Close dialogs
- **Arrow Keys**: Navigate in dropdowns

### Screen Reader Support
- All interactive elements have ARIA labels
- Bookmark count announced
- State changes announced

### High Contrast Mode
Component adapts to high contrast mode automatically.

## Performance

### Thumbnail Optimization
- Thumbnails compressed to ~50KB
- Lazy loading for large bookmark lists
- Debounced search (300ms)

### Storage Limits
- LocalStorage: ~5-10MB per domain (varies by browser)
- Estimated capacity: ~50-100 bookmarks with thumbnails

### Best Practices
- Limit thumbnails to 200x150px
- Use folders to organize >20 bookmarks
- Export/backup important bookmarks regularly

## Browser Compatibility

- **Chrome/Edge**: Full support
- **Firefox**: Full support
- **Safari**: Full support
- **Mobile Browsers**: Full support with responsive design

## Troubleshooting

### Thumbnails not generating
- Ensure map is fully loaded before saving
- Check browser console for errors
- Verify `EnableThumbnails="true"` is set

### Bookmarks not persisting
- Check browser localStorage is enabled
- Verify not in incognito/private mode
- Check storage quota (browser developer tools)

### Share URLs not working
- Ensure base URL is correct
- Implement URL parameter parsing in your app
- Check URL encoding for special characters

## See Also

- [Examples.md](./Examples.md) - Comprehensive examples
- [HonuaMap Component](../Map/README.md)
- [ComponentBus Documentation](../../Core/README.md)

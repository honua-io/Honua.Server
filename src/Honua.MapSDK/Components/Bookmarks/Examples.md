# HonuaBookmarks Examples

Comprehensive examples for using the HonuaBookmarks component in various scenarios.

## Table of Contents

1. [Basic Examples](#basic-examples)
2. [Layout Variations](#layout-variations)
3. [Custom Storage](#custom-storage)
4. [Event Handling](#event-handling)
5. [Advanced Features](#advanced-features)
6. [Integration Patterns](#integration-patterns)
7. [Real-World Scenarios](#real-world-scenarios)

---

## Basic Examples

### Example 1: Simple Bookmarks Panel

The simplest way to add bookmarks to your map:

```razor
@page "/map-basic"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Bookmarks

<div style="display: flex; height: 100vh;">
    <div style="flex: 1;">
        <HonuaMap Id="map1"
                  Center="@(new[] { -122.4, 37.7 })"
                  Zoom="12" />
    </div>
    <div style="width: 350px; border-left: 1px solid #ddd;">
        <HonuaBookmarks SyncWith="map1" />
    </div>
</div>
```

### Example 2: Floating Panel

Overlay bookmarks on top of the map:

```razor
@page "/map-floating"

<div style="position: relative; height: 100vh;">
    <HonuaMap Id="map1"
              Center="@(new[] { -122.4, 37.7 })"
              Zoom="12" />

    <HonuaBookmarks SyncWith="map1"
                    Position="top-right"
                    Title="Saved Views" />
</div>
```

### Example 3: Dropdown Selector

Compact dropdown for quick navigation:

```razor
@page "/map-dropdown"

<MudContainer MaxWidth="MaxWidth.Large" Style="padding: 20px;">
    <MudGrid>
        <MudItem xs="12" md="8">
            <MudText Typo="Typo.h5">Project Sites</MudText>
        </MudItem>
        <MudItem xs="12" md="4">
            <HonuaBookmarks SyncWith="map1"
                            Layout="dropdown"
                            Title="Jump to location" />
        </MudItem>
    </MudGrid>

    <div style="height: 600px; margin-top: 20px;">
        <HonuaMap Id="map1"
                  Center="@(new[] { -122.4, 37.7 })"
                  Zoom="10" />
    </div>
</MudContainer>
```

---

## Layout Variations

### Example 4: Grid View with Thumbnails

Beautiful grid layout with thumbnail previews:

```razor
@page "/map-grid"

<div style="display: flex; height: 100vh;">
    <div style="flex: 1;">
        <HonuaMap Id="map1"
                  Center="@(new[] { -122.4, 37.7 })"
                  Zoom="12" />
    </div>
    <div style="width: 400px; border-left: 1px solid #ddd; overflow-y: auto;">
        <HonuaBookmarks SyncWith="map1"
                        ViewMode="grid"
                        EnableThumbnails="true"
                        Title="Photo Gallery" />
    </div>
</div>
```

### Example 5: Compact Sidebar

Space-efficient compact layout:

```razor
@page "/map-compact"

<div style="position: relative; height: 100vh;">
    <HonuaMap Id="map1"
              Center="@(new[] { -122.4, 37.7 })"
              Zoom="12" />

    <HonuaBookmarks SyncWith="map1"
                    Layout="compact"
                    Position="bottom-left"
                    EnableThumbnails="false" />
</div>
```

### Example 6: Multiple Position Examples

```razor
@page "/map-positions"

<div style="position: relative; height: 100vh;">
    <HonuaMap Id="map1"
              Center="@(new[] { -122.4, 37.7 })"
              Zoom="12" />

    <!-- Top Left -->
    <HonuaBookmarks SyncWith="map1"
                    Position="top-left"
                    Title="Quick Access"
                    Layout="compact" />

    <!-- Top Right -->
    <HonuaBookmarks SyncWith="map1"
                    Position="top-right"
                    Title="My Locations"
                    ShowFolders="true" />
</div>
```

---

## Custom Storage

### Example 7: API Backend Storage

Store bookmarks on a server for cross-device sync:

```csharp
// Services/ApiBookmarkStorage.cs
using System.Net.Http.Json;
using Honua.MapSDK.Models;
using Honua.MapSDK.Services.BookmarkStorage;

public class ApiBookmarkStorage : IBookmarkStorage
{
    private readonly HttpClient _http;
    private readonly string _userId;

    public ApiBookmarkStorage(HttpClient http, string userId)
    {
        _http = http;
        _userId = userId;
    }

    public async Task<List<Bookmark>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Bookmark>>(
            $"/api/users/{_userId}/bookmarks"
        ) ?? new List<Bookmark>();
    }

    public async Task<Bookmark?> GetByIdAsync(string id)
    {
        return await _http.GetFromJsonAsync<Bookmark>(
            $"/api/users/{_userId}/bookmarks/{id}"
        );
    }

    public async Task<string> SaveAsync(Bookmark bookmark)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/users/{_userId}/bookmarks",
            bookmark
        );

        var result = await response.Content.ReadFromJsonAsync<SaveResult>();
        return result!.Id;
    }

    public async Task DeleteAsync(string id)
    {
        await _http.DeleteAsync($"/api/users/{_userId}/bookmarks/{id}");
    }

    public async Task<List<Bookmark>> SearchAsync(string query)
    {
        return await _http.GetFromJsonAsync<List<Bookmark>>(
            $"/api/users/{_userId}/bookmarks/search?q={Uri.EscapeDataString(query)}"
        ) ?? new List<Bookmark>();
    }

    public async Task<List<Bookmark>> GetByFolderAsync(string? folderId)
    {
        var url = folderId == null
            ? $"/api/users/{_userId}/bookmarks?uncategorized=true"
            : $"/api/users/{_userId}/bookmarks?folderId={folderId}";

        return await _http.GetFromJsonAsync<List<Bookmark>>(url)
            ?? new List<Bookmark>();
    }

    public async Task<List<BookmarkFolder>> GetFoldersAsync()
    {
        return await _http.GetFromJsonAsync<List<BookmarkFolder>>(
            $"/api/users/{_userId}/bookmark-folders"
        ) ?? new List<BookmarkFolder>();
    }

    public async Task<string> SaveFolderAsync(BookmarkFolder folder)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/users/{_userId}/bookmark-folders",
            folder
        );

        var result = await response.Content.ReadFromJsonAsync<SaveResult>();
        return result!.Id;
    }

    public async Task DeleteFolderAsync(string id, bool deleteBookmarks = false)
    {
        await _http.DeleteAsync(
            $"/api/users/{_userId}/bookmark-folders/{id}?deleteBookmarks={deleteBookmarks}"
        );
    }

    public async Task<string> ExportAsync()
    {
        var response = await _http.GetAsync(
            $"/api/users/{_userId}/bookmarks/export"
        );
        return await response.Content.ReadAsStringAsync();
    }

    public async Task ImportAsync(string json)
    {
        await _http.PostAsJsonAsync(
            $"/api/users/{_userId}/bookmarks/import",
            new { data = json }
        );
    }

    private class SaveResult
    {
        public string Id { get; set; } = string.Empty;
    }
}
```

Usage:

```razor
@page "/map-api-storage"
@inject HttpClient Http
@inject AuthenticationStateProvider AuthProvider

<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1" Storage="@_storage" />

@code {
    private ApiBookmarkStorage? _storage;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst("sub")?.Value ?? "anonymous";

        _storage = new ApiBookmarkStorage(Http, userId);
    }
}
```

### Example 8: Hybrid Storage (LocalStorage + Cloud Sync)

```csharp
public class HybridBookmarkStorage : IBookmarkStorage
{
    private readonly LocalStorageBookmarkStorage _localStorage;
    private readonly ApiBookmarkStorage _cloudStorage;
    private readonly ILogger<HybridBookmarkStorage> _logger;

    public HybridBookmarkStorage(
        LocalStorageBookmarkStorage localStorage,
        ApiBookmarkStorage cloudStorage,
        ILogger<HybridBookmarkStorage> logger)
    {
        _localStorage = localStorage;
        _cloudStorage = cloudStorage;
        _logger = logger;
    }

    public async Task<List<Bookmark>> GetAllAsync()
    {
        try
        {
            // Try cloud first
            var bookmarks = await _cloudStorage.GetAllAsync();

            // Update local cache
            foreach (var bookmark in bookmarks)
            {
                await _localStorage.SaveAsync(bookmark);
            }

            return bookmarks;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloud storage unavailable, using local cache");
            return await _localStorage.GetAllAsync();
        }
    }

    public async Task<string> SaveAsync(Bookmark bookmark)
    {
        // Save to local first (fast)
        var id = await _localStorage.SaveAsync(bookmark);

        // Sync to cloud in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _cloudStorage.SaveAsync(bookmark);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync bookmark to cloud");
            }
        });

        return id;
    }

    // Implement other methods similarly...
}
```

---

## Event Handling

### Example 9: Tracking Bookmark Usage

```razor
@page "/map-analytics"
@inject ILogger<MapAnalytics> Logger

<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1"
                OnBookmarkSelected="HandleBookmarkSelected"
                OnBookmarkCreated="HandleBookmarkCreated" />

<MudPaper Style="padding: 20px; margin-top: 20px;">
    <MudText Typo="Typo.h6">Analytics</MudText>
    <MudText>Total Bookmarks: @_totalBookmarks</MudText>
    <MudText>Most Popular: @_mostPopular</MudText>
    <MudText>Last Created: @_lastCreated</MudText>
</MudPaper>

@code {
    private int _totalBookmarks = 0;
    private string _mostPopular = "None";
    private string _lastCreated = "None";

    private void HandleBookmarkSelected(Bookmark bookmark)
    {
        Logger.LogInformation(
            "Bookmark selected: {Name} (ID: {Id}, Access count: {Count})",
            bookmark.Name,
            bookmark.Id,
            bookmark.AccessCount
        );

        _mostPopular = bookmark.Name;
        StateHasChanged();
    }

    private void HandleBookmarkCreated(Bookmark bookmark)
    {
        Logger.LogInformation("New bookmark created: {Name}", bookmark.Name);

        _totalBookmarks++;
        _lastCreated = bookmark.Name;
        StateHasChanged();
    }
}
```

### Example 10: Synchronized Components

Multiple components responding to bookmark selection:

```razor
@page "/map-synchronized"
@inject ComponentBus Bus

<MudGrid>
    <MudItem xs="12" md="8">
        <HonuaMap Id="map1" />
    </MudItem>
    <MudItem xs="12" md="4">
        <HonuaBookmarks SyncWith="map1" />

        <MudPaper Style="padding: 20px; margin-top: 20px;">
            <MudText Typo="Typo.h6">Current Location</MudText>
            @if (_currentBookmark != null)
            {
                <MudText>@_currentBookmark.Name</MudText>
                <MudText Typo="Typo.caption">
                    Zoom: @_currentBookmark.Zoom.ToString("F1")
                </MudText>
                @if (!string.IsNullOrEmpty(_currentBookmark.Description))
                {
                    <MudText Typo="Typo.body2" Style="margin-top: 10px;">
                        @_currentBookmark.Description
                    </MudText>
                }
            }
            else
            {
                <MudText Color="Color.Secondary">
                    No bookmark selected
                </MudText>
            }
        </MudPaper>
    </MudItem>
</MudGrid>

@code {
    private Bookmark? _currentBookmark;

    protected override void OnInitialized()
    {
        Bus.Subscribe<BookmarkSelectedMessage>(args =>
        {
            // Update UI based on bookmark selection
            _currentBookmark = new Bookmark
            {
                Name = args.Message.BookmarkName,
                Center = args.Message.Center,
                Zoom = args.Message.Zoom,
                Bearing = args.Message.Bearing,
                Pitch = args.Message.Pitch
            };

            InvokeAsync(StateHasChanged);
        });
    }
}
```

---

## Advanced Features

### Example 11: Pre-populated Bookmarks

Load default bookmarks for new users:

```razor
@page "/map-defaults"
@inject IBookmarkStorage Storage

<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1" Storage="@Storage" />

@code {
    protected override async Task OnInitializedAsync()
    {
        var bookmarks = await Storage.GetAllAsync();

        // Add default bookmarks if none exist
        if (!bookmarks.Any())
        {
            await AddDefaultBookmarks();
        }
    }

    private async Task AddDefaultBookmarks()
    {
        var defaults = new[]
        {
            new Bookmark
            {
                Name = "San Francisco",
                Description = "Downtown San Francisco",
                Center = new[] { -122.4194, 37.7749 },
                Zoom = 13,
                Tags = new List<string> { "city", "default" }
            },
            new Bookmark
            {
                Name = "Golden Gate Bridge",
                Description = "Iconic suspension bridge",
                Center = new[] { -122.4783, 37.8199 },
                Zoom = 15,
                Tags = new List<string> { "landmark", "default" }
            },
            new Bookmark
            {
                Name = "Alcatraz Island",
                Description = "Former federal prison",
                Center = new[] { -122.4230, 37.8267 },
                Zoom = 16,
                Tags = new List<string> { "landmark", "default" }
            }
        };

        foreach (var bookmark in defaults)
        {
            await Storage.SaveAsync(bookmark);
        }
    }
}
```

### Example 12: Bookmark Templates

Create bookmarks from templates:

```razor
@page "/map-templates"

<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1" Storage="@_storage" />

<MudPaper Style="padding: 20px; margin-top: 20px;">
    <MudText Typo="Typo.h6">Quick Templates</MudText>
    <MudStack Row="true" Spacing="2" Style="margin-top: 10px;">
        <MudButton Variant="Variant.Outlined"
                   OnClick="() => CreateFromTemplate(\"overview\")">
            Overview
        </MudButton>
        <MudButton Variant="Variant.Outlined"
                   OnClick="() => CreateFromTemplate(\"detail\")">
            Detailed View
        </MudButton>
        <MudButton Variant="Variant.Outlined"
                   OnClick="() => CreateFromTemplate(\"oblique\")">
            3D Perspective
        </MudButton>
    </MudStack>
</MudPaper>

@code {
    [Inject] private IBookmarkStorage _storage { get; set; } = null!;
    [Inject] private ComponentBus Bus { get; set; } = null!;

    private readonly Dictionary<string, BookmarkTemplate> _templates = new()
    {
        ["overview"] = new()
        {
            ZoomRange = (8, 10),
            Bearing = 0,
            Pitch = 0,
            NamePrefix = "Overview -"
        },
        ["detail"] = new()
        {
            ZoomRange = (16, 18),
            Bearing = 0,
            Pitch = 0,
            NamePrefix = "Detail -"
        },
        ["oblique"] = new()
        {
            ZoomRange = (15, 17),
            Bearing = 45,
            Pitch = 60,
            NamePrefix = "3D View -"
        }
    };

    private async Task CreateFromTemplate(string templateId)
    {
        var template = _templates[templateId];

        // Get current map center (would need to implement)
        var center = new[] { -122.4, 37.7 };

        var bookmark = new Bookmark
        {
            Name = $"{template.NamePrefix} {DateTime.Now:HH:mm}",
            Center = center,
            Zoom = (template.ZoomRange.Min + template.ZoomRange.Max) / 2.0,
            Bearing = template.Bearing,
            Pitch = template.Pitch,
            Tags = new List<string> { "template", templateId }
        };

        await _storage.SaveAsync(bookmark);

        // Refresh bookmarks display
        await Bus.PublishAsync(new BookmarkCreatedMessage
        {
            BookmarkId = bookmark.Id,
            BookmarkName = bookmark.Name,
            ComponentId = "templates",
            Center = bookmark.Center,
            Zoom = bookmark.Zoom
        });
    }

    private class BookmarkTemplate
    {
        public (double Min, double Max) ZoomRange { get; set; }
        public double Bearing { get; set; }
        public double Pitch { get; set; }
        public string NamePrefix { get; set; } = string.Empty;
    }
}
```

### Example 13: Automatic Bookmark Capture

Automatically create bookmarks at intervals:

```razor
@page "/map-auto-capture"
@implements IDisposable

<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1" Storage="@_storage" />

<MudPaper Style="padding: 20px;">
    <MudSwitch @bind-Checked="_autoCapture"
               Label="Auto-capture bookmarks every 30 seconds"
               Color="Color.Primary" />
</MudPaper>

@code {
    [Inject] private IBookmarkStorage _storage { get; set; } = null!;
    [Inject] private ComponentBus Bus { get; set; } = null!;

    private bool _autoCapture;
    private Timer? _captureTimer;
    private double[] _lastCenter = new[] { 0.0, 0.0 };
    private double _lastZoom = 2;

    protected override void OnInitialized()
    {
        Bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            _lastCenter = args.Message.Center;
            _lastZoom = args.Message.Zoom;
        });

        _captureTimer = new Timer(async _ =>
        {
            if (_autoCapture)
            {
                await CaptureBookmark();
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private async Task CaptureBookmark()
    {
        var bookmark = new Bookmark
        {
            Name = $"Auto-capture {DateTime.Now:HH:mm:ss}",
            Center = _lastCenter,
            Zoom = _lastZoom,
            Tags = new List<string> { "auto-capture" }
        };

        await _storage.SaveAsync(bookmark);
    }

    public void Dispose()
    {
        _captureTimer?.Dispose();
    }
}
```

---

## Integration Patterns

### Example 14: Timeline + Bookmarks

Combine timeline animation with bookmarks:

```razor
@page "/map-timeline-bookmarks"

<HonuaMap Id="map1" />
<HonuaTimeline SyncWith="map1" />
<HonuaBookmarks SyncWith="map1" />

@code {
    [Inject] private ComponentBus Bus { get; set; } = null!;

    protected override void OnInitialized()
    {
        // Create bookmarks at key timeline moments
        Bus.Subscribe<TimeChangedMessage>(async args =>
        {
            if (IsKeyMoment(args.Message.CurrentTime))
            {
                // Auto-create bookmark for significant time
            }
        });
    }

    private bool IsKeyMoment(DateTime time)
    {
        // Example: Hourly snapshots
        return time.Minute == 0;
    }
}
```

### Example 15: Search + Bookmarks

Save search results as bookmarks:

```razor
@page "/map-search-bookmarks"
@inject IBookmarkStorage Storage

<HonuaMap Id="map1" />
<HonuaSearch SyncWith="map1" />
<HonuaBookmarks SyncWith="map1" Storage="@Storage" />

@code {
    [Inject] private ComponentBus Bus { get; set; } = null!;

    protected override void OnInitialized()
    {
        Bus.Subscribe<SearchResultSelectedMessage>(async args =>
        {
            // Optionally auto-save search results
            if (_autoSaveSearches)
            {
                var bookmark = new Bookmark
                {
                    Name = args.Message.DisplayName,
                    Center = new[] { args.Message.Longitude, args.Message.Latitude },
                    Zoom = 15,
                    Tags = new List<string> { "search", args.Message.Type ?? "location" },
                    Metadata = new Dictionary<string, object>
                    {
                        ["searchResult"] = true,
                        ["resultType"] = args.Message.Type ?? "unknown"
                    }
                };

                await Storage.SaveAsync(bookmark);
            }
        });
    }

    private bool _autoSaveSearches = false;
}
```

---

## Real-World Scenarios

### Example 16: Field Inspection App

Track inspection sites with bookmarks and metadata:

```razor
@page "/inspection-app"
@inject IBookmarkStorage Storage

<HonuaMap Id="map1" />
<HonuaBookmarks SyncWith="map1"
                Storage="@Storage"
                OnBookmarkCreated="HandleInspectionCreated" />

<MudPaper Style="padding: 20px; margin-top: 20px;">
    <MudText Typo="Typo.h6">New Inspection</MudText>
    <MudTextField @bind-Value="_siteName" Label="Site Name" />
    <MudSelect @bind-Value="_inspectionType" Label="Type">
        <MudSelectItem Value="@(\"routine\")">Routine</MudSelectItem>
        <MudSelectItem Value="@(\"emergency\")">Emergency</MudSelectItem>
        <MudSelectItem Value="@(\"followup\")">Follow-up</MudSelectItem>
    </MudSelect>
    <MudButton Color="Color.Primary"
               Variant="Variant.Filled"
               OnClick="CreateInspection">
        Create Inspection Bookmark
    </MudButton>
</MudPaper>

@code {
    private string _siteName = "";
    private string _inspectionType = "routine";
    private double[] _currentCenter = new[] { -122.4, 37.7 };
    private double _currentZoom = 15;

    protected override void OnInitialized()
    {
        Bus.Subscribe<MapExtentChangedMessage>(args =>
        {
            _currentCenter = args.Message.Center;
            _currentZoom = args.Message.Zoom;
        });
    }

    private async Task CreateInspection()
    {
        var bookmark = new Bookmark
        {
            Name = _siteName,
            Description = $"{_inspectionType} inspection scheduled",
            Center = _currentCenter,
            Zoom = _currentZoom,
            Tags = new List<string> { "inspection", _inspectionType },
            Metadata = new Dictionary<string, object>
            {
                ["inspectionType"] = _inspectionType,
                ["scheduledDate"] = DateTime.UtcNow,
                ["status"] = "pending"
            }
        };

        await Storage.SaveAsync(bookmark);
    }

    private void HandleInspectionCreated(Bookmark bookmark)
    {
        // Send notification, update schedule, etc.
    }
}
```

### Example 17: Tour Guide Application

Create guided tours with bookmarks:

```razor
@page "/tour-guide"
@inject IBookmarkStorage Storage

<HonuaMap Id="map1" />

<MudPaper Style="padding: 20px; margin-bottom: 20px;">
    <MudText Typo="Typo.h5">@_currentTour?.Name</MudText>
    <MudText Typo="Typo.body2">@_currentTour?.Description</MudText>

    <div style="margin-top: 20px;">
        <MudButton Variant="Variant.Filled"
                   Color="Color.Primary"
                   OnClick="StartTour"
                   Disabled="_tourRunning">
            Start Tour
        </MudButton>
        <MudButton Variant="Variant.Outlined"
                   OnClick="StopTour"
                   Disabled="!_tourRunning">
            Stop Tour
        </MudButton>
    </div>

    <MudLinearProgress Value="_tourProgress"
                       Color="Color.Primary"
                       Style="margin-top: 10px;"
                       Class="@(_tourRunning ? "" : "d-none")" />
</MudPaper>

<HonuaBookmarks SyncWith="map1" Storage="@Storage" />

@code {
    [Inject] private ComponentBus Bus { get; set; } = null!;

    private Tour? _currentTour;
    private bool _tourRunning;
    private int _currentStop;
    private double _tourProgress;

    protected override async Task OnInitializedAsync()
    {
        // Load tour stops from bookmarks
        var stops = await Storage.GetAllAsync();
        var tourStops = stops.Where(b => b.Tags.Contains("tour")).ToList();

        _currentTour = new Tour
        {
            Name = "San Francisco Highlights",
            Description = "A guided tour of SF's famous landmarks",
            Stops = tourStops
        };
    }

    private async Task StartTour()
    {
        _tourRunning = true;
        _currentStop = 0;

        while (_tourRunning && _currentStop < _currentTour!.Stops.Count)
        {
            var stop = _currentTour.Stops[_currentStop];

            // Navigate to bookmark
            await Bus.PublishAsync(new FlyToRequestMessage
            {
                MapId = "map1",
                Center = stop.Center,
                Zoom = stop.Zoom,
                Bearing = stop.Bearing,
                Pitch = stop.Pitch,
                Duration = 2000
            });

            _tourProgress = (_currentStop + 1) * 100.0 / _currentTour.Stops.Count;
            StateHasChanged();

            // Wait at stop
            await Task.Delay(5000);

            _currentStop++;
        }

        _tourRunning = false;
        _tourProgress = 0;
        StateHasChanged();
    }

    private void StopTour()
    {
        _tourRunning = false;
        _tourProgress = 0;
    }

    private class Tour
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<Bookmark> Stops { get; set; } = new();
    }
}
```

### Example 18: Real Estate Property Viewer

Browse property listings with bookmarks:

```razor
@page "/real-estate"
@inject IBookmarkStorage Storage

<div style="display: flex; height: 100vh;">
    <div style="flex: 1;">
        <HonuaMap Id="map1" />
    </div>
    <div style="width: 400px; overflow-y: auto; border-left: 1px solid #ddd;">
        <MudTabs>
            <MudTabPanel Text="Properties">
                <HonuaBookmarks SyncWith="map1"
                                Storage="@Storage"
                                ViewMode="grid"
                                EnableThumbnails="true"
                                OnBookmarkSelected="ShowPropertyDetails" />
            </MudTabPanel>
            <MudTabPanel Text="Details">
                @if (_selectedProperty != null)
                {
                    <div style="padding: 20px;">
                        <MudText Typo="Typo.h6">@_selectedProperty.Name</MudText>
                        <MudText Typo="Typo.body2">@_selectedProperty.Description</MudText>

                        @if (_selectedProperty.Metadata.ContainsKey("price"))
                        {
                            <MudText Typo="Typo.h5" Color="Color.Primary" Style="margin-top: 10px;">
                                @_selectedProperty.Metadata["price"]
                            </MudText>
                        }

                        <MudDivider Style="margin: 15px 0;" />

                        <MudStack Spacing="2">
                            @if (_selectedProperty.Metadata.ContainsKey("bedrooms"))
                            {
                                <div>
                                    <MudIcon Icon="@Icons.Material.Filled.Bed" Size="Size.Small" />
                                    <span>@_selectedProperty.Metadata["bedrooms"] Bedrooms</span>
                                </div>
                            }
                            @if (_selectedProperty.Metadata.ContainsKey("bathrooms"))
                            {
                                <div>
                                    <MudIcon Icon="@Icons.Material.Filled.Bathtub" Size="Size.Small" />
                                    <span>@_selectedProperty.Metadata["bathrooms"] Bathrooms</span>
                                </div>
                            }
                            @if (_selectedProperty.Metadata.ContainsKey("sqft"))
                            {
                                <div>
                                    <MudIcon Icon="@Icons.Material.Filled.SquareFoot" Size="Size.Small" />
                                    <span>@_selectedProperty.Metadata["sqft"] sq ft</span>
                                </div>
                            }
                        </MudStack>

                        <MudButton Variant="Variant.Filled"
                                   Color="Color.Primary"
                                   FullWidth="true"
                                   Style="margin-top: 20px;">
                            Schedule Tour
                        </MudButton>
                    </div>
                }
                else
                {
                    <div style="padding: 20px; text-align: center;">
                        <MudText Color="Color.Secondary">
                            Select a property to view details
                        </MudText>
                    </div>
                }
            </MudTabPanel>
        </MudTabs>
    </div>
</div>

@code {
    private Bookmark? _selectedProperty;

    protected override async Task OnInitializedAsync()
    {
        // Load sample properties
        var properties = new[]
        {
            new Bookmark
            {
                Name = "Modern Downtown Loft",
                Description = "Stunning views of the city skyline",
                Center = new[] { -122.4083, 37.7833 },
                Zoom = 17,
                Tags = new List<string> { "property", "loft", "luxury" },
                Metadata = new Dictionary<string, object>
                {
                    ["price"] = "$1,250,000",
                    ["bedrooms"] = 2,
                    ["bathrooms"] = 2,
                    ["sqft"] = 1500
                }
            },
            new Bookmark
            {
                Name = "Victorian in Mission District",
                Description = "Classic SF architecture with modern updates",
                Center = new[] { -122.4194, 37.7599 },
                Zoom = 18,
                Tags = new List<string> { "property", "victorian", "historic" },
                Metadata = new Dictionary<string, object>
                {
                    ["price"] = "$2,100,000",
                    ["bedrooms"] = 4,
                    ["bathrooms"] = 3,
                    ["sqft"] = 2800
                }
            }
        };

        foreach (var property in properties)
        {
            await Storage.SaveAsync(property);
        }
    }

    private void ShowPropertyDetails(Bookmark bookmark)
    {
        _selectedProperty = bookmark;
        StateHasChanged();
    }
}
```

---

## Tips & Best Practices

1. **Performance**: Limit thumbnails to 200x150px for optimal performance
2. **Organization**: Use folders when you have >10 bookmarks
3. **Naming**: Use descriptive names with timestamps for temporal bookmarks
4. **Tags**: Add relevant tags for easier searching
5. **Metadata**: Store additional context in the Metadata dictionary
6. **Storage**: Export bookmarks regularly as backup
7. **Sharing**: Generate share URLs for collaboration
8. **Thumbnails**: Disable thumbnails on low-bandwidth connections
9. **Layouts**: Use dropdown for <5 bookmarks, list for 5-20, folders for >20
10. **Events**: Subscribe to ComponentBus messages for cross-component coordination

---

## Additional Resources

- [HonuaBookmarks README](./README.md)
- [Honua.MapSDK Documentation](../../README.md)
- [ComponentBus Guide](../../Core/README.md)

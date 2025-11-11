# HonuaFullscreen Examples

Complete examples demonstrating various use cases for the HonuaFullscreen component.

## Example 1: Basic Fullscreen Button

Simple fullscreen button in the top-right corner of the map.

```razor
@page "/examples/fullscreen-basic"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen

<div class="map-container">
    <HonuaMapLibre
        Id="basic-map"
        Style="https://demotiles.maplibre.org/style.json"
        Center="@(new[] { -122.4194, 37.7749 })"
        Zoom="12" />

    <HonuaFullscreen SyncWith="basic-map" />
</div>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 600px;
    }
</style>
```

## Example 2: Fullscreen with Custom Styling

Customize the button appearance and position.

```razor
@page "/examples/fullscreen-custom"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen

<div class="map-container">
    <HonuaMapLibre
        Id="styled-map"
        Style="https://demotiles.maplibre.org/style.json"
        Center="@(new[] { -0.1276, 51.5074 })"
        Zoom="10" />

    <!-- Large primary button in bottom-left -->
    <HonuaFullscreen
        SyncWith="styled-map"
        Position="bottom-left"
        ButtonColor="Color.Primary"
        ButtonSize="Size.Large"
        ButtonVariant="Variant.Outlined" />
</div>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 600px;
    }
</style>
```

## Example 3: Embedded in Toolbar

Integrate fullscreen button into a custom toolbar.

```razor
@page "/examples/fullscreen-toolbar"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen
@using Honua.MapSDK.Components.Bookmarks
@using Honua.MapSDK.Components.CoordinateDisplay

<MudPaper Elevation="3" Class="pa-4">
    <MudToolBar Dense="true">
        <MudText Typo="Typo.h6">Map Viewer</MudText>
        <MudSpacer />

        <!-- Fullscreen button embedded in toolbar -->
        <HonuaFullscreen
            SyncWith="toolbar-map"
            Position="@null"
            ButtonVariant="Variant.Text"
            ButtonSize="Size.Small" />

        <MudIconButton Icon="@Icons.Material.Filled.Settings" />
    </MudToolBar>

    <div class="map-container">
        <HonuaMapLibre
            Id="toolbar-map"
            Style="https://demotiles.maplibre.org/style.json"
            Center="@(new[] { 2.3522, 48.8566 })"
            Zoom="11" />
    </div>
</MudPaper>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 500px;
    }
</style>
```

## Example 4: Multiple Maps with Fullscreen

Handle fullscreen for multiple maps on the same page.

```razor
@page "/examples/fullscreen-multiple"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen

<MudGrid>
    <MudItem xs="12" md="6">
        <MudPaper Elevation="2" Class="pa-2">
            <MudText Typo="Typo.h6" Class="mb-2">Map 1 - San Francisco</MudText>
            <div class="map-container">
                <HonuaMapLibre
                    Id="map1"
                    Style="https://demotiles.maplibre.org/style.json"
                    Center="@(new[] { -122.4194, 37.7749 })"
                    Zoom="12" />

                <HonuaFullscreen SyncWith="map1" />
            </div>
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6">
        <MudPaper Elevation="2" Class="pa-2">
            <MudText Typo="Typo.h6" Class="mb-2">Map 2 - New York</MudText>
            <div class="map-container">
                <HonuaMapLibre
                    Id="map2"
                    Style="https://demotiles.maplibre.org/style.json"
                    Center="@(new[] { -74.0060, 40.7128 })"
                    Zoom="12" />

                <HonuaFullscreen SyncWith="map2" Position="top-left" />
            </div>
        </MudPaper>
    </MudItem>
</MudGrid>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 400px;
    }
</style>
```

## Example 5: Fullscreen with Event Handling

React to fullscreen state changes.

```razor
@page "/examples/fullscreen-events"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen

<MudStack Spacing="3">
    <MudPaper Class="pa-4">
        <MudAlert Severity="@_alertSeverity" Variant="Variant.Filled">
            @_statusMessage
        </MudAlert>
    </MudPaper>

    <div class="map-container">
        <HonuaMapLibre
            Id="event-map"
            Style="https://demotiles.maplibre.org/style.json"
            Center="@(new[] { -118.2437, 34.0522 })"
            Zoom="10" />

        <HonuaFullscreen
            SyncWith="event-map"
            OnFullscreenChanged="HandleFullscreenChange" />
    </div>

    <MudPaper Class="pa-4">
        <MudText Typo="Typo.h6" Class="mb-2">Fullscreen History</MudText>
        <MudList Dense="true">
            @foreach (var entry in _fullscreenHistory)
            {
                <MudListItem>
                    <MudIcon Icon="@entry.Icon" Size="Size.Small" Class="mr-2" />
                    @entry.Message
                </MudListItem>
            }
        </MudList>
    </MudPaper>
</MudStack>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 500px;
    }
</style>

@code {
    private string _statusMessage = "Map is ready. Click fullscreen button or press F11.";
    private Severity _alertSeverity = Severity.Info;
    private List<HistoryEntry> _fullscreenHistory = new();

    private void HandleFullscreenChange(bool isFullscreen)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");

        if (isFullscreen)
        {
            _statusMessage = $"Fullscreen mode activated at {timestamp}. Press Esc to exit.";
            _alertSeverity = Severity.Success;
            _fullscreenHistory.Insert(0, new HistoryEntry
            {
                Icon = Icons.Material.Filled.Fullscreen,
                Message = $"{timestamp} - Entered fullscreen mode"
            });
        }
        else
        {
            _statusMessage = $"Exited fullscreen mode at {timestamp}.";
            _alertSeverity = Severity.Info;
            _fullscreenHistory.Insert(0, new HistoryEntry
            {
                Icon = Icons.Material.Filled.FullscreenExit,
                Message = $"{timestamp} - Exited fullscreen mode"
            });
        }

        // Keep only last 10 entries
        if (_fullscreenHistory.Count > 10)
        {
            _fullscreenHistory = _fullscreenHistory.Take(10).ToList();
        }

        StateHasChanged();
    }

    private class HistoryEntry
    {
        public string Icon { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
```

## Example 6: Programmatic Fullscreen Control

Control fullscreen mode programmatically from code.

```razor
@page "/examples/fullscreen-programmatic"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen

<MudStack Spacing="3">
    <MudPaper Class="pa-4">
        <MudButtonGroup>
            <MudButton
                StartIcon="@Icons.Material.Filled.Fullscreen"
                Color="Color.Success"
                OnClick="EnterFullscreen"
                Disabled="@_isFullscreen">
                Enter Fullscreen
            </MudButton>
            <MudButton
                StartIcon="@Icons.Material.Filled.FullscreenExit"
                Color="Color.Error"
                OnClick="ExitFullscreen"
                Disabled="@(!_isFullscreen)">
                Exit Fullscreen
            </MudButton>
            <MudButton
                StartIcon="@Icons.Material.Filled.SwapHoriz"
                Color="Color.Primary"
                OnClick="ToggleFullscreen">
                Toggle Fullscreen
            </MudButton>
        </MudButtonGroup>
    </MudPaper>

    <div class="map-container">
        <HonuaMapLibre
            Id="programmatic-map"
            Style="https://demotiles.maplibre.org/style.json"
            Center="@(new[] { -87.6298, 41.8781 })"
            Zoom="10" />

        <!-- Hidden fullscreen control (controlled programmatically) -->
        <HonuaFullscreen
            @ref="_fullscreenControl"
            SyncWith="programmatic-map"
            Position="top-right"
            OnFullscreenChanged="@((isFs) => _isFullscreen = isFs)" />
    </div>
</MudStack>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 500px;
    }
</style>

@code {
    private HonuaFullscreen? _fullscreenControl;
    private bool _isFullscreen = false;

    private async Task EnterFullscreen()
    {
        if (_fullscreenControl != null)
        {
            await _fullscreenControl.EnterFullscreen();
        }
    }

    private async Task ExitFullscreen()
    {
        if (_fullscreenControl != null)
        {
            await _fullscreenControl.ExitFullscreen();
        }
    }

    private async Task ToggleFullscreen()
    {
        if (_fullscreenControl != null)
        {
            await _fullscreenControl.ToggleFullscreen();
        }
    }
}
```

## Example 7: Fullscreen with ComponentBus

Subscribe to fullscreen changes via ComponentBus for cross-component coordination.

```razor
@page "/examples/fullscreen-bus"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen
@using Honua.MapSDK.Core
@using Honua.MapSDK.Core.Messages
@inject ComponentBus Bus

<MudStack Spacing="3">
    <MudPaper Class="pa-4">
        <MudChip
            Icon="@_fullscreenIcon"
            Color="@_fullscreenColor"
            Variant="Variant.Filled"
            Size="Size.Large">
            @_fullscreenStatus
        </MudChip>
    </MudPaper>

    <div class="map-container">
        <HonuaMapLibre
            Id="bus-map"
            Style="https://demotiles.maplibre.org/style.json"
            Center="@(new[] { -95.3698, 29.7604 })"
            Zoom="10" />

        <HonuaFullscreen SyncWith="bus-map" />
    </div>
</MudStack>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 500px;
    }
</style>

@code {
    private string _fullscreenStatus = "Normal Mode";
    private string _fullscreenIcon = Icons.Material.Filled.Crop;
    private Color _fullscreenColor = Color.Default;

    protected override void OnInitialized()
    {
        // Subscribe to fullscreen changes via ComponentBus
        Bus.Subscribe<FullscreenChangedMessage>(args =>
        {
            var msg = args.Message;

            if (msg.IsFullscreen)
            {
                _fullscreenStatus = "Fullscreen Mode Active";
                _fullscreenIcon = Icons.Material.Filled.Fullscreen;
                _fullscreenColor = Color.Success;
            }
            else
            {
                _fullscreenStatus = "Normal Mode";
                _fullscreenIcon = Icons.Material.Filled.Crop;
                _fullscreenColor = Color.Default;
            }

            InvokeAsync(StateHasChanged);
        });
    }
}
```

## Example 8: Fullscreen Specific Element

Make only a specific element fullscreen, not the entire page.

```razor
@page "/examples/fullscreen-element"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen

<MudGrid>
    <MudItem xs="12" md="8">
        <!-- This entire paper will go fullscreen -->
        <MudPaper id="map-panel" Elevation="3" Class="pa-2">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                <MudText Typo="Typo.h6">Map Panel</MudText>

                <!-- Fullscreen button for the entire panel -->
                <HonuaFullscreen
                    TargetElementId="map-panel"
                    Position="@null"
                    ButtonSize="Size.Small" />
            </div>

            <div class="map-container">
                <HonuaMapLibre
                    Id="element-map"
                    Style="https://demotiles.maplibre.org/style.json"
                    Center="@(new[] { 139.6917, 35.6895 })"
                    Zoom="10" />
            </div>

            <div class="pa-2">
                <MudText Typo="Typo.body2">
                    This entire panel (including text and map) will go fullscreen.
                </MudText>
            </div>
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="4">
        <MudPaper Elevation="2" Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-2">Info Panel</MudText>
            <MudText Typo="Typo.body2">
                This panel will remain visible when the map goes fullscreen.
                Only the left panel will be fullscreen.
            </MudText>
        </MudPaper>
    </MudItem>
</MudGrid>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 500px;
    }
</style>
```

## Example 9: Disable Keyboard Shortcuts

Disable automatic keyboard shortcuts if they conflict with your app.

```razor
@page "/examples/fullscreen-no-shortcuts"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen

<MudAlert Severity="Severity.Info" Class="mb-4">
    Keyboard shortcuts (F11) are disabled. Use the button to toggle fullscreen.
</MudAlert>

<div class="map-container">
    <HonuaMapLibre
        Id="no-shortcuts-map"
        Style="https://demotiles.maplibre.org/style.json"
        Center="@(new[] { 12.4964, 41.9028 })"
        Zoom="10" />

    <HonuaFullscreen
        SyncWith="no-shortcuts-map"
        EnableKeyboardShortcuts="false" />
</div>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 500px;
    }
</style>
```

## Example 10: Fullscreen with Other Controls

Combine fullscreen with other map controls.

```razor
@page "/examples/fullscreen-combined"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Fullscreen
@using Honua.MapSDK.Components.CoordinateDisplay
@using Honua.MapSDK.Components.Bookmarks

<div class="map-container">
    <HonuaMapLibre
        Id="combined-map"
        Style="https://demotiles.maplibre.org/style.json"
        Center="@(new[] { 151.2093, -33.8688 })"
        Zoom="10" />

    <!-- Multiple controls positioned around the map -->
    <HonuaFullscreen
        SyncWith="combined-map"
        Position="top-right" />

    <HonuaBookmarks
        SyncWith="combined-map"
        Position="top-left"
        Layout="compact" />

    <HonuaCoordinateDisplay
        SyncWith="combined-map"
        Position="bottom-left" />
</div>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 600px;
    }
</style>
```

## Tips & Best Practices

1. **Positioning**: Use `top-right` for less visual interference with map controls
2. **Multiple Maps**: Use unique `SyncWith` IDs for each map
3. **Custom Elements**: Use `TargetElementId` to fullscreen specific containers
4. **Event Handling**: Subscribe to `OnFullscreenChanged` for UI updates
5. **Keyboard Shortcuts**: Keep enabled unless conflicts exist
6. **ComponentBus**: Use for coordinating multiple components during fullscreen changes
7. **Mobile**: Fullscreen works great on mobile devices for immersive viewing
8. **Accessibility**: The component includes proper ARIA labels automatically

## Common Use Cases

- **Data Visualization**: Full-screen mode for detailed map analysis
- **Presentations**: Quick fullscreen for showing maps to audiences
- **Mobile Apps**: Better map viewing on small screens
- **Photo/Video Integration**: Fullscreen map with media overlays
- **Dashboard**: Expand specific map panels from multi-map layouts

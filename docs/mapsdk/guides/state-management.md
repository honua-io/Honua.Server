# State Management Guide

This guide covers state management patterns for Honua.MapSDK applications, including ComponentBus patterns, shared state, persistence, and real-time synchronization.

---

## Table of Contents

1. [ComponentBus Patterns](#componentbus-patterns)
2. [Shared State](#shared-state)
3. [URL State Persistence](#url-state-persistence)
4. [Local Storage](#local-storage)
5. [Redux/Flux Patterns](#reduxflux-patterns)
6. [Real-time Sync](#realtime-sync)

---

## ComponentBus Patterns

### Basic ComponentBus Usage

```csharp
@inject ComponentBus Bus

@code {
    protected override void OnInitialized()
    {
        // Subscribe to messages
        Bus.Subscribe<MapExtentChangedMessage>(HandleExtentChanged);
        Bus.Subscribe<FeatureClickedMessage>(HandleFeatureClicked);
    }

    private void HandleExtentChanged(MessageContext<MapExtentChangedMessage> context)
    {
        var message = context.Message;
        Console.WriteLine($"Map extent changed: Zoom {message.Zoom}");
    }

    private void HandleFeatureClicked(MessageContext<FeatureClickedMessage> context)
    {
        var message = context.Message;
        Console.WriteLine($"Feature clicked: {message.FeatureId}");
    }

    public void Dispose()
    {
        // Clean up subscriptions
        Bus.Unsubscribe<MapExtentChangedMessage>(HandleExtentChanged);
        Bus.Unsubscribe<FeatureClickedMessage>(HandleFeatureClicked);
    }
}
```

### Custom Messages

```csharp
// Define custom message
public record FilterChangedMessage(string FilterName, object FilterValue) : IComponentBusMessage;

// Publish message
Bus.Publish(new FilterChangedMessage("category", "residential"));

// Subscribe to message
Bus.Subscribe<FilterChangedMessage>(context =>
{
    var filter = context.Message;
    ApplyFilter(filter.FilterName, filter.FilterValue);
});
```

### Scoped Component Communication

```csharp
// Use scoped bus for component group
public class MapDashboard : ComponentBase
{
    [Inject] private IComponentBusFactory BusFactory { get; set; }
    private ComponentBus _scopedBus;

    protected override void OnInitialized()
    {
        // Create scoped bus for this dashboard
        _scopedBus = BusFactory.CreateScoped("dashboard-1");
    }

    private void BroadcastToScope()
    {
        _scopedBus.Publish(new SelectionChangedMessage("feature-123"));
    }
}
```

---

## Shared State

### State Container Pattern

```csharp
// Define state container
public class AppState
{
    private List<Feature> _selectedFeatures = new();
    private double[] _mapCenter = new[] { -122.4194, 37.7749 };
    private double _mapZoom = 12;

    public event Action? OnStateChanged;

    public IReadOnlyList<Feature> SelectedFeatures => _selectedFeatures.AsReadOnly();
    public double[] MapCenter => _mapCenter;
    public double MapZoom => _mapZoom;

    public void SelectFeature(Feature feature)
    {
        if (!_selectedFeatures.Contains(feature))
        {
            _selectedFeatures.Add(feature);
            NotifyStateChanged();
        }
    }

    public void DeselectFeature(Feature feature)
    {
        if (_selectedFeatures.Remove(feature))
        {
            NotifyStateChanged();
        }
    }

    public void ClearSelection()
    {
        _selectedFeatures.Clear();
        NotifyStateChanged();
    }

    public void UpdateMapView(double[] center, double zoom)
    {
        _mapCenter = center;
        _mapZoom = zoom;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}

// Register in Program.cs
builder.Services.AddScoped<AppState>();
```

### Using State Container

```razor
@inject AppState State
@implements IDisposable

<MudChip Color="Color.Primary">
    Selected: @State.SelectedFeatures.Count
</MudChip>

@code {
    protected override void OnInitialized()
    {
        State.OnStateChanged += StateHasChanged;
    }

    public void Dispose()
    {
        State.OnStateChanged -= StateHasChanged;
    }
}
```

### Cascading State

```razor
<!-- App.razor -->
<CascadingValue Value="@_appState">
    <Router AppAssembly="@typeof(App).Assembly">
        @* Router content *@
    </Router>
</CascadingValue>

@code {
    private AppState _appState = new();
}

<!-- Child Component -->
@code {
    [CascadingParameter]
    public AppState State { get; set; }

    private void HandleSelection(Feature feature)
    {
        State.SelectFeature(feature);
    }
}
```

---

## URL State Persistence

### Save Map State to URL

```csharp
@inject NavigationManager NavigationManager

private async Task SaveMapStateToUrl()
{
    var center = await _map.GetCenterAsync();
    var zoom = await _map.GetZoomAsync();

    var query = QueryHelpers.AddQueryString("",
        new Dictionary<string, string>
        {
            ["lat"] = center[1].ToString("F6"),
            ["lon"] = center[0].ToString("F6"),
            ["zoom"] = zoom.ToString("F2")
        });

    NavigationManager.NavigateTo(query, replace: true);
}

protected override async Task OnInitializedAsync()
{
    // Restore from URL
    var uri = new Uri(NavigationManager.Uri);
    var query = QueryHelpers.ParseQuery(uri.Query);

    if (query.TryGetValue("lat", out var lat) &&
        query.TryGetValue("lon", out var lon) &&
        query.TryGetValue("zoom", out var zoom))
    {
        _initialCenter = new[] {
            double.Parse(lon),
            double.Parse(lat)
        };
        _initialZoom = double.Parse(zoom);
    }
}
```

### Share Map View Links

```razor
<MudButton OnClick="@CopyShareLink" StartIcon="@Icons.Material.Filled.Share">
    Share Map View
</MudButton>

@code {
    [Inject] private ISnackbar Snackbar { get; set; }

    private async Task CopyShareLink()
    {
        var center = await _map.GetCenterAsync();
        var zoom = await _map.GetZoomAsync();

        var shareUrl = $"{NavigationManager.BaseUri}map?" +
            $"lat={center[1]:F6}&lon={center[0]:F6}&zoom={zoom:F2}";

        await JS.InvokeVoidAsync("navigator.clipboard.writeText", shareUrl);
        Snackbar.Add("Link copied to clipboard!", Severity.Success);
    }
}
```

---

## Local Storage

### Save/Load User Preferences

```csharp
public class PreferencesService
{
    private readonly ILocalStorageService _localStorage;

    public PreferencesService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task SaveMapPreferences(MapPreferences prefs)
    {
        await _localStorage.SetItemAsync("map-preferences", prefs);
    }

    public async Task<MapPreferences?> LoadMapPreferences()
    {
        return await _localStorage.GetItemAsync<MapPreferences>("map-preferences");
    }

    public async Task SaveRecentSearches(List<string> searches)
    {
        await _localStorage.SetItemAsync("recent-searches", searches);
    }

    public async Task<List<string>> LoadRecentSearches()
    {
        return await _localStorage.GetItemAsync<List<string>>("recent-searches")
            ?? new List<string>();
    }
}

public class MapPreferences
{
    public string PreferredBasemap { get; set; } = string.Empty;
    public double[] LastMapCenter { get; set; } = Array.Empty<double>();
    public double LastMapZoom { get; set; }
    public bool ShowLabels { get; set; } = true;
    public bool EnableClustering { get; set; } = true;
}

// Register in Program.cs
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<PreferencesService>();
```

### Usage

```razor
@inject PreferencesService PreferencesService

@code {
    protected override async Task OnInitializedAsync()
    {
        var prefs = await PreferencesService.LoadMapPreferences();
        if (prefs != null)
        {
            _mapCenter = prefs.LastMapCenter;
            _mapZoom = prefs.LastMapZoom;
            _basemapUrl = prefs.PreferredBasemap;
        }
    }

    private async Task SaveCurrentView()
    {
        var prefs = new MapPreferences
        {
            PreferredBasemap = _basemapUrl,
            LastMapCenter = await _map.GetCenterAsync(),
            LastMapZoom = await _map.GetZoomAsync(),
            ShowLabels = _showLabels,
            EnableClustering = _enableClustering
        };

        await PreferencesService.SaveMapPreferences(prefs);
        Snackbar.Add("Preferences saved", Severity.Success);
    }
}
```

---

## Redux/Flux Patterns

### Fluxor Implementation

```csharp
// Install: dotnet add package Fluxor.Blazor.Web

// State
public record AppFeatureState
{
    public List<Feature> Features { get; init; } = new();
    public List<Feature> SelectedFeatures { get; init; } = new();
    public bool IsLoading { get; init; }
    public string? Error { get; init; }
}

// Actions
public record LoadFeaturesAction();
public record LoadFeaturesSuccessAction(List<Feature> Features);
public record LoadFeaturesFailureAction(string Error);
public record SelectFeatureAction(Feature Feature);
public record DeselectFeatureAction(Feature Feature);

// Reducers
public class AppReducers
{
    [ReducerMethod]
    public static AppFeatureState ReduceLoadFeaturesAction(
        AppFeatureState state,
        LoadFeaturesAction action) =>
        state with { IsLoading = true, Error = null };

    [ReducerMethod]
    public static AppFeatureState ReduceLoadFeaturesSuccessAction(
        AppFeatureState state,
        LoadFeaturesSuccessAction action) =>
        state with { Features = action.Features, IsLoading = false };

    [ReducerMethod]
    public static AppFeatureState ReduceSelectFeatureAction(
        AppFeatureState state,
        SelectFeatureAction action)
    {
        var selected = state.SelectedFeatures.ToList();
        if (!selected.Contains(action.Feature))
        {
            selected.Add(action.Feature);
        }
        return state with { SelectedFeatures = selected };
    }
}

// Effects
public class AppEffects
{
    private readonly IFeatureService _featureService;

    public AppEffects(IFeatureService featureService)
    {
        _featureService = featureService;
    }

    [EffectMethod]
    public async Task HandleLoadFeaturesAction(LoadFeaturesAction action, IDispatcher dispatcher)
    {
        try
        {
            var features = await _featureService.GetFeaturesAsync();
            dispatcher.Dispatch(new LoadFeaturesSuccessAction(features));
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new LoadFeaturesFailureAction(ex.Message));
        }
    }
}

// Usage in component
@inject IState<AppFeatureState> State
@inject IDispatcher Dispatcher

<MudButton OnClick="@LoadFeatures">Load Features</MudButton>
<MudText>Loaded: @State.Value.Features.Count</MudText>

@code {
    private void LoadFeatures()
    {
        Dispatcher.Dispatch(new LoadFeaturesAction());
    }

    private void SelectFeature(Feature feature)
    {
        Dispatcher.Dispatch(new SelectFeatureAction(feature));
    }
}
```

---

## Real-time Sync

### SignalR State Synchronization

```csharp
// Hub
public class MapStateHub : Hub
{
    private static readonly Dictionary<string, MapState> _roomStates = new();

    public async Task JoinRoom(string roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        if (_roomStates.TryGetValue(roomId, out var state))
        {
            await Clients.Caller.SendAsync("ReceiveState", state);
        }
    }

    public async Task UpdateMapView(string roomId, double[] center, double zoom)
    {
        var state = _roomStates.GetValueOrDefault(roomId) ?? new MapState();
        state.Center = center;
        state.Zoom = zoom;
        state.LastUpdate = DateTime.UtcNow;
        _roomStates[roomId] = state;

        await Clients.OthersInGroup(roomId).SendAsync("MapViewChanged", center, zoom);
    }

    public async Task SelectFeature(string roomId, string featureId)
    {
        await Clients.OthersInGroup(roomId).SendAsync("FeatureSelected", featureId);
    }
}

public class MapState
{
    public double[] Center { get; set; } = Array.Empty<double>();
    public double Zoom { get; set; }
    public DateTime LastUpdate { get; set; }
}
```

### Client Implementation

```csharp
@implements IAsyncDisposable

private HubConnection? _hubConnection;
private string _roomId = "room-123";

protected override async Task OnInitializedAsync()
{
    _hubConnection = new HubConnectionBuilder()
        .WithUrl(NavigationManager.ToAbsoluteUri("/mapStateHub"))
        .WithAutomaticReconnect()
        .Build();

    _hubConnection.On<MapState>("ReceiveState", state =>
    {
        _mapCenter = state.Center;
        _mapZoom = state.Zoom;
        StateHasChanged();
    });

    _hubConnection.On<double[], double>("MapViewChanged", async (center, zoom) =>
    {
        await _map.SetViewAsync(center, zoom);
    });

    _hubConnection.On<string>("FeatureSelected", featureId =>
    {
        HighlightFeature(featureId);
    });

    await _hubConnection.StartAsync();
    await _hubConnection.SendAsync("JoinRoom", _roomId);
}

private async Task HandleExtentChanged(MapExtentChangedMessage message)
{
    // Broadcast to other users
    if (_hubConnection?.State == HubConnectionState.Connected)
    {
        await _hubConnection.SendAsync("UpdateMapView", _roomId,
            message.Center, message.Zoom);
    }
}

public async ValueTask DisposeAsync()
{
    if (_hubConnection != null)
    {
        await _hubConnection.DisposeAsync();
    }
}
```

---

## Best Practices

### State Management Checklist

- [ ] Use ComponentBus for component-to-component communication
- [ ] Implement state container for app-wide state
- [ ] Persist important state to URL for shareability
- [ ] Save user preferences to local storage
- [ ] Use Fluxor for complex state management
- [ ] Implement real-time sync for collaborative features
- [ ] Clean up event subscriptions in Dispose
- [ ] Avoid circular dependencies in state updates
- [ ] Use immutable state updates when possible
- [ ] Profile state updates for performance issues

### Anti-Patterns to Avoid

❌ **Don't** store everything in a single global state
❌ **Don't** forget to unsubscribe from events
❌ **Don't** mutate state directly (use immutable patterns)
❌ **Don't** over-use cascading parameters
❌ **Don't** ignore state synchronization in real-time apps

---

## Further Reading

- [Fluxor Documentation](https://github.com/mrpmorris/Fluxor)
- [SignalR with Blazor](https://docs.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor)
- [Blazor State Management](https://docs.microsoft.com/en-us/aspnet/core/blazor/state-management)

---

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*

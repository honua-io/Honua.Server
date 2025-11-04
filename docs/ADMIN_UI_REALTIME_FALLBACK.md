# Admin UI Real-Time Updates - Database Fallback Strategy

**Date:** 2025-11-03

---

## Overview

HonuaIO supports multiple metadata providers, each with different real-time update capabilities. The Admin UI must gracefully handle all scenarios.

**Hot Reload Architecture:**

When an administrator makes a metadata change via the Admin UI, all servers consuming that metadata provider automatically reload without restart:

- **Public API servers** (WMS, WFS, OGC Features, WMTS, Tiles, etc.) - hot reload metadata
- **Admin API servers** - reload and broadcast updates to UI clients via SignalR
- **Any Honua.Server.Host instances** connected to the same provider

**This enables zero-downtime metadata management:**
- Create a new WMS service → immediately available to public WMS clients
- Update layer styling → new styles served instantly
- Disable a service → public API returns 404 without restart
- Reconfigure caching → changes applied immediately

---

## Metadata Provider Comparison

| Provider | Real-Time Support | Mechanism | Latency | Multi-Server Support |
|----------|------------------|-----------|---------|---------------------|
| **PostgresMetadataProvider** | ✅ Yes | NOTIFY/LISTEN | ~100ms (same server)<br>~1s (cross-server) | ✅ Yes |
| **RedisMetadataProvider** | ✅ Yes | Pub/Sub | <100ms | ✅ Yes |
| **SqlServerMetadataProvider** | ⚠️ Polling | Timer-based polling | Configurable (default 5-30s) | ✅ Yes |
| **JsonMetadataProvider** | ⚠️ Optional | FileSystemWatcher | ~100ms | ❌ No (file-based) |
| **YamlMetadataProvider** | ⚠️ Optional | FileSystemWatcher | ~100ms | ❌ No (file-based) |

---

## Detection Pattern

All providers implement `IMetadataProvider` with:

```csharp
public interface IMetadataProvider
{
    bool SupportsChangeNotifications { get; }
    event EventHandler<MetadataChangedEventArgs>? MetadataChanged;
}
```

**Server-side detection:**

The Admin API server subscribes to provider changes and pushes to UI clients via SignalR:

```csharp
// Server: Admin/MetadataChangeNotificationHub.cs
public class MetadataChangeNotificationHub : Hub
{
    private readonly IMutableMetadataProvider _metadataProvider;

    public MetadataChangeNotificationHub(IMutableMetadataProvider metadataProvider)
    {
        _metadataProvider = metadataProvider;

        if (_metadataProvider.SupportsChangeNotifications)
        {
            _metadataProvider.MetadataChanged += OnMetadataChanged;
        }
    }

    private async void OnMetadataChanged(object? sender, MetadataChangedEventArgs e)
    {
        // Broadcast to all connected admin UI clients
        await Clients.All.SendAsync("MetadataChanged", new
        {
            e.ChangeType,
            e.EntityType,
            e.EntityId,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task<bool> GetSupportsRealTimeUpdates()
    {
        return _metadataProvider.SupportsChangeNotifications;
    }
}
```

**Client-side (Admin UI) detection:**

```razor
@inject HubConnection MetadataHub
@inject HttpClient Http

@code {
    protected override async Task OnInitializedAsync()
    {
        // Check if server supports real-time updates
        _supportsRealTime = await MetadataHub.InvokeAsync<bool>("GetSupportsRealTimeUpdates");

        if (_supportsRealTime)
        {
            // Subscribe to server-side notifications
            MetadataHub.On<MetadataChangedNotification>("MetadataChanged", HandleMetadataChanged);
        }
        else
        {
            // Use fallback strategy (manual refresh or polling)
            _showRefreshButton = true;
        }
    }

    private async Task HandleMetadataChanged(MetadataChangedNotification notification)
    {
        // Refresh data from admin API
        await LoadServicesAsync();
        StateHasChanged();
    }

    private async Task LoadServicesAsync()
    {
        // Call REST API (works in both combined and detached deployments)
        _services = await Http.GetFromJsonAsync<List<ServiceDto>>("/admin/metadata/services");
    }
}
```

**Key Architecture Points:**

✅ **UI never directly injects `IMutableMetadataProvider`** - this ensures detached deployment works
✅ **All data access goes through REST API** (`/admin/metadata/*`) - consistent auth (OAuth/SAML)
✅ **Server manages provider subscriptions** - UI gets notifications via SignalR
✅ **Works in combined or detached mode** - same client code for both deployment models

---

## Fallback Strategies

### Strategy 1: Manual Refresh (Simplest)

**For providers without real-time support:**

```razor
@inject HttpClient Http
@inject HubConnection MetadataHub
@inject ISnackbar Snackbar

<MudDataGrid Items="@_services">
    @* Data grid content *@
</MudDataGrid>

@if (!_supportsRealTime)
{
    <MudButton OnClick="RefreshAsync" Variant="Variant.Outlined" Color="Color.Primary">
        <MudIcon Icon="@Icons.Material.Filled.Refresh" />
        Refresh
    </MudButton>

    <MudText Typo="Typo.Caption" Class="mt-2">
        Real-time updates not available. Click Refresh to see latest changes.
    </MudText>
}

@code {
    private List<ServiceDto> _services = new();
    private bool _supportsRealTime;

    protected override async Task OnInitializedAsync()
    {
        // Query server capabilities via SignalR hub
        _supportsRealTime = await MetadataHub.InvokeAsync<bool>("GetSupportsRealTimeUpdates");

        if (_supportsRealTime)
        {
            // Subscribe to real-time notifications
            MetadataHub.On<MetadataChangedNotification>("MetadataChanged", HandleMetadataChanged);
        }

        await LoadServicesAsync();
    }

    private async Task RefreshAsync()
    {
        await LoadServicesAsync();
        Snackbar.Add("Data refreshed", Severity.Success);
    }

    private async Task LoadServicesAsync()
    {
        // Call admin REST API (authenticated via bearer token)
        _services = await Http.GetFromJsonAsync<List<ServiceDto>>("/admin/metadata/services");
    }

    private async Task HandleMetadataChanged(MetadataChangedNotification notification)
    {
        if (notification.EntityType == "Service")
        {
            await LoadServicesAsync();
            StateHasChanged();
        }
    }
}
```

**Pros:**
- Simple to implement
- Works with all providers
- No overhead when not needed

**Cons:**
- Manual user action required
- Not real-time

---

### Strategy 2: UI-Side Polling (Automatic)

**For better UX when real-time not available:**

```csharp
// Shared/Services/ApiPollingService.cs
public class ApiPollingService : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly HubConnection _metadataHub;
    private Timer? _timer;
    private bool _isPolling;
    private bool _supportsRealTime;

    public event Action? OnDataChanged;

    public ApiPollingService(HttpClient http, HubConnection metadataHub)
    {
        _http = http;
        _metadataHub = metadataHub;
    }

    public async Task StartPollingAsync(int intervalSeconds = 5)
    {
        // Check if server supports real-time updates
        _supportsRealTime = await _metadataHub.InvokeAsync<bool>("GetSupportsRealTimeUpdates");

        if (_isPolling || _supportsRealTime)
        {
            return; // Don't poll if real-time is available
        }

        _timer = new Timer(
            async _ => await CheckForChangesAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(intervalSeconds));

        _isPolling = true;
    }

    private async Task CheckForChangesAsync()
    {
        try
        {
            // Poll admin API for changes (uses bearer token automatically)
            // This could call a specific "check for updates" endpoint
            // or just trigger a data refresh in the UI
            OnDataChanged?.Invoke();
        }
        catch
        {
            // Silently fail - next poll will try again
        }
    }

    public void StopPolling()
    {
        _timer?.Dispose();
        _timer = null;
        _isPolling = false;
    }

    public ValueTask DisposeAsync()
    {
        StopPolling();
        return ValueTask.CompletedTask;
    }
}

// Register as scoped service
builder.Services.AddScoped<ApiPollingService>();
```

**Usage in components:**

```razor
@inject ApiPollingService Polling
@inject HubConnection MetadataHub
@inject HttpClient Http
@implements IAsyncDisposable

@code {
    protected override async Task OnInitializedAsync()
    {
        var supportsRealTime = await MetadataHub.InvokeAsync<bool>("GetSupportsRealTimeUpdates");

        if (!supportsRealTime)
        {
            // Start polling if real-time not available
            Polling.OnDataChanged += HandleDataChanged;
            await Polling.StartPollingAsync(intervalSeconds: 5);
        }
        else
        {
            // Subscribe to SignalR notifications
            MetadataHub.On<MetadataChangedNotification>("MetadataChanged", HandleMetadataChanged);
        }
    }

    private async void HandleDataChanged()
    {
        // Refresh data from admin API
        await InvokeAsync(async () =>
        {
            _services = await Http.GetFromJsonAsync<List<ServiceDto>>("/admin/metadata/services");
            StateHasChanged();
        });
    }

    public async ValueTask DisposeAsync()
    {
        Polling.OnDataChanged -= HandleDataChanged;
        await Polling.DisposeAsync();
    }
}
```

**Pros:**
- Automatic updates
- Configurable interval
- Only polls when needed

**Cons:**
- Network overhead (every 5s)
- Not truly real-time

---

### Strategy 3: Hybrid (Recommended)

**Combine real-time + manual refresh + smart polling:**

```razor
@inject HttpClient Http
@inject HubConnection MetadataHub
@inject NavigationState NavState

<MudPaper Class="pa-2 mb-2">
    <div class="d-flex align-items-center">
        @if (_hasRealTimeUpdates)
        {
            <MudChip Color="Color.Success" Size="Size.Small">
                <MudIcon Icon="@Icons.Material.Filled.Wifi" Size="Size.Small" />
                Live Updates
            </MudChip>
        }
        else if (_isPolling)
        {
            <MudChip Color="Color.Warning" Size="Size.Small">
                <MudIcon Icon="@Icons.Material.Filled.Update" Size="Size.Small" />
                Auto-Refresh (5s)
            </MudChip>
        }
        else
        {
            <MudChip Color="Color.Default" Size="Size.Small">
                <MudIcon Icon="@Icons.Material.Filled.CloudOff" Size="Size.Small" />
                Manual Mode
            </MudChip>
        }

        <MudSpacer />

        <MudIconButton
            Icon="@Icons.Material.Filled.Refresh"
            OnClick="RefreshAsync"
            Size="Size.Small"
            Title="Refresh data" />

        @if (!_hasRealTimeUpdates)
        {
            <MudSwitch
                @bind-Value="_isPolling"
                Label="Auto-Refresh"
                Color="Color.Primary"
                Size="Size.Small"
                T="bool"
                ValueChanged="OnPollingToggled" />
        }
    </div>
</MudPaper>

<MudDataGrid Items="@_services" />

@code {
    private List<ServiceDto> _services = new();
    private bool _hasRealTimeUpdates;
    private bool _isPolling;
    private Timer? _pollTimer;

    protected override async Task OnInitializedAsync()
    {
        // Query server capabilities via SignalR
        _hasRealTimeUpdates = await MetadataHub.InvokeAsync<bool>("GetSupportsRealTimeUpdates");

        if (_hasRealTimeUpdates)
        {
            // Subscribe to real-time SignalR notifications
            MetadataHub.On<MetadataChangedNotification>("MetadataChanged", HandleMetadataChanged);
        }
        else
        {
            // Default to polling enabled for providers without real-time
            _isPolling = true;
            StartPolling();
        }

        await LoadServicesAsync();
    }

    private void OnPollingToggled(bool enabled)
    {
        if (enabled)
        {
            StartPolling();
        }
        else
        {
            StopPolling();
        }
    }

    private void StartPolling()
    {
        _pollTimer = new Timer(
            async _ => await InvokeAsync(RefreshAsync),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));
    }

    private void StopPolling()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private async Task RefreshAsync()
    {
        await LoadServicesAsync();
        StateHasChanged();
    }

    private async Task LoadServicesAsync()
    {
        // Call admin REST API (authenticated via bearer token)
        _services = await Http.GetFromJsonAsync<List<ServiceDto>>("/admin/metadata/services");
    }

    private async Task HandleMetadataChanged(MetadataChangedNotification notification)
    {
        if (notification.EntityType == "Service")
        {
            await LoadServicesAsync();
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
    }
}
```

**Pros:**
- Best UX for all providers
- User can control polling
- Shows current mode to user
- Graceful degradation

**Cons:**
- More complex implementation

---

## Provider-Specific Details

### PostgreSQL (Best Experience)

```json
{
  "Metadata": {
    "Provider": "postgres",
    "EnableNotifications": true  // Real-time via NOTIFY/LISTEN
  }
}
```

**Real-time:** ✅ Yes (NOTIFY/LISTEN)
**Multi-server:** ✅ Yes
**Admin UI:** Subscribe to SignalR `MetadataChanged` notifications from server
**Server:** Subscribes to provider's `MetadataChanged` event, broadcasts to UI clients
**Fallback:** None needed

---

### Redis (Best Experience)

```json
{
  "Metadata": {
    "Provider": "redis",
    "KeyPrefix": "honua"
  }
}
```

**Real-time:** ✅ Yes (Pub/Sub)
**Multi-server:** ✅ Yes
**Admin UI:** Subscribe to SignalR `MetadataChanged` notifications from server
**Server:** Subscribes to provider's `MetadataChanged` event, broadcasts to UI clients
**Fallback:** None needed

---

### SQL Server (Polling)

```json
{
  "Metadata": {
    "Provider": "sqlserver",
    "EnablePolling": true,
    "PollingIntervalSeconds": 5  // Check every 5 seconds
  }
}
```

**Real-time:** ⚠️ Polling-based
**Multi-server:** ✅ Yes (all servers poll)
**Latency:** 5-30 seconds (configurable)
**Admin UI:** Subscribe to SignalR `MetadataChanged` notifications from server
**Server:**
- Provider fires `MetadataChanged` event when poll detects change
- Server broadcasts to UI clients via SignalR (works like real-time, just slower)
**Fallback:** Manual refresh button for immediate updates

---

### JSON File (Single Server)

```json
{
  "Metadata": {
    "Provider": "json",
    "Path": "metadata.json",
    "WatchForChanges": true  // Enable FileSystemWatcher
  }
}
```

**Real-time:** ⚠️ FileSystemWatcher (if enabled)
**Multi-server:** ❌ No (file-based)
**Admin UI:** Subscribe to SignalR `MetadataChanged` notifications from server
**Server:**
- If `WatchForChanges=true`: Provider fires `MetadataChanged` event, server broadcasts to UI
- If `WatchForChanges=false`: No automatic updates, server returns `SupportsRealTimeUpdates=false`
**Fallback:** Manual refresh button or UI-side polling

**Note:** For multi-server deployments, use PostgreSQL or Redis instead.

---

### YAML File (Single Server)

```json
{
  "Metadata": {
    "Provider": "yaml",
    "Path": "metadata.yaml",
    "WatchForChanges": true
  }
}
```

**Real-time:** ⚠️ FileSystemWatcher (if enabled)
**Multi-server:** ❌ No (file-based)
**Admin UI:** Same as JSON
**Fallback:** Manual refresh button

---

## Recommended Configuration by Deployment Type

| Deployment Type | Recommended Provider | Real-Time Updates | Notes |
|----------------|---------------------|-------------------|-------|
| **Production Multi-Server** | PostgreSQL | ✅ Yes (NOTIFY/LISTEN) | Best choice - database already required |
| **Production Multi-Server** | Redis | ✅ Yes (Pub/Sub) | If Redis already in use |
| **Production Single Server** | PostgreSQL | ✅ Yes | Still best for ACID guarantees |
| **Enterprise (SQL Server)** | SQL Server | ⚠️ Polling (5s) | Works well, slight delay |
| **Development** | JSON + FileWatcher | ⚠️ Yes (local) | Easy to edit, works locally |
| **GitOps/CI/CD** | YAML + FileWatcher | ⚠️ Yes (local) | Version controlled |

---

## Implementation in Admin UI

### Universal Component Pattern

**This pattern works in both combined and detached deployments:**

```razor
@* UniversalMetadataComponent.razor *@
@inject HttpClient Http
@inject HubConnection MetadataHub
@implements IAsyncDisposable

@code {
    private List<ServiceDto> _services = new();
    private bool _supportsRealTime;
    private Timer? _pollTimer;

    protected override async Task OnInitializedAsync()
    {
        // Query server capabilities via SignalR
        _supportsRealTime = await MetadataHub.InvokeAsync<bool>("GetSupportsRealTimeUpdates");

        if (_supportsRealTime)
        {
            // Real-time mode: Subscribe to SignalR notifications
            MetadataHub.On<MetadataChangedNotification>("MetadataChanged", OnMetadataChanged);
        }
        else
        {
            // Fallback: Poll admin API every 5 seconds
            _pollTimer = new Timer(
                async _ => await InvokeAsync(LoadDataAsync),
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));
        }

        await LoadDataAsync();
    }

    private async Task OnMetadataChanged(MetadataChangedNotification notification)
    {
        await InvokeAsync(async () =>
        {
            await LoadDataAsync();
            StateHasChanged();
        });
    }

    private async Task LoadDataAsync()
    {
        // Call admin REST API (authenticated via bearer token)
        // Works in both combined and detached deployments
        _services = await Http.GetFromJsonAsync<List<ServiceDto>>("/admin/metadata/services");
    }

    public async ValueTask DisposeAsync()
    {
        if (_supportsRealTime)
        {
            // Unsubscribe from SignalR notifications
            MetadataHub.Remove("MetadataChanged");
        }

        if (_pollTimer != null)
        {
            await _pollTimer.DisposeAsync();
        }
    }
}
```

**Architecture benefits:**
- ✅ **No direct provider injection** - respects REST API boundaries
- ✅ **Works in detached mode** - all communication via HTTP/SignalR
- ✅ **Consistent authentication** - bearer token passed via HttpClient
- ✅ **Server manages provider** - UI only consumes notifications

---

## Migration Path

If a deployment starts with JSON/YAML and later moves to PostgreSQL:

1. **Phase 1:** Deploy with JSON provider
   - Server: JSON provider may or may not support `SupportsChangeNotifications`
   - Admin UI: Queries server via `GetSupportsRealTimeUpdates()`, shows "Manual Mode" or polls
   - Works fine for single server

2. **Phase 2:** Migrate to PostgreSQL
   - Run migration script to import JSON → PostgreSQL
   - Update `appsettings.json` to use Postgres provider
   - Restart server

3. **Phase 3:** Automatic upgrade
   - Server: PostgreSQL provider now supports real-time (NOTIFY/LISTEN)
   - Server: `MetadataChangeNotificationHub` starts broadcasting changes
   - Admin UI: Queries `GetSupportsRealTimeUpdates()`, receives `true`
   - Admin UI: Automatically switches to real-time mode
   - **No UI code changes needed!**

---

## Summary

| Scenario | Solution |
|----------|----------|
| **PostgreSQL or Redis** | ✅ Real-time updates work out of the box |
| **SQL Server** | ⚠️ Polling-based events (5-30s delay) |
| **JSON/YAML with FileWatcher** | ⚠️ Real-time for single server only |
| **JSON/YAML without FileWatcher** | ❌ Manual refresh button |
| **Any provider** | ✅ Admin UI adapts automatically via `SupportsChangeNotifications` |

**Recommended Strategy:** Use **Hybrid Strategy** (Strategy 3) for best user experience across all providers.

---

## Code Checklist for Admin UI

- [ ] Check `MetadataProvider.SupportsChangeNotifications` on component init
- [ ] Subscribe to `MetadataChanged` event if real-time available
- [ ] Show status indicator (Live Updates / Auto-Refresh / Manual Mode)
- [ ] Provide manual refresh button as backup
- [ ] Allow user to toggle polling on/off for non-real-time providers
- [ ] Dispose event subscriptions and timers properly
- [ ] Handle errors gracefully (failed polls, lost connections)

---

**Next Steps:**

Want me to implement the **Hybrid Strategy (Strategy 3)** in the Admin UI components? This will ensure the UI works optimally with all metadata providers.

# Honua.Server UI Architecture Overview

**Exploration Date:** November 8, 2025  
**Codebase Version:** .NET 9 / Blazor Web  
**UI Framework:** Blazor (ASP.NET Core) with MudBlazor  
**Hosting:** Server-side Blazor with Interactive Server rendering  

---

## Table of Contents

1. [UI Framework & Technology Stack](#ui-framework--technology-stack)
2. [Project Structure & Component Organization](#project-structure--component-organization)
3. [Key User Workflows & Pages](#key-user-workflows--pages)
4. [State Management Architecture](#state-management-architecture)
5. [Styling & CSS Approach](#styling--css-approach)
6. [Error Handling & User Feedback](#error-handling--user-feedback)
7. [Loading States & Async Patterns](#loading-states--async-patterns)
8. [Real-Time Communication](#real-time-communication)
9. [Responsiveness & Mobile Support](#responsiveness--mobile-support)
10. [Performance Optimizations](#performance-optimizations)
11. [Identified Issues & Responsiveness Gaps](#identified-issues--responsiveness-gaps)
12. [Critical UI Files](#critical-ui-files)

---

## 1. UI Framework & Technology Stack

### Framework: Blazor (Server-Side)
- **Runtime:** ASP.NET Core with Blazor Web hosting model
- **Rendering Mode:** `InteractiveServer` (Server-side rendering with dynamic updates)
- **.NET Version:** .NET 9.0
- **Syntax:** Razor Components (.razor files)

### UI Component Library: MudBlazor 8.0.0
**Purpose:** Provides Material Design components and theming

**Key Features:**
- Material Design components (buttons, cards, dialogs, tables, grids)
- Built-in responsive grid system (`xs`, `sm`, `md`, `lg`, `xl` breakpoints)
- Dialog service for modals
- Snackbar service for toast notifications
- Theme provider with customizable colors
- Data table with pagination and sorting
- Form validation integration

**MudBlazor Providers Used:**
```razor
<MudThemeProvider />     <!-- Material Design theming -->
<MudPopoverProvider />   <!-- Popover/tooltip support -->
<MudDialogProvider />    <!-- Modal dialog support -->
<MudSnackbarProvider />  <!-- Toast notification support -->
```

### Additional Libraries
- **FluentValidation** (v11.11.0) - Server-side form validation
- **SignalR Client** (v9.0.0) - Real-time communication
- **System.Text.Json** (v9.0.9) - JSON serialization
- **Microsoft.Extensions.Http** (v9.0.1) - HTTP client factory

### JavaScript Interop
- **MapLibre GL JS** (v4.1.2) - Vector tile rendering with GPU acceleration
- **Custom JS Modules** - Interop handlers for file downloads, drag-and-drop, map management

---

## 2. Project Structure & Component Organization

### Honua.Admin.Blazor Directory Structure

```
src/Honua.Admin.Blazor/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor           (Root layout with drawer navigation)
│   │   └── NavMenu.razor              (Navigation menu with feature flags)
│   ├── Pages/
│   │   ├── Home.razor                 (Dashboard)
│   │   ├── Login.razor                (Authentication)
│   │   ├── Error.razor                (Global error page)
│   │   ├── Alerts/
│   │   │   ├── AlertConfiguration.razor
│   │   │   ├── AlertHistory.razor
│   │   │   └── AlertRuleEditor.razor
│   │   ├── GeoEtl/
│   │   │   ├── TemplateGallery.razor
│   │   │   ├── WorkflowDesigner.razor
│   │   │   ├── WorkflowList.razor
│   │   │   ├── WorkflowRuns.razor
│   │   │   ├── WorkflowSchedules.razor
│   │   │   └── ScheduleEditorDialog.razor
│   │   ├── Maps/
│   │   │   ├── MapList.razor
│   │   │   ├── MapViewer.razor        (Full-screen map visualization)
│   │   │   ├── MapEditor.razor
│   │   │   └── Dialogs/
│   │   │       └── LayerEditorDialog.razor
│   │   ├── LayerEditor.razor          (Layer configuration)
│   │   ├── LayerCreationWizard.razor  (Multi-step wizard)
│   │   ├── ServiceEditor.razor
│   │   ├── DataSourceList.razor
│   │   ├── UserManagement.razor
│   │   ├── RoleManagement.razor
│   │   ├── PermissionManagement.razor
│   │   ├── AuditLogViewer.razor
│   │   ├── VersionHistory.razor
│   │   ├── CacheSettings.razor
│   │   ├── CorsSettings.razor
│   │   └── ... (34 more pages total)
│   └── Shared/
│       ├── LoginDisplay.razor
│       ├── ConfirmDialog.razor
│       ├── AlertRuleDialog.razor
│       ├── UserDialog.razor
│       ├── RoleDialog.razor
│       ├── DataSourceDialog.razor
│       ├── MapLibreMapPreview.razor
│       ├── GlobalSearch.razor
│       ├── AdvancedFilterPanel.razor
│       ├── WhereClauseBuilder.razor
│       ├── DatabaseTableBrowser.razor
│       └── ... (18 more dialogs/shared components)
├── Shared/
│   ├── Services/
│   │   ├── EditorState.cs             (Form unsaved changes tracking)
│   │   ├── NavigationState.cs         (Breadcrumb & folder navigation)
│   │   ├── NotificationService.cs     (Toast notifications)
│   │   ├── SearchStateService.cs
│   │   ├── MetadataHubService.cs      (SignalR for metadata updates)
│   │   ├── AuthenticationService.cs
│   │   ├── AdminAuthenticationStateProvider.cs
│   │   └── [ApiClient].cs             (12+ API clients for different entities)
│   └── Models/
│       ├── AuthModels.cs
│       ├── UserModels.cs
│       ├── LayerModels.cs
│       ├── ServiceModels.cs
│       └── ... (9 more model files)
├── Services/
│   └── GeoEtlProgressService.cs       (Workflow execution tracking via SignalR)
├── wwwroot/
│   ├── css/
│   │   └── app.css                    (Custom utility styles)
│   └── js/
│       └── app.js                     (File download, drag-drop, MapLibre interop)
├── App.razor                          (Root component)
├── Program.cs                         (Service configuration)
└── appsettings.json

Total Components: ~72 Razor components across all directories
```

### Honua.MapSDK Directory Structure (Separate Library)

```
src/Honua.MapSDK/
├── Components/
│   ├── Map/
│   │   ├── HonuaMap.razor             (Primary map component)
│   │   ├── HonuaMapLibre.razor        (MapLibre GL wrapper)
│   │   ├── HonuaLeaflet.razor         (Alternative Leaflet wrapper)
│   │   └── MapLibreExtensions.cs
│   ├── Controls/
│   │   ├── LayerControl.razor
│   │   ├── BasemapSwitcher.razor
│   │   ├── DrawingToolbar.razor
│   │   ├── GeocodingSearch.razor
│   │   └── MeasurementDisplay.razor
│   ├── FilterPanel/
│   │   └── HonuaFilterPanel.razor     (Data filtering with UI)
│   ├── DataGrid/
│   │   └── HonuaDataGrid.razor        (Attribute table for features)
│   ├── Chart/
│   │   └── HonuaChart.razor           (Dynamic charting)
│   ├── LayerList/
│   │   └── HonuaLayerList.razor
│   ├── Legend/
│   │   └── HonuaLegend.razor
│   ├── Search/
│   │   └── HonuaSearch.razor
│   ├── Timeline/
│   │   └── HonuaTimeline.razor
│   ├── Analysis/
│   │   └── HonuaAnalysis.razor
│   ├── Bookmarks/
│   │   ├── HonuaBookmarks.razor
│   │   └── BookmarkEditDialog.razor
│   ├── AttributeTable/
│   │   └── HonuaAttributeTable.razor
│   ├── Print/
│   │   └── HonuaPrint.razor
│   ├── Compare/
│   │   └── HonuaCompare.razor
│   ├── ElevationProfile/
│   │   └── HonuaElevationProfile.razor
│   ├── Heatmap/
│   │   └── HonuaHeatmap.razor
│   ├── Routing/
│   │   └── HonuaRouting.razor
│   ├── ImportWizard/
│   │   ├── HonuaImportWizard.razor
│   │   └── Steps/
│   ├── OverviewMap/
│   │   └── HonuaOverviewMap.razor
│   ├── ErrorBoundary/
│   │   └── ErrorBoundary.razor
│   └── Popup/
│       └── HonuaPopup.razor
├── Core/
│   ├── ComponentBus.cs                (Event pub-sub for component sync)
│   └── Messages/
│       ├── MapReadyMessage.cs
│       ├── FilterAppliedMessage.cs
│       ├── LayerVisibilityChangedMessage.cs
│       └── (30+ message types)
├── Services/
│   ├── DataLoading/
│   │   ├── DataCache.cs               (LRU cache with TTL)
│   │   ├── DataLoader.cs              (Parallel + dedup loading)
│   │   ├── StreamingLoader.cs         (Chunked streaming)
│   │   └── CompressionHelper.cs       (gzip/brotli)
│   ├── Performance/
│   │   └── PerformanceMonitor.cs
│   └── ... (Other services)
├── Models/
│   ├── LayerModel.cs
│   ├── MapStyleModel.cs
│   ├── FeatureModel.cs
│   └── ... (Many geometry/config models)
├── Utilities/
│   ├── ResponsiveHelper.cs            (Screen size detection)
│   ├── GeometryUtils.cs
│   └── ... (Geometry/spatial utilities)
├── Logging/
│   ├── MapSdkLogger.cs                (Structured logging)
│   └── (Logging configuration)
├── Configuration/
│   └── MapSdkOptions.cs               (SDK configuration)
├── wwwroot/
│   └── js/
│       ├── honua-map.js               (MapLibre initialization & interop)
│       ├── honua-draw.js              (Drawing tools)
│       ├── honua-routing.js           (Routing visualization)
│       ├── honua-compare.js           (Map comparison)
│       ├── honua-layerlist.js
│       ├── honua-search.js
│       ├── honua-popup.js
│       ├── honua-analysis.js
│       ├── maplibre-interop.js        (MapLibre JS interop handler)
│       └── leaflet-interop.js         (Leaflet JS interop handler)
└── ServiceCollectionExtensions.cs     (DI registration)

Total Components: 40+ specialized map/data components
```

---

## 3. Key User Workflows & Pages

### Primary User Workflows

#### 1. **Dashboard & Overview** (`/`)
- **Component:** `Home.razor`
- **Purpose:** Landing page showing system statistics
- **Features:**
  - Service count, layer count, folder count cards
  - Quick action buttons (Create Service, Import Data, Browse Folders)
  - Recent activity feed (placeholder)
  - Getting started guides
- **Loading:** Async dashboard stats from API
- **Error Handling:** Silent failure, displays zeros

#### 2. **Service Management** (`/services`, `/services/{id}`, `/services/new`)
- **Components:**
  - `ServiceList.razor` (master list)
  - `ServiceDetail.razor` (view/edit single)
  - `ServiceEditor.razor` (creation/editing)
- **Features:**
  - CRUD operations
  - Metadata editing
  - Layer management within services
- **State:** Uses `EditorState` to track unsaved changes
- **Dialogs:** Multiple dialog components for complex operations

#### 3. **Layer Management** (`/layers`, `/layers/new`, `/layers/{id}/edit`)
- **Components:**
  - `LayerList.razor`
  - `LayerEditor.razor` (full form)
  - `LayerCreationWizard.razor` (multi-step)
  - `LayerDetail.razor`
- **Features:**
  - Configuration of geometry types, ID fields, styling
  - Field mapping
  - Multi-step creation wizard
  - Validation

#### 4. **Map Visualization** (`/maps`, `/maps/view/{id}`, `/maps/edit/{id}`)
- **Components:**
  - `MapList.razor` (list view)
  - `MapViewer.razor` (full-screen rendering)
  - `MapEditor.razor` (configuration)
  - `LayerEditorDialog.razor` (layer styling in map editor)
- **Features:**
  - Full-screen map with floating info panel
  - Layer list panel
  - Layer styling controls
  - Export functionality
- **Interop:** Uses MapLibre GL JS for rendering
- **Responsive:** Full viewport usage, responsive panels

#### 5. **Data Import** (`/import`, `/import/esri`, `/import/jobs`)
- **Components:**
  - `DataImportWizard.razor` (multi-step file upload)
  - `EsriServiceImportWizard.razor` (ESRI REST API integration)
  - `ImportJobsList.razor` (background jobs)
- **Features:**
  - Drag-and-drop file upload
  - Progressive progress tracking
  - Multiple import formats support
  - Job monitoring

#### 6. **Folder/Organization** (`/folders`, `/folders/{id}`)
- **Component:** `FolderBrowser.razor`
- **Features:**
  - Hierarchical folder navigation
  - Breadcrumb updates
  - Uses `NavigationState` service

#### 7. **GeoETL Workflows** (`/geoetl/*`) - Enterprise Feature
- **Components:**
  - `TemplateGallery.razor` (template browsing)
  - `WorkflowList.razor` (workflow list)
  - `WorkflowDesigner.razor` (visual workflow builder)
  - `WorkflowRuns.razor` (execution history)
  - `WorkflowRunDetail.razor` (individual run progress)
  - `WorkflowSchedules.razor` (scheduling)
  - `ScheduleEditorDialog.razor` (schedule configuration)
- **Features:**
  - Visual workflow design
  - Real-time progress tracking via SignalR
  - Template management
  - Scheduling capabilities
- **Real-Time:** Uses `GeoEtlProgressService` for live updates

#### 8. **Alerts & Notifications** (`/alerts`)
- **Components:**
  - `AlertConfiguration.razor` (main config page)
  - `AlertRuleEditor.razor` (rule creation)
  - `AlertHistory.razor` (alert log)
  - Various dialog components
- **Features:**
  - Rule definition
  - Notification channel setup
  - Alert history viewing
  - Channel testing

#### 9. **User & Security Management** (`/users`, `/roles`, `/permissions`)
- **Components:**
  - `UserManagement.razor` (user CRUD)
  - `RoleManagement.razor` (role CRUD)
  - `PermissionManagement.razor` (permission assignment)
  - Dialog components for editing
- **Features:**
  - User creation/enable/disable
  - Role assignment
  - Permission matrix
  - Statistics cards (responsive grid)

#### 10. **Audit & Logs** (`/audit`, `/versions`)
- **Components:**
  - `AuditLogViewer.razor` (audit trail)
  - `VersionHistory.razor` (change history)
  - `VersionDiff.razor` (version comparison)

#### 11. **Configuration** (`/settings`, `/cache`, `/cors`, `/licensing`)
- **Components:**
  - `CacheSettings.razor` (cache management)
  - `CorsSettings.razor` (CORS configuration)
  - `LicensingInfo.razor` (license info & feature flags)
- **Features:**
  - Settings management
  - Feature flag display (Enterprise tier badges)

---

## 4. State Management Architecture

### Local Component State
- **Framework Default:** Blazor's built-in `@code` block for component state
- **Pattern:** Mutable fields with `StateHasChanged()` calls after mutations

### Scoped Services (Session-Wide State)

#### 1. **EditorState**
```csharp
// Tracks unsaved changes across form components
public class EditorState
{
    public event Action? OnChange;
    public bool HasAnyUnsavedChanges();
    public bool HasUnsavedChanges(string editorId);
    public void MarkDirty(string editorId);
    public void MarkClean(string editorId);
    public async Task<bool> ConfirmNavigationAsync(IDialogService dialogService);
}
```
**Usage:** Form components mark themselves dirty when modified, dialogs confirm before navigation

#### 2. **NavigationState**
```csharp
// Manages folder navigation and breadcrumbs
public class NavigationState
{
    public string? CurrentFolderId { get; }
    public IReadOnlyList<BreadcrumbItem> Breadcrumbs { get; }
    public event Action? OnChange;
    public void NavigateToFolder(string? folderId, string? folderName = null);
    public void NavigateUp();
    public void SetBreadcrumbs(params BreadcrumbItem[] items);
}
```
**Usage:** Pages subscribe to `OnChange` to update breadcrumbs in MainLayout

#### 3. **NotificationService**
```csharp
// Wrapper around MudBlazor ISnackbar for toast notifications
public class NotificationService
{
    public void Success(string message, string? title = null);
    public void Error(string message, string? title = null);
    public void Warning(string message, string? title = null);
    public void Info(string message, string? title = null);
    public void Clear();
}
```
**Usage:** Pages inject and call for user feedback

#### 4. **SearchStateService**
- Manages global search state
- Likely tracks search history/filters

### API Clients (Scoped)
```csharp
// 12 specialized API clients, one per entity type:
ServiceApiClient
LayerApiClient
FolderApiClient
DataSourceApiClient
ImportApiClient
SnapshotApiClient
AuditLogApiClient
CacheApiClient
UserApiClient
CorsApiClient
RbacApiClient
FeatureFlagService (feature flags)
```
**Pattern:** Injected into pages/components, handle HTTP calls to backend

### Real-Time State (SignalR)

#### 1. **MetadataHubService**
```csharp
// Monitors metadata changes from server
public class MetadataHubService : IAsyncDisposable
{
    public event Func<MetadataChangedNotification, Task>? OnMetadataChanged;
    public bool IsConnected { get; }
    public async Task<bool> InitializeAsync();
}
```
**Usage:** Notifies UI of remote changes (other users' edits)

#### 2. **GeoEtlProgressService**
```csharp
// Tracks workflow execution progress in real-time
public class GeoEtlProgressService : IAsyncDisposable
{
    public bool IsConnected { get; }
    public async Task InitializeAsync(string baseUrl);
    public async Task SubscribeToWorkflowAsync(Guid runId);
    public event Func<WorkflowProgressMessage, Task>? OnProgressUpdated;
}
```
**Usage:** `WorkflowRunDetail.razor` subscribes for live progress updates

### MapSDK Component Bus (Inter-Component Communication)
```csharp
// Zero-config component synchronization via message passing
public class ComponentBus
{
    public void Subscribe<T>(Action<BusMessage<T>> handler) where T : IComponentMessage;
    public async Task PublishAsync<T>(T message, string sender) where T : IComponentMessage;
}
```
**Pattern:** Components communicate via strongly-typed messages
- `FilterAppliedMessage` - Filter changes propagate to map
- `LayerVisibilityChangedMessage` - Layer visibility toggles
- `FeatureClickedMessage` - Map feature selection
- `DataRowSelectedMessage` - Table row selection highlights on map

---

## 5. Styling & CSS Approach

### Styling Strategy: MudBlazor Theming + Minimal Custom CSS

#### MudBlazor Components
- **No CSS Modules:** Uses MudBlazor's built-in styling
- **Theme Provider:** Global Material Design theme
- **CSS Loading:**
  ```html
  <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
  <link rel="stylesheet" href="css/app.css" />
  ```

#### Custom CSS (`/wwwroot/css/app.css`)

**File Size:** ~56 lines (minimal override)

**Contents:**
```css
/* Font family */
html, body {
    font-family: 'Roboto', sans-serif;
}

/* Loading indicator */
.mud-progress-circular {
    margin: 2rem auto;
    display: block;
}

/* Service card hover effects */
.service-card {
    transition: transform 0.2s ease-in-out;
}
.service-card:hover {
    transform: translateY(-4px);
}

/* Custom scrollbar for tables */
.mud-table-container::-webkit-scrollbar {
    width: 8px;
    height: 8px;
}
.mud-table-container::-webkit-scrollbar-track {
    background: #f1f1f1;
}
.mud-table-container::-webkit-scrollbar-thumb {
    background: #888;
    border-radius: 4px;
}

/* Utility classes */
.mt-2 { margin-top: 0.5rem; }
.mt-4 { margin-top: 1rem; }
... /* margin/padding utilities */
```

**Styling Approach:**
- Reliance on MudBlazor component styling
- Minimal custom CSS (only for app-specific needs)
- **No CSS Modules, Styled-Components, or Tailwind**
- **Inline Styles:** Limited use for responsive sizing
  ```razor
  <div style="position: relative; width: 100vw; height: 100vh;">
  ```

#### Responsive Design Pattern

**MudBlazor Grid System:**
```razor
<MudGrid>
    <MudItem xs="12" sm="6" md="3">
        <!-- xs: 0px (mobile), sm: 600px, md: 960px, lg: 1280px, xl: 1920px -->
    </MudItem>
</MudGrid>
```
- Used extensively throughout app
- Examples in `Home.razor`, `UserManagement.razor`, `AlertConfiguration.razor`

**MudTable Responsive:**
```razor
<MudTable Breakpoint="Breakpoint.Sm" Dense="false">
```
- Tables collapse to mobile-friendly view at small breakpoints

**Color System:**
```csharp
Color.Primary      // Theme primary
Color.Secondary    // Theme secondary
Color.Error        // Red/error state
Color.Warning      // Orange/warning state
Color.Success      // Green/success state
Color.Info         // Blue/info state
Color.Default      // Neutral
```

---

## 6. Error Handling & User Feedback

### Error Display Patterns

#### 1. **MudAlert Component**
```razor
@if (!string.IsNullOrEmpty(_errorMessage))
{
    <MudAlert Severity="Severity.Error" Class="mb-4">
        @_errorMessage
    </MudAlert>
}
```
**Usage:** Inline error messages on pages

#### 2. **Toast Notifications (ISnackbar)**
```csharp
// Via NotificationService
NotificationService.Error("Operation failed", "Error");
NotificationService.Success("Saved successfully");
NotificationService.Warning("This action is irreversible");
NotificationService.Info("Processing...");
```
**Features:**
- Auto-dismissal (3-5 seconds based on severity)
- Close button available
- Color-coded by severity

#### 3. **Dialog Confirmations**
```csharp
var parameters = new DialogParameters
{
    { "ContentText", "Are you sure?" },
    { "Color", Color.Warning }
};
var dialog = await DialogService.ShowAsync<ConfirmDialog>("Title", parameters);
var result = await dialog.Result;
```
**Usage:** Critical operations (delete, navigation with unsaved changes)

#### 4. **Global Error Page**
- **Route:** `/Error`
- **Component:** `Error.razor`
- **Display:** Generic error message with return-to-home button

### Error Handling Code Patterns

**Try-Catch with Notifications:**
```csharp
try
{
    await ApiClient.SaveAsync(model);
    NotificationService.Success("Saved successfully");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Save failed");
    _errorMessage = ex.Message;
    NotificationService.Error($"Failed: {ex.Message}");
}
finally
{
    _loading = false;
}
```

**Silent Failures:**
```csharp
try
{
    var stats = await ServiceApi.GetDashboardStatsAsync();
    if (stats != null) { /* use stats */ }
}
catch (Exception)
{
    // Silently fail - dashboard shows zeros (Home.razor)
}
finally
{
    _loading = false;
}
```

### User Feedback Mechanisms

#### 1. **Loading States**
```razor
@if (_loading)
{
    <MudProgressLinear Indeterminate="true" Class="mb-4" />
    <!-- OR -->
    <MudProgressCircular Size="Size.Large" Indeterminate="true" />
}
```

#### 2. **Form Validation**
```csharp
<MudForm @ref="_form" @bind-IsValid="@_formValid">
    <MudTextField @bind-Value="_model.Title"
                  Label="Title"
                  Required="true"
                  Validation="@(new Func<string, string>(ValidateRequired))" />
</MudForm>
```
**Libraries:** FluentValidation for complex rules

#### 3. **Button States**
```razor
<MudButton Disabled="@(!_formValid || _loading)"
           OnClick="SaveAsync">
    @(_loading ? "Saving..." : "Save")
</MudButton>
```

#### 4. **Status Badges**
```razor
<MudChip Color="Color.Success" Icon="@Icons.Material.Filled.CheckCircle">
    Active
</MudChip>
```

---

## 7. Loading States & Async Patterns

### Async/Await Pattern
```csharp
protected override async Task OnInitializedAsync()
{
    _loading = true;
    try
    {
        var data = await ApiClient.GetDataAsync();
        _items = data;
    }
    catch (Exception ex)
    {
        _errorMessage = ex.Message;
    }
    finally
    {
        _loading = false;
    }
}
```

### Loading Indicators

#### Progress Bar
```razor
<MudProgressLinear Indeterminate="true" Class="mb-4" />
```

#### Progress Circle
```razor
<MudProgressCircular Size="Size.Large" Indeterminate="true" />
```

#### Loading Skeleton
- Not visible in code (potential gap for large datasets)

### Streaming & Chunked Loading (MapSDK)
```csharp
var loader = new StreamingLoader(httpClient);
await loader.StreamGeoJsonFeaturesAsync(
    url: "...",
    chunkSize: 100,
    onChunk: features => { /* process chunk */ }
);
```
**Purpose:** Large dataset loading without blocking UI

### Cancellation Tokens
```csharp
public async Task LoadDataAsync(CancellationToken cancellationToken = default)
{
    var data = await _httpClient.GetAsync(url, cancellationToken);
}
```
**Usage:** Allows cancellation of in-flight requests

---

## 8. Real-Time Communication

### SignalR Integration

#### 1. **MetadataHubService**
- **Hub URL:** `/admin/hub/metadata`
- **Purpose:** Notify UI of remote metadata changes
- **Message:** `MetadataChangedNotification`
  - `ChangeType` (Added, Modified, Deleted)
  - `EntityType` (Service, Layer, etc.)
  - `EntityId`
- **Auto-Reconnect:** With exponential backoff (0s, 2s, 10s, 30s)
- **Token Provider:** Uses `AuthenticationService.GetAccessTokenAsync()`

#### 2. **GeoEtlProgressService**
- **Hub URL:** `/hubs/geoetl-progress`
- **Purpose:** Real-time workflow execution tracking
- **Subscriptions:** Per-workflow via `SubscribeToWorkflowAsync(runId)`
- **Messages:** Workflow progress, step completion, errors
- **Usage:** `WorkflowRunDetail.razor` displays live progress

### SignalR Connection Lifecycle
```csharp
// Initialization
await hubConnection.StartAsync();

// Event handlers
hubConnection.On<T>("MethodName", async (data) => { /* handle */ });

// Reconnection
hubConnection.Reconnecting += error => { /* handle */ };
hubConnection.Reconnected += connectionId => { /* handle */ };
hubConnection.Closed += error => { /* handle */ };

// Cleanup
await hubConnection.DisposeAsync();
```

---

## 9. Responsiveness & Mobile Support

### Responsive Grid System (MudBlazor)

**Breakpoints:**
- `xs`: 0px (mobile phones)
- `sm`: 600px (tablets)
- `md`: 960px (laptops)
- `lg`: 1280px (desktops)
- `xl`: 1920px (large monitors)

**Example:**
```razor
<MudItem xs="12" sm="6" md="3">
    <!-- Full width on mobile, half on tablets, quarter on desktop -->
</MudItem>
```

**Files Using Responsive Grids:**
- `Home.razor` - Dashboard cards (xs="12" sm="6" md="3")
- `UserManagement.razor` - Statistics cards
- `VersionDiff.razor`
- `ServiceDetail.razor`
- All pages with `MaxWidth="MaxWidth.ExtraLarge"`

### Mobile-Optimized Components

#### 1. **MudDrawer Navigation**
```razor
<MudDrawer @bind-Open="_drawerOpen" ClipMode="DrawerClipMode.Always">
    <NavMenu />
</MudDrawer>
```
- Collapsible sidebar (good for mobile)

#### 2. **MudTable with Breakpoint**
```razor
<MudTable Breakpoint="Breakpoint.Sm">
    <!-- Stacks vertically on small screens -->
</MudTable>
```

#### 3. **MudContainer with MaxWidth**
```razor
<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <!-- Constrains content width, centers on large screens -->
</MudContainer>
```

### Full-Screen Components (MapViewer)
```html
<div style="position: relative; width: 100vw; height: 100vh;">
    <HonuaMap />
</div>
```
- Uses 100% viewport width/height
- Floating info panel positioned absolutely
- Layer list panel positioned absolutely

### Responsive Design Utilities (MapSDK)

**ResponsiveHelper Class:**
```csharp
public class ResponsiveHelper
{
    public bool IsMobile { get; }
    public bool IsTablet { get; }
    public bool IsDesktop { get; }
    public Size CurrentSize { get; }
    // Breakpoints: xs (0-639), sm (640-768), md (769-1024), lg (1025+)
}
```

---

## 10. Performance Optimizations

### Application-Level

#### 1. **HTTP Client Configuration**
```csharp
builder.Services.AddHttpClient("AdminApi", client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30); // 30-second timeout
})
.AddHttpMessageHandler<BearerTokenHandler>();
```

#### 2. **Scoped Services**
```csharp
builder.Services.AddScoped<NavigationState>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<MetadataHubService>();
```
- One instance per user session (circuit)
- Efficient resource usage

#### 3. **Bearer Token Handler**
- Injects authorization header automatically
- Avoids token retrieval on every request

### MapSDK Performance Features

#### 1. **Data Caching**
```csharp
var cache = new DataCache(new CacheOptions
{
    MaxSizeMB = 50,
    DefaultTtlSeconds = 300,
    MaxItems = 100
});
var data = await cache.GetOrCreateAsync("key", async () =>
{
    return await LoadExpensiveDataAsync();
});
```
- LRU (Least Recently Used) eviction
- TTL-based expiration
- Size and count limits

#### 2. **Data Loader with Deduplication**
```csharp
var loader = new DataLoader(httpClient, cache);
// Multiple concurrent requests for same URL return same promise
var data1 = loader.LoadAsync("url");
var data2 = loader.LoadAsync("url"); // Deduped!

// Parallel loading with concurrency control
var results = await loader.LoadManyAsync(urls, maxParallel: 4);
```

#### 3. **Streaming for Large Datasets**
```csharp
var loader = new StreamingLoader(httpClient);
await loader.StreamGeoJsonFeaturesAsync(
    url: "...",
    chunkSize: 100,
    onChunk: features => { /* process */ }
);
```
- Chunks large datasets
- Progressive rendering
- Prevents blocking

#### 4. **Compression**
```csharp
CompressionHelper.CompressGzip(data);
CompressionHelper.CompressRrotli(data);
```
- Automatic gzip/brotli support
- Reduces network payload

#### 5. **GPU Acceleration**
```csharp
[Parameter] public bool EnableGPU { get; set; } = true;
```
- MapLibre GL JS uses WebGL
- Hardware-accelerated rendering

### Server-Side Performance
- **InteractiveServer Rendering:** Server maintains circuit, rapid updates
- **Automatic Reconnect:** SignalR with exponential backoff

---

## 11. Identified Issues & Responsiveness Gaps

### Critical Issues Found

#### 1. **Responsiveness Issues**

**Issue 1: Full-Screen Map Viewport**
- **Location:** `MapViewer.razor` (lines 30-43)
- **Problem:** Uses `width: 100vw; height: 100vh;` which doesn't account for viewport scrollbars
- **Impact:** Map container slightly larger than viewport, horizontal scroll appears on some browsers
- **Recommendation:** Use `width: 100%; height: 100%;` on parent container instead

**Issue 2: Map Floating Panels (Not Responsive)**
- **Location:** `MapViewer.razor` (lines 46-97)
- **Problem:** Fixed positioning with hard-coded pixel values for absolute positioning
```html
<MudPaper Style="position: absolute; top: 20px; left: 20px; max-width: 400px;">
```
- **Impact:** On mobile devices (<768px), panel may obscure map controls or extend beyond viewport
- **Recommendation:** 
  - Use CSS media queries to adjust positioning on mobile
  - Change to `bottom: 20px;` on mobile to avoid top bar overlap
  - Reduce `max-width` on mobile (e.g., `max-width: calc(100% - 40px)`)

**Issue 3: Full-Width Container MaxWidth Inconsistency**
- **Location:** Multiple pages (`Home.razor`, `UserManagement.razor`, etc.)
- **Problem:** Inconsistent use of `MaxWidth.ExtraLarge` (2560px) - may cause layout shift on mobile when MudDrawer collapses
- **Impact:** Navigation animation may cause content reflow

#### 2. **Missing Skeleton/Placeholder Loading**
- **Location:** All data-heavy pages
- **Problem:** Loading states use `MudProgressLinear` or `MudProgressCircular` - no skeleton screens
- **Impact:** Poor perceived performance, blank UI feels sluggish
- **Recommendation:** Implement skeleton components for data tables and card grids

#### 3. **No Virtual Scrolling for Large Lists**
- **Location:** Tables, dropdowns throughout app
- **Problem:** Lists load all rows immediately (no pagination in some views)
- **Impact:** Performance degradation with 1000+ rows
- **Recommendation:** Implement MudVirtualize for large datasets

#### 4. **TODO/FIXME Items in UI Code** (47 instances found)

**Critical TODOs:**
| File | Line | Issue |
|------|------|-------|
| `ServiceDetail.razor` | 707 | TODO: Implement save to backend |
| `AlertConfiguration.razor` | 366 | TODO: Replace with actual API calls |
| `LayerCreationWizard.razor` | 551 | TODO: Call backend endpoint to fetch preview data |
| `GeoEtl/WorkflowDesigner.razor` | 339 | TODO: Load workflow from API |
| `DataSourceList.razor` | 283 | TODO: Implement table browser dialog |
| `MapLibreMapPreview.razor` | 104 | TODO: Implement actual layer data fetching |

#### 5. **Authentication Context Not Fully Integrated**
- **Location:** `ScheduleEditorDialog.razor` (lines 171-174)
- **Problem:** Hard-coded tenant/user IDs
```csharp
tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"), // TODO: Get from auth
```
- **Impact:** Workflows/jobs created with wrong ownership
- **Recommendation:** Inject `AuthenticationService` and extract user ID from token

#### 6. **Silent Failures in Dashboard**
- **Location:** `Home.razor` (lines 119-137)
- **Problem:** API errors silently fail, dashboard shows zeros
- **Impact:** User unaware of system issues
- **Recommendation:** Add logging or warning toast on failures

#### 7. **Missing Error Boundary in Complex Components**
- **Location:** `MapViewer.razor`, `WorkflowDesigner.razor`
- **Problem:** No try-catch around JS interop calls to MapLibre
- **Impact:** JS errors can crash entire map UI
- **Recommendation:** Implement `ErrorBoundary.razor` wrapper or try-catch in JS interop

#### 8. **Incomplete Form Validation**
- **Location:** Multiple form components
- **Problem:** Some forms lack client-side validation rules
- **Impact:** Invalid data submitted to server
- **Recommendation:** Add FluentValidation validators to all forms

### Performance Issues

#### 1. **SignalR Hub Connection Not Isolated**
- **Location:** `MetadataHubService.cs`, `GeoEtlProgressService.cs`
- **Problem:** Multiple services maintain separate SignalR connections
- **Impact:** Multiple WebSocket connections for single user
- **Recommendation:** Consolidate to single hub connection

#### 2. **No Request Deduplication for API Calls**
- **Location:** API clients across app
- **Problem:** Multiple simultaneous requests to same endpoint not deduped
- **Impact:** Wasted bandwidth
- **Recommendation:** Implement request deduplication in HTTP client handler

#### 3. **MapSDK Streaming Not Utilized**
- **Location:** Data import and large dataset queries
- **Problem:** Code exists but not used in actual pages
- **Impact:** Large datasets cause UI freeze
- **Recommendation:** Enable streaming loaders for ImportJobsList, large layer previews

---

## 12. Critical UI Files

### Essential Files for Understanding Architecture

**Core Application:**
- `/src/Honua.Admin.Blazor/Program.cs` - Service registration and dependency injection
- `/src/Honua.Admin.Blazor/Components/App.razor` - Root HTML and script loading
- `/src/Honua.Admin.Blazor/Components/Layout/MainLayout.razor` - Main layout with drawer/breadcrumbs
- `/src/Honua.Admin.Blazor/Components/Layout/NavMenu.razor` - Navigation with feature flags
- `/src/Honua.Admin.Blazor/wwwroot/css/app.css` - Global styles
- `/src/Honua.Admin.Blazor/wwwroot/js/app.js` - JavaScript interop handlers

**Key Services:**
- `/src/Honua.Admin.Blazor/Shared/Services/EditorState.cs` - Form state management
- `/src/Honua.Admin.Blazor/Shared/Services/NavigationState.cs` - Navigation/breadcrumbs
- `/src/Honua.Admin.Blazor/Shared/Services/NotificationService.cs` - Toast notifications
- `/src/Honua.Admin.Blazor/Shared/Services/MetadataHubService.cs` - Real-time metadata updates
- `/src/Honua.Admin.Blazor/Shared/Services/AuthenticationService.cs` - Authentication

**Critical Pages (User Workflows):**
- `/src/Honua.Admin.Blazor/Components/Pages/Home.razor` - Dashboard
- `/src/Honua.Admin.Blazor/Components/Pages/Maps/MapViewer.razor` - Full-screen map
- `/src/Honua.Admin.Blazor/Components/Pages/LayerEditor.razor` - Layer configuration
- `/src/Honua.Admin.Blazor/Components/Pages/GeoEtl/WorkflowDesigner.razor` - Workflow builder
- `/src/Honua.Admin.Blazor/Components/Pages/UserManagement.razor` - User CRUD with responsive stats

**MapSDK (Separate Library):**
- `/src/Honua.MapSDK/Components/Map/HonuaMap.razor` - Primary map component with ComponentBus
- `/src/Honua.MapSDK/Core/ComponentBus.cs` - Event pub-sub system
- `/src/Honua.MapSDK/wwwroot/js/honua-map.js` - MapLibre GL initialization
- `/src/Honua.MapSDK/Services/DataLoading/DataCache.cs` - Caching strategy
- `/src/Honua.MapSDK/Services/DataLoading/StreamingLoader.cs` - Large dataset handling
- `/src/Honua.MapSDK/Utilities/ResponsiveHelper.cs` - Responsive breakpoints

**Shared Components (Dialogs/Reusable):**
- `/src/Honua.Admin.Blazor/Components/Shared/ConfirmDialog.razor` - Confirmation dialog pattern
- `/src/Honua.Admin.Blazor/Components/Shared/AlertRuleDialog.razor` - Complex dialog pattern
- `/src/Honua.Admin.Blazor/Components/Shared/DataSourceDialog.razor` - Form dialog pattern
- `/src/Honua.Admin.Blazor/Components/Shared/MapLibreMapPreview.razor` - Map preview component

---

## Summary

### UI Architecture Strengths
1. **Blazor with MudBlazor** - Type-safe C# components with Material Design
2. **ComponentBus Pattern** - Zero-config component synchronization in MapSDK
3. **Comprehensive State Management** - Multiple patterns for different use cases
4. **Real-Time Updates** - SignalR integration for live notifications
5. **Performance Optimizations** - Caching, streaming, compression in MapSDK
6. **Responsive Grid System** - MudBlazor's 5-breakpoint system for mobile support

### Key Gaps & Opportunities for Improvement
1. **Responsiveness Issues** - MapViewer floating panels not mobile-optimized
2. **Loading States** - No skeleton screens for better perceived performance
3. **Virtual Scrolling** - Missing for large datasets
4. **Error Boundaries** - Limited error handling in complex components
5. **Incomplete TODOs** - 47 TODO items suggest work-in-progress features
6. **Authentication Integration** - Hard-coded IDs in some workflows
7. **Mobile Testing** - Limited evidence of mobile-first testing

### Recommended Focus Areas
1. Fix MapViewer responsive positioning for mobile
2. Implement skeleton loaders for data-heavy pages
3. Add virtual scrolling to large tables
4. Complete pending TODO items (especially API integrations)
5. Consolidate SignalR connections
6. Add comprehensive error boundaries


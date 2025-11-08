# HonuaEditor Component

A comprehensive feature editing component with full CRUD operations, validation, undo/redo, and backend synchronization.

## Overview

HonuaEditor provides a complete editing solution for geographic features with:

- **Create**: Draw new points, lines, and polygons
- **Read**: Load and display existing features
- **Update**: Edit geometry and attributes
- **Delete**: Remove features with confirmation
- **Validation**: Client and server-side validation
- **Undo/Redo**: Unlimited operation history
- **Session Management**: Track and manage editing sessions
- **Conflict Detection**: Handle concurrent edits

## Features

### Drawing Tools
- Point creation
- Line drawing with vertex snapping
- Polygon drawing with closure validation
- Visual feedback during drawing
- Drawing hints and instructions

### Edit Operations
- Select features for editing
- Move/drag entire features
- Edit individual vertices (add, move, delete)
- Reshape geometries
- Visual vertex handles
- Snap to vertices and edges

### Attribute Editing
- Dynamic attribute forms based on validation rules
- Field type support:
  - Text fields with min/max length
  - Number fields with range validation
  - Date/time pickers
  - Boolean checkboxes
  - Domain/coded value dropdowns
  - Email and URL validation
- Required field validation
- Pattern matching with regex
- Custom validation rules

### Edit Session Management
- Start/stop editing sessions
- Track all operations in session
- Dirty state detection
- Operation statistics
- Session history with timestamps
- Auto-save support
- Manual save/cancel

### Validation System
- Attribute validation rules
- Geometry validation (topology)
- Required field checking
- Type validation
- Range validation
- Custom validation callbacks
- Error message display
- Warning vs error severity

### Backend Integration
- REST API integration
- Create: `POST /api/features`
- Update: `PUT /api/features/{id}`
- Delete: `DELETE /api/features/{id}`
- Batch operations: `POST /api/features/batch`
- Optimistic UI updates
- Conflict detection and resolution
- Version control

## Basic Usage

```razor
@page "/editor-demo"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Editor

<HonuaMap Id="main-map" Center="@(new[] { -122.4, 37.8 })" Zoom="12">
    <HonuaEditor
        SyncWith="main-map"
        EditableLayers="@(new List<string> { "buildings", "parcels" })"
        AllowCreate="true"
        AllowUpdate="true"
        AllowDelete="true"
        ShowAttributeForm="true"
        ApiEndpoint="/api/features"
        Position="top-right"
        OnFeatureCreated="@HandleFeatureCreated"
        OnFeatureUpdated="@HandleFeatureUpdated"
        OnFeatureDeleted="@HandleFeatureDeleted" />
</HonuaMap>

@code {
    private void HandleFeatureCreated(Feature feature)
    {
        Console.WriteLine($"Feature created: {feature.Id}");
    }

    private void HandleFeatureUpdated(Feature feature)
    {
        Console.WriteLine($"Feature updated: {feature.Id}");
    }

    private void HandleFeatureDeleted(string featureId)
    {
        Console.WriteLine($"Feature deleted: {featureId}");
    }
}
```

## Parameters

### Core Settings

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | auto-generated | Unique component identifier |
| `SyncWith` | string | null | Map ID to synchronize with |
| `EditableLayers` | List&lt;string&gt; | empty | Layer IDs that can be edited |
| `Position` | string | null | Position on map (top-right, etc.) |
| `Width` | string | "350px" | Component width |

### Permissions

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `AllowCreate` | bool | true | Allow creating new features |
| `AllowUpdate` | bool | true | Allow updating features |
| `AllowDelete` | bool | true | Allow deleting features |

### Validation

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ValidationRules` | Dictionary&lt;string, List&lt;ValidationRule&gt;&gt; | empty | Validation rules by layer |
| `ShowAttributeForm` | bool | true | Show attribute editing dialog |

### Backend Integration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ApiEndpoint` | string | "/api/features" | API endpoint for operations |
| `AutoSave` | bool | false | Auto-save changes |

### UI Customization

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowToolbar` | bool | true | Show editing toolbar |
| `Collapsible` | bool | true | Allow collapsing sections |
| `CssClass` | string | null | Custom CSS class |
| `Style` | string | null | Custom inline styles |

### Event Callbacks

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnFeatureCreated` | EventCallback&lt;Feature&gt; | Fired when feature is created |
| `OnFeatureUpdated` | EventCallback&lt;Feature&gt; | Fired when feature is updated |
| `OnFeatureDeleted` | EventCallback&lt;string&gt; | Fired when feature is deleted |
| `OnEditError` | EventCallback&lt;string&gt; | Fired when edit error occurs |

## Validation Rules

Define validation rules for each layer:

```csharp
var validationRules = new Dictionary<string, List<ValidationRule>>
{
    ["buildings"] = new List<ValidationRule>
    {
        new ValidationRule
        {
            FieldName = "name",
            DisplayName = "Building Name",
            Type = ValidationType.String,
            IsRequired = true,
            MinLength = 3,
            MaxLength = 100,
            ErrorMessage = "Building name must be between 3 and 100 characters"
        },
        new ValidationRule
        {
            FieldName = "height",
            DisplayName = "Height (meters)",
            Type = ValidationType.Number,
            IsRequired = true,
            MinValue = 0,
            MaxValue = 1000
        },
        new ValidationRule
        {
            FieldName = "type",
            DisplayName = "Building Type",
            Type = ValidationType.Domain,
            IsRequired = true,
            DomainValues = new List<ValidationDomainValue>
            {
                new() { Code = "residential", Name = "Residential", SortOrder = 1 },
                new() { Code = "commercial", Name = "Commercial", SortOrder = 2 },
                new() { Code = "industrial", Name = "Industrial", SortOrder = 3 }
            }
        },
        new ValidationRule
        {
            FieldName = "constructionDate",
            DisplayName = "Construction Date",
            Type = ValidationType.Date,
            IsRequired = false
        }
    }
};

<HonuaEditor ValidationRules="@validationRules" ... />
```

## Edit Workflow

### 1. Start Editing
Click the "Start editing" button to begin an editing session. This creates a new `EditSession` that tracks all operations.

### 2. Create Features
Use the Create menu to select a geometry type (Point, Line, Polygon), then draw on the map following the on-screen hints.

### 3. Edit Features
Click to select a feature, then:
- Click "Edit vertices" to modify individual vertices
- Click "Move feature" to translate the entire feature
- Click "Edit attributes" to modify properties

### 4. Save or Cancel
- Click "Save changes" to persist edits to the server
- Click "Cancel editing" to discard all changes and end the session

## Undo/Redo

The editor maintains an unlimited operation history:

- Click the Undo button to revert the last operation
- Click the Redo button to reapply an undone operation
- History is preserved throughout the editing session
- History is cleared when the session ends

## Edit History Panel

The edit history panel shows:
- All operations performed in the current session
- Operation type (Create, Update, Delete, Move, etc.)
- Timestamp of each operation
- Sync status (synced to server or pending)
- Most recent 10 operations displayed

## Validation Errors

When validation fails:
- Errors are displayed in a prominent alert
- Field-specific errors are shown in the attribute form
- Save is blocked until all errors are resolved
- Warnings are shown but don't block save

## Backend API

The editor expects a REST API with these endpoints:

### Create Feature
```http
POST /api/features
Content-Type: application/json

{
  "type": "Feature",
  "geometry": {
    "type": "Point",
    "coordinates": [-122.4, 37.8]
  },
  "properties": {
    "name": "Building A",
    "height": 50
  }
}

Response: 201 Created
{
  "id": "feature-123",
  "version": 1,
  ...
}
```

### Update Feature
```http
PUT /api/features/feature-123
Content-Type: application/json

{
  "type": "Feature",
  "id": "feature-123",
  "geometry": { ... },
  "properties": { ... }
}

Response: 200 OK
{
  "id": "feature-123",
  "version": 2,
  ...
}
```

### Delete Feature
```http
DELETE /api/features/feature-123

Response: 204 No Content
```

### Batch Operations
```http
POST /api/features/batch
Content-Type: application/json

{
  "operations": [
    {
      "type": "create",
      "feature": { ... }
    },
    {
      "type": "update",
      "feature": { ... }
    },
    {
      "type": "delete",
      "feature": { ... }
    }
  ]
}

Response: 200 OK
{
  "success": true,
  "savedCount": 3
}
```

## ComponentBus Messages

### Published Messages

**FeatureCreatedMessage**
```csharp
{
    FeatureId: "feature-123",
    LayerId: "buildings",
    GeometryType: "Polygon",
    Geometry: { ... },
    Attributes: { ... },
    ComponentId: "editor-1",
    Timestamp: DateTime.UtcNow
}
```

**FeatureUpdatedMessage**
```csharp
{
    FeatureId: "feature-123",
    LayerId: "buildings",
    Geometry: { ... },
    Attributes: { ... },
    UpdateType: "geometry", // or "attributes" or "both"
    ComponentId: "editor-1",
    Timestamp: DateTime.UtcNow
}
```

**FeatureDeletedMessage**
```csharp
{
    FeatureId: "feature-123",
    LayerId: "buildings",
    ComponentId: "editor-1",
    Timestamp: DateTime.UtcNow
}
```

**EditSessionStartedMessage**
```csharp
{
    SessionId: "session-456",
    ComponentId: "editor-1",
    EditableLayers: ["buildings", "parcels"],
    Timestamp: DateTime.UtcNow
}
```

**EditSessionEndedMessage**
```csharp
{
    SessionId: "session-456",
    ComponentId: "editor-1",
    ChangesSaved: true,
    OperationCount: 15,
    Timestamp: DateTime.UtcNow
}
```

**EditSessionStateChangedMessage**
```csharp
{
    SessionId: "session-456",
    ComponentId: "editor-1",
    IsDirty: true,
    UnsavedChanges: 3,
    CanUndo: true,
    CanRedo: false
}
```

### Subscribed Messages

**LayerSelectedMessage** - Adds layer to editable layers list
**FeatureClickedMessage** - Selects feature for editing when in edit mode

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      HonuaEditor.razor                       │
│  ┌────────────┐  ┌──────────────┐  ┌────────────────────┐  │
│  │  Toolbar   │  │ Edit Session │  │  Attribute Form    │  │
│  │  Controls  │  │    Status    │  │      Dialog        │  │
│  └────────────┘  └──────────────┘  └────────────────────┘  │
│                                                               │
│  ┌────────────────────────┐  ┌───────────────────────────┐  │
│  │    Edit History        │  │  Validation Errors        │  │
│  │  - Operations list     │  │  - Field-level errors     │  │
│  │  - Undo/Redo buttons   │  │  - Warning messages       │  │
│  └────────────────────────┘  └───────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                            │
                            ↓
        ┌───────────────────────────────────────┐
        │      FeatureEditService (C#)          │
        │  - Session management                  │
        │  - Validation                          │
        │  - Backend synchronization             │
        │  - Undo/redo logic                     │
        └───────────────────────────────────────┘
                            │
        ┌───────────────────┴───────────────────┐
        ↓                                       ↓
┌──────────────────┐              ┌──────────────────────┐
│  ComponentBus    │              │   REST API Backend   │
│  - Messages      │              │   - CRUD endpoints   │
│  - Events        │              │   - Validation       │
└──────────────────┘              │   - Persistence      │
                                  └──────────────────────┘
        ↓
┌─────────────────────────────────────────────┐
│       honua-editor.js (JavaScript)          │
│  - MapLibre GL Draw integration             │
│  - Vertex editing                           │
│  - Snap to features                         │
│  - Visual feedback                          │
└─────────────────────────────────────────────┘
        ↓
┌─────────────────────────────────────────────┐
│         MapLibre GL JS + Draw Plugin        │
│  - Rendering                                 │
│  - User interaction                          │
│  - Geometry manipulation                     │
└─────────────────────────────────────────────┘
```

## Conflict Detection

When multiple users edit the same feature, conflicts can occur. The editor uses version numbers to detect conflicts:

```csharp
// Automatic conflict detection on save
var conflicts = await EditService.DetectConflictsAsync(sessionId, apiEndpoint);

if (conflicts.Any())
{
    // Handle conflicts
    foreach (var conflict in conflicts)
    {
        Console.WriteLine($"Conflict on feature {conflict.FeatureId}");
        Console.WriteLine($"Local version: {conflict.LocalVersion}");
        Console.WriteLine($"Server version: {conflict.ServerVersion}");

        // Resolution strategies:
        // 1. Keep local changes (overwrite server)
        // 2. Discard local changes (use server version)
        // 3. Merge changes (if possible)
        // 4. Prompt user to resolve
    }
}
```

## Performance Considerations

- **Batch operations**: Use batch endpoint for multiple edits
- **Auto-save interval**: Balance between data safety and server load
- **History size**: Limited to 100 operations by default
- **Validation**: Client-side validation runs first to minimize server requests
- **Optimistic updates**: UI updates immediately, rolls back on error

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

Requires ES6 modules and MapLibre GL JS 3.x.

## Accessibility

- Keyboard navigation support
- ARIA labels on all interactive elements
- High contrast mode support
- Screen reader compatible
- Focus indicators

## See Also

- [Examples.md](Examples.md) - Comprehensive usage examples
- [HonuaDraw Component](../Draw/README.md) - Simple drawing without editing
- [ComponentBus Documentation](../../Core/README.md) - Message bus system
- [MapLibre GL Draw](https://github.com/mapbox/mapbox-gl-draw) - Underlying draw library

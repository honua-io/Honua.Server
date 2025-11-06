# HonuaPopup Component

A feature-rich popup/tooltip component for displaying feature information on click or hover in Honua.MapSDK maps.

## Features

- **Multiple Trigger Modes**: Click, hover, or manual control
- **Customizable Templates**: Razor templates, HTML templates, or default attribute table
- **Multi-Feature Support**: Navigate between multiple selected features
- **Action Buttons**: Built-in and custom actions (zoom, edit, delete, copy coordinates, etc.)
- **Responsive Design**: Adapts to different screen sizes with touch support
- **Auto-Pan**: Automatically adjusts map view to keep popup visible
- **ComponentBus Integration**: Syncs with other MapSDK components
- **Dark Mode Support**: Automatic theme detection
- **Accessibility**: ARIA labels and keyboard navigation

## Installation

The HonuaPopup component is part of Honua.MapSDK. No additional installation required.

## Basic Usage

### Simple Popup with Default Template

```razor
<HonuaMap Id="map1" />

<HonuaPopup SyncWith="map1" TriggerMode="PopupTrigger.Click" />
```

This creates a popup that:
- Shows on feature click
- Displays all feature attributes in a table
- Auto-pans to keep popup visible
- Includes a close button

### Hover Tooltip

```razor
<HonuaPopup
    SyncWith="map1"
    TriggerMode="PopupTrigger.Hover"
    MaxWidth="300"
    ShowCloseButton="false" />
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | string | Auto-generated | Unique identifier for the component |
| `SyncWith` | string? | null | Map ID to synchronize with |
| `TriggerMode` | PopupTrigger | Click | When to show popup (Click, Hover, Manual) |
| `Template` | RenderFragment<PopupContent>? | null | Custom Razor template |
| `MaxWidth` | int | 400 | Maximum width in pixels |
| `MaxHeight` | int | 600 | Maximum height in pixels |
| `ShowCloseButton` | bool | true | Show close button |
| `CloseOnMapClick` | bool | true | Close when clicking outside |
| `AllowMultipleFeatures` | bool | true | Enable multi-feature pagination |
| `ShowActions` | bool | true | Show action buttons |
| `CustomActions` | List<PopupAction>? | null | Custom action buttons |
| `QueryLayers` | string[]? | null | Layer IDs to query (null = all) |
| `AutoPan` | bool | true | Auto-pan to keep popup visible |
| `LayerTemplates` | Dictionary<string, PopupTemplate>? | null | Per-layer templates |
| `CssClass` | string? | null | Custom CSS class |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnFeatureClick` | EventCallback<PopupContent> | Triggered when feature is clicked |
| `OnActionTriggered` | EventCallback<(PopupAction, PopupContent)> | Triggered when action button clicked |
| `OnPopupOpened` | EventCallback<PopupContent> | Triggered when popup opens |
| `OnPopupClosed` | EventCallback | Triggered when popup closes |

## Custom Templates

### Razor Template

```razor
<HonuaPopup SyncWith="map1">
    <Template Context="content">
        <div class="custom-popup">
            <h3>@content.Properties.GetValueOrDefault("name")</h3>
            <p>
                <strong>Type:</strong> @content.Properties.GetValueOrDefault("type")
            </p>
            <p>
                <strong>Population:</strong>
                @string.Format("{0:N0}", content.Properties.GetValueOrDefault("population"))
            </p>
            @if (content.Properties.ContainsKey("image"))
            {
                <img src="@content.Properties["image"]" alt="Feature image" />
            }
        </div>
    </Template>
</HonuaPopup>
```

### HTML String Template

```csharp
var template = new PopupTemplate
{
    Title = "{name}",
    ContentTemplate = @"
        <div class='feature-info'>
            <p><strong>Type:</strong> {type}</p>
            <p><strong>Status:</strong> {status}</p>
            <p><strong>Updated:</strong> {last_update}</p>
        </div>
    ",
    Fields = new List<PopupField>
    {
        new PopupField
        {
            FieldName = "name",
            Label = "Name",
            Type = PopupFieldType.Text,
            Order = 1
        },
        new PopupField
        {
            FieldName = "population",
            Label = "Population",
            Type = PopupFieldType.Number,
            Format = "N0",
            Order = 2
        },
        new PopupField
        {
            FieldName = "area",
            Label = "Area",
            Type = PopupFieldType.Number,
            Format = "N2",
            Order = 3
        }
    }
};

var layerTemplates = new Dictionary<string, PopupTemplate>
{
    { "cities-layer", template }
};
```

```razor
<HonuaPopup
    SyncWith="map1"
    LayerTemplates="layerTemplates" />
```

## Custom Actions

### Built-in Actions

```csharp
var customActions = new List<PopupAction>
{
    new PopupAction
    {
        Id = "zoom",
        Label = "Zoom To",
        Icon = Icons.Material.Filled.ZoomIn,
        Type = PopupActionType.ZoomTo,
        Color = "primary",
        Order = 1
    },
    new PopupAction
    {
        Id = "copy-coords",
        Label = "Copy Coordinates",
        Icon = Icons.Material.Filled.ContentCopy,
        Type = PopupActionType.CopyCoordinates,
        Order = 2
    },
    new PopupAction
    {
        Id = "delete",
        Label = "Delete",
        Icon = Icons.Material.Filled.Delete,
        Type = PopupActionType.Delete,
        Color = "error",
        Order = 3
    }
};
```

```razor
<HonuaPopup
    SyncWith="map1"
    CustomActions="customActions" />
```

### Custom Action Handlers

```razor
<HonuaPopup
    SyncWith="map1"
    CustomActions="customActions"
    OnActionTriggered="HandleActionTriggered" />

@code {
    private List<PopupAction> customActions = new()
    {
        new PopupAction
        {
            Id = "export",
            Label = "Export Data",
            Icon = Icons.Material.Filled.FileDownload,
            Type = PopupActionType.Custom,
            Order = 1
        },
        new PopupAction
        {
            Id = "share",
            Label = "Share",
            Icon = Icons.Material.Filled.Share,
            Type = PopupActionType.Custom,
            Order = 2
        }
    };

    private async Task HandleActionTriggered((PopupAction Action, PopupContent Content) args)
    {
        switch (args.Action.Id)
        {
            case "export":
                await ExportFeatureData(args.Content);
                break;
            case "share":
                await ShareFeature(args.Content);
                break;
        }
    }

    private async Task ExportFeatureData(PopupContent content)
    {
        // Export logic
        var json = JsonSerializer.Serialize(content.Properties);
        await JS.InvokeVoidAsync("downloadFile", "feature.json", json);
    }

    private async Task ShareFeature(PopupContent content)
    {
        // Share logic
        var url = $"/features/{content.FeatureId}";
        await JS.InvokeVoidAsync("navigator.share", new { url, title = "Feature" });
    }
}
```

## Advanced Features

### Per-Layer Templates

Define different templates for different layers:

```csharp
var layerTemplates = new Dictionary<string, PopupTemplate>
{
    {
        "buildings-layer",
        new PopupTemplate
        {
            Title = "Building: {name}",
            Fields = new List<PopupField>
            {
                new() { FieldName = "name", Label = "Name", Order = 1 },
                new() { FieldName = "height", Label = "Height (m)", Type = PopupFieldType.Number, Order = 2 },
                new() { FieldName = "year_built", Label = "Year Built", Order = 3 }
            }
        }
    },
    {
        "roads-layer",
        new PopupTemplate
        {
            Title = "{street_name}",
            Fields = new List<PopupField>
            {
                new() { FieldName = "type", Label = "Road Type", Order = 1 },
                new() { FieldName = "lanes", Label = "Lanes", Type = PopupFieldType.Number, Order = 2 },
                new() { FieldName = "speed_limit", Label = "Speed Limit", Order = 3 }
            }
        }
    }
};
```

### Conditional Field Display

```csharp
var template = new PopupTemplate
{
    Fields = new List<PopupField>
    {
        new PopupField
        {
            FieldName = "status",
            Label = "Status",
            VisibilityCondition = "properties.active === true",
            Order = 1
        },
        new PopupField
        {
            FieldName = "owner",
            Label = "Owner",
            VisibilityCondition = "properties.type === 'private'",
            Order = 2
        }
    }
};
```

### Field Types and Formatting

```csharp
new List<PopupField>
{
    new()
    {
        FieldName = "name",
        Type = PopupFieldType.Text
    },
    new()
    {
        FieldName = "area",
        Type = PopupFieldType.Number,
        Format = "N2" // 2 decimal places
    },
    new()
    {
        FieldName = "price",
        Type = PopupFieldType.Currency,
        Format = "C2" // Currency with 2 decimals
    },
    new()
    {
        FieldName = "completion",
        Type = PopupFieldType.Percentage
    },
    new()
    {
        FieldName = "created_date",
        Type = PopupFieldType.DateTime,
        Format = "yyyy-MM-dd HH:mm"
    },
    new()
    {
        FieldName = "website",
        Type = PopupFieldType.Url
    },
    new()
    {
        FieldName = "email",
        Type = PopupFieldType.Email
    },
    new()
    {
        FieldName = "photo",
        Type = PopupFieldType.Image
    },
    new()
    {
        FieldName = "active",
        Type = PopupFieldType.Boolean
    }
}
```

### Multi-Feature Selection

When multiple features are clicked, users can navigate between them:

```razor
<HonuaPopup
    SyncWith="map1"
    AllowMultipleFeatures="true"
    MaxWidth="500" />
```

The popup will show pagination controls (Previous/Next buttons) when multiple features are selected.

### Manual Popup Control

```razor
<HonuaPopup
    @ref="popupRef"
    SyncWith="map1"
    TriggerMode="PopupTrigger.Manual" />

<MudButton OnClick="ShowCustomPopup">Show Info</MudButton>

@code {
    private HonuaPopup? popupRef;

    private async Task ShowCustomPopup()
    {
        var content = new PopupContent
        {
            FeatureId = "custom-1",
            LayerId = "custom",
            Coordinates = new[] { -122.4194, 37.7749 }, // San Francisco
            Properties = new Dictionary<string, object>
            {
                { "name", "San Francisco" },
                { "state", "California" },
                { "population", 873965 }
            }
        };

        await popupRef!.ShowPopup(content);
    }
}
```

## ComponentBus Integration

The HonuaPopup component integrates with the ComponentBus for cross-component communication.

### Messages Published

- **PopupOpenedMessage**: When popup opens
- **PopupClosedMessage**: When popup closes

### Messages Subscribed

- **FeatureClickedMessage**: Opens popup when feature is clicked
- **FeatureHoveredMessage**: Opens popup on hover (if TriggerMode is Hover)
- **FeatureSelectedMessage**: Opens popup when feature is selected
- **OpenPopupRequestMessage**: Opens popup at specific location
- **ClosePopupRequestMessage**: Closes the popup

### Example: Opening Popup from Another Component

```csharp
// From any component
await Bus.PublishAsync(new OpenPopupRequestMessage
{
    MapId = "map1",
    Coordinates = new[] { longitude, latitude },
    Properties = new Dictionary<string, object>
    {
        { "title", "Custom Location" },
        { "description", "A programmatically opened popup" }
    }
}, "my-component");
```

## Styling

### Custom CSS

```razor
<HonuaPopup
    SyncWith="map1"
    CssClass="my-custom-popup" />
```

```css
.my-custom-popup .popup-title {
    color: #e91e63;
    font-size: 1.5rem;
}

.my-custom-popup .property-label {
    font-weight: 700;
    color: #9c27b0;
}
```

### Size Customization

```razor
<HonuaPopup
    SyncWith="map1"
    MaxWidth="600"
    MaxHeight="800" />
```

## Accessibility

The HonuaPopup component follows accessibility best practices:

- ARIA labels on all interactive elements
- Keyboard navigation support
- Focus management
- Screen reader compatible
- High contrast mode support

## Performance Considerations

1. **Query Layers**: Specify `QueryLayers` to limit feature queries
2. **Template Complexity**: Keep templates simple for better performance
3. **Multi-Feature Limit**: Consider limiting the number of features returned
4. **Auto-Pan**: Disable if not needed for better responsiveness

```razor
<HonuaPopup
    SyncWith="map1"
    QueryLayers="new[] { \"interactive-layer-1\", \"interactive-layer-2\" }"
    AutoPan="false" />
```

## Browser Support

- Chrome/Edge: Full support
- Firefox: Full support
- Safari: Full support
- Mobile browsers: Full support with touch gestures

## Common Patterns

### Read-Only Information Display

```razor
<HonuaPopup
    SyncWith="map1"
    ShowActions="false"
    CloseOnMapClick="true" />
```

### Editable Features with Actions

```razor
<HonuaPopup
    SyncWith="map1"
    ShowActions="true"
    CustomActions="editActions"
    OnActionTriggered="HandleEdit" />
```

### Tooltip-Style Hover Info

```razor
<HonuaPopup
    SyncWith="map1"
    TriggerMode="PopupTrigger.Hover"
    ShowCloseButton="false"
    ShowActions="false"
    MaxWidth="250"
    MaxHeight="200" />
```

### Integration with Editor Component

```razor
<HonuaMap Id="map1" />

<HonuaDraw SyncWith="map1" />

<HonuaPopup
    SyncWith="map1"
    CustomActions="editorActions"
    OnActionTriggered="HandleEditorAction" />

@code {
    private List<PopupAction> editorActions = new()
    {
        new() { Id = "edit", Label = "Edit", Icon = Icons.Material.Filled.Edit, Type = PopupActionType.Edit },
        new() { Id = "delete", Label = "Delete", Icon = Icons.Material.Filled.Delete, Type = PopupActionType.Delete, Color = "error" }
    };

    private async Task HandleEditorAction((PopupAction Action, PopupContent Content) args)
    {
        if (args.Action.Id == "edit")
        {
            await Bus.PublishAsync(new EditorFeatureSelectedMessage
            {
                FeatureId = args.Content.FeatureId,
                LayerId = args.Content.LayerId,
                ComponentId = "editor",
                Attributes = args.Content.Properties
            }, "popup");
        }
    }
}
```

## Troubleshooting

### Popup Not Showing

1. Verify `SyncWith` matches the map ID
2. Check that features are clickable/hoverable on the map
3. Ensure `TriggerMode` is set correctly
4. Check browser console for errors

### Template Not Rendering

1. Verify template syntax is correct
2. Check that property names match feature attributes
3. Ensure Template parameter is bound correctly

### Actions Not Working

1. Verify `ShowActions="true"`
2. Check that actions have unique IDs
3. Ensure `OnActionTriggered` is bound if using custom actions

## See Also

- [Examples.md](./Examples.md) - Practical examples
- [HonuaMap Documentation](../Map/README.md)
- [HonuaDraw Documentation](../Draw/README.md)
- [ComponentBus Documentation](../../Core/README.md)

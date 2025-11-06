# HonuaPopup Examples

This document provides practical examples of using the HonuaPopup component in various scenarios.

## Table of Contents

1. [Basic Click Popup](#1-basic-click-popup)
2. [Hover Tooltip](#2-hover-tooltip)
3. [Custom HTML Template](#3-custom-html-template)
4. [Image Gallery Popup](#4-image-gallery-popup)
5. [Popup with Action Buttons](#5-popup-with-action-buttons)
6. [Multi-Feature Popup with Pagination](#6-multi-feature-popup-with-pagination)
7. [Per-Layer Custom Templates](#7-per-layer-custom-templates)
8. [Integration with Editor Component](#8-integration-with-editor-component)
9. [Real Estate Property Popup](#9-real-estate-property-popup)
10. [Weather Station Popup](#10-weather-station-popup)

---

## 1. Basic Click Popup

The simplest popup showing feature attributes in a table format.

```razor
@page "/examples/popup/basic"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Popup

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Basic Click Popup</MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="basic-map"
            Center="new[] { -122.4194, 37.7749 }"
            Zoom="12"
            Style="height: 100%;" />

        <HonuaPopup
            SyncWith="basic-map"
            TriggerMode="PopupTrigger.Click" />
    </div>
</MudContainer>
```

**Result**: Click any feature on the map to see its attributes in a clean, auto-generated table.

---

## 2. Hover Tooltip

Instant information display on hover without requiring clicks.

```razor
@page "/examples/popup/hover"

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Hover Tooltip</MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="hover-map"
            Center="new[] { -73.9857, 40.7484 }"
            Zoom="13"
            Style="height: 100%;" />

        <HonuaPopup
            SyncWith="hover-map"
            TriggerMode="PopupTrigger.Hover"
            MaxWidth="300"
            MaxHeight="200"
            ShowCloseButton="false"
            ShowActions="false"
            AutoPan="false" />
    </div>
</MudContainer>
```

**Result**: Hover over features to see instant tooltips without clicking.

---

## 3. Custom HTML Template

Using a custom Razor template for rich popup content.

```razor
@page "/examples/popup/custom-template"
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Custom Template Popup</MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="template-map"
            Center="new[] { -118.2437, 34.0522 }"
            Zoom="11"
            Style="height: 100%;" />

        <HonuaPopup SyncWith="template-map">
            <Template Context="content">
                <div class="custom-popup-template">
                    @{
                        var name = content.Properties.GetValueOrDefault("name")?.ToString() ?? "Unknown";
                        var type = content.Properties.GetValueOrDefault("type")?.ToString() ?? "N/A";
                        var description = content.Properties.GetValueOrDefault("description")?.ToString();
                    }

                    <MudCard Elevation="0">
                        <MudCardHeader>
                            <CardHeaderAvatar>
                                <MudAvatar Color="Color.Primary">
                                    <MudIcon Icon="@GetIconForType(type)" />
                                </MudAvatar>
                            </CardHeaderAvatar>
                            <CardHeaderContent>
                                <MudText Typo="Typo.h6">@name</MudText>
                                <MudText Typo="Typo.body2" Color="Color.Secondary">@type</MudText>
                            </CardHeaderContent>
                        </MudCardHeader>

                        @if (!string.IsNullOrEmpty(description))
                        {
                            <MudCardContent>
                                <MudText Typo="Typo.body2">@description</MudText>
                            </MudCardContent>
                        }

                        @if (content.Properties.ContainsKey("rating"))
                        {
                            <MudCardActions>
                                <MudRating
                                    ReadOnly="true"
                                    SelectedValue="@Convert.ToInt32(content.Properties["rating"])"
                                    MaxValue="5" />
                            </MudCardActions>
                        }
                    </MudCard>
                </div>
            </Template>
        </HonuaPopup>
    </div>
</MudContainer>

@code {
    private string GetIconForType(string type)
    {
        return type.ToLower() switch
        {
            "restaurant" => Icons.Material.Filled.Restaurant,
            "hotel" => Icons.Material.Filled.Hotel,
            "park" => Icons.Material.Filled.Park,
            "museum" => Icons.Material.Filled.Museum,
            "shopping" => Icons.Material.Filled.ShoppingCart,
            _ => Icons.Material.Filled.Place
        };
    }
}

<style>
    .custom-popup-template {
        max-width: 350px;
    }
</style>
```

**Result**: Rich, styled popup with icons, ratings, and formatted content.

---

## 4. Image Gallery Popup

Popup displaying images with a carousel.

```razor
@page "/examples/popup/image-gallery"

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Image Gallery Popup</MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="gallery-map"
            Center="new[] { 2.3522, 48.8566 }"
            Zoom="13"
            Style="height: 100%;" />

        <HonuaPopup SyncWith="gallery-map" MaxWidth="500">
            <Template Context="content">
                <div class="gallery-popup">
                    <MudText Typo="Typo.h5" Class="mb-3">
                        @content.Properties.GetValueOrDefault("name")
                    </MudText>

                    @{
                        var images = GetImages(content.Properties);
                    }

                    @if (images.Any())
                    {
                        <MudCarousel
                            Class="popup-carousel"
                            Style="height: 250px;"
                            ShowArrows="true"
                            ShowBullets="true"
                            EnableSwipeGesture="true"
                            AutoCycle="false">
                            @foreach (var image in images)
                            {
                                <MudCarouselItem>
                                    <div class="carousel-image" style="background-image: url('@image');">
                                    </div>
                                </MudCarouselItem>
                            }
                        </MudCarousel>
                    }

                    <MudText Typo="Typo.body2" Class="mt-3">
                        @content.Properties.GetValueOrDefault("description")
                    </MudText>

                    <MudChipSet Class="mt-2">
                        @foreach (var tag in GetTags(content.Properties))
                        {
                            <MudChip Size="Size.Small" Color="Color.Primary" Variant="Variant.Outlined">
                                @tag
                            </MudChip>
                        }
                    </MudChipSet>
                </div>
            </Template>
        </HonuaPopup>
    </div>
</MudContainer>

@code {
    private List<string> GetImages(Dictionary<string, object> properties)
    {
        var images = new List<string>();

        // Check for image array
        if (properties.ContainsKey("images") && properties["images"] is JsonElement imagesElement)
        {
            if (imagesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in imagesElement.EnumerateArray())
                {
                    images.Add(img.GetString() ?? "");
                }
            }
        }
        // Check for single image
        else if (properties.ContainsKey("image"))
        {
            images.Add(properties["image"]?.ToString() ?? "");
        }

        return images.Where(i => !string.IsNullOrEmpty(i)).ToList();
    }

    private List<string> GetTags(Dictionary<string, object> properties)
    {
        if (properties.ContainsKey("tags") && properties["tags"] is JsonElement tagsElement)
        {
            if (tagsElement.ValueKind == JsonValueKind.Array)
            {
                return tagsElement.EnumerateArray()
                    .Select(t => t.GetString() ?? "")
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();
            }
        }

        return new List<string>();
    }
}

<style>
    .carousel-image {
        width: 100%;
        height: 250px;
        background-size: cover;
        background-position: center;
        border-radius: 8px;
    }
</style>
```

**Result**: Feature popup with image carousel and tags.

---

## 5. Popup with Action Buttons

Interactive popup with zoom, edit, and export actions.

```razor
@page "/examples/popup/actions"
@inject ISnackbar Snackbar

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Popup with Actions</MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="actions-map"
            Center="new[] { 151.2093, -33.8688 }"
            Zoom="12"
            Style="height: 100%;" />

        <HonuaPopup
            SyncWith="actions-map"
            CustomActions="customActions"
            ShowActions="true"
            OnActionTriggered="HandleAction" />
    </div>
</MudContainer>

@code {
    private List<PopupAction> customActions = new()
    {
        new PopupAction
        {
            Id = "zoom",
            Label = "Zoom To",
            Icon = Icons.Material.Filled.ZoomIn,
            Type = PopupActionType.ZoomTo,
            Color = "primary",
            Tooltip = "Zoom to this feature",
            Order = 1
        },
        new PopupAction
        {
            Id = "copy-coords",
            Label = "Copy Coordinates",
            Icon = Icons.Material.Filled.ContentCopy,
            Type = PopupActionType.CopyCoordinates,
            Tooltip = "Copy coordinates to clipboard",
            Order = 2
        },
        new PopupAction
        {
            Id = "export",
            Label = "Export",
            Icon = Icons.Material.Filled.FileDownload,
            Type = PopupActionType.Custom,
            Tooltip = "Export feature data",
            Order = 3
        },
        new PopupAction
        {
            Id = "share",
            Label = "Share",
            Icon = Icons.Material.Filled.Share,
            Type = PopupActionType.Custom,
            Color = "info",
            Tooltip = "Share this feature",
            Order = 4
        },
        new PopupAction
        {
            Id = "delete",
            Label = "Delete",
            Icon = Icons.Material.Filled.Delete,
            Type = PopupActionType.Delete,
            Color = "error",
            Tooltip = "Delete this feature",
            Order = 5
        }
    };

    private async Task HandleAction((PopupAction Action, PopupContent Content) args)
    {
        switch (args.Action.Id)
        {
            case "export":
                var json = JsonSerializer.Serialize(args.Content.Properties, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                Snackbar.Add("Feature data exported", Severity.Success);
                // Download logic here
                break;

            case "share":
                var shareUrl = $"/features/{args.Content.FeatureId}";
                Snackbar.Add($"Share URL: {shareUrl}", Severity.Info);
                break;
        }
    }
}
```

**Result**: Feature popup with multiple action buttons for common operations.

---

## 6. Multi-Feature Popup with Pagination

Navigate between multiple selected features.

```razor
@page "/examples/popup/multi-feature"

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Multi-Feature Selection</MudText>
    <MudText Typo="Typo.body1" Class="mb-3">
        Click where multiple features overlap to see pagination controls.
    </MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="multi-map"
            Center="new[] { -0.1278, 51.5074 }"
            Zoom="14"
            Style="height: 100%;" />

        <HonuaPopup
            SyncWith="multi-map"
            AllowMultipleFeatures="true"
            MaxWidth="450"
            OnPopupOpened="HandlePopupOpened" />
    </div>

    @if (featureCount > 1)
    {
        <MudAlert Severity="Severity.Info" Class="mt-3">
            Found @featureCount features at this location. Use the pagination controls to navigate.
        </MudAlert>
    }
</MudContainer>

@code {
    private int featureCount = 0;

    private void HandlePopupOpened(PopupContent content)
    {
        // This would be set from the component's internal state
        featureCount = 1; // Updated by component
    }
}
```

**Result**: When clicking overlapping features, pagination controls appear to navigate between them.

---

## 7. Per-Layer Custom Templates

Different templates for different layer types.

```razor
@page "/examples/popup/layer-templates"

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Layer-Specific Templates</MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="layers-map"
            Center="new[] { 139.6917, 35.6895 }"
            Zoom="12"
            Style="height: 100%;" />

        <HonuaPopup
            SyncWith="layers-map"
            LayerTemplates="layerTemplates" />
    </div>
</MudContainer>

@code {
    private Dictionary<string, PopupTemplate> layerTemplates = new()
    {
        {
            "buildings-layer",
            new PopupTemplate
            {
                Title = "Building: {name}",
                ContentTemplate = @"
                    <div class='building-popup'>
                        <div class='info-row'>
                            <strong>Height:</strong> {height} meters
                        </div>
                        <div class='info-row'>
                            <strong>Year Built:</strong> {year_built}
                        </div>
                        <div class='info-row'>
                            <strong>Floors:</strong> {floors}
                        </div>
                        <div class='info-row'>
                            <strong>Use:</strong> {building_use}
                        </div>
                    </div>
                ",
                Actions = new List<PopupAction>
                {
                    new()
                    {
                        Id = "view-3d",
                        Label = "View in 3D",
                        Icon = Icons.Material.Filled.ViewInAr,
                        Type = PopupActionType.Custom,
                        Order = 1
                    }
                }
            }
        },
        {
            "parks-layer",
            new PopupTemplate
            {
                Title = "{name}",
                ContentTemplate = @"
                    <div class='park-popup'>
                        <div class='park-header'>
                            <span class='park-icon'>🌳</span>
                            <span class='park-type'>{park_type}</span>
                        </div>
                        <div class='info-row'>
                            <strong>Area:</strong> {area_sqm} m²
                        </div>
                        <div class='info-row'>
                            <strong>Facilities:</strong> {facilities}
                        </div>
                        <div class='info-row'>
                            <strong>Opening Hours:</strong> {opening_hours}
                        </div>
                    </div>
                ",
                Actions = new List<PopupAction>
                {
                    new()
                    {
                        Id = "directions",
                        Label = "Get Directions",
                        Icon = Icons.Material.Filled.Directions,
                        Type = PopupActionType.Custom,
                        Color = "primary",
                        Order = 1
                    }
                }
            }
        },
        {
            "restaurants-layer",
            new PopupTemplate
            {
                Title = "{name}",
                Fields = new List<PopupField>
                {
                    new() { FieldName = "cuisine", Label = "Cuisine", Order = 1 },
                    new() { FieldName = "rating", Label = "Rating", Type = PopupFieldType.Number, Format = "N1", Order = 2 },
                    new() { FieldName = "price_range", Label = "Price Range", Order = 3 },
                    new() { FieldName = "phone", Label = "Phone", Type = PopupFieldType.Phone, Order = 4 },
                    new() { FieldName = "website", Label = "Website", Type = PopupFieldType.Url, Order = 5 }
                },
                Actions = new List<PopupAction>
                {
                    new()
                    {
                        Id = "reserve",
                        Label = "Make Reservation",
                        Icon = Icons.Material.Filled.TableRestaurant,
                        Type = PopupActionType.Custom,
                        Color = "success",
                        Order = 1
                    }
                }
            }
        }
    };
}

<style>
    .building-popup,
    .park-popup {
        padding: 8px 0;
    }

    .info-row {
        padding: 4px 0;
        line-height: 1.6;
    }

    .park-header {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 12px;
    }

    .park-icon {
        font-size: 24px;
    }

    .park-type {
        font-weight: 600;
        color: #4caf50;
    }
</style>
```

**Result**: Different popup styles and actions for buildings, parks, and restaurants.

---

## 8. Integration with Editor Component

Popup that works with the drawing/editing component.

```razor
@page "/examples/popup/editor-integration"
@inject ComponentBus Bus

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Editor Integration</MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="editor-map"
            Center="new[] { -122.3321, 47.6062 }"
            Zoom="12"
            Style="height: 100%;" />

        <HonuaDraw
            SyncWith="editor-map"
            Position="top-left"
            OnFeatureDrawn="HandleFeatureDrawn" />

        <HonuaPopup
            SyncWith="editor-map"
            CustomActions="editorActions"
            OnActionTriggered="HandleEditorAction"
            OnFeatureClick="HandleFeatureClick" />
    </div>

    @if (editingFeature != null)
    {
        <MudPaper Class="mt-3 pa-4">
            <MudText Typo="Typo.h6">Editing: @editingFeature.FeatureId</MudText>
            <MudTextField
                @bind-Value="featureName"
                Label="Name"
                Variant="Variant.Outlined"
                Class="mt-2" />
            <MudTextField
                @bind-Value="featureDescription"
                Label="Description"
                Lines="3"
                Variant="Variant.Outlined"
                Class="mt-2" />
            <MudButton
                Color="Color.Primary"
                Variant="Variant.Filled"
                OnClick="SaveFeature"
                Class="mt-2">
                Save Changes
            </MudButton>
        </MudPaper>
    }
</MudContainer>

@code {
    private List<PopupAction> editorActions = new()
    {
        new()
        {
            Id = "edit",
            Label = "Edit Properties",
            Icon = Icons.Material.Filled.Edit,
            Type = PopupActionType.Custom,
            Color = "primary",
            Order = 1
        },
        new()
        {
            Id = "edit-geometry",
            Label = "Edit Shape",
            Icon = Icons.Material.Filled.Architecture,
            Type = PopupActionType.Custom,
            Order = 2
        },
        new()
        {
            Id = "delete",
            Label = "Delete",
            Icon = Icons.Material.Filled.Delete,
            Type = PopupActionType.Delete,
            Color = "error",
            Order = 3
        }
    };

    private PopupContent? editingFeature;
    private string featureName = "";
    private string featureDescription = "";

    private void HandleFeatureDrawn(DrawingFeature feature)
    {
        // Feature drawn, could auto-open popup
    }

    private void HandleFeatureClick(PopupContent content)
    {
        // Feature clicked in popup
    }

    private async Task HandleEditorAction((PopupAction Action, PopupContent Content) args)
    {
        switch (args.Action.Id)
        {
            case "edit":
                editingFeature = args.Content;
                featureName = args.Content.Properties.GetValueOrDefault("name")?.ToString() ?? "";
                featureDescription = args.Content.Properties.GetValueOrDefault("description")?.ToString() ?? "";
                StateHasChanged();
                break;

            case "edit-geometry":
                await Bus.PublishAsync(new EditorFeatureSelectedMessage
                {
                    FeatureId = args.Content.FeatureId,
                    LayerId = args.Content.LayerId,
                    ComponentId = "editor",
                    Attributes = args.Content.Properties
                }, "popup");
                break;
        }
    }

    private async Task SaveFeature()
    {
        if (editingFeature == null) return;

        // Update feature properties
        editingFeature.Properties["name"] = featureName;
        editingFeature.Properties["description"] = featureDescription;

        await Bus.PublishAsync(new FeatureUpdatedMessage
        {
            FeatureId = editingFeature.FeatureId,
            LayerId = editingFeature.LayerId,
            Attributes = editingFeature.Properties,
            UpdateType = "attributes",
            ComponentId = "popup"
        }, "popup");

        editingFeature = null;
        StateHasChanged();
    }
}
```

**Result**: Click features to edit their properties or geometry through the popup.

---

## 9. Real Estate Property Popup

Detailed property information with pricing and features.

```razor
@page "/examples/popup/real-estate"

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Real Estate Property Popup</MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="property-map"
            Center="new[] { -80.1918, 25.7617 }"
            Zoom="13"
            Style="height: 100%;" />

        <HonuaPopup SyncWith="property-map" MaxWidth="450">
            <Template Context="content">
                <div class="property-popup">
                    @{
                        var price = content.Properties.GetValueOrDefault("price")?.ToString() ?? "N/A";
                        var beds = content.Properties.GetValueOrDefault("bedrooms")?.ToString() ?? "0";
                        var baths = content.Properties.GetValueOrDefault("bathrooms")?.ToString() ?? "0";
                        var sqft = content.Properties.GetValueOrDefault("square_feet")?.ToString() ?? "0";
                        var status = content.Properties.GetValueOrDefault("status")?.ToString() ?? "Available";
                        var image = content.Properties.GetValueOrDefault("image")?.ToString();
                    }

                    @if (!string.IsNullOrEmpty(image))
                    {
                        <div class="property-image" style="background-image: url('@image');"></div>
                    }

                    <div class="property-details">
                        <div class="property-price">@price</div>

                        <div class="property-specs">
                            <div class="spec-item">
                                <MudIcon Icon="@Icons.Material.Filled.Bed" Size="Size.Small" />
                                <span>@beds beds</span>
                            </div>
                            <div class="spec-item">
                                <MudIcon Icon="@Icons.Material.Filled.Bathtub" Size="Size.Small" />
                                <span>@baths baths</span>
                            </div>
                            <div class="spec-item">
                                <MudIcon Icon="@Icons.Material.Filled.SquareFoot" Size="Size.Small" />
                                <span>@sqft sqft</span>
                            </div>
                        </div>

                        <MudChip Size="Size.Small"
                                 Color="@(status == "Available" ? Color.Success : Color.Warning)"
                                 Class="mt-2">
                            @status
                        </MudChip>

                        <MudText Typo="Typo.body2" Class="mt-2">
                            @content.Properties.GetValueOrDefault("address")
                        </MudText>

                        <MudDivider Class="my-3" />

                        <MudText Typo="Typo.body2" Color="Color.Secondary">
                            @content.Properties.GetValueOrDefault("description")
                        </MudText>

                        <div class="property-actions">
                            <MudButton
                                Variant="Variant.Filled"
                                Color="Color.Primary"
                                FullWidth="true"
                                StartIcon="@Icons.Material.Filled.Phone">
                                Contact Agent
                            </MudButton>
                            <MudButton
                                Variant="Variant.Outlined"
                                Color="Color.Primary"
                                FullWidth="true"
                                StartIcon="@Icons.Material.Filled.CalendarToday"
                                Class="mt-2">
                                Schedule Tour
                            </MudButton>
                        </div>
                    </div>
                </div>
            </Template>
        </HonuaPopup>
    </div>
</MudContainer>

<style>
    .property-popup {
        max-width: 450px;
    }

    .property-image {
        width: 100%;
        height: 200px;
        background-size: cover;
        background-position: center;
        border-radius: 8px;
        margin-bottom: 16px;
    }

    .property-price {
        font-size: 1.5rem;
        font-weight: 700;
        color: #1976d2;
        margin-bottom: 12px;
    }

    .property-specs {
        display: flex;
        gap: 16px;
        margin-bottom: 12px;
    }

    .spec-item {
        display: flex;
        align-items: center;
        gap: 4px;
        font-size: 0.875rem;
    }

    .property-actions {
        margin-top: 16px;
    }
</style>
```

**Result**: Rich property details with pricing, specs, and action buttons.

---

## 10. Weather Station Popup

Live weather data with charts.

```razor
@page "/examples/popup/weather"
@using ApexCharts

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Weather Station Popup</MudText>

    <div style="height: 600px; position: relative;">
        <HonuaMap
            Id="weather-map"
            Center="new[] { -96.7970, 32.7767 }"
            Zoom="11"
            Style="height: 100%;" />

        <HonuaPopup SyncWith="weather-map" MaxWidth="500" MaxHeight="700">
            <Template Context="content">
                <div class="weather-popup">
                    <div class="weather-header">
                        <div>
                            <MudText Typo="Typo.h6">
                                @content.Properties.GetValueOrDefault("station_name")
                            </MudText>
                            <MudText Typo="Typo.caption" Color="Color.Secondary">
                                Last updated: @GetLastUpdate(content.Properties)
                            </MudText>
                        </div>
                    </div>

                    <MudGrid Class="mt-3">
                        <MudItem xs="6">
                            <div class="weather-metric">
                                <MudIcon Icon="@Icons.Material.Filled.Thermostat"
                                         Color="Color.Error"
                                         Size="Size.Large" />
                                <div>
                                    <MudText Typo="Typo.h4">
                                        @content.Properties.GetValueOrDefault("temperature")°C
                                    </MudText>
                                    <MudText Typo="Typo.caption">Temperature</MudText>
                                </div>
                            </div>
                        </MudItem>
                        <MudItem xs="6">
                            <div class="weather-metric">
                                <MudIcon Icon="@Icons.Material.Filled.Water"
                                         Color="Color.Info"
                                         Size="Size.Large" />
                                <div>
                                    <MudText Typo="Typo.h4">
                                        @content.Properties.GetValueOrDefault("humidity")%
                                    </MudText>
                                    <MudText Typo="Typo.caption">Humidity</MudText>
                                </div>
                            </div>
                        </MudItem>
                        <MudItem xs="6">
                            <div class="weather-metric">
                                <MudIcon Icon="@Icons.Material.Filled.Air"
                                         Color="Color.Success"
                                         Size="Size.Large" />
                                <div>
                                    <MudText Typo="Typo.h4">
                                        @content.Properties.GetValueOrDefault("wind_speed") km/h
                                    </MudText>
                                    <MudText Typo="Typo.caption">Wind Speed</MudText>
                                </div>
                            </div>
                        </MudItem>
                        <MudItem xs="6">
                            <div class="weather-metric">
                                <MudIcon Icon="@Icons.Material.Filled.Compress"
                                         Color="Color.Warning"
                                         Size="Size.Large" />
                                <div>
                                    <MudText Typo="Typo.h4">
                                        @content.Properties.GetValueOrDefault("pressure") hPa
                                    </MudText>
                                    <MudText Typo="Typo.caption">Pressure</MudText>
                                </div>
                            </div>
                        </MudItem>
                    </MudGrid>

                    <MudDivider Class="my-3" />

                    <MudText Typo="Typo.subtitle2" Class="mb-2">
                        24-Hour Temperature Trend
                    </MudText>

                    <div class="weather-chart" style="height: 150px;">
                        <!-- Chart would be rendered here -->
                        <div class="chart-placeholder">Temperature chart</div>
                    </div>
                </div>
            </Template>
        </HonuaPopup>
    </div>
</MudContainer>

@code {
    private string GetLastUpdate(Dictionary<string, object> properties)
    {
        if (properties.ContainsKey("last_update"))
        {
            if (DateTime.TryParse(properties["last_update"]?.ToString(), out var date))
            {
                return date.ToString("MMM d, yyyy HH:mm");
            }
        }
        return "N/A";
    }
}

<style>
    .weather-popup {
        padding: 8px;
    }

    .weather-header {
        display: flex;
        justify-content: space-between;
        align-items: start;
    }

    .weather-metric {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 12px;
        background: #f5f5f5;
        border-radius: 8px;
    }

    .weather-chart {
        background: #f5f5f5;
        border-radius: 8px;
        padding: 16px;
    }

    .chart-placeholder {
        display: flex;
        align-items: center;
        justify-content: center;
        height: 100%;
        color: #999;
    }
</style>
```

**Result**: Comprehensive weather station information with metrics and trends.

---

## Tips and Best Practices

1. **Performance**: Use `QueryLayers` to limit feature queries to interactive layers only
2. **Mobile**: Test popups on mobile devices to ensure touch-friendly interactions
3. **Content**: Keep popup content concise - use links to detail pages for more info
4. **Actions**: Limit to 3-5 action buttons for better UX
5. **Images**: Optimize image sizes for faster loading
6. **Templates**: Use conditional rendering to handle missing data gracefully
7. **Accessibility**: Always include ARIA labels and keyboard navigation support

## Additional Resources

- [README.md](./README.md) - Full component documentation
- [HonuaMap Documentation](../Map/README.md)
- [MudBlazor Components](https://mudblazor.com/)
- [MapLibre GL JS Popups](https://maplibre.org/maplibre-gl-js-docs/api/markers/#popup)

# Tutorial 05: Building a Data Collection App

> **Learning Objectives**: Create a field data collection application with editing workflows, attribute forms, validation, offline support, and data synchronization.

---

## Prerequisites

- Completed previous tutorials OR MapSDK basics
- .NET 8.0 SDK
- Understanding of data validation

**Estimated Time**: 55 minutes

---

## Overview

Build a field data collection app with:

- ✏️ **Drawing & editing** features on map
- 📝 **Attribute forms** with validation
- ✅ **Data validation** and business rules
- 🔄 **Backend integration** with API
- 📱 **Offline support** patterns
- 🔄 **Data synchronization** strategies

---

## Complete Implementation

### Data Models (`Models/FieldData.cs`)

```csharp
namespace FieldDataCollection.Models
{
    public class Feature
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = "Point"; // Point, LineString, Polygon
        public FeatureGeometry Geometry { get; set; } = new();
        public FeatureProperties Properties { get; set; } = new();
        public FeatureStatus Status { get; set; } = FeatureStatus.Draft;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool IsSynced { get; set; } = false;
        public DateTime? LastSyncDate { get; set; }
    }

    public class FeatureGeometry
    {
        public string Type { get; set; } = "Point";
        public List<double[]> Coordinates { get; set; } = new();
    }

    public class FeatureProperties
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, object> CustomFields { get; set; } = new();
    }

    public enum FeatureStatus
    {
        Draft,
        Submitted,
        Approved,
        Rejected
    }

    public class FormDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<FormField> Fields { get; set; } = new();
    }

    public class FormField
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public FieldType Type { get; set; }
        public bool Required { get; set; }
        public List<string>? Options { get; set; }
        public string? ValidationPattern { get; set; }
        public string? DefaultValue { get; set; }
    }

    public enum FieldType
    {
        Text,
        Number,
        Date,
        Select,
        Checkbox,
        TextArea
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
```

### Editor Service (`Services/EditorService.cs`)

```csharp
using FieldDataCollection.Models;

namespace FieldDataCollection.Services
{
    public interface IEditorService
    {
        Task<List<Feature>> GetFeaturesAsync();
        Task<Feature?> GetFeatureAsync(string id);
        Task<Feature> SaveFeatureAsync(Feature feature);
        Task<bool> DeleteFeatureAsync(string id);
        Task<ValidationResult> ValidateFeatureAsync(Feature feature);
        Task<List<FormDefinition>> GetFormDefinitionsAsync();
        Task<bool> SyncFeaturesAsync(List<Feature> features);
    }

    public class EditorService : IEditorService
    {
        private readonly List<Feature> _features = new();
        private readonly List<FormDefinition> _formDefinitions;

        public EditorService()
        {
            _formDefinitions = GenerateFormDefinitions();
        }

        public Task<List<Feature>> GetFeaturesAsync()
        {
            return Task.FromResult(_features);
        }

        public Task<Feature?> GetFeatureAsync(string id)
        {
            return Task.FromResult(_features.FirstOrDefault(f => f.Id == id));
        }

        public Task<Feature> SaveFeatureAsync(Feature feature)
        {
            var existing = _features.FirstOrDefault(f => f.Id == feature.Id);
            if (existing != null)
            {
                _features.Remove(existing);
                feature.ModifiedDate = DateTime.Now;
            }
            _features.Add(feature);
            return Task.FromResult(feature);
        }

        public Task<bool> DeleteFeatureAsync(string id)
        {
            var feature = _features.FirstOrDefault(f => f.Id == id);
            if (feature != null)
            {
                _features.Remove(feature);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<ValidationResult> ValidateFeatureAsync(Feature feature)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(feature.Properties.Name))
            {
                result.IsValid = false;
                result.Errors.Add("Name is required");
            }

            if (feature.Geometry.Coordinates.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("Geometry is required");
            }

            if (string.IsNullOrWhiteSpace(feature.Properties.Category))
            {
                result.IsValid = false;
                result.Errors.Add("Category is required");
            }

            return Task.FromResult(result);
        }

        public Task<List<FormDefinition>> GetFormDefinitionsAsync()
        {
            return Task.FromResult(_formDefinitions);
        }

        public Task<bool> SyncFeaturesAsync(List<Feature> features)
        {
            foreach (var feature in features.Where(f => !f.IsSynced))
            {
                feature.IsSynced = true;
                feature.LastSyncDate = DateTime.Now;
            }
            return Task.FromResult(true);
        }

        private List<FormDefinition> GenerateFormDefinitions()
        {
            return new List<FormDefinition>
            {
                new FormDefinition
                {
                    Name = "Tree Inventory",
                    Category = "Environmental",
                    Fields = new List<FormField>
                    {
                        new FormField { Name = "species", Label = "Species", Type = FieldType.Select, Required = true,
                            Options = new List<string> { "Oak", "Maple", "Pine", "Cedar" } },
                        new FormField { Name = "height", Label = "Height (m)", Type = FieldType.Number, Required = true },
                        new FormField { Name = "diameter", Label = "Diameter (cm)", Type = FieldType.Number, Required = true },
                        new FormField { Name = "condition", Label = "Condition", Type = FieldType.Select, Required = true,
                            Options = new List<string> { "Excellent", "Good", "Fair", "Poor" } },
                        new FormField { Name = "notes", Label = "Notes", Type = FieldType.TextArea, Required = false }
                    }
                },
                new FormDefinition
                {
                    Name = "Infrastructure Inspection",
                    Category = "Infrastructure",
                    Fields = new List<FormField>
                    {
                        new FormField { Name = "asset_type", Label = "Asset Type", Type = FieldType.Select, Required = true,
                            Options = new List<string> { "Bridge", "Road", "Sidewalk", "Sign" } },
                        new FormField { Name = "condition_rating", Label = "Condition Rating (1-5)", Type = FieldType.Number, Required = true },
                        new FormField { Name = "maintenance_required", Label = "Maintenance Required", Type = FieldType.Checkbox, Required = false },
                        new FormField { Name = "inspection_date", Label = "Inspection Date", Type = FieldType.Date, Required = true },
                        new FormField { Name = "comments", Label = "Comments", Type = FieldType.TextArea, Required = false }
                    }
                }
            };
        }
    }
}
```

### Editor Page (`Pages/FieldEditor.razor`)

```razor
@page "/editor"
@using FieldDataCollection.Models
@using FieldDataCollection.Services
@using Honua.MapSDK.Components
@inject IEditorService EditorService
@inject ISnackbar Snackbar

<PageTitle>Field Data Editor</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-0" Style="height: 100vh;">
    <MudAppBar Elevation="4">
        <MudIcon Icon="@Icons.Material.Filled.Edit" Size="Size.Large" Class="mr-3" />
        <MudText Typo="Typo.h5">Field Data Collection</MudText>
        <MudSpacer />
        <MudChip Icon="@Icons.Material.Filled.CloudOff" Color="@(_isOffline ? Color.Warning : Color.Success)">
            @(_isOffline ? "OFFLINE" : "ONLINE")
        </MudChip>
        <MudBadge Content="@_pendingSync.ToString()" Color="Color.Error" Overlap="true" Visible="@(_pendingSync > 0)">
            <MudIconButton Icon="@Icons.Material.Filled.CloudUpload" Color="Color.Inherit" OnClick="@SyncData" />
        </MudBadge>
    </MudAppBar>

    <MudGrid Class="pa-3" Style="height: calc(100vh - 64px);">
        <!-- Map with Editor -->
        <MudItem xs="12" md="8" Style="height: 100%;">
            <MudPaper Elevation="3" Style="height: 100%; position: relative;">
                <HonuaMap @ref="_map"
                          Id="editor-map"
                          Center="@(new[] { -122.4194, 37.7749 })"
                          Zoom="13"
                          MapStyle="https://demotiles.maplibre.org/style.json"
                          OnMapReady="@HandleMapReady"
                          OnFeatureClicked="@HandleFeatureClicked"
                          Style="height: 100%;" />

                <!-- Drawing Tools -->
                <div style="position: absolute; top: 10px; left: 10px; z-index: 1000;">
                    <HonuaEditor @ref="_editor"
                                 SyncWith="editor-map"
                                 AllowPoint="true"
                                 AllowLine="true"
                                 AllowPolygon="true"
                                 AllowEdit="true"
                                 AllowDelete="true"
                                 OnFeatureDrawn="@HandleFeatureDrawn"
                                 OnFeatureModified="@HandleFeatureModified">
                        <EditorControls>
                            <MudButtonGroup Vertical="true" OverrideStyles="false">
                                <MudButton Variant="Variant.Filled" Color="Color.Primary" StartIcon="@Icons.Material.Filled.Place" OnClick="@(() => _editor!.StartDrawing(\"Point\"))">Point</MudButton>
                                <MudButton Variant="Variant.Filled" Color="Color.Primary" StartIcon="@Icons.Material.Filled.Timeline" OnClick="@(() => _editor!.StartDrawing(\"LineString\"))">Line</MudButton>
                                <MudButton Variant="Variant.Filled" Color="Color.Primary" StartIcon="@Icons.Material.Filled.Crop" OnClick="@(() => _editor!.StartDrawing(\"Polygon\"))">Polygon</MudButton>
                                <MudDivider />
                                <MudButton Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.Edit" OnClick="@(() => _editor!.EnableEdit())">Edit</MudButton>
                                <MudButton Variant="Variant.Outlined" Color="Color.Error" StartIcon="@Icons.Material.Filled.Delete" OnClick="@DeleteSelectedFeature">Delete</MudButton>
                            </MudButtonGroup>
                        </EditorControls>
                    </HonuaEditor>
                </div>
            </MudPaper>
        </MudItem>

        <!-- Form Panel -->
        <MudItem xs="12" md="4" Style="height: 100%;">
            <MudPaper Elevation="3" Style="height: 100%; overflow-y: auto; padding: 16px;">
                @if (_selectedFeature != null)
                {
                    <MudText Typo="Typo.h6" Class="mb-3">Edit Feature</MudText>

                    <MudSelect @bind-Value="_selectedFormDefinition"
                               Label="Form Template"
                               Variant="Variant.Outlined"
                               Class="mb-3">
                        @foreach (var form in _formDefinitions)
                        {
                            <MudSelectItem Value="@form">@form.Name</MudSelectItem>
                        }
                    </MudSelect>

                    @if (_selectedFormDefinition != null)
                    {
                        <MudDivider Class="mb-3" />

                        @foreach (var field in _selectedFormDefinition.Fields)
                        {
                            <div class="mb-3">
                                @switch (field.Type)
                                {
                                    case FieldType.Text:
                                        <MudTextField @bind-Value="_formData[field.Name]"
                                                      Label="@field.Label"
                                                      Required="@field.Required"
                                                      Variant="Variant.Outlined" />
                                        break;
                                    case FieldType.Number:
                                        <MudNumericField @bind-Value="_formData[field.Name]"
                                                         Label="@field.Label"
                                                         Required="@field.Required"
                                                         Variant="Variant.Outlined" />
                                        break;
                                    case FieldType.Select:
                                        <MudSelect @bind-Value="_formData[field.Name]"
                                                   Label="@field.Label"
                                                   Required="@field.Required"
                                                   Variant="Variant.Outlined">
                                            @foreach (var option in field.Options ?? new List<string>())
                                            {
                                                <MudSelectItem Value="@option">@option</MudSelectItem>
                                            }
                                        </MudSelect>
                                        break;
                                    case FieldType.TextArea:
                                        <MudTextField @bind-Value="_formData[field.Name]"
                                                      Label="@field.Label"
                                                      Required="@field.Required"
                                                      Variant="Variant.Outlined"
                                                      Lines="3" />
                                        break;
                                    case FieldType.Checkbox:
                                        <MudCheckBox @bind-Checked="_formData[field.Name]"
                                                     Label="@field.Label" />
                                        break;
                                    case FieldType.Date:
                                        <MudDatePicker @bind-Date="_formData[field.Name]"
                                                       Label="@field.Label"
                                                       Required="@field.Required"
                                                       Variant="Variant.Outlined" />
                                        break;
                                }
                            </div>
                        }
                    }

                    <MudDivider Class="my-3" />

                    <MudStack Row="true" Justify="Justify.FlexEnd" Spacing="2">
                        <MudButton Variant="Variant.Outlined" OnClick="@ClearForm">Cancel</MudButton>
                        <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="@SaveFeature">Save</MudButton>
                    </MudStack>

                    @if (_validationErrors.Any())
                    {
                        <MudAlert Severity="Severity.Error" Class="mt-3">
                            <MudText Typo="Typo.body2"><strong>Validation Errors:</strong></MudText>
                            @foreach (var error in _validationErrors)
                            {
                                <MudText Typo="Typo.body2">• @error</MudText>
                            }
                        </MudAlert>
                    }
                }
                else
                {
                    <MudText Typo="Typo.body1" Align="Align.Center" Color="Color.Secondary" Class="mt-8">
                        Draw or select a feature to edit
                    </MudText>
                }

                <!-- Feature List -->
                <MudDivider Class="my-4" />
                <MudText Typo="Typo.h6" Class="mb-2">Features (@_features.Count)</MudText>
                @foreach (var feature in _features)
                {
                    <MudCard Class="mb-2 cursor-pointer" @onclick="@(() => SelectFeature(feature))">
                        <MudCardContent Class="pa-2">
                            <MudStack Row="true" Justify="Justify.SpaceBetween">
                                <MudText Typo="Typo.body2">@feature.Properties.Name</MudText>
                                <MudChip Size="Size.Small" Color="@(feature.IsSynced ? Color.Success : Color.Warning)">
                                    @(feature.IsSynced ? "Synced" : "Pending")
                                </MudChip>
                            </MudStack>
                        </MudCardContent>
                    </MudCard>
                }
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private HonuaMap? _map;
    private HonuaEditor? _editor;
    private List<Feature> _features = new();
    private Feature? _selectedFeature;
    private List<FormDefinition> _formDefinitions = new();
    private FormDefinition? _selectedFormDefinition;
    private Dictionary<string, object> _formData = new();
    private List<string> _validationErrors = new();
    private bool _isOffline = false;
    private int _pendingSync => _features.Count(f => !f.IsSynced);

    protected override async Task OnInitializedAsync()
    {
        _features = await EditorService.GetFeaturesAsync();
        _formDefinitions = await EditorService.GetFormDefinitionsAsync();
    }

    private async Task HandleMapReady(MapReadyMessage message)
    {
        await RenderFeaturesOnMap();
    }

    private async Task RenderFeaturesOnMap()
    {
        if (_map == null) return;
        // Render existing features
        foreach (var feature in _features)
        {
            await _map.AddFeatureAsync(feature);
        }
    }

    private void HandleFeatureDrawn(FeatureDrawnMessage message)
    {
        _selectedFeature = new Feature
        {
            Geometry = new FeatureGeometry
            {
                Type = message.GeometryType,
                Coordinates = message.Coordinates
            },
            CreatedBy = "Current User"
        };
    }

    private void HandleFeatureClicked(FeatureClickedMessage message)
    {
        _selectedFeature = _features.FirstOrDefault(f => f.Id == message.FeatureId);
        if (_selectedFeature != null)
        {
            _formData = new Dictionary<string, object>(_selectedFeature.Properties.CustomFields);
        }
    }

    private void HandleFeatureModified(FeatureModifiedMessage message)
    {
        if (_selectedFeature != null)
        {
            _selectedFeature.Geometry.Coordinates = message.Coordinates;
            _selectedFeature.ModifiedDate = DateTime.Now;
        }
    }

    private async Task SaveFeature()
    {
        if (_selectedFeature == null) return;

        // Populate properties from form
        _selectedFeature.Properties.CustomFields = _formData;
        _selectedFeature.Properties.Category = _selectedFormDefinition?.Category ?? "";

        // Validate
        var validation = await EditorService.ValidateFeatureAsync(_selectedFeature);
        _validationErrors = validation.Errors;

        if (validation.IsValid)
        {
            await EditorService.SaveFeatureAsync(_selectedFeature);
            if (!_features.Contains(_selectedFeature))
            {
                _features.Add(_selectedFeature);
            }
            Snackbar.Add("Feature saved successfully", Severity.Success);
            ClearForm();
        }
    }

    private async Task DeleteSelectedFeature()
    {
        if (_selectedFeature != null)
        {
            await EditorService.DeleteFeatureAsync(_selectedFeature.Id);
            _features.Remove(_selectedFeature);
            Snackbar.Add("Feature deleted", Severity.Info);
            ClearForm();
        }
    }

    private void SelectFeature(Feature feature)
    {
        _selectedFeature = feature;
        _formData = new Dictionary<string, object>(feature.Properties.CustomFields);
    }

    private void ClearForm()
    {
        _selectedFeature = null;
        _formData.Clear();
        _validationErrors.Clear();
    }

    private async Task SyncData()
    {
        var unsyncedFeatures = _features.Where(f => !f.IsSynced).ToList();
        if (unsyncedFeatures.Any())
        {
            var success = await EditorService.SyncFeaturesAsync(unsyncedFeatures);
            if (success)
            {
                Snackbar.Add($"Synced {unsyncedFeatures.Count} features", Severity.Success);
            }
        }
        else
        {
            Snackbar.Add("No features to sync", Severity.Info);
        }
    }
}
```

---

## Key Features Implemented

✅ **Drawing tools** - Point, line, polygon creation
✅ **Attribute forms** - Dynamic form generation
✅ **Validation** - Required fields and business rules
✅ **Offline support** - Track sync status
✅ **Data synchronization** - Batch sync pending features
✅ **Edit workflows** - Modify existing features

---

## Next Steps

- 📖 [Tutorial 06: Advanced Styling](Tutorial_06_AdvancedStyling.md)
- 📖 [State Management Guide](../guides/StateManagement.md)

---

**Congratulations!** You've built a field data collection app!

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*

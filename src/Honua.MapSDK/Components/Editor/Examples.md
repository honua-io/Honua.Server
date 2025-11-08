# HonuaEditor Examples

Comprehensive examples demonstrating various use cases and scenarios for the HonuaEditor component.

## Table of Contents

1. [Basic Editing](#1-basic-editing)
2. [Custom Attribute Form with Validation](#2-custom-attribute-form-with-validation)
3. [Edit Session with Save/Cancel](#3-edit-session-with-savecancel)
4. [Undo/Redo Demonstration](#4-undoredo-demonstration)
5. [Backend Integration with API](#5-backend-integration-with-api)
6. [Bulk Editing Multiple Features](#6-bulk-editing-multiple-features)
7. [Read-Only Mode with Permissions](#7-read-only-mode-with-permissions)
8. [Real-Time Collaboration](#8-real-time-collaboration)

---

## 1. Basic Editing

Simple editing with create, update, and delete operations.

```razor
@page "/examples/basic-editor"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Editor
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Basic Feature Editing</MudText>

    <MudPaper Elevation="0" Class="pa-4">
        <div style="position: relative; height: 600px;">
            <HonuaMap
                Id="basic-map"
                Center="@(new[] { -122.4194, 37.7749 })"
                Zoom="13"
                Style="mapbox://styles/mapbox/streets-v12">

                <HonuaEditor
                    SyncWith="basic-map"
                    EditableLayers="@_editableLayers"
                    AllowCreate="true"
                    AllowUpdate="true"
                    AllowDelete="true"
                    Position="top-right"
                    OnFeatureCreated="@OnFeatureCreated"
                    OnFeatureUpdated="@OnFeatureUpdated"
                    OnFeatureDeleted="@OnFeatureDeleted" />
            </HonuaMap>
        </div>
    </MudPaper>

    @if (_lastAction != null)
    {
        <MudAlert Severity="Severity.Info" Class="mt-4">
            @_lastAction
        </MudAlert>
    }
</MudContainer>

@code {
    private List<string> _editableLayers = new() { "buildings", "roads" };
    private string? _lastAction;

    private void OnFeatureCreated(Feature feature)
    {
        _lastAction = $"Created {feature.GeometryType} feature with ID: {feature.Id}";
        StateHasChanged();
    }

    private void OnFeatureUpdated(Feature feature)
    {
        _lastAction = $"Updated {feature.GeometryType} feature: {feature.Id}";
        StateHasChanged();
    }

    private void OnFeatureDeleted(string featureId)
    {
        _lastAction = $"Deleted feature: {featureId}";
        StateHasChanged();
    }
}
```

---

## 2. Custom Attribute Form with Validation

Comprehensive validation rules for feature attributes.

```razor
@page "/examples/validated-editor"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Editor
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Feature Editing with Validation</MudText>

    <MudGrid>
        <MudItem xs="12" md="9">
            <MudPaper Elevation="0" Class="pa-4">
                <div style="position: relative; height: 600px;">
                    <HonuaMap
                        Id="validated-map"
                        Center="@(new[] { -73.9857, 40.7484 })"
                        Zoom="13">

                        <HonuaEditor
                            SyncWith="validated-map"
                            EditableLayers="@_layers"
                            ValidationRules="@_validationRules"
                            ShowAttributeForm="true"
                            Position="top-right"
                            OnEditError="@OnEditError" />
                    </HonuaMap>
                </div>
            </MudPaper>
        </MudItem>

        <MudItem xs="12" md="3">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6" Class="mb-3">Validation Rules</MudText>

                <MudText Typo="Typo.body2" Class="mb-2">
                    <strong>Building Name:</strong> Required, 3-100 chars
                </MudText>
                <MudText Typo="Typo.body2" Class="mb-2">
                    <strong>Height:</strong> 0-1000 meters
                </MudText>
                <MudText Typo="Typo.body2" Class="mb-2">
                    <strong>Type:</strong> Must select from list
                </MudText>
                <MudText Typo="Typo.body2" Class="mb-2">
                    <strong>Floors:</strong> 1-200 floors
                </MudText>
                <MudText Typo="Typo.body2" Class="mb-2">
                    <strong>Address:</strong> Optional
                </MudText>
                <MudText Typo="Typo.body2" Class="mb-2">
                    <strong>Website:</strong> Valid URL format
                </MudText>
            </MudPaper>

            @if (_errorMessages.Any())
            {
                <MudPaper Class="pa-4 mt-4">
                    <MudText Typo="Typo.h6" Color="Color.Error" Class="mb-2">
                        Validation Errors
                    </MudText>
                    @foreach (var error in _errorMessages)
                    {
                        <MudText Typo="Typo.body2" Color="Color.Error">
                            • @error
                        </MudText>
                    }
                </MudPaper>
            }
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private List<string> _layers = new() { "buildings" };
    private List<string> _errorMessages = new();

    private Dictionary<string, List<ValidationRule>> _validationRules = new()
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
                MaxValue = 1000,
                ErrorMessage = "Height must be between 0 and 1000 meters"
            },
            new ValidationRule
            {
                FieldName = "floors",
                DisplayName = "Number of Floors",
                Type = ValidationType.Integer,
                IsRequired = true,
                MinValue = 1,
                MaxValue = 200,
                ErrorMessage = "Floors must be between 1 and 200"
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
                    new() { Code = "industrial", Name = "Industrial", SortOrder = 3 },
                    new() { Code = "mixed_use", Name = "Mixed Use", SortOrder = 4 },
                    new() { Code = "institutional", Name = "Institutional", SortOrder = 5 }
                }
            },
            new ValidationRule
            {
                FieldName = "address",
                DisplayName = "Street Address",
                Type = ValidationType.String,
                IsRequired = false,
                MaxLength = 200
            },
            new ValidationRule
            {
                FieldName = "website",
                DisplayName = "Website",
                Type = ValidationType.Url,
                IsRequired = false,
                ErrorMessage = "Must be a valid URL (e.g., https://example.com)"
            },
            new ValidationRule
            {
                FieldName = "constructionDate",
                DisplayName = "Construction Date",
                Type = ValidationType.Date,
                IsRequired = false
            },
            new ValidationRule
            {
                FieldName = "occupied",
                DisplayName = "Currently Occupied",
                Type = ValidationType.Boolean,
                IsRequired = false
            }
        }
    };

    private void OnEditError(string errorMessage)
    {
        _errorMessages.Add($"{DateTime.Now:HH:mm:ss} - {errorMessage}");
        if (_errorMessages.Count > 5)
        {
            _errorMessages.RemoveAt(0);
        }
        StateHasChanged();
    }
}
```

---

## 3. Edit Session with Save/Cancel

Track editing sessions with statistics and explicit save/cancel.

```razor
@page "/examples/session-editor"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Editor
@using Honua.MapSDK.Core
@using Honua.MapSDK.Core.Messages
@using Honua.MapSDK.Models
@inject ComponentBus Bus

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Edit Session Management</MudText>

    <MudGrid>
        <MudItem xs="12" md="9">
            <div style="position: relative; height: 600px;">
                <HonuaMap Id="session-map" Center="@(new[] { -118.2437, 34.0522 })" Zoom="12">
                    <HonuaEditor
                        SyncWith="session-map"
                        EditableLayers="@_layers"
                        ApiEndpoint="/api/features"
                        AutoSave="false"
                        Position="top-right" />
                </HonuaMap>
            </div>
        </MudItem>

        <MudItem xs="12" md="3">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6" Class="mb-3">Session Info</MudText>

                @if (_sessionInfo != null)
                {
                    <MudText Typo="Typo.body2" Class="mb-2">
                        <strong>Session ID:</strong><br/>
                        <code style="font-size: 0.7rem;">@_sessionInfo.SessionId</code>
                    </MudText>

                    <MudDivider Class="my-2" />

                    <MudText Typo="Typo.body2" Class="mb-2">
                        <strong>Status:</strong>
                        @(_sessionInfo.IsDirty ?
                            $"{_sessionInfo.UnsavedChanges} unsaved" :
                            "All saved")
                    </MudText>

                    <MudText Typo="Typo.body2" Class="mb-2">
                        <strong>Can Undo:</strong> @(_sessionInfo.CanUndo ? "Yes" : "No")
                    </MudText>

                    <MudText Typo="Typo.body2" Class="mb-2">
                        <strong>Can Redo:</strong> @(_sessionInfo.CanRedo ? "Yes" : "No")
                    </MudText>
                }
                else
                {
                    <MudText Typo="Typo.body2" Color="Color.Secondary">
                        No active session
                    </MudText>
                }
            </MudPaper>

            <MudPaper Class="pa-4 mt-4">
                <MudText Typo="Typo.h6" Class="mb-3">Activity Log</MudText>

                <MudList Dense="true">
                    @foreach (var log in _activityLog.TakeLast(10).Reverse())
                    {
                        <MudListItem>
                            <MudText Typo="Typo.caption">
                                @log.Time.ToString("HH:mm:ss") - @log.Message
                            </MudText>
                        </MudListItem>
                    }
                </MudList>
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private List<string> _layers = new() { "parcels" };
    private EditSessionInfo? _sessionInfo;
    private List<ActivityLogEntry> _activityLog = new();

    protected override void OnInitialized()
    {
        // Subscribe to session events
        Bus.Subscribe<EditSessionStartedMessage>(args =>
        {
            _sessionInfo = new EditSessionInfo
            {
                SessionId = args.Message.SessionId,
                IsDirty = false,
                UnsavedChanges = 0,
                CanUndo = false,
                CanRedo = false
            };

            AddLog("Session started");
            InvokeAsync(StateHasChanged);
        });

        Bus.Subscribe<EditSessionStateChangedMessage>(args =>
        {
            if (_sessionInfo != null)
            {
                _sessionInfo.IsDirty = args.Message.IsDirty;
                _sessionInfo.UnsavedChanges = args.Message.UnsavedChanges;
                _sessionInfo.CanUndo = args.Message.CanUndo;
                _sessionInfo.CanRedo = args.Message.CanRedo;

                AddLog($"State changed: {args.Message.UnsavedChanges} unsaved");
                InvokeAsync(StateHasChanged);
            }
        });

        Bus.Subscribe<EditSessionEndedMessage>(args =>
        {
            AddLog($"Session ended: {args.Message.OperationCount} operations, " +
                   $"{(args.Message.ChangesSaved ? "saved" : "discarded")}");

            _sessionInfo = null;
            InvokeAsync(StateHasChanged);
        });

        Bus.Subscribe<FeatureCreatedMessage>(args =>
        {
            AddLog($"Created {args.Message.GeometryType}");
            InvokeAsync(StateHasChanged);
        });

        Bus.Subscribe<FeatureUpdatedMessage>(args =>
        {
            AddLog($"Updated {args.Message.UpdateType}");
            InvokeAsync(StateHasChanged);
        });

        Bus.Subscribe<FeatureDeletedMessage>(args =>
        {
            AddLog("Deleted feature");
            InvokeAsync(StateHasChanged);
        });
    }

    private void AddLog(string message)
    {
        _activityLog.Add(new ActivityLogEntry
        {
            Time = DateTime.Now,
            Message = message
        });

        if (_activityLog.Count > 50)
        {
            _activityLog.RemoveAt(0);
        }
    }

    private class EditSessionInfo
    {
        public string SessionId { get; set; } = "";
        public bool IsDirty { get; set; }
        public int UnsavedChanges { get; set; }
        public bool CanUndo { get; set; }
        public bool CanRedo { get; set; }
    }

    private class ActivityLogEntry
    {
        public DateTime Time { get; set; }
        public string Message { get; set; } = "";
    }
}
```

---

## 4. Undo/Redo Demonstration

Showcase unlimited undo/redo functionality with keyboard shortcuts.

```razor
@page "/examples/undo-redo"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Editor
@using Honua.MapSDK.Models
@using Honua.MapSDK.Services.Editing
@inject FeatureEditService EditService

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Undo/Redo Operations</MudText>

    <MudAlert Severity="Severity.Info" Class="mb-4">
        <MudText>
            <strong>Keyboard Shortcuts:</strong><br/>
            • Ctrl+Z (Windows) or Cmd+Z (Mac) - Undo<br/>
            • Ctrl+Y (Windows) or Cmd+Shift+Z (Mac) - Redo
        </MudText>
    </MudAlert>

    <MudGrid>
        <MudItem xs="12" md="8">
            <div style="position: relative; height: 600px;">
                <HonuaMap Id="undo-map" Center="@(new[] { -87.6298, 41.8781 })" Zoom="12">
                    <HonuaEditor
                        @ref="_editor"
                        Id="undo-editor"
                        SyncWith="undo-map"
                        EditableLayers="@_layers"
                        Position="top-right" />
                </HonuaMap>
            </div>
        </MudItem>

        <MudItem xs="12" md="4">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6" Class="mb-3">Operation History</MudText>

                <MudButton
                    Variant="Variant.Outlined"
                    Color="Color.Primary"
                    StartIcon="@Icons.Material.Filled.Undo"
                    OnClick="@ManualUndo"
                    Disabled="@(!CanUndo)"
                    FullWidth="true"
                    Class="mb-2">
                    Undo (Ctrl+Z)
                </MudButton>

                <MudButton
                    Variant="Variant.Outlined"
                    Color="Color.Primary"
                    StartIcon="@Icons.Material.Filled.Redo"
                    OnClick="@ManualRedo"
                    Disabled="@(!CanRedo)"
                    FullWidth="true"
                    Class="mb-4">
                    Redo (Ctrl+Y)
                </MudButton>

                <MudDivider Class="my-3" />

                @if (_currentSession != null)
                {
                    <MudText Typo="Typo.body2" Class="mb-2">
                        <strong>Total Operations:</strong>
                        @_currentSession.Operations.Count
                    </MudText>

                    <MudText Typo="Typo.body2" Class="mb-2">
                        <strong>Current Position:</strong>
                        @(_currentSession.CurrentIndex + 1)
                    </MudText>

                    <MudDivider Class="my-2" />

                    <MudText Typo="Typo.subtitle2" Class="mb-2">
                        Recent Operations:
                    </MudText>

                    <MudList Dense="true">
                        @foreach (var op in _currentSession.Operations.TakeLast(10).Reverse())
                        {
                            var isCurrent = _currentSession.Operations.IndexOf(op) == _currentSession.CurrentIndex;
                            <MudListItem
                                Icon="@GetOperationIcon(op.Type)"
                                IconColor="@(isCurrent ? Color.Primary : Color.Default)">
                                <MudText Typo="Typo.caption">
                                    @op.Description
                                    @if (isCurrent)
                                    {
                                        <MudChip Size="Size.Small" Color="Color.Primary">Current</MudChip>
                                    }
                                </MudText>
                            </MudListItem>
                        }
                    </MudList>
                }
            </MudPaper>

            <MudPaper Class="pa-4 mt-4">
                <MudText Typo="Typo.h6" Class="mb-2">Try These Actions:</MudText>
                <MudList Dense="true">
                    <MudListItem Icon="@Icons.Material.Filled.Circle">
                        Draw multiple features
                    </MudListItem>
                    <MudListItem Icon="@Icons.Material.Filled.Circle">
                        Edit their geometries
                    </MudListItem>
                    <MudListItem Icon="@Icons.Material.Filled.Circle">
                        Delete some features
                    </MudListItem>
                    <MudListItem Icon="@Icons.Material.Filled.Circle">
                        Use Undo to revert
                    </MudListItem>
                    <MudListItem Icon="@Icons.Material.Filled.Circle">
                        Use Redo to reapply
                    </MudListItem>
                </MudList>
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private HonuaEditor? _editor;
    private List<string> _layers = new() { "features" };
    private EditSession? _currentSession;

    private bool CanUndo => _currentSession?.CanUndo ?? false;
    private bool CanRedo => _currentSession?.CanRedo ?? false;

    protected override void OnInitialized()
    {
        // Start a session for tracking
        _currentSession = EditService.StartSession("demo-session");

        // Setup keyboard shortcuts
        // In a real app, you'd use JSInterop for keyboard handling
    }

    private async Task ManualUndo()
    {
        if (_currentSession != null)
        {
            EditService.Undo(_currentSession.Id);
            StateHasChanged();
        }
    }

    private async Task ManualRedo()
    {
        if (_currentSession != null)
        {
            EditService.Redo(_currentSession.Id);
            StateHasChanged();
        }
    }

    private string GetOperationIcon(EditOperationType type)
    {
        return type switch
        {
            EditOperationType.Create => Icons.Material.Filled.Add,
            EditOperationType.Update => Icons.Material.Filled.Edit,
            EditOperationType.Delete => Icons.Material.Filled.Delete,
            EditOperationType.Move => Icons.Material.Filled.OpenWith,
            EditOperationType.Reshape => Icons.Material.Filled.Edit,
            _ => Icons.Material.Filled.Circle
        };
    }
}
```

---

## 5. Backend Integration with API

Full integration with a REST API backend.

```razor
@page "/examples/api-editor"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Editor
@using Honua.MapSDK.Models
@using System.Net.Http.Json

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Backend API Integration</MudText>

    <MudGrid>
        <MudItem xs="12" md="8">
            <div style="position: relative; height: 600px;">
                <HonuaMap Id="api-map" Center="@(new[] { -71.0589, 42.3601 })" Zoom="13">
                    <HonuaEditor
                        SyncWith="api-map"
                        EditableLayers="@_layers"
                        ApiEndpoint="@_apiEndpoint"
                        AutoSave="@_autoSave"
                        Position="top-right"
                        OnFeatureCreated="@OnFeatureCreated"
                        OnFeatureUpdated="@OnFeatureUpdated"
                        OnFeatureDeleted="@OnFeatureDeleted"
                        OnEditError="@OnEditError" />
                </HonuaMap>
            </div>
        </MudItem>

        <MudItem xs="12" md="4">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6" Class="mb-3">API Configuration</MudText>

                <MudTextField
                    @bind-Value="_apiEndpoint"
                    Label="API Endpoint"
                    Variant="Variant.Outlined"
                    FullWidth="true"
                    Class="mb-3" />

                <MudSwitch
                    @bind-Checked="_autoSave"
                    Label="Auto-save changes"
                    Color="Color.Primary"
                    Class="mb-3" />

                <MudDivider Class="my-3" />

                <MudText Typo="Typo.subtitle2" Class="mb-2">API Statistics</MudText>

                <MudText Typo="Typo.body2" Class="mb-1">
                    <strong>Features Created:</strong> @_stats.Created
                </MudText>
                <MudText Typo="Typo.body2" Class="mb-1">
                    <strong>Features Updated:</strong> @_stats.Updated
                </MudText>
                <MudText Typo="Typo.body2" Class="mb-1">
                    <strong>Features Deleted:</strong> @_stats.Deleted
                </MudText>
                <MudText Typo="Typo.body2" Class="mb-1">
                    <strong>API Errors:</strong> @_stats.Errors
                </MudText>
            </MudPaper>

            <MudPaper Class="pa-4 mt-4">
                <MudText Typo="Typo.h6" Class="mb-3">API Response Log</MudText>

                <MudList Dense="true">
                    @foreach (var log in _apiLog.TakeLast(8).Reverse())
                    {
                        <MudListItem>
                            <MudText Typo="Typo.caption" Color="@log.IsError ? Color.Error : Color.Success">
                                @log.Timestamp.ToString("HH:mm:ss") - @log.Message
                            </MudText>
                        </MudListItem>
                    }
                </MudList>
            </MudPaper>

            <MudPaper Class="pa-4 mt-4">
                <MudText Typo="Typo.h6" Class="mb-2">Expected API Endpoints:</MudText>
                <MudText Typo="Typo.caption">
                    <code>POST /api/features</code> - Create<br/>
                    <code>PUT /api/features/:id</code> - Update<br/>
                    <code>DELETE /api/features/:id</code> - Delete<br/>
                    <code>POST /api/features/batch</code> - Batch
                </MudText>
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private List<string> _layers = new() { "pois" };
    private string _apiEndpoint = "/api/features";
    private bool _autoSave = false;
    private ApiStats _stats = new();
    private List<ApiLogEntry> _apiLog = new();

    private void OnFeatureCreated(Feature feature)
    {
        _stats.Created++;
        AddApiLog($"Created feature {feature.Id}", false);
        StateHasChanged();
    }

    private void OnFeatureUpdated(Feature feature)
    {
        _stats.Updated++;
        AddApiLog($"Updated feature {feature.Id}", false);
        StateHasChanged();
    }

    private void OnFeatureDeleted(string featureId)
    {
        _stats.Deleted++;
        AddApiLog($"Deleted feature {featureId}", false);
        StateHasChanged();
    }

    private void OnEditError(string error)
    {
        _stats.Errors++;
        AddApiLog($"Error: {error}", true);
        StateHasChanged();
    }

    private void AddApiLog(string message, bool isError)
    {
        _apiLog.Add(new ApiLogEntry
        {
            Timestamp = DateTime.Now,
            Message = message,
            IsError = isError
        });

        if (_apiLog.Count > 50)
        {
            _apiLog.RemoveAt(0);
        }
    }

    private class ApiStats
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Deleted { get; set; }
        public int Errors { get; set; }
    }

    private class ApiLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = "";
        public bool IsError { get; set; }
    }
}
```

---

## 6. Bulk Editing Multiple Features

Edit multiple features simultaneously with batch operations.

```razor
@page "/examples/bulk-editor"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Editor
@using Honua.MapSDK.Models
@using Honua.MapSDK.Core
@using Honua.MapSDK.Core.Messages
@inject ComponentBus Bus

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Bulk Feature Editing</MudText>

    <MudGrid>
        <MudItem xs="12" md="8">
            <div style="position: relative; height: 600px;">
                <HonuaMap Id="bulk-map" Center="@(new[] { 2.3522, 48.8566 })" Zoom="12">
                    <HonuaEditor
                        SyncWith="bulk-map"
                        EditableLayers="@_layers"
                        Position="top-right" />
                </HonuaMap>
            </div>
        </MudItem>

        <MudItem xs="12" md="4">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6" Class="mb-3">Bulk Operations</MudText>

                <MudNumericField
                    @bind-Value="_selectedCount"
                    Label="Features Selected"
                    Variant="Variant.Outlined"
                    ReadOnly="true"
                    FullWidth="true"
                    Class="mb-3" />

                <MudButton
                    Variant="Variant.Filled"
                    Color="Color.Primary"
                    FullWidth="true"
                    StartIcon="@Icons.Material.Filled.Edit"
                    OnClick="@BulkUpdateAttributes"
                    Disabled="@(_selectedCount == 0)"
                    Class="mb-2">
                    Update All Attributes
                </MudButton>

                <MudButton
                    Variant="Variant.Filled"
                    Color="Color.Secondary"
                    FullWidth="true"
                    StartIcon="@Icons.Material.Filled.ContentCopy"
                    OnClick="@BulkCopy"
                    Disabled="@(_selectedCount == 0)"
                    Class="mb-2">
                    Duplicate Selected
                </MudButton>

                <MudButton
                    Variant="Variant.Filled"
                    Color="Color.Error"
                    FullWidth="true"
                    StartIcon="@Icons.Material.Filled.Delete"
                    OnClick="@BulkDelete"
                    Disabled="@(_selectedCount == 0)"
                    Class="mb-2">
                    Delete All Selected
                </MudButton>
            </MudPaper>

            <MudPaper Class="pa-4 mt-4">
                <MudText Typo="Typo.h6" Class="mb-3">Bulk Attribute Update</MudText>

                <MudTextField
                    @bind-Value="_bulkField"
                    Label="Field Name"
                    Variant="Variant.Outlined"
                    FullWidth="true"
                    Class="mb-2" />

                <MudTextField
                    @bind-Value="_bulkValue"
                    Label="New Value"
                    Variant="Variant.Outlined"
                    FullWidth="true"
                    Class="mb-3" />

                <MudButton
                    Variant="Variant.Outlined"
                    Color="Color.Primary"
                    FullWidth="true"
                    OnClick="@ApplyBulkUpdate"
                    Disabled="@(string.IsNullOrEmpty(_bulkField) || _selectedCount == 0)">
                    Apply to Selected
                </MudButton>
            </MudPaper>

            <MudPaper Class="pa-4 mt-4">
                <MudText Typo="Typo.h6" Class="mb-2">Recent Bulk Operations</MudText>

                <MudList Dense="true">
                    @foreach (var op in _bulkOperations.TakeLast(5).Reverse())
                    {
                        <MudListItem>
                            <MudText Typo="Typo.caption">
                                @op.Time.ToString("HH:mm:ss") - @op.Description
                            </MudText>
                        </MudListItem>
                    }
                </MudList>
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private List<string> _layers = new() { "zones" };
    private int _selectedCount = 0;
    private string _bulkField = "category";
    private string _bulkValue = "";
    private List<BulkOperation> _bulkOperations = new();
    private List<Feature> _selectedFeatures = new();

    protected override void OnInitialized()
    {
        // Subscribe to feature selection
        Bus.Subscribe<EditorFeatureSelectedMessage>(args =>
        {
            // In real implementation, track selected features
            _selectedCount++;
            InvokeAsync(StateHasChanged);
        });
    }

    private void BulkUpdateAttributes()
    {
        AddBulkOperation($"Updated attributes for {_selectedCount} features");
        // Implement bulk update logic
    }

    private void BulkCopy()
    {
        AddBulkOperation($"Duplicated {_selectedCount} features");
        // Implement bulk copy logic
    }

    private void BulkDelete()
    {
        AddBulkOperation($"Deleted {_selectedCount} features");
        _selectedCount = 0;
        // Implement bulk delete logic
    }

    private void ApplyBulkUpdate()
    {
        AddBulkOperation($"Set {_bulkField}={_bulkValue} for {_selectedCount} features");
        // Implement bulk field update logic
    }

    private void AddBulkOperation(string description)
    {
        _bulkOperations.Add(new BulkOperation
        {
            Time = DateTime.Now,
            Description = description
        });
    }

    private class BulkOperation
    {
        public DateTime Time { get; set; }
        public string Description { get; set; } = "";
    }
}
```

---

## 7. Read-Only Mode with Permissions

Control editing permissions based on user roles.

```razor
@page "/examples/permission-editor"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Editor

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Permission-Based Editing</MudText>

    <MudPaper Class="pa-4 mb-4">
        <MudText Typo="Typo.h6" Class="mb-3">Simulate User Role</MudText>

        <MudRadioGroup @bind-SelectedOption="_userRole" T="string">
            <MudRadio Option="@("viewer")" Color="Color.Default">
                Viewer (Read-only)
            </MudRadio>
            <MudRadio Option="@("editor")" Color="Color.Primary">
                Editor (Create & Update)
            </MudRadio>
            <MudRadio Option="@("admin")" Color="Color.Success">
                Admin (Full Access)
            </MudRadio>
        </MudRadioGroup>
    </MudPaper>

    <div style="position: relative; height: 600px;">
        <HonuaMap Id="permission-map" Center="@(new[] { 13.4050, 52.5200 })" Zoom="12">
            <HonuaEditor
                SyncWith="permission-map"
                EditableLayers="@_layers"
                AllowCreate="@_permissions.CanCreate"
                AllowUpdate="@_permissions.CanUpdate"
                AllowDelete="@_permissions.CanDelete"
                Position="top-right" />
        </HonuaMap>
    </div>

    <MudPaper Class="pa-4 mt-4">
        <MudText Typo="Typo.h6" Class="mb-2">Current Permissions</MudText>
        <MudChip Color="@(_permissions.CanCreate ? Color.Success : Color.Default)">
            Create: @(_permissions.CanCreate ? "Allowed" : "Denied")
        </MudChip>
        <MudChip Color="@(_permissions.CanUpdate ? Color.Success : Color.Default)">
            Update: @(_permissions.CanUpdate ? "Allowed" : "Denied")
        </MudChip>
        <MudChip Color="@(_permissions.CanDelete ? Color.Success : Color.Default)">
            Delete: @(_permissions.CanDelete ? "Allowed" : "Denied")
        </MudChip>
    </MudPaper>
</MudContainer>

@code {
    private List<string> _layers = new() { "protected-areas" };
    private string _userRole = "viewer";
    private UserPermissions _permissions = new();

    protected override void OnParametersSet()
    {
        _permissions = _userRole switch
        {
            "viewer" => new UserPermissions { CanCreate = false, CanUpdate = false, CanDelete = false },
            "editor" => new UserPermissions { CanCreate = true, CanUpdate = true, CanDelete = false },
            "admin" => new UserPermissions { CanCreate = true, CanUpdate = true, CanDelete = true },
            _ => new UserPermissions()
        };
    }

    private class UserPermissions
    {
        public bool CanCreate { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDelete { get; set; }
    }
}
```

---

## 8. Real-Time Collaboration

Multiple users editing simultaneously with conflict detection.

```razor
@page "/examples/collab-editor"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Editor
@using Honua.MapSDK.Models
@using Honua.MapSDK.Services.Editing
@using Microsoft.AspNetCore.SignalR.Client
@inject NavigationManager Navigation
@inject FeatureEditService EditService

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Real-Time Collaborative Editing</MudText>

    <MudAlert Severity="Severity.Info" Class="mb-4">
        Multiple users can edit simultaneously. Conflicts are detected and highlighted.
    </MudAlert>

    <MudGrid>
        <MudItem xs="12" md="9">
            <div style="position: relative; height: 600px;">
                <HonuaMap Id="collab-map" Center="@(new[] { 139.6917, 35.6895 })" Zoom="12">
                    <HonuaEditor
                        @ref="_editor"
                        SyncWith="collab-map"
                        EditableLayers="@_layers"
                        ApiEndpoint="/api/features"
                        Position="top-right"
                        OnFeatureCreated="@OnFeatureModified"
                        OnFeatureUpdated="@OnFeatureModified"
                        OnFeatureDeleted="@(id => OnFeatureDeleted(id))" />
                </HonuaMap>
            </div>
        </MudItem>

        <MudItem xs="12" md="3">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6" Class="mb-3">
                    Active Users (@_activeUsers.Count)
                </MudText>

                <MudList Dense="true">
                    @foreach (var user in _activeUsers)
                    {
                        <MudListItem Icon="@Icons.Material.Filled.Person" IconColor="Color.Success">
                            @user.Name
                            @if (user.IsEditing)
                            {
                                <MudChip Size="Size.Small" Color="Color.Warning">Editing</MudChip>
                            }
                        </MudListItem>
                    }
                </MudList>
            </MudPaper>

            @if (_conflicts.Any())
            {
                <MudPaper Class="pa-4 mt-4">
                    <MudText Typo="Typo.h6" Color="Color.Error" Class="mb-3">
                        Conflicts Detected (@_conflicts.Count)
                    </MudText>

                    @foreach (var conflict in _conflicts)
                    {
                        <MudCard Class="mb-2">
                            <MudCardContent>
                                <MudText Typo="Typo.body2">
                                    <strong>Feature:</strong> @conflict.FeatureId
                                </MudText>
                                <MudText Typo="Typo.caption">
                                    Local: v@conflict.LocalVersion<br/>
                                    Server: v@conflict.ServerVersion
                                </MudText>
                            </MudCardContent>
                            <MudCardActions>
                                <MudButton Size="Size.Small" OnClick="@(() => ResolveConflict(conflict, true))">
                                    Keep Mine
                                </MudButton>
                                <MudButton Size="Size.Small" OnClick="@(() => ResolveConflict(conflict, false))">
                                    Use Server
                                </MudButton>
                            </MudCardActions>
                        </MudCard>
                    }
                </MudPaper>
            }
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private HonuaEditor? _editor;
    private List<string> _layers = new() { "shared-layer" };
    private List<ActiveUser> _activeUsers = new();
    private List<EditConflict> _conflicts = new();
    private HubConnection? _hubConnection;
    private string _sessionId = "";

    protected override async Task OnInitializedAsync()
    {
        // Setup SignalR connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/editorhub"))
            .Build();

        _hubConnection.On<string, string>("FeatureModified", async (featureId, userId) =>
        {
            // Another user modified a feature
            if (_sessionId != null)
            {
                var conflicts = await EditService.DetectConflictsAsync(_sessionId, "/api/features");
                _conflicts = conflicts;
                await InvokeAsync(StateHasChanged);
            }
        });

        _hubConnection.On<string>("UserJoined", (userName) =>
        {
            _activeUsers.Add(new ActiveUser { Name = userName, IsEditing = false });
            InvokeAsync(StateHasChanged);
        });

        _hubConnection.On<string>("UserLeft", (userName) =>
        {
            _activeUsers.RemoveAll(u => u.Name == userName);
            InvokeAsync(StateHasChanged);
        });

        await _hubConnection.StartAsync();
    }

    private async Task OnFeatureModified(Feature feature)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("NotifyFeatureModified", feature.Id, _sessionId);
        }
    }

    private async Task OnFeatureDeleted(string featureId)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("NotifyFeatureDeleted", featureId, _sessionId);
        }
    }

    private void ResolveConflict(EditConflict conflict, bool keepLocal)
    {
        _conflicts.Remove(conflict);
        StateHasChanged();

        // Implement conflict resolution logic
        // If keepLocal: overwrite server version
        // Else: reload server version
    }

    private class ActiveUser
    {
        public string Name { get; set; } = "";
        public bool IsEditing { get; set; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
```

---

## Server-Side API Example

Example ASP.NET Core API controller for feature editing:

```csharp
[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    private readonly IFeatureRepository _repository;

    public FeaturesController(IFeatureRepository repository)
    {
        _repository = repository;
    }

    [HttpPost]
    public async Task<ActionResult<Feature>> Create([FromBody] Feature feature)
    {
        // Validate
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Assign ID and version
        feature.Id = Guid.NewGuid().ToString();
        feature.Version = 1;
        feature.CreatedAt = DateTime.UtcNow;

        // Save
        await _repository.AddAsync(feature);

        return CreatedAtAction(nameof(GetById), new { id = feature.Id }, feature);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Feature>> Update(string id, [FromBody] Feature feature)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        // Check version for conflict detection
        if (existing.Version != feature.Version)
            return Conflict(new { message = "Version mismatch", serverVersion = existing.Version });

        // Update
        feature.Version++;
        feature.ModifiedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(feature);

        return Ok(feature);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var feature = await _repository.GetByIdAsync(id);
        if (feature == null)
            return NotFound();

        await _repository.DeleteAsync(id);

        return NoContent();
    }

    [HttpPost("batch")]
    public async Task<ActionResult<BatchResult>> Batch([FromBody] BatchRequest request)
    {
        var result = new BatchResult();

        foreach (var op in request.Operations)
        {
            try
            {
                switch (op.Type.ToLower())
                {
                    case "create":
                        await _repository.AddAsync(op.Feature);
                        result.Created++;
                        break;
                    case "update":
                        await _repository.UpdateAsync(op.Feature);
                        result.Updated++;
                        break;
                    case "delete":
                        await _repository.DeleteAsync(op.Feature.Id);
                        result.Deleted++;
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error processing {op.Type}: {ex.Message}");
            }
        }

        return Ok(result);
    }
}
```

---

These examples cover the major use cases for the HonuaEditor component. Mix and match features as needed for your application!

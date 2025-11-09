# Advanced Filtering Integration Guide

## Overview
This guide explains how to integrate the advanced table filtering system into Blazor pages.

## Core Components Created
1. **SearchModels.cs** - Extended with `ColumnFilter`, `AdvancedFilterPreset`, and `FilterOperator` enum
2. **SearchStateService.cs** - Extended with advanced preset management
3. **FilterHelper.cs** - Utility class for applying filters to data
4. **AdvancedTableFilter.razor** - Reusable filter component

## Integration Steps

### 1. ServiceList.razor Integration

#### Add Filter Toggle Button
Add this button to the toolbar (after search field):
```razor
<MudButton StartIcon="@Icons.Material.Filled.FilterList"
           Variant="Variant.Outlined"
           Size="Size.Small"
           OnClick="@(() => _showFilters = !_showFilters)">
    @(_showFilters ? "Hide" : "Show") Filters
    @if (_activeFilters.Any())
    {
        <MudChip Size="Size.Small" Color="Color.Primary" Style="margin-left: 8px;">@_activeFilters.Count</MudChip>
    }
</MudButton>
```

#### Add Filter Panel
Add after the toolbar MudStack:
```razor
@if (_showFilters)
{
    <AdvancedTableFilter AvailableColumns="_serviceColumns"
                        OnFiltersChanged="HandleFiltersChanged"
                        TableType="services" />
}
```

#### Add Private Fields
```csharp
private bool _showFilters = false;
private List<ColumnFilter> _activeFilters = new();

private Dictionary<string, string> _serviceColumns = new()
{
    { "Title", "Service Name" },
    { "ServiceType", "Type" },
    { "FolderId", "Folder" },
    { "LayerCount", "Layer Count" },
    { "Enabled", "Status" }
};

private Dictionary<string, Func<ServiceListItem, object?>> _propertySelectors = new()
{
    { "Title", s => s.Title },
    { "ServiceType", s => s.ServiceType },
    { "FolderId", s => s.FolderId ?? string.Empty },
    { "LayerCount", s => s.LayerCount },
    { "Enabled", s => s.Enabled ? "Running" : "Stopped" }
};
```

#### Update Filtered Data
Replace the `_filteredServices` property:
```csharp
private IEnumerable<ServiceListItem> _filteredServices
{
    get
    {
        var result = _services.AsEnumerable();

        // Apply text search
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            result = result.Where(s =>
                s.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                s.ServiceType.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply advanced filters
        if (_activeFilters.Any())
        {
            result = FilterHelper.ApplyFilters(result, _activeFilters, _propertySelectors);
        }

        return result;
    }
}
```

#### Add Filter Handler
```csharp
private void HandleFiltersChanged(List<ColumnFilter> filters)
{
    _activeFilters = filters;
    StateHasChanged();
}
```

### 2. UserManagement.razor Integration

#### Column Configuration
```csharp
private Dictionary<string, string> _userColumns = new()
{
    { "Username", "Username" },
    { "DisplayName", "Display Name" },
    { "Email", "Email" },
    { "IsEnabled", "Status" },
    { "IsLockedOut", "Locked" },
    { "FailedLoginAttempts", "Failed Attempts" }
};

private Dictionary<string, Func<UserResponse, object?>> _propertySelectors = new()
{
    { "Username", u => u.Username },
    { "DisplayName", u => u.DisplayName ?? string.Empty },
    { "Email", u => u.Email ?? string.Empty },
    { "IsEnabled", u => u.IsEnabled ? "Active" : "Disabled" },
    { "IsLockedOut", u => u.IsLockedOut ? "Locked" : "Unlocked" },
    { "FailedLoginAttempts", u => u.FailedLoginAttempts }
};
```

#### Filtered Users Property
```csharp
private IEnumerable<UserResponse> _filteredUsers
{
    get
    {
        var result = _users.AsEnumerable();

        if (_activeFilters.Any())
        {
            result = FilterHelper.ApplyFilters(result, _activeFilters, _propertySelectors);
        }

        return result;
    }
}
```

Update the MudTable binding from `Items="@_users"` to `Items="@_filteredUsers"`

### 3. WorkflowList.razor Integration

#### Column Configuration
```csharp
private Dictionary<string, string> _workflowColumns = new()
{
    { "Name", "Workflow Name" },
    { "Category", "Category" },
    { "NodeCount", "Nodes" },
    { "UpdatedAt", "Last Updated" }
};

private Dictionary<string, Func<WorkflowDto, object?>> _propertySelectors = new()
{
    { "Name", w => w.Metadata.Name },
    { "Category", w => w.Metadata.Category ?? string.Empty },
    { "NodeCount", w => w.Nodes.Count },
    { "UpdatedAt", w => w.UpdatedAt.ToString("g") }
};
```

#### Filtered Workflows Property
```csharp
private IEnumerable<WorkflowDto> _filteredWorkflows
{
    get
    {
        var result = _workflows?.AsEnumerable() ?? Enumerable.Empty<WorkflowDto>();

        // Apply text search
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            result = result.Where(w =>
                w.Metadata.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                (w.Metadata.Description?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Apply advanced filters
        if (_activeFilters.Any())
        {
            result = FilterHelper.ApplyFilters(result, _activeFilters, _propertySelectors);
        }

        return result;
    }
}
```

## Features

### Supported Operators
- **Equals** - Exact match
- **Not Equals** - Does not match
- **Contains** - String contains text
- **Not Contains** - String does not contain text
- **Starts With** - String starts with text
- **Ends With** - String ends with text
- **Greater Than** - Numeric/Date comparison
- **Less Than** - Numeric/Date comparison
- **Greater Than or Equal** - Numeric/Date comparison
- **Less Than or Equal** - Numeric/Date comparison
- **Between** - Range filter (requires two values)
- **Is Empty** - Null or empty check
- **Is Not Empty** - Not null/empty check

### Filter Presets
Users can save filter combinations as presets:
1. Configure filters
2. Click "Save Preset"
3. Enter preset name and description
4. Preset is saved to localStorage per table type
5. Load presets by clicking on chips
6. Delete presets using the close icon

## Testing

Test the following scenarios:
1. **Single Filter** - Add one filter and verify results
2. **Multiple Filters** - Add multiple filters (AND logic)
3. **Different Operators** - Test various operators
4. **Range Filters** - Test "Between" operator
5. **Empty Filters** - Test "Is Empty" / "Is Not Empty"
6. **Preset Saving** - Save and load presets
7. **Clear Filters** - Clear all filters
8. **Persistence** - Reload page and verify presets are saved

## Notes

- Filters use AND logic (all conditions must match)
- Presets are stored in localStorage per table type
- Text comparisons are case-insensitive
- Numeric and date comparisons are type-aware
- The filter panel can be shown/hidden with a toggle button

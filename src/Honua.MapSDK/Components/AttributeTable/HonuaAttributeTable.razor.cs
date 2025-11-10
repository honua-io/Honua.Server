// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
using Honua.MapSDK.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MudSelectionMode = MudBlazor.SelectionMode;

namespace Honua.MapSDK.Components.AttributeTable;

/// <summary>
/// HonuaAttributeTable - Advanced attribute table for GIS feature management
/// More specialized than HonuaDataGrid with tight map integration
/// </summary>
public partial class HonuaAttributeTable : ComponentBase, IAsyncDisposable
{
    #region Injected Services
    [Inject] protected ComponentBus Bus { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected IDialogService DialogService { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    #endregion

    #region Parameters - Core

    /// <summary>
    /// Map ID to synchronize with
    /// </summary>
    [Parameter]
    public string? SyncWith { get; set; }

    /// <summary>
    /// Layer ID to display features from
    /// </summary>
    [Parameter]
    public string? LayerId { get; set; }

    /// <summary>
    /// Features to display (if not loading from layer)
    /// </summary>
    [Parameter]
    public List<FeatureRecord>? Features { get; set; }

    /// <summary>
    /// Table configuration
    /// </summary>
    [Parameter]
    public TableConfiguration? Configuration { get; set; }

    #endregion

    #region Parameters - Appearance

    /// <summary>
    /// Table title
    /// </summary>
    [Parameter]
    public string Title { get; set; } = "Attribute Table";

    /// <summary>
    /// Additional CSS classes
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>
    /// Inline styles
    /// </summary>
    [Parameter]
    public string Style { get; set; } = "width: 100%; height: 600px;";

    /// <summary>
    /// Show toolbar
    /// </summary>
    [Parameter]
    public bool ShowToolbar { get; set; } = true;

    /// <summary>
    /// Show pagination controls
    /// </summary>
    [Parameter]
    public bool ShowPagination { get; set; } = true;

    /// <summary>
    /// Page size (records per page)
    /// </summary>
    [Parameter]
    public int PageSize { get; set; } = 100;

    #endregion

    #region Parameters - Features

    /// <summary>
    /// Allow inline editing of feature attributes
    /// </summary>
    [Parameter]
    public bool AllowEdit { get; set; } = false;

    /// <summary>
    /// Allow deleting features
    /// </summary>
    [Parameter]
    public bool AllowDelete { get; set; } = false;

    /// <summary>
    /// Allow exporting data
    /// </summary>
    [Parameter]
    public bool AllowExport { get; set; } = true;

    /// <summary>
    /// Selection mode
    /// </summary>
    [Parameter]
    public Models.SelectionMode SelectionMode { get; set; } = Models.SelectionMode.Multiple;

    /// <summary>
    /// Highlight selected features on map
    /// </summary>
    [Parameter]
    public bool HighlightSelected { get; set; } = true;

    #endregion

    #region Parameters - Child Content

    /// <summary>
    /// Custom column definitions
    /// </summary>
    [Parameter]
    public RenderFragment? Columns { get; set; }

    /// <summary>
    /// Custom toolbar content
    /// </summary>
    [Parameter]
    public RenderFragment? ToolbarContent { get; set; }

    #endregion

    #region Parameters - Events

    /// <summary>
    /// Callback when row is selected
    /// </summary>
    [Parameter]
    public EventCallback<FeatureRecord> OnRowSelected { get; set; }

    /// <summary>
    /// Callback when multiple rows are selected
    /// </summary>
    [Parameter]
    public EventCallback<List<FeatureRecord>> OnRowsSelected { get; set; }

    /// <summary>
    /// Callback when rows are updated
    /// </summary>
    [Parameter]
    public EventCallback<List<FeatureRecord>> OnRowsUpdated { get; set; }

    /// <summary>
    /// Callback when row is deleted
    /// </summary>
    [Parameter]
    public EventCallback<string> OnRowDeleted { get; set; }

    #endregion

    #region Private Fields

    private List<FeatureRecord> _allFeatures = new();
    private List<FeatureRecord> _displayedFeatures = new();
    private FeatureRecord? _selectedFeature;
    private HashSet<FeatureRecord> _selectedFeatures = new();
    private List<ColumnConfig> _columns = new();
    private TableConfiguration? _configuration;
    private bool _autoGenerateColumns = true;
    private bool _hasGeometry = false;
    private bool _isLoading = false;
    private bool _isSynced = false;
    private bool _showSelectedOnly = false;
    private string? _errorMessage;
    private string? _searchString;
    private FilterConfig? _activeFilter;
    private List<FilterPreset> _filterPresets = new();

    private Func<FeatureRecord, bool> _quickFilterFunc => feature =>
    {
        if (string.IsNullOrWhiteSpace(_searchString))
            return true;

        // Search across all visible properties
        foreach (var kvp in feature.Properties)
        {
            var value = kvp.Value?.ToString();
            if (value?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }

        return false;
    };

    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        SetupSubscriptions();
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        // Apply configuration
        if (Configuration != null)
        {
            _configuration = Configuration;
            _columns = Configuration.Columns;
            _autoGenerateColumns = _columns.Count == 0;
        }

        // Load features
        if (Features != null)
        {
            _allFeatures = Features;
            _displayedFeatures = _allFeatures;
            _hasGeometry = _allFeatures.Any(f => f.Geometry != null);

            if (_autoGenerateColumns && _allFeatures.Count > 0)
            {
                GenerateColumns();
            }
        }
        else if (!string.IsNullOrEmpty(LayerId))
        {
            // Features will be loaded from layer via ComponentBus
            await RequestFeaturesFromLayer();
        }

        // Enable sync if SyncWith is set
        if (SyncWith != null && !_isSynced)
        {
            _isSynced = true;
        }
    }

    #endregion

    #region ComponentBus Integration

    private void SetupSubscriptions()
    {
        // Listen for feature clicks on map
        Bus.Subscribe<FeatureClickedMessage>(async args =>
        {
            if (_isSynced && args.Message.MapId == SyncWith && args.Message.LayerId == LayerId)
            {
                await HighlightFeatureFromMap(args.Message.FeatureId);
                await InvokeAsync(StateHasChanged);
            }
        });

        // Listen for map extent changes
        Bus.Subscribe<MapExtentChangedMessage>(async args =>
        {
            if (_isSynced && args.Message.MapId == SyncWith)
            {
                // Optionally filter by extent
                await InvokeAsync(StateHasChanged);
            }
        });

        // Listen for data loaded messages
        Bus.Subscribe<DataLoadedMessage>(async args =>
        {
            if (args.Message.Source == LayerId)
            {
                await InvokeAsync(StateHasChanged);
            }
        });

        // Listen for filter applied
        Bus.Subscribe<FilterAppliedMessage>(async args =>
        {
            if (args.Message.AffectedLayers?.Contains(LayerId ?? "") == true)
            {
                await ApplyFilter(args.Message);
                await InvokeAsync(StateHasChanged);
            }
        });

        // Listen for filter cleared
        Bus.Subscribe<FilterClearedMessage>(async args =>
        {
            await ClearFilter(args.Message.FilterId);
            await InvokeAsync(StateHasChanged);
        });

        // Listen for all filters cleared
        Bus.Subscribe<AllFiltersClearedMessage>(async args =>
        {
            await ClearAllFilters();
            await InvokeAsync(StateHasChanged);
        });
    }

    #endregion

    #region Data Loading

    private async Task RequestFeaturesFromLayer()
    {
        if (string.IsNullOrEmpty(LayerId) || string.IsNullOrEmpty(SyncWith))
            return;

        _isLoading = true;
        StateHasChanged();

        try
        {
            // Request data from map component via ComponentBus
            await Bus.PublishAsync(new DataRequestMessage
            {
                ComponentId = $"attribute-table-{Guid.NewGuid():N}",
                MapId = SyncWith
            });
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error requesting features: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    #endregion

    #region Column Generation

    private void GenerateColumns()
    {
        if (_columns.Count > 0 && !_autoGenerateColumns)
            return;

        _columns.Clear();

        if (_allFeatures.Count == 0)
            return;

        var sampleFeature = _allFeatures.First();

        // Add ID column first
        _columns.Add(new ColumnConfig
        {
            FieldName = "Id",
            DisplayName = "Feature ID",
            DataType = ColumnDataType.String,
            Visible = true,
            Editable = false,
            Width = 150,
            Frozen = true
        });

        // Add property columns
        foreach (var prop in sampleFeature.Properties.Keys.OrderBy(k => k))
        {
            var value = sampleFeature.Properties[prop];
            var dataType = InferDataType(value);

            _columns.Add(new ColumnConfig
            {
                FieldName = prop,
                DisplayName = FormatColumnName(prop),
                DataType = dataType,
                Visible = true,
                Editable = AllowEdit,
                Width = GetDefaultWidth(dataType)
            });
        }

        // Add geometry column if present
        if (_hasGeometry)
        {
            _columns.Add(new ColumnConfig
            {
                FieldName = "GeometryType",
                DisplayName = "Geometry",
                DataType = ColumnDataType.Geometry,
                Visible = true,
                Editable = false,
                Width = 120
            });
        }
    }

    private ColumnDataType InferDataType(object? value)
    {
        if (value == null) return ColumnDataType.String;

        return value switch
        {
            bool => ColumnDataType.Boolean,
            int => ColumnDataType.Integer,
            long => ColumnDataType.Integer,
            float => ColumnDataType.Decimal,
            double => ColumnDataType.Decimal,
            decimal => ColumnDataType.Decimal,
            DateTime => ColumnDataType.DateTime,
            DateTimeOffset => ColumnDataType.DateTime,
            _ => ColumnDataType.String
        };
    }

    private string FormatColumnName(string propertyName)
    {
        // Remove underscores, add spaces before capitals
        var result = new StringBuilder();
        for (int i = 0; i < propertyName.Length; i++)
        {
            if (propertyName[i] == '_')
            {
                result.Append(' ');
            }
            else if (i > 0 && char.IsUpper(propertyName[i]) && !char.IsUpper(propertyName[i - 1]))
            {
                result.Append(' ');
                result.Append(propertyName[i]);
            }
            else
            {
                result.Append(propertyName[i]);
            }
        }

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result.ToString().ToLower());
    }

    private int GetDefaultWidth(ColumnDataType dataType)
    {
        return dataType switch
        {
            ColumnDataType.Boolean => 80,
            ColumnDataType.Integer => 100,
            ColumnDataType.Decimal => 120,
            ColumnDataType.Number => 120,
            ColumnDataType.Currency => 120,
            ColumnDataType.Date => 120,
            ColumnDataType.DateTime => 180,
            ColumnDataType.Geometry => 120,
            _ => 200
        };
    }

    #endregion

    #region Selection

    private async Task OnFeatureSelected(FeatureRecord? feature)
    {
        if (feature == null) return;

        _selectedFeature = feature;
        feature.IsSelected = true;

        await OnRowSelected.InvokeAsync(feature);

        // Highlight on map
        if (HighlightSelected && !string.IsNullOrEmpty(SyncWith))
        {
            await JS.InvokeVoidAsync("highlightFeatures", SyncWith, new[] { feature.Id }, LayerId);
        }

        // Publish to ComponentBus
        await Bus.PublishAsync(new DataRowSelectedMessage
        {
            GridId = $"attribute-table-{LayerId}",
            RowId = feature.Id,
            Data = feature.Properties,
            Geometry = feature.Geometry
        });
    }

    private async Task OnMultipleFeaturesSelected(HashSet<FeatureRecord> features)
    {
        _selectedFeatures = features;

        // Update selection state
        foreach (var feature in _allFeatures)
        {
            feature.IsSelected = features.Contains(feature);
        }

        await OnRowsSelected.InvokeAsync(features.ToList());

        // Highlight on map
        if (HighlightSelected && !string.IsNullOrEmpty(SyncWith) && features.Count > 0)
        {
            var ids = features.Select(f => f.Id).ToArray();
            await JS.InvokeVoidAsync("highlightFeatures", SyncWith, ids, LayerId);
        }
    }

    private async Task HighlightFeatureFromMap(string featureId)
    {
        var feature = _allFeatures.FirstOrDefault(f => f.Id == featureId);
        if (feature != null)
        {
            _selectedFeature = feature;
            feature.IsSelected = true;

            // Scroll to row in grid if possible
            StateHasChanged();
        }
    }

    private async Task ZoomToSelected()
    {
        if (_selectedFeatures.Count == 0 || string.IsNullOrEmpty(SyncWith))
            return;

        var ids = _selectedFeatures.Select(f => f.Id).ToArray();

        if (ids.Length == 1)
        {
            await JS.InvokeVoidAsync("zoomToFeature", SyncWith, ids[0], LayerId);
        }
        else
        {
            await JS.InvokeVoidAsync("zoomToFeatures", SyncWith, ids, LayerId);
        }
    }

    #endregion

    #region Property Access

    private object? GetPropertyValue(FeatureRecord feature, string fieldName)
    {
        if (fieldName == "Id")
            return feature.Id;
        if (fieldName == "GeometryType")
            return feature.GeometryType;

        return feature.Properties.TryGetValue(fieldName, out var value) ? value : null;
    }

    private void UpdatePropertyValue(FeatureRecord feature, string fieldName, object? value)
    {
        if (fieldName == "Id" || fieldName == "GeometryType")
            return; // Can't edit these

        feature.Properties[fieldName] = value;

        // Mark as modified
        _ = OnRowsUpdated.InvokeAsync(new List<FeatureRecord> { feature });
    }

    #endregion

    #region Formatting

    private string FormatValue(object? value, ColumnConfig column)
    {
        if (value == null)
            return string.Empty;

        if (!string.IsNullOrEmpty(column.Format))
        {
            if (value is IFormattable formattable)
                return formattable.ToString(column.Format, CultureInfo.CurrentCulture);
        }

        return column.DataType switch
        {
            ColumnDataType.Currency => string.Format(CultureInfo.CurrentCulture, "{0:C2}", value),
            ColumnDataType.Percentage => string.Format(CultureInfo.CurrentCulture, "{0:P1}", value),
            ColumnDataType.Date => value is DateTime dt ? dt.ToString("yyyy-MM-dd") : value.ToString() ?? "",
            ColumnDataType.DateTime => value is DateTime dtm ? dtm.ToString("yyyy-MM-dd HH:mm:ss") : value.ToString() ?? "",
            ColumnDataType.Decimal => string.Format(CultureInfo.CurrentCulture, "{0:N2}", value),
            ColumnDataType.Integer => string.Format(CultureInfo.CurrentCulture, "{0:N0}", value),
            _ => value.ToString() ?? ""
        };
    }

    private string GetConditionalStyle(object? value, ColumnConfig column)
    {
        if (column.ConditionalFormats == null || column.ConditionalFormats.Count == 0)
            return string.Empty;

        foreach (var format in column.ConditionalFormats)
        {
            if (EvaluateCondition(value, format.Condition))
            {
                var styles = new List<string>();

                if (!string.IsNullOrEmpty(format.BackgroundColor))
                    styles.Add($"background-color: {format.BackgroundColor}");
                if (!string.IsNullOrEmpty(format.TextColor))
                    styles.Add($"color: {format.TextColor}");
                if (!string.IsNullOrEmpty(format.FontWeight))
                    styles.Add($"font-weight: {format.FontWeight}");

                return string.Join("; ", styles);
            }
        }

        return string.Empty;
    }

    private bool EvaluateCondition(object? value, string condition)
    {
        // Simple condition evaluation (e.g., "> 100", "== 'active'", "< 50")
        // In production, use a proper expression parser
        try
        {
            if (value == null) return false;

            if (condition.StartsWith(">="))
            {
                var threshold = double.Parse(condition.Substring(2).Trim());
                return Convert.ToDouble(value) >= threshold;
            }
            else if (condition.StartsWith("<="))
            {
                var threshold = double.Parse(condition.Substring(2).Trim());
                return Convert.ToDouble(value) <= threshold;
            }
            else if (condition.StartsWith(">"))
            {
                var threshold = double.Parse(condition.Substring(1).Trim());
                return Convert.ToDouble(value) > threshold;
            }
            else if (condition.StartsWith("<"))
            {
                var threshold = double.Parse(condition.Substring(1).Trim());
                return Convert.ToDouble(value) < threshold;
            }
            else if (condition.StartsWith("=="))
            {
                var expected = condition.Substring(2).Trim().Trim('\'', '"');
                return value.ToString() == expected;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Filtering

    private void ShowFilterBuilder()
    {
        // TODO: Show advanced filter builder dialog
        Snackbar.Add("Advanced filter builder coming soon", Severity.Info);
    }

    private void ShowFilterPresets()
    {
        // TODO: Show filter presets dialog
        Snackbar.Add("Filter presets coming soon", Severity.Info);
    }

    private void SaveCurrentFilter()
    {
        // TODO: Save current filter as preset
        Snackbar.Add("Save filter preset coming soon", Severity.Info);
    }

    private async Task ClearAllFilters()
    {
        _activeFilter = null;
        _searchString = null;
        _showSelectedOnly = false;
        _displayedFeatures = _allFeatures;
        StateHasChanged();

        await Bus.PublishAsync(new AllFiltersClearedMessage
        {
            Source = $"attribute-table-{LayerId}"
        });
    }

    private void ShowSelectedOnly()
    {
        _showSelectedOnly = !_showSelectedOnly;
        _displayedFeatures = _showSelectedOnly
            ? _selectedFeatures.ToList()
            : _allFeatures;
        StateHasChanged();
    }

    private async Task ApplyFilter(FilterAppliedMessage message)
    {
        // Apply filter from ComponentBus
        // TODO: Implement proper filter application
        await Task.CompletedTask;
    }

    private async Task ClearFilter(string filterId)
    {
        if (_activeFilter?.Id == filterId)
        {
            _activeFilter = null;
            _displayedFeatures = _allFeatures;
            StateHasChanged();
        }
        await Task.CompletedTask;
    }

    #endregion

    #region Summary Calculations

    private string CalculateSummary(SummaryConfig summary)
    {
        try
        {
            var values = _displayedFeatures
                .Select(f => GetPropertyValue(f, summary.FieldName))
                .Where(v => v != null)
                .ToList();

            if (values.Count == 0)
                return "N/A";

            object result = summary.Function switch
            {
                AggregateFunction.Count => values.Count,
                AggregateFunction.Sum => values.Sum(v => Convert.ToDouble(v)),
                AggregateFunction.Average => values.Average(v => Convert.ToDouble(v)),
                AggregateFunction.Min => values.Min(v => Convert.ToDouble(v)),
                AggregateFunction.Max => values.Max(v => Convert.ToDouble(v)),
                AggregateFunction.First => values.First(),
                AggregateFunction.Last => values.Last(),
                _ => 0
            };

            if (!string.IsNullOrEmpty(summary.Format) && result is IFormattable formattable)
            {
                return formattable.ToString(summary.Format, CultureInfo.CurrentCulture);
            }

            return result.ToString() ?? "N/A";
        }
        catch
        {
            return "Error";
        }
    }

    #endregion

    #region Export

    private async Task ExportData(ExportFormat format)
    {
        try
        {
            var visibleColumns = _columns.Where(c => c.Visible && c.DataType != ColumnDataType.Geometry).ToList();
            var fileName = $"{Title.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}";
            var data = _displayedFeatures.Select(f => f.Properties).ToList();

            switch (format)
            {
                case ExportFormat.CSV:
                    await JS.InvokeVoidAsync("exportToCSV", data, $"{fileName}.csv", visibleColumns);
                    Snackbar.Add("Exported to CSV", Severity.Success);
                    break;

                case ExportFormat.Excel:
                    await JS.InvokeVoidAsync("exportToExcel", data, $"{fileName}.xlsx", visibleColumns);
                    Snackbar.Add("Exported to Excel", Severity.Success);
                    break;

                case ExportFormat.JSON:
                    await JS.InvokeVoidAsync("exportToJSON", data, $"{fileName}.json");
                    Snackbar.Add("Exported to JSON", Severity.Success);
                    break;

                case ExportFormat.GeoJSON:
                    if (_hasGeometry)
                    {
                        await JS.InvokeVoidAsync("exportToGeoJSON", _displayedFeatures, $"{fileName}.geojson");
                        Snackbar.Add("Exported to GeoJSON", Severity.Success);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Export failed: {ex.Message}", Severity.Error);
        }
    }

    private async Task CopySelectedToClipboard()
    {
        if (_selectedFeatures.Count == 0)
            return;

        try
        {
            var visibleColumns = _columns.Where(c => c.Visible && c.DataType != ColumnDataType.Geometry).ToList();
            var data = _selectedFeatures.Select(f => f.Properties).ToList();

            await JS.InvokeVoidAsync("copyToClipboard", data, visibleColumns);
            Snackbar.Add($"Copied {_selectedFeatures.Count} rows to clipboard", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Copy failed: {ex.Message}", Severity.Error);
        }
    }

    private async Task PrintTable()
    {
        try
        {
            await JS.InvokeVoidAsync("printTable", "attribute-table", Title);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Print failed: {ex.Message}", Severity.Error);
        }
    }

    #endregion

    #region Bulk Operations

    private void ShowBulkEditDialog()
    {
        // TODO: Show bulk edit dialog
        Snackbar.Add("Bulk edit coming soon", Severity.Info);
    }

    private void ShowCalculateFieldDialog()
    {
        // TODO: Show calculate field dialog
        Snackbar.Add("Field calculator coming soon", Severity.Info);
    }

    private async Task DeleteSelected()
    {
        if (_selectedFeatures.Count == 0)
            return;

        var confirm = await DialogService.ShowMessageBox(
            "Confirm Delete",
            $"Are you sure you want to delete {_selectedFeatures.Count} feature(s)?",
            yesText: "Delete", cancelText: "Cancel");

        if (confirm == true)
        {
            foreach (var feature in _selectedFeatures.ToList())
            {
                _allFeatures.Remove(feature);
                _displayedFeatures.Remove(feature);
                await OnRowDeleted.InvokeAsync(feature.Id);
            }

            _selectedFeatures.Clear();
            Snackbar.Add($"Deleted features", Severity.Success);
            StateHasChanged();
        }
    }

    #endregion

    #region Column Management

    private void ToggleColumnVisibility(ColumnConfig column, bool visible)
    {
        column.Visible = visible;
        StateHasChanged();
    }

    #endregion

    #region Utility Methods

    private void DisableSync()
    {
        _isSynced = false;
        StateHasChanged();

        // Clear highlights
        if (!string.IsNullOrEmpty(SyncWith))
        {
            _ = JS.InvokeVoidAsync("clearHighlight", SyncWith);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Refresh the table data
    /// </summary>
    public async Task RefreshAsync()
    {
        if (Features != null)
        {
            _allFeatures = Features;
            _displayedFeatures = _allFeatures;
            StateHasChanged();
        }
        else
        {
            await RequestFeaturesFromLayer();
        }
    }

    /// <summary>
    /// Get selected features
    /// </summary>
    public List<FeatureRecord> GetSelectedFeatures() => _selectedFeatures.ToList();

    /// <summary>
    /// Clear selection
    /// </summary>
    public void ClearSelection()
    {
        _selectedFeatures.Clear();
        _selectedFeature = null;

        foreach (var feature in _allFeatures)
        {
            feature.IsSelected = false;
        }

        StateHasChanged();

        // Clear map highlights
        if (!string.IsNullOrEmpty(SyncWith))
        {
            _ = JS.InvokeVoidAsync("clearHighlight", SyncWith);
        }
    }

    /// <summary>
    /// Select features by IDs
    /// </summary>
    public void SelectFeatures(params string[] featureIds)
    {
        _selectedFeatures.Clear();

        foreach (var id in featureIds)
        {
            var feature = _allFeatures.FirstOrDefault(f => f.Id == id);
            if (feature != null)
            {
                _selectedFeatures.Add(feature);
                feature.IsSelected = true;
            }
        }

        StateHasChanged();
    }

    /// <summary>
    /// Apply custom filter
    /// </summary>
    public void ApplyCustomFilter(Func<FeatureRecord, bool> predicate)
    {
        _displayedFeatures = _allFeatures.Where(predicate).ToList();
        StateHasChanged();
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        // Cleanup
        await Task.CompletedTask;
    }

    #endregion
}

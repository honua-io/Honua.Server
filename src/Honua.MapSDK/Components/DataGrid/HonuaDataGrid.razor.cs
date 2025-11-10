// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.Dynamic;
using System.Globalization;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Honua.MapSDK.Components.DataGrid;

/// <summary>
/// HonuaDataGrid - Interactive data grid component that auto-syncs with HonuaMap
/// Displays feature data in a table with filtering, sorting, and export capabilities
/// </summary>
/// <typeparam name="TItem">The type of items displayed in the grid</typeparam>
public partial class HonuaDataGrid<TItem> : ComponentBase, IAsyncDisposable
{
    #region Injected Services
    [Inject] protected ComponentBus Bus { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected HttpClient? HttpClient { get; set; }
    #endregion

    #region Parameters - Data Source

    /// <summary>
    /// Unique identifier for this grid instance
    /// </summary>
    [Parameter]
    public string Id { get; set; } = $"grid-{Guid.NewGuid():N}";

    /// <summary>
    /// Data source URL or identifier
    /// Supports: https://.../features.geojson, wfs://..., grpc://...
    /// </summary>
    [Parameter]
    public string? Source { get; set; }

    /// <summary>
    /// Direct data binding - provide items directly instead of loading from source
    /// </summary>
    [Parameter]
    public IEnumerable<TItem>? Items { get; set; }

    /// <summary>
    /// Map ID to synchronize with - when set, grid filters by map extent
    /// </summary>
    [Parameter]
    public string? SyncWith { get; set; }

    #endregion

    #region Parameters - Appearance

    /// <summary>
    /// Grid title displayed in toolbar
    /// </summary>
    [Parameter]
    public string Title { get; set; } = "Data Grid";

    /// <summary>
    /// Additional CSS classes
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>
    /// Inline styles
    /// </summary>
    [Parameter]
    public string Style { get; set; } = "width: 100%; height: 500px;";

    /// <summary>
    /// Use dense layout (more compact)
    /// </summary>
    [Parameter]
    public bool Dense { get; set; } = true;

    #endregion

    #region Parameters - Features

    /// <summary>
    /// Show the toolbar
    /// </summary>
    [Parameter]
    public bool ShowToolbar { get; set; } = true;

    /// <summary>
    /// Show search box in toolbar
    /// </summary>
    [Parameter]
    public bool ShowSearch { get; set; } = true;

    /// <summary>
    /// Show export menu in toolbar
    /// </summary>
    [Parameter]
    public bool ShowExport { get; set; } = true;

    /// <summary>
    /// Show refresh button in toolbar
    /// </summary>
    [Parameter]
    public bool ShowRefresh { get; set; } = true;

    /// <summary>
    /// Enable column filtering
    /// </summary>
    [Parameter]
    public bool Filterable { get; set; } = true;

    /// <summary>
    /// Enable column sorting
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; } = true;

    /// <summary>
    /// Enable multi-row selection
    /// </summary>
    [Parameter]
    public bool MultiSelection { get; set; } = false;

    /// <summary>
    /// Allow hiding columns
    /// </summary>
    [Parameter]
    public bool HideableColumns { get; set; } = true;

    #endregion

    #region Parameters - Pagination

    /// <summary>
    /// Number of rows per page
    /// </summary>
    [Parameter]
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Available page size options
    /// </summary>
    [Parameter]
    public int[] PageSizeOptions { get; set; } = new[] { 10, 25, 50, 100, 250 };

    /// <summary>
    /// Pager info format string
    /// </summary>
    [Parameter]
    public string PagerInfoFormat { get; set; } = "{first_item}-{last_item} of {all_items}";

    #endregion

    #region Parameters - Child Content

    /// <summary>
    /// Column definitions - if not provided, columns are auto-generated
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
    public EventCallback<TItem> OnRowSelected { get; set; }

    /// <summary>
    /// Callback when multiple rows are selected
    /// </summary>
    [Parameter]
    public EventCallback<IEnumerable<TItem>> OnMultipleRowsSelected { get; set; }

    /// <summary>
    /// Callback when data is loaded
    /// </summary>
    [Parameter]
    public EventCallback<int> OnDataLoaded { get; set; }

    /// <summary>
    /// Callback when error occurs
    /// </summary>
    [Parameter]
    public EventCallback<string> OnError { get; set; }

    #endregion

    #region Private Fields

    private MudDataGrid<TItem>? _dataGrid;
    private List<TItem> _items = new();
    private List<TItem> _allItems = new(); // Unfiltered items
    private TItem? _selectedItem;
    private HashSet<TItem> _selectedItems = new();
    private bool _isLoading = false;
    private bool _isSynced = false;
    private bool _autoGeneratedColumns = false;
    private bool _hasGeometry = false;
    private string? _errorMessage;
    private string? _searchString;
    private double[]? _currentMapBounds;

    private readonly List<ColumnDefinition> _columnDefinitions = new();

    private Func<TItem, bool> _quickFilterFunc => item =>
    {
        if (string.IsNullOrWhiteSpace(_searchString))
            return true;

        // Search across all properties
        var props = typeof(TItem).GetProperties();
        foreach (var prop in props)
        {
            try
            {
                var value = prop.GetValue(item);
                if (value?.ToString()?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }
            catch
            {
                // Skip properties that can't be accessed
            }
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

        // Load data if source is provided
        if (Source != null && _allItems.Count == 0)
        {
            await LoadDataFromSource();
        }
        // Use provided items
        else if (Items != null)
        {
            _allItems = Items.ToList();
            _items = _allItems;
            GenerateColumns();
            await PublishDataLoaded();
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
        // Listen for map extent changes (for auto-sync)
        Bus.Subscribe<MapExtentChangedMessage>(async args =>
        {
            if (_isSynced && args.Message.MapId == SyncWith)
            {
                _currentMapBounds = args.Message.Bounds;
                await FilterByMapExtent(args.Message.Bounds);
                await InvokeAsync(StateHasChanged);
            }
        });

        // Listen for feature clicks (highlight row)
        Bus.Subscribe<FeatureClickedMessage>(async args =>
        {
            if (_isSynced && args.Message.MapId == SyncWith)
            {
                await HighlightRowByFeature(args.Message.FeatureId, args.Message.Properties);
                await InvokeAsync(StateHasChanged);
            }
        });

        // Listen for filter applied
        Bus.Subscribe<FilterAppliedMessage>(async args =>
        {
            await ApplyAttributeFilter(args.Message);
            await InvokeAsync(StateHasChanged);
        });

        // Listen for filter cleared
        Bus.Subscribe<FilterClearedMessage>(async args =>
        {
            await ClearFilter();
            await InvokeAsync(StateHasChanged);
        });

        // Listen for all filters cleared
        Bus.Subscribe<AllFiltersClearedMessage>(async args =>
        {
            await ClearFilter();
            await InvokeAsync(StateHasChanged);
        });
    }

    #endregion

    #region Data Loading

    private async Task LoadDataFromSource()
    {
        if (string.IsNullOrEmpty(Source))
            return;

        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            if (Source.StartsWith("http://") || Source.StartsWith("https://"))
            {
                await LoadFromHttp(Source);
            }
            else if (Source.StartsWith("wfs://"))
            {
                await LoadFromWfs(Source);
            }
            else if (Source.StartsWith("grpc://"))
            {
                await LoadFromGrpc(Source);
            }
            else
            {
                throw new NotSupportedException($"Unsupported source protocol: {Source}");
            }

            GenerateColumns();
            await PublishDataLoaded();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading data: {ex.Message}";
            await OnError.InvokeAsync(_errorMessage);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadFromHttp(string url)
    {
        if (HttpClient == null)
            throw new InvalidOperationException("HttpClient not configured");

        var response = await HttpClient.GetStringAsync(url);

        // Try to detect if it's GeoJSON
        if (url.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase) || response.TrimStart().StartsWith("{\"type\":\"FeatureCollection"))
        {
            await LoadFromGeoJson(response);
        }
        else
        {
            // Try to parse as JSON array
            var items = JsonSerializer.Deserialize<List<TItem>>(response);
            if (items != null)
            {
                _allItems = items;
                _items = _allItems;
            }
        }
    }

    private async Task LoadFromGeoJson(string geoJsonString)
    {
        var doc = JsonDocument.Parse(geoJsonString);
        var root = doc.RootElement;

        if (root.GetProperty("type").GetString() == "FeatureCollection")
        {
            var features = root.GetProperty("features");
            var items = new List<TItem>();

            foreach (var feature in features.EnumerateArray())
            {
                var properties = feature.GetProperty("properties");
                var geometry = feature.TryGetProperty("geometry", out var geo) ? geo : (JsonElement?)null;

                // Convert to TItem
                var item = ConvertFeatureToItem(properties, geometry);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            _allItems = items;
            _items = _allItems;
            _hasGeometry = true;
        }

        await Task.CompletedTask;
    }

    private TItem? ConvertFeatureToItem(JsonElement properties, JsonElement? geometry)
    {
        try
        {
            // If TItem is a dictionary or dynamic, create it dynamically
            if (typeof(TItem) == typeof(Dictionary<string, object>) ||
                typeof(TItem) == typeof(ExpandoObject))
            {
                var dict = new Dictionary<string, object>();

                foreach (var prop in properties.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? "",
                        JsonValueKind.Number => prop.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.ToString()
                    };
                }

                if (geometry.HasValue)
                {
                    dict["_geometry"] = geometry.Value.GetRawText();
                    dict["_geometryType"] = geometry.Value.GetProperty("type").GetString() ?? "Unknown";
                }

                return (TItem)(object)dict;
            }
            else
            {
                // Deserialize to TItem
                var item = JsonSerializer.Deserialize<TItem>(properties.GetRawText());

                // If TItem has a Geometry property, set it
                if (geometry.HasValue && item != null)
                {
                    var geomProp = typeof(TItem).GetProperty("Geometry");
                    if (geomProp != null)
                    {
                        geomProp.SetValue(item, geometry.Value.GetRawText());
                    }
                }

                return item;
            }
        }
        catch
        {
            return default;
        }
    }

    private async Task LoadFromWfs(string wfsUrl)
    {
        // TODO: Implement WFS loading
        await Task.CompletedTask;
        throw new NotImplementedException("WFS loading not yet implemented");
    }

    private async Task LoadFromGrpc(string grpcUrl)
    {
        // TODO: Implement gRPC loading
        await Task.CompletedTask;
        throw new NotImplementedException("gRPC loading not yet implemented");
    }

    #endregion

    #region Column Generation

    private void GenerateColumns()
    {
        if (Columns != null)
        {
            // User provided columns, don't auto-generate
            _autoGeneratedColumns = false;
            return;
        }

        _columnDefinitions.Clear();
        _autoGeneratedColumns = true;

        if (_allItems.Count == 0)
            return;

        var sampleItem = _allItems.First();

        if (typeof(TItem) == typeof(Dictionary<string, object>))
        {
            // Dynamic columns from dictionary
            var dict = (Dictionary<string, object>)(object)sampleItem!;
            foreach (var kvp in dict)
            {
                var isGeometry = kvp.Key.StartsWith("_geometry");
                if (kvp.Key == "_geometry") continue; // Skip raw geometry

                _columnDefinitions.Add(new ColumnDefinition
                {
                    PropertyName = kvp.Key,
                    Title = FormatColumnTitle(kvp.Key),
                    PropertyFunc = item => GetDictionaryValue((Dictionary<string, object>)(object)item!, kvp.Key),
                    IsGeometry = kvp.Key == "_geometryType",
                    Sortable = !isGeometry,
                    Filterable = !isGeometry,
                    Hideable = true,
                    Hidden = false
                });
            }
        }
        else
        {
            // Columns from TItem properties
            var properties = typeof(TItem).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var isGeometry = prop.Name.Equals("Geometry", StringComparison.OrdinalIgnoreCase);

                _columnDefinitions.Add(new ColumnDefinition
                {
                    PropertyName = prop.Name,
                    Title = FormatColumnTitle(prop.Name),
                    PropertyFunc = item => prop.GetValue(item),
                    IsGeometry = isGeometry,
                    Sortable = !isGeometry,
                    Filterable = !isGeometry,
                    Hideable = true,
                    Hidden = false
                });
            }
        }
    }

    private string FormatColumnTitle(string propertyName)
    {
        // Remove underscores and capitalize
        if (propertyName.StartsWith("_"))
            propertyName = propertyName.Substring(1);

        // Add spaces before capital letters
        var result = new StringBuilder();
        for (int i = 0; i < propertyName.Length; i++)
        {
            if (i > 0 && char.IsUpper(propertyName[i]))
                result.Append(' ');
            result.Append(propertyName[i]);
        }

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result.ToString().ToLower());
    }

    private object? GetDictionaryValue(Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var value) ? value : null;
    }

    #endregion

    #region Filtering

    private async Task FilterByMapExtent(double[] bounds)
    {
        if (!_hasGeometry || _allItems.Count == 0)
            return;

        // Filter items that fall within the map bounds
        // This is a simplified implementation - for production, use a proper spatial library
        _items = _allItems.Where(item => IsWithinBounds(item, bounds)).ToList();

        await Task.CompletedTask;
    }

    private bool IsWithinBounds(TItem item, double[] bounds)
    {
        // TODO: Implement proper spatial filtering
        // For now, just return true (show all items)
        // In production, parse geometry and check if it intersects with bounds
        return true;
    }

    private async Task ApplyAttributeFilter(FilterAppliedMessage filter)
    {
        // TODO: Implement attribute filtering based on filter expression
        await Task.CompletedTask;
    }

    private async Task ClearFilter()
    {
        _items = _allItems;
        _currentMapBounds = null;
        await Task.CompletedTask;
    }

    #endregion

    #region Row Selection

    private async Task HandleRowSelected(TItem? item)
    {
        if (item == null) return;

        _selectedItem = item;
        await OnRowSelected.InvokeAsync(item);

        // Publish to ComponentBus
        var geometry = GetItemGeometry(item);
        var rowId = GetItemId(item);

        await Bus.PublishAsync(new DataRowSelectedMessage
        {
            GridId = Id,
            RowId = rowId,
            Data = GetItemAsDictionary(item),
            Geometry = geometry
        }, Id);
    }

    private async Task HandleMultipleRowsSelected(HashSet<TItem> items)
    {
        _selectedItems = items;
        await OnMultipleRowsSelected.InvokeAsync(items);
    }

    private async Task HighlightRowByFeature(string featureId, Dictionary<string, object> properties)
    {
        // Find and select the row matching the feature
        var matchingItem = _items.FirstOrDefault(item =>
        {
            var itemId = GetItemId(item);
            return itemId == featureId;
        });

        if (matchingItem != null)
        {
            _selectedItem = matchingItem;
        }

        await Task.CompletedTask;
    }

    #endregion

    #region Export

    private async Task ExportToJson()
    {
        try
        {
            var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await DownloadFile($"{Title}-{DateTime.Now:yyyyMMdd}.json", json, "application/json");
        }
        catch (Exception ex)
        {
            _errorMessage = $"Export failed: {ex.Message}";
        }
    }

    private async Task ExportToCsv()
    {
        try
        {
            var csv = new StringBuilder();

            // Header
            var headers = _columnDefinitions.Where(c => !c.IsGeometry).Select(c => c.Title);
            csv.AppendLine(string.Join(",", headers));

            // Rows
            foreach (var item in _items)
            {
                var values = _columnDefinitions
                    .Where(c => !c.IsGeometry)
                    .Select(c => EscapeCsvValue(c.PropertyFunc(item)?.ToString() ?? ""));
                csv.AppendLine(string.Join(",", values));
            }

            await DownloadFile($"{Title}-{DateTime.Now:yyyyMMdd}.csv", csv.ToString(), "text/csv");
        }
        catch (Exception ex)
        {
            _errorMessage = $"Export failed: {ex.Message}";
        }
    }

    private async Task ExportToGeoJson()
    {
        if (!_hasGeometry)
            return;

        try
        {
            var features = new List<object>();

            foreach (var item in _items)
            {
                var properties = GetItemAsDictionary(item);
                var geometry = GetItemGeometry(item);

                features.Add(new
                {
                    type = "Feature",
                    properties,
                    geometry = geometry != null ? JsonSerializer.Deserialize<object>(geometry.ToString()!) : null
                });
            }

            var featureCollection = new
            {
                type = "FeatureCollection",
                features
            };

            var json = JsonSerializer.Serialize(featureCollection, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await DownloadFile($"{Title}-{DateTime.Now:yyyyMMdd}.geojson", json, "application/geo+json");
        }
        catch (Exception ex)
        {
            _errorMessage = $"Export failed: {ex.Message}";
        }
    }

    private string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private async Task DownloadFile(string filename, string content, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var base64 = Convert.ToBase64String(bytes);

        await JS.InvokeVoidAsync("downloadFile", filename, base64, contentType);
    }

    #endregion

    #region Utility Methods

    private async Task RefreshData()
    {
        if (Source != null)
        {
            _allItems.Clear();
            _items.Clear();
            await LoadDataFromSource();
        }
        else if (Items != null)
        {
            _allItems = Items.ToList();
            _items = _allItems;
            StateHasChanged();
        }
    }

    private void DisableSync()
    {
        _isSynced = false;
        _items = _allItems;
        _currentMapBounds = null;
        StateHasChanged();
    }

    private async Task PublishDataLoaded()
    {
        await Bus.PublishAsync(new DataLoadedMessage
        {
            ComponentId = Id,
            FeatureCount = _allItems.Count,
            Source = Source ?? "Direct"
        }, Id);

        await OnDataLoaded.InvokeAsync(_allItems.Count);
    }

    private string GetGeometryType(object? value)
    {
        if (value == null) return "None";

        var str = value.ToString();
        return str ?? "Unknown";
    }

    private string FormatValue(object value, string? format)
    {
        if (value == null) return "";

        if (!string.IsNullOrEmpty(format))
        {
            if (value is IFormattable formattable)
                return formattable.ToString(format, CultureInfo.CurrentCulture);
        }

        return value.ToString() ?? "";
    }

    private object? GetItemGeometry(TItem item)
    {
        if (typeof(TItem) == typeof(Dictionary<string, object>))
        {
            var dict = (Dictionary<string, object>)(object)item!;
            return dict.TryGetValue("_geometry", out var geom) ? geom : null;
        }
        else
        {
            var geomProp = typeof(TItem).GetProperty("Geometry");
            return geomProp?.GetValue(item);
        }
    }

    private string GetItemId(TItem item)
    {
        // Try common ID properties
        var idProps = new[] { "Id", "ID", "id", "FeatureId", "FID", "OBJECTID" };

        if (typeof(TItem) == typeof(Dictionary<string, object>))
        {
            var dict = (Dictionary<string, object>)(object)item!;
            foreach (var prop in idProps)
            {
                if (dict.TryGetValue(prop, out var value))
                    return value?.ToString() ?? Guid.NewGuid().ToString();
            }
        }
        else
        {
            foreach (var propName in idProps)
            {
                var prop = typeof(TItem).GetProperty(propName);
                if (prop != null)
                {
                    var value = prop.GetValue(item);
                    if (value != null)
                        return value.ToString() ?? Guid.NewGuid().ToString();
                }
            }
        }

        return Guid.NewGuid().ToString();
    }

    private Dictionary<string, object> GetItemAsDictionary(TItem item)
    {
        if (typeof(TItem) == typeof(Dictionary<string, object>))
        {
            return (Dictionary<string, object>)(object)item!;
        }
        else
        {
            var dict = new Dictionary<string, object>();
            var properties = typeof(TItem).GetProperties();

            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(item);
                    if (value != null)
                        dict[prop.Name] = value;
                }
                catch
                {
                    // Skip properties that can't be accessed
                }
            }

            return dict;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Refresh the grid data from source
    /// </summary>
    public async Task RefreshAsync()
    {
        await RefreshData();
    }

    /// <summary>
    /// Get currently selected item
    /// </summary>
    public TItem? GetSelectedItem() => _selectedItem;

    /// <summary>
    /// Get currently selected items (multi-selection)
    /// </summary>
    public IEnumerable<TItem> GetSelectedItems() => _selectedItems;

    /// <summary>
    /// Get all items currently displayed in the grid
    /// </summary>
    public IEnumerable<TItem> GetItems() => _items;

    /// <summary>
    /// Get total item count (before filtering)
    /// </summary>
    public int GetTotalCount() => _allItems.Count;

    /// <summary>
    /// Clear selection
    /// </summary>
    public void ClearSelection()
    {
        _selectedItem = default;
        _selectedItems.Clear();
        StateHasChanged();
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        // Cleanup subscriptions if needed
        await Task.CompletedTask;
    }

    #endregion

    #region Helper Classes

    private class ColumnDefinition
    {
        public required string PropertyName { get; init; }
        public required string Title { get; init; }
        public required Func<TItem, object?> PropertyFunc { get; init; }
        public bool IsGeometry { get; init; }
        public bool Sortable { get; init; } = true;
        public bool Filterable { get; init; } = true;
        public bool Hideable { get; init; } = true;
        public bool Hidden { get; init; } = false;
        public string? Format { get; init; }
    }

    #endregion
}

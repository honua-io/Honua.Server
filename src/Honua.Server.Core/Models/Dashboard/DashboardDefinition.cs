// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Server.Core.Models.Dashboard;

/// <summary>
/// Represents a complete dashboard definition including layout and widgets.
/// </summary>
public class DashboardDefinition
{
    /// <summary>
    /// Unique identifier for the dashboard.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Dashboard name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Dashboard description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// User ID of the dashboard owner.
    /// </summary>
    public required string OwnerId { get; set; }

    /// <summary>
    /// Tags for categorization and search.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Dashboard layout configuration.
    /// </summary>
    public required DashboardLayout Layout { get; set; }

    /// <summary>
    /// List of widgets in the dashboard.
    /// </summary>
    public List<WidgetDefinition> Widgets { get; set; } = new();

    /// <summary>
    /// Widget connections for cross-filtering and interactions.
    /// </summary>
    public List<WidgetConnection> Connections { get; set; } = new();

    /// <summary>
    /// Whether this dashboard is public or private.
    /// </summary>
    public bool IsPublic { get; set; } = false;

    /// <summary>
    /// Whether this is a template dashboard.
    /// </summary>
    public bool IsTemplate { get; set; } = false;

    /// <summary>
    /// Refresh interval in seconds (null = no auto-refresh).
    /// </summary>
    public int? RefreshInterval { get; set; }

    /// <summary>
    /// Theme settings for the dashboard.
    /// </summary>
    public DashboardTheme? Theme { get; set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// JSON schema version for compatibility.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0";
}

/// <summary>
/// Dashboard layout configuration.
/// </summary>
public class DashboardLayout
{
    /// <summary>
    /// Layout type (grid, flex, etc.).
    /// </summary>
    public string Type { get; set; } = "grid";

    /// <summary>
    /// Number of columns in the grid.
    /// </summary>
    public int Columns { get; set; } = 12;

    /// <summary>
    /// Row height in pixels.
    /// </summary>
    public int RowHeight { get; set; } = 60;

    /// <summary>
    /// Gap between widgets in pixels.
    /// </summary>
    public int Gap { get; set; } = 16;

    /// <summary>
    /// Whether the layout is responsive.
    /// </summary>
    public bool Responsive { get; set; } = true;
}

/// <summary>
/// Widget definition within a dashboard.
/// </summary>
public class WidgetDefinition
{
    /// <summary>
    /// Unique widget identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Widget title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Widget type (map, chart, table, filter, kpi).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Grid position configuration.
    /// </summary>
    public required WidgetPosition Position { get; set; }

    /// <summary>
    /// Widget-specific configuration (serialized JSON).
    /// </summary>
    public required WidgetConfig Config { get; set; }

    /// <summary>
    /// Data source configuration.
    /// </summary>
    public WidgetDataSource? DataSource { get; set; }

    /// <summary>
    /// Whether the widget is visible.
    /// </summary>
    public bool Visible { get; set; } = true;
}

/// <summary>
/// Widget position and size in the grid.
/// </summary>
public class WidgetPosition
{
    /// <summary>
    /// Grid column position (0-based).
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Grid row position (0-based).
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Widget width in grid columns.
    /// </summary>
    public int Width { get; set; } = 4;

    /// <summary>
    /// Widget height in grid rows.
    /// </summary>
    public int Height { get; set; } = 4;

    /// <summary>
    /// Minimum width constraint.
    /// </summary>
    public int? MinWidth { get; set; }

    /// <summary>
    /// Minimum height constraint.
    /// </summary>
    public int? MinHeight { get; set; }
}

/// <summary>
/// Base widget configuration (extended by specific widget types).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(MapWidgetConfig), "map")]
[JsonDerivedType(typeof(ChartWidgetConfig), "chart")]
[JsonDerivedType(typeof(TableWidgetConfig), "table")]
[JsonDerivedType(typeof(FilterWidgetConfig), "filter")]
[JsonDerivedType(typeof(KpiWidgetConfig), "kpi")]
public abstract class WidgetConfig
{
    /// <summary>
    /// Whether to show the widget header.
    /// </summary>
    public bool ShowHeader { get; set; } = true;

    /// <summary>
    /// Custom CSS classes.
    /// </summary>
    public List<string> CssClasses { get; set; } = new();
}

/// <summary>
/// Map widget configuration.
/// </summary>
public class MapWidgetConfig : WidgetConfig
{
    /// <summary>
    /// Initial map center [longitude, latitude].
    /// </summary>
    public double[]? Center { get; set; }

    /// <summary>
    /// Initial zoom level.
    /// </summary>
    public double Zoom { get; set; } = 10;

    /// <summary>
    /// Base map style URL or identifier.
    /// </summary>
    public string? BaseMapStyle { get; set; }

    /// <summary>
    /// Layers to display on the map.
    /// </summary>
    public List<MapLayerConfig> Layers { get; set; } = new();

    /// <summary>
    /// Whether to show navigation controls.
    /// </summary>
    public bool ShowControls { get; set; } = true;

    /// <summary>
    /// Whether map interactions are enabled.
    /// </summary>
    public bool Interactive { get; set; } = true;
}

/// <summary>
/// Map layer configuration.
/// </summary>
public class MapLayerConfig
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public string? SourceLayer { get; set; }
    public Dictionary<string, object> Paint { get; set; } = new();
    public Dictionary<string, object> Layout { get; set; } = new();
}

/// <summary>
/// Chart widget configuration.
/// </summary>
public class ChartWidgetConfig : WidgetConfig
{
    /// <summary>
    /// Chart type (bar, line, pie, scatter, area, etc.).
    /// </summary>
    public required string ChartType { get; set; }

    /// <summary>
    /// X-axis field name.
    /// </summary>
    public string? XAxis { get; set; }

    /// <summary>
    /// Y-axis field name(s).
    /// </summary>
    public List<string> YAxis { get; set; } = new();

    /// <summary>
    /// Chart color scheme.
    /// </summary>
    public List<string> Colors { get; set; } = new();

    /// <summary>
    /// Whether to show the legend.
    /// </summary>
    public bool ShowLegend { get; set; } = true;

    /// <summary>
    /// Whether to show data labels.
    /// </summary>
    public bool ShowDataLabels { get; set; } = false;

    /// <summary>
    /// Aggregation function (sum, avg, count, min, max).
    /// </summary>
    public string? Aggregation { get; set; }
}

/// <summary>
/// Table widget configuration.
/// </summary>
public class TableWidgetConfig : WidgetConfig
{
    /// <summary>
    /// Columns to display.
    /// </summary>
    public List<TableColumnConfig> Columns { get; set; } = new();

    /// <summary>
    /// Whether to enable sorting.
    /// </summary>
    public bool Sortable { get; set; } = true;

    /// <summary>
    /// Whether to enable filtering.
    /// </summary>
    public bool Filterable { get; set; } = true;

    /// <summary>
    /// Whether to enable pagination.
    /// </summary>
    public bool Paginated { get; set; } = true;

    /// <summary>
    /// Rows per page.
    /// </summary>
    public int PageSize { get; set; } = 25;

    /// <summary>
    /// Whether rows are selectable.
    /// </summary>
    public bool Selectable { get; set; } = true;
}

/// <summary>
/// Table column configuration.
/// </summary>
public class TableColumnConfig
{
    public required string Field { get; set; }
    public required string Header { get; set; }
    public string? Format { get; set; }
    public int? Width { get; set; }
    public bool Visible { get; set; } = true;
    public bool Sortable { get; set; } = true;
}

/// <summary>
/// Filter panel widget configuration.
/// </summary>
public class FilterWidgetConfig : WidgetConfig
{
    /// <summary>
    /// Filter controls.
    /// </summary>
    public List<FilterControl> Filters { get; set; } = new();

    /// <summary>
    /// Whether to show apply/reset buttons.
    /// </summary>
    public bool ShowActions { get; set; } = true;

    /// <summary>
    /// Whether filters apply automatically.
    /// </summary>
    public bool AutoApply { get; set; } = false;
}

/// <summary>
/// Individual filter control configuration.
/// </summary>
public class FilterControl
{
    public required string Id { get; set; }
    public required string Label { get; set; }
    public required string Field { get; set; }
    public required string Type { get; set; } // text, select, date, range, etc.
    public List<string> Options { get; set; } = new();
    public object? DefaultValue { get; set; }
}

/// <summary>
/// KPI/metric card widget configuration.
/// </summary>
public class KpiWidgetConfig : WidgetConfig
{
    /// <summary>
    /// Field to aggregate for the KPI value.
    /// </summary>
    public required string ValueField { get; set; }

    /// <summary>
    /// Aggregation function (sum, avg, count, min, max).
    /// </summary>
    public string Aggregation { get; set; } = "sum";

    /// <summary>
    /// Number format (e.g., "0,0", "0.00", "0%").
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Prefix text (e.g., "$", "Total: ").
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Suffix text (e.g., " units", " km").
    /// </summary>
    public string? Suffix { get; set; }

    /// <summary>
    /// Icon to display.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// KPI color.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Show trend indicator.
    /// </summary>
    public bool ShowTrend { get; set; } = false;

    /// <summary>
    /// Comparison period for trend (e.g., "previous", "year-ago").
    /// </summary>
    public string? TrendComparison { get; set; }
}

/// <summary>
/// Widget data source configuration.
/// </summary>
public class WidgetDataSource
{
    /// <summary>
    /// Data source type (layer, query, api, static).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Data source identifier (layer name, query ID, API endpoint).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// OData filter query.
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Fields to include in the query.
    /// </summary>
    public List<string> Fields { get; set; } = new();

    /// <summary>
    /// Static data (for testing or simple widgets).
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Refresh interval in seconds.
    /// </summary>
    public int? RefreshInterval { get; set; }
}

/// <summary>
/// Widget connection for cross-filtering and interactions.
/// </summary>
public class WidgetConnection
{
    /// <summary>
    /// Source widget ID.
    /// </summary>
    public required Guid SourceWidgetId { get; set; }

    /// <summary>
    /// Target widget ID.
    /// </summary>
    public required Guid TargetWidgetId { get; set; }

    /// <summary>
    /// Connection type (filter, select, hover, etc.).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Field mapping between source and target.
    /// </summary>
    public Dictionary<string, string> FieldMapping { get; set; } = new();

    /// <summary>
    /// Whether the connection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Dashboard theme configuration.
/// </summary>
public class DashboardTheme
{
    /// <summary>
    /// Primary color.
    /// </summary>
    public string? Primary { get; set; }

    /// <summary>
    /// Secondary color.
    /// </summary>
    public string? Secondary { get; set; }

    /// <summary>
    /// Background color.
    /// </summary>
    public string? Background { get; set; }

    /// <summary>
    /// Text color.
    /// </summary>
    public string? TextColor { get; set; }

    /// <summary>
    /// Font family.
    /// </summary>
    public string? FontFamily { get; set; }
}

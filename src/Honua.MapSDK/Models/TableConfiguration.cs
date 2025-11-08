namespace Honua.MapSDK.Models;

/// <summary>
/// Configuration for the attribute table component
/// </summary>
public class TableConfiguration
{
    /// <summary>
    /// Column configurations
    /// </summary>
    public List<ColumnConfig> Columns { get; set; } = new();

    /// <summary>
    /// Active filter configuration
    /// </summary>
    public FilterConfig? Filter { get; set; }

    /// <summary>
    /// Active sort configuration
    /// </summary>
    public SortConfig? Sort { get; set; }

    /// <summary>
    /// Show summary row with aggregate calculations
    /// </summary>
    public bool ShowSummary { get; set; }

    /// <summary>
    /// Summary aggregations to display
    /// </summary>
    public List<SummaryConfig> Summaries { get; set; } = new();
}

/// <summary>
/// Column configuration for attribute table
/// </summary>
public class ColumnConfig
{
    /// <summary>
    /// Field name from feature properties
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// Display name in column header
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Data type of the column
    /// </summary>
    public ColumnDataType DataType { get; set; } = ColumnDataType.String;

    /// <summary>
    /// Column is visible in the table
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Column can be edited inline
    /// </summary>
    public bool Editable { get; set; } = false;

    /// <summary>
    /// Column width in pixels (0 = auto)
    /// </summary>
    public int Width { get; set; } = 0;

    /// <summary>
    /// Frozen column (sticky to left)
    /// </summary>
    public bool Frozen { get; set; } = false;

    /// <summary>
    /// Display format string (e.g., "C2" for currency, "N0" for whole numbers)
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Conditional formatting rules
    /// </summary>
    public List<ConditionalFormat>? ConditionalFormats { get; set; }

    /// <summary>
    /// Sort order (0 = not sorted)
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Sort direction
    /// </summary>
    public SortDirection? SortDirection { get; set; }

    /// <summary>
    /// Calculated column expression (e.g., "{field1} + {field2}")
    /// </summary>
    public string? CalculatedExpression { get; set; }

    /// <summary>
    /// Whether this is a calculated/virtual column
    /// </summary>
    public bool IsCalculated => !string.IsNullOrEmpty(CalculatedExpression);
}

/// <summary>
/// Data type enum for columns
/// </summary>
public enum ColumnDataType
{
    String,
    Number,
    Integer,
    Decimal,
    Boolean,
    Date,
    DateTime,
    Time,
    Currency,
    Percentage,
    Url,
    Email,
    Phone,
    Geometry
}

/// <summary>
/// Sort direction enum
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Filter configuration
/// </summary>
public class FilterConfig
{
    /// <summary>
    /// Filter ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Filter name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Filter type
    /// </summary>
    public TableFilterType Type { get; set; } = TableFilterType.Simple;

    /// <summary>
    /// Simple filter: field name
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Simple filter: operator
    /// </summary>
    public FilterOperator Operator { get; set; } = FilterOperator.Equals;

    /// <summary>
    /// Simple filter: value to compare
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Advanced filter: SQL-like expression
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// Compound filter: child filters
    /// </summary>
    public List<FilterConfig>? ChildFilters { get; set; }

    /// <summary>
    /// Compound filter: logical operator (AND/OR)
    /// </summary>
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;

    /// <summary>
    /// Whether this filter is active
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Filter type enum
/// </summary>
public enum TableFilterType
{
    Simple,      // Field operator value
    Advanced,    // SQL-like expression
    Compound     // Multiple filters with AND/OR
}

/// <summary>
/// Filter operator enum
/// </summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    IsNull,
    IsNotNull,
    In,
    NotIn,
    Between
}

/// <summary>
/// Logical operator for compound filters
/// </summary>
public enum LogicalOperator
{
    And,
    Or
}

/// <summary>
/// Sort configuration
/// </summary>
public class SortConfig
{
    /// <summary>
    /// Sort columns (in order)
    /// </summary>
    public List<SortColumn> Columns { get; set; } = new();
}

/// <summary>
/// Sort column specification
/// </summary>
public class SortColumn
{
    /// <summary>
    /// Field name to sort by
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// Sort direction
    /// </summary>
    public SortDirection Direction { get; set; } = SortDirection.Ascending;

    /// <summary>
    /// Sort priority (1 = primary sort)
    /// </summary>
    public int Priority { get; set; } = 1;
}

/// <summary>
/// Summary aggregation configuration
/// </summary>
public class SummaryConfig
{
    /// <summary>
    /// Field to aggregate
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// Aggregation function
    /// </summary>
    public AggregateFunction Function { get; set; } = AggregateFunction.Count;

    /// <summary>
    /// Display label
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Format string for the result
    /// </summary>
    public string? Format { get; set; }
}

/// <summary>
/// Aggregate function enum
/// </summary>
public enum AggregateFunction
{
    Count,
    Sum,
    Average,
    Min,
    Max,
    First,
    Last,
    StdDev,
    Variance
}

/// <summary>
/// Conditional formatting rule
/// </summary>
public class ConditionalFormat
{
    /// <summary>
    /// Condition to evaluate
    /// </summary>
    public required string Condition { get; set; }

    /// <summary>
    /// CSS class to apply when condition is true
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Background color (hex or CSS color)
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// Text color (hex or CSS color)
    /// </summary>
    public string? TextColor { get; set; }

    /// <summary>
    /// Icon to display
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Font weight (normal, bold, etc.)
    /// </summary>
    public string? FontWeight { get; set; }
}

/// <summary>
/// Selection mode for the table
/// </summary>
public enum SelectionMode
{
    None,
    Single,
    Multiple
}

/// <summary>
/// Export format enum
/// </summary>
public enum ExportFormat
{
    CSV,
    Excel,
    JSON,
    GeoJSON
}

/// <summary>
/// Feature record for attribute table
/// </summary>
public class FeatureRecord
{
    /// <summary>
    /// Feature ID
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Feature properties (attributes)
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// Geometry (GeoJSON format)
    /// </summary>
    public object? Geometry { get; set; }

    /// <summary>
    /// Geometry type (Point, LineString, Polygon, etc.)
    /// </summary>
    public string? GeometryType { get; set; }

    /// <summary>
    /// Whether this record is selected
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Whether this record is highlighted
    /// </summary>
    public bool IsHighlighted { get; set; }

    /// <summary>
    /// Layer ID this feature belongs to
    /// </summary>
    public string? LayerId { get; set; }
}

/// <summary>
/// Filter preset for saving and loading filter configurations
/// </summary>
public class FilterPreset
{
    /// <summary>
    /// Preset ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Preset name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Preset description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Filter configuration
    /// </summary>
    public required FilterConfig Filter { get; set; }

    /// <summary>
    /// Created date
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified date
    /// </summary>
    public DateTime Modified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this is a shared preset
    /// </summary>
    public bool IsShared { get; set; }

    /// <summary>
    /// Creator user ID
    /// </summary>
    public string? CreatedBy { get; set; }
}

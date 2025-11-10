// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Base class for all filter definitions
/// </summary>
public abstract class FilterDefinition
{
    /// <summary>
    /// Unique identifier for this filter
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name for the filter
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Type of filter
    /// </summary>
    public abstract FilterType Type { get; }

    /// <summary>
    /// Whether this filter is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Converts filter to a MapLibre filter expression
    /// </summary>
    public abstract object ToExpression();
}

/// <summary>
/// Spatial filter - filters features based on geographic location
/// </summary>
public class SpatialFilter : FilterDefinition
{
    public override FilterType Type => FilterType.Spatial;

    /// <summary>
    /// Type of spatial filter
    /// </summary>
    public SpatialFilterType SpatialType { get; set; } = SpatialFilterType.BoundingBox;

    /// <summary>
    /// Bounding box coordinates [west, south, east, north]
    /// </summary>
    public double[]? BoundingBox { get; set; }

    /// <summary>
    /// Center point for circle or distance filters [longitude, latitude]
    /// </summary>
    public double[]? Center { get; set; }

    /// <summary>
    /// Radius in meters for circle filters
    /// </summary>
    public double? Radius { get; set; }

    /// <summary>
    /// Polygon coordinates for polygon filters
    /// </summary>
    public List<double[]>? Polygon { get; set; }

    /// <summary>
    /// Distance in meters for "within distance" filters
    /// </summary>
    public double? Distance { get; set; }

    public override object ToExpression()
    {
        return SpatialType switch
        {
            SpatialFilterType.BoundingBox => new
            {
                type = "bbox",
                bbox = BoundingBox
            },
            SpatialFilterType.Circle => new
            {
                type = "circle",
                center = Center,
                radius = Radius
            },
            SpatialFilterType.Polygon => new
            {
                type = "polygon",
                coordinates = Polygon
            },
            SpatialFilterType.WithinDistance => new
            {
                type = "distance",
                center = Center,
                distance = Distance
            },
            _ => throw new NotSupportedException($"Spatial filter type {SpatialType} not supported")
        };
    }

    public override string ToString()
    {
        return SpatialType switch
        {
            SpatialFilterType.BoundingBox => "Within map extent",
            SpatialFilterType.Circle => $"Within {Radius}m radius",
            SpatialFilterType.Polygon => "Within drawn polygon",
            SpatialFilterType.WithinDistance => $"Within {Distance}m of point",
            _ => "Spatial filter"
        };
    }
}

/// <summary>
/// Attribute filter - filters features based on property values
/// </summary>
public class AttributeFilter : FilterDefinition
{
    public override FilterType Type => FilterType.Attribute;

    /// <summary>
    /// Field name to filter on
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Filter operator
    /// </summary>
    public AttributeOperator Operator { get; set; } = AttributeOperator.Equals;

    /// <summary>
    /// Value to compare against
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Multiple values for IN/NOT IN operators
    /// </summary>
    public List<object>? Values { get; set; }

    /// <summary>
    /// Data type of the field
    /// </summary>
    public FieldType FieldType { get; set; } = FieldType.String;

    public override object ToExpression()
    {
        // Convert to MapLibre filter expression format
        return Operator switch
        {
            AttributeOperator.Equals => new object[] { "==", new object[] { "get", Field }, Value ?? "" },
            AttributeOperator.NotEquals => new object[] { "!=", new object[] { "get", Field }, Value ?? "" },
            AttributeOperator.GreaterThan => new object[] { ">", new object[] { "get", Field }, Value ?? 0 },
            AttributeOperator.LessThan => new object[] { "<", new object[] { "get", Field }, Value ?? 0 },
            AttributeOperator.GreaterThanOrEqual => new object[] { ">=", new object[] { "get", Field }, Value ?? 0 },
            AttributeOperator.LessThanOrEqual => new object[] { "<=", new object[] { "get", Field }, Value ?? 0 },
            AttributeOperator.Contains => new object[] { "in", Value ?? "", new object[] { "get", Field } },
            AttributeOperator.StartsWith => new object[] { "^=", new object[] { "get", Field }, Value ?? "" },
            AttributeOperator.EndsWith => new object[] { "$=", new object[] { "get", Field }, Value ?? "" },
            AttributeOperator.In => new object[] { "in", new object[] { "get", Field }, Values?.ToArray() ?? Array.Empty<object>() },
            AttributeOperator.NotIn => new object[] { "!", new object[] { "in", new object[] { "get", Field }, Values?.ToArray() ?? Array.Empty<object>() } },
            AttributeOperator.IsNull => new object[] { "==", new object[] { "get", Field }, null },
            AttributeOperator.IsNotNull => new object[] { "!=", new object[] { "get", Field }, null },
            _ => throw new NotSupportedException($"Operator {Operator} not supported")
        };
    }

    public override string ToString()
    {
        var operatorSymbol = Operator switch
        {
            AttributeOperator.Equals => "=",
            AttributeOperator.NotEquals => "≠",
            AttributeOperator.GreaterThan => ">",
            AttributeOperator.LessThan => "<",
            AttributeOperator.GreaterThanOrEqual => "≥",
            AttributeOperator.LessThanOrEqual => "≤",
            AttributeOperator.Contains => "contains",
            AttributeOperator.StartsWith => "starts with",
            AttributeOperator.EndsWith => "ends with",
            AttributeOperator.In => "in",
            AttributeOperator.NotIn => "not in",
            AttributeOperator.IsNull => "is null",
            AttributeOperator.IsNotNull => "is not null",
            _ => "?"
        };

        if (Operator == AttributeOperator.IsNull || Operator == AttributeOperator.IsNotNull)
        {
            return $"{Label ?? Field} {operatorSymbol}";
        }
        else if (Operator == AttributeOperator.In || Operator == AttributeOperator.NotIn)
        {
            var valueList = Values != null ? string.Join(", ", Values.Take(3)) : "";
            if (Values?.Count > 3)
            {
                valueList += $" (+{Values.Count - 3} more)";
            }
            return $"{Label ?? Field} {operatorSymbol} [{valueList}]";
        }
        else
        {
            return $"{Label ?? Field} {operatorSymbol} {Value}";
        }
    }
}

/// <summary>
/// Temporal filter - filters features based on date/time
/// </summary>
public class TemporalFilter : FilterDefinition
{
    public override FilterType Type => FilterType.Temporal;

    /// <summary>
    /// Date field to filter on
    /// </summary>
    public string DateField { get; set; } = string.Empty;

    /// <summary>
    /// Type of temporal filter
    /// </summary>
    public TemporalFilterType TemporalType { get; set; } = TemporalFilterType.Between;

    /// <summary>
    /// Start date
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Number of days/weeks/months for relative filters
    /// </summary>
    public int? RelativeValue { get; set; }

    /// <summary>
    /// Unit for relative filters
    /// </summary>
    public RelativeTimeUnit? RelativeUnit { get; set; }

    public override object ToExpression()
    {
        // Calculate actual dates for relative filters
        if (TemporalType == TemporalFilterType.LastN && RelativeValue.HasValue && RelativeUnit.HasValue)
        {
            EndDate = DateTime.UtcNow;
            StartDate = RelativeUnit.Value switch
            {
                RelativeTimeUnit.Days => DateTime.UtcNow.AddDays(-RelativeValue.Value),
                RelativeTimeUnit.Weeks => DateTime.UtcNow.AddDays(-RelativeValue.Value * 7),
                RelativeTimeUnit.Months => DateTime.UtcNow.AddMonths(-RelativeValue.Value),
                RelativeTimeUnit.Years => DateTime.UtcNow.AddYears(-RelativeValue.Value),
                _ => DateTime.UtcNow
            };
        }

        return TemporalType switch
        {
            TemporalFilterType.Before => new object[] { "<", new object[] { "get", DateField }, StartDate?.ToString("o") ?? "" },
            TemporalFilterType.After => new object[] { ">", new object[] { "get", DateField }, StartDate?.ToString("o") ?? "" },
            TemporalFilterType.Between or TemporalFilterType.LastN => new object[]
            {
                "all",
                new object[] { ">=", new object[] { "get", DateField }, StartDate?.ToString("o") ?? "" },
                new object[] { "<=", new object[] { "get", DateField }, EndDate?.ToString("o") ?? "" }
            },
            _ => throw new NotSupportedException($"Temporal filter type {TemporalType} not supported")
        };
    }

    public override string ToString()
    {
        return TemporalType switch
        {
            TemporalFilterType.Before => $"Before {StartDate:yyyy-MM-dd}",
            TemporalFilterType.After => $"After {StartDate:yyyy-MM-dd}",
            TemporalFilterType.Between => $"{StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            TemporalFilterType.LastN => $"Last {RelativeValue} {RelativeUnit?.ToString().ToLower()}",
            _ => "Temporal filter"
        };
    }
}

/// <summary>
/// Type of spatial filter
/// </summary>
public enum SpatialFilterType
{
    BoundingBox,
    Circle,
    Polygon,
    WithinDistance
}

/// <summary>
/// Attribute filter operators
/// </summary>
public enum AttributeOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
    StartsWith,
    EndsWith,
    In,
    NotIn,
    IsNull,
    IsNotNull
}

/// <summary>
/// Type of temporal filter
/// </summary>
public enum TemporalFilterType
{
    Before,
    After,
    Between,
    LastN
}

/// <summary>
/// Time unit for relative temporal filters
/// </summary>
public enum RelativeTimeUnit
{
    Days,
    Weeks,
    Months,
    Years
}

/// <summary>
/// Field data type
/// </summary>
public enum FieldType
{
    String,
    Number,
    Date,
    Boolean
}

/// <summary>
/// Filter type enum (from MapMessages.cs)
/// </summary>
public enum FilterType
{
    Spatial,
    Attribute,
    Temporal
}

/// <summary>
/// Predefined field configuration for attribute filters
/// </summary>
public class FilterFieldConfig
{
    public string Field { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public FieldType Type { get; set; } = FieldType.String;
    public List<object>? PredefinedValues { get; set; }
}

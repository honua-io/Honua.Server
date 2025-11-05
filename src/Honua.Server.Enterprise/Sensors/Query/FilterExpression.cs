namespace Honua.Server.Enterprise.Sensors.Query;

/// <summary>
/// Base class for filter expressions in OData-style queries.
/// </summary>
public abstract record FilterExpression;

/// <summary>
/// Represents a comparison expression (e.g., "result gt 20.0").
/// </summary>
public sealed record ComparisonExpression : FilterExpression
{
    /// <summary>
    /// The property name to compare.
    /// </summary>
    public string Property { get; init; } = default!;

    /// <summary>
    /// The comparison operator.
    /// Values: "eq", "ne", "gt", "ge", "lt", "le"
    /// </summary>
    public string Operator { get; init; } = default!;

    /// <summary>
    /// The value to compare against.
    /// </summary>
    public object Value { get; init; } = default!;
}

/// <summary>
/// Represents a logical expression combining multiple filters (e.g., "result gt 20 and phenomenonTime gt 2025-01-01").
/// </summary>
public sealed record LogicalExpression : FilterExpression
{
    /// <summary>
    /// The logical operator.
    /// Values: "and", "or", "not"
    /// </summary>
    public string Operator { get; init; } = default!;

    /// <summary>
    /// The left operand.
    /// </summary>
    public FilterExpression? Left { get; init; }

    /// <summary>
    /// The right operand.
    /// </summary>
    public FilterExpression? Right { get; init; }
}

/// <summary>
/// Represents a function call expression (e.g., "geo.intersects(location, geometry'...')").
/// </summary>
public sealed record FunctionExpression : FilterExpression
{
    /// <summary>
    /// The function name.
    /// Common functions: "substringof", "startswith", "endswith", "geo.intersects", "geo.distance"
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// The function arguments.
    /// </summary>
    public IReadOnlyList<object> Arguments { get; init; } = Array.Empty<object>();
}

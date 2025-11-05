namespace Honua.Server.Enterprise.Sensors.Query;

/// <summary>
/// Base class for filter expressions in OData-style queries.
/// </summary>
public abstract record FilterExpression;

/// <summary>
/// Comparison operators for filter expressions.
/// </summary>
public enum ComparisonOperator
{
    /// <summary>
    /// Equals (eq)
    /// </summary>
    Equals,

    /// <summary>
    /// Not equals (ne)
    /// </summary>
    NotEquals,

    /// <summary>
    /// Greater than (gt)
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Greater than or equal (ge)
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// Less than (lt)
    /// </summary>
    LessThan,

    /// <summary>
    /// Less than or equal (le)
    /// </summary>
    LessThanOrEqual
}

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
    /// </summary>
    public ComparisonOperator Operator { get; init; }

    /// <summary>
    /// The value to compare against.
    /// </summary>
    public object Value { get; init} = default!;
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

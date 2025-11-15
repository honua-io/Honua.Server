// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Host.Filtering;

/// <summary>
/// Extension methods for applying FilterExpression to IQueryable data sources.
/// Converts parsed filter expressions into LINQ expressions for efficient database queries.
/// </summary>
/// <remarks>
/// <para>
/// These extensions convert FilterExpression trees into compiled LINQ expressions that
/// can be executed by Entity Framework Core, LINQ to SQL, or other IQueryable providers.
/// The generated expressions are optimized for database execution and support:
/// </para>
/// <list type="bullet">
/// <item><description>Type-safe property access with reflection</description></item>
/// <item><description>Automatic type conversion for values</description></item>
/// <item><description>Case-insensitive string comparisons (configurable)</description></item>
/// <item><description>Null-safe comparisons</description></item>
/// <item><description>Translation to SQL WHERE clauses</description></item>
/// </list>
/// <para>
/// <b>Example Usage:</b>
/// </para>
/// <code>
/// var query = dbContext.Shares.AsQueryable();
/// var parser = new FilterExpressionParser();
/// var filter = parser.Parse("createdAt gt 2025-01-01 and status eq 'active'");
/// var filteredQuery = query.ApplyFilter(filter);
/// var results = await filteredQuery.ToListAsync();
/// </code>
/// <para>
/// <b>Performance Considerations:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Filters are translated to SQL WHERE clauses for efficient execution</description></item>
/// <item><description>Indexes on filtered properties improve query performance</description></item>
/// <item><description>String functions (contains, startswith) may not use indexes efficiently</description></item>
/// <item><description>Avoid filtering on computed properties (use database columns)</description></item>
/// </list>
/// </remarks>
public static class FilterQueryableExtensions
{
    /// <summary>
    /// Applies a FilterExpression to an IQueryable, returning a filtered query.
    /// </summary>
    /// <typeparam name="T">The entity type being queried.</typeparam>
    /// <param name="query">The IQueryable to filter.</param>
    /// <param name="filter">The filter expression to apply.</param>
    /// <returns>A filtered IQueryable with the WHERE clause applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when query or filter is null.</exception>
    /// <exception cref="FilterApplicationException">
    /// Thrown when the filter cannot be applied (e.g., property doesn't exist, type mismatch).
    /// </exception>
    /// <remarks>
    /// <b>Example:</b>
    /// <code>
    /// // Parse filter
    /// var parser = new FilterExpressionParser();
    /// var filter = parser.Parse("age gt 18 and status eq 'active'");
    ///
    /// // Apply to query
    /// var query = dbContext.Users.AsQueryable();
    /// var filtered = query.ApplyFilter(filter);
    ///
    /// // Execute query
    /// var users = await filtered.ToListAsync();
    /// </code>
    /// <para>
    /// <b>Generated SQL Example:</b>
    /// </para>
    /// <code>
    /// SELECT * FROM Users
    /// WHERE Age > 18 AND Status = 'active'
    /// </code>
    /// </remarks>
    public static IQueryable<T> ApplyFilter<T>(
        this IQueryable<T> query,
        FilterExpression filter)
    {
        Guard.NotNull(query);
        Guard.NotNull(filter);

        try
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var predicate = BuildPredicate(filter, parameter);
            var lambda = Expression.Lambda<Func<T, bool>>(predicate, parameter);

            return query.Where(lambda);
        }
        catch (Exception ex) when (ex is not FilterApplicationException)
        {
            throw new FilterApplicationException(
                $"Failed to apply filter to type {typeof(T).Name}: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Builds a LINQ expression predicate from a FilterExpression.
    /// </summary>
    private static Expression BuildPredicate(FilterExpression filter, ParameterExpression parameter)
    {
        return filter switch
        {
            ComparisonExpression comparison => BuildComparisonExpression(comparison, parameter),
            LogicalExpression logical => BuildLogicalExpression(logical, parameter),
            NotExpression not => BuildNotExpression(not, parameter),
            StringFunctionExpression stringFunc => BuildStringFunctionExpression(stringFunc, parameter),
            _ => throw new FilterApplicationException($"Unsupported filter expression type: {filter.GetType().Name}")
        };
    }

    /// <summary>
    /// Builds a comparison expression (eq, ne, gt, ge, lt, le).
    /// </summary>
    private static Expression BuildComparisonExpression(
        ComparisonExpression comparison,
        ParameterExpression parameter)
    {
        var property = GetPropertyExpression(parameter, comparison.Property);
        var value = ConvertValue(comparison.Value, property.Type);
        var constant = Expression.Constant(value, property.Type);

        return comparison.Operator switch
        {
            ComparisonOperator.Eq => Expression.Equal(property, constant),
            ComparisonOperator.Ne => Expression.NotEqual(property, constant),
            ComparisonOperator.Gt => Expression.GreaterThan(property, constant),
            ComparisonOperator.Ge => Expression.GreaterThanOrEqual(property, constant),
            ComparisonOperator.Lt => Expression.LessThan(property, constant),
            ComparisonOperator.Le => Expression.LessThanOrEqual(property, constant),
            _ => throw new FilterApplicationException($"Unsupported comparison operator: {comparison.Operator}")
        };
    }

    /// <summary>
    /// Builds a logical expression (and, or).
    /// </summary>
    private static Expression BuildLogicalExpression(
        LogicalExpression logical,
        ParameterExpression parameter)
    {
        var left = BuildPredicate(logical.Left, parameter);
        var right = BuildPredicate(logical.Right, parameter);

        return logical.Operator switch
        {
            LogicalOperator.And => Expression.AndAlso(left, right),
            LogicalOperator.Or => Expression.OrElse(left, right),
            _ => throw new FilterApplicationException($"Unsupported logical operator: {logical.Operator}")
        };
    }

    /// <summary>
    /// Builds a NOT expression.
    /// </summary>
    private static Expression BuildNotExpression(
        NotExpression not,
        ParameterExpression parameter)
    {
        var expression = BuildPredicate(not.Expression, parameter);
        return Expression.Not(expression);
    }

    /// <summary>
    /// Builds a string function expression (contains, startswith, endswith).
    /// </summary>
    private static Expression BuildStringFunctionExpression(
        StringFunctionExpression stringFunc,
        ParameterExpression parameter)
    {
        var property = GetPropertyExpression(parameter, stringFunc.Property);

        // Ensure property is a string
        if (property.Type != typeof(string))
        {
            throw new FilterApplicationException(
                $"Property '{stringFunc.Property}' is not a string (type: {property.Type.Name})");
        }

        var value = Expression.Constant(stringFunc.Value, typeof(string));

        // Get the appropriate string method
        MethodInfo? method = stringFunc.Function switch
        {
            StringFunction.Contains => typeof(string).GetMethod(
                nameof(string.Contains),
                new[] { typeof(string), typeof(StringComparison) }),
            StringFunction.StartsWith => typeof(string).GetMethod(
                nameof(string.StartsWith),
                new[] { typeof(string), typeof(StringComparison) }),
            StringFunction.EndsWith => typeof(string).GetMethod(
                nameof(string.EndsWith),
                new[] { typeof(string), typeof(StringComparison) }),
            _ => throw new FilterApplicationException($"Unsupported string function: {stringFunc.Function}")
        };

        if (method == null)
        {
            throw new FilterApplicationException(
                $"Failed to find method for string function: {stringFunc.Function}");
        }

        // Use OrdinalIgnoreCase for case-insensitive comparison
        var comparisonType = Expression.Constant(StringComparison.OrdinalIgnoreCase);

        // Handle null property values - return false if property is null
        var nullCheck = Expression.Equal(property, Expression.Constant(null, typeof(string)));
        var methodCall = Expression.Call(property, method, value, comparisonType);
        var result = Expression.Condition(
            nullCheck,
            Expression.Constant(false),
            methodCall
        );

        return result;
    }

    /// <summary>
    /// Gets a property expression from a parameter, supporting nested properties with dot notation.
    /// </summary>
    /// <param name="parameter">The parameter expression (e.g., x in x => x.Property).</param>
    /// <param name="propertyPath">
    /// The property path (supports dot notation for nested properties, e.g., "User.FirstName").
    /// </param>
    /// <returns>A member expression representing the property access.</returns>
    /// <exception cref="FilterApplicationException">
    /// Thrown when the property doesn't exist on the type.
    /// </exception>
    private static MemberExpression GetPropertyExpression(
        ParameterExpression parameter,
        string propertyPath)
    {
        Expression expression = parameter;
        var type = parameter.Type;

        // Handle nested properties (e.g., "User.FirstName")
        var parts = propertyPath.Split('.');

        foreach (var part in parts)
        {
            var property = type.GetProperty(
                part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null)
            {
                throw new FilterApplicationException(
                    $"Property '{part}' not found on type '{type.Name}'. " +
                    $"Available properties: {string.Join(", ", type.GetProperties().Select(p => p.Name))}");
            }

            expression = Expression.Property(expression, property);
            type = property.PropertyType;
        }

        return (MemberExpression)expression;
    }

    /// <summary>
    /// Converts a value object to the target type, handling common conversions.
    /// </summary>
    private static object? ConvertValue(object value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // If types already match, no conversion needed
        if (value.GetType() == underlyingType)
        {
            return value;
        }

        try
        {
            // Handle DateTime conversions
            if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
            {
                if (value is string dateString)
                {
                    if (DateTime.TryParse(dateString, out var dateValue))
                    {
                        return underlyingType == typeof(DateTimeOffset)
                            ? new DateTimeOffset(dateValue, TimeSpan.Zero)
                            : (object)dateValue;
                    }
                }
                else if (value is DateTime dt)
                {
                    return underlyingType == typeof(DateTimeOffset)
                        ? new DateTimeOffset(dt, TimeSpan.Zero)
                        : (object)dt;
                }
            }

            // Handle Guid conversions
            if (underlyingType == typeof(Guid) && value is string guidString)
            {
                if (Guid.TryParse(guidString, out var guidValue))
                {
                    return guidValue;
                }
            }

            // Handle enum conversions
            if (underlyingType.IsEnum && value is string enumString)
            {
                if (Enum.TryParse(underlyingType, enumString, ignoreCase: true, out var enumValue))
                {
                    return enumValue;
                }
            }

            // Use Convert.ChangeType for standard conversions
            return Convert.ChangeType(value, underlyingType);
        }
        catch (Exception ex)
        {
            throw new FilterApplicationException(
                $"Cannot convert value '{value}' (type: {value.GetType().Name}) to type '{targetType.Name}'",
                ex);
        }
    }
}

/// <summary>
/// Exception thrown when applying a filter expression to a query fails.
/// </summary>
/// <remarks>
/// This exception indicates that the filter expression is syntactically valid but cannot
/// be applied to the target entity type. Common causes include:
/// <list type="bullet">
/// <item><description>Property doesn't exist on the entity type</description></item>
/// <item><description>Type mismatch between filter value and property type</description></item>
/// <item><description>Unsupported operation on the property type</description></item>
/// </list>
/// <para>
/// This exception should be caught and converted to a 400 Bad Request response.
/// </para>
/// </remarks>
public sealed class FilterApplicationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FilterApplicationException"/> class.
    /// </summary>
    /// <param name="message">The error message describing why the filter cannot be applied.</param>
    public FilterApplicationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterApplicationException"/> class.
    /// </summary>
    /// <param name="message">The error message describing why the filter cannot be applied.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public FilterApplicationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Data;
using Honua.Server.Core.OData.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;

namespace Honua.Server.Core.OData;

/// <summary>
/// Adapts OData query options to Honua FeatureQuery model.
/// Converts OData filter expressions to Honua query expressions.
/// </summary>
public static class ODataQueryAdapter
{
    /// <summary>
    /// Converts OData QueryOptions to FeatureQuery for use with IFeatureRepository.
    /// </summary>
    public static FeatureQuery ToFeatureQuery(QueryOptions options)
    {
        var sortOrders = options.OrderBy != null
            ? options.OrderBy.Select(o => new FeatureSortOrder(
                o.Property,
                o.Direction == SortDirection.Ascending ? FeatureSortDirection.Ascending : FeatureSortDirection.Descending
            )).ToList()
            : null;

        var propertyNames = options.Select?.ToList();

        var filter = options.Filter != null
            ? new QueryFilter(ConvertFilterExpression(options.Filter))
            : null;

        return new FeatureQuery(
            Limit: options.Top,
            Offset: options.Skip,
            PropertyNames: propertyNames,
            SortOrders: sortOrders,
            Filter: filter
        );
    }

    /// <summary>
    /// Converts OData FilterExpression to Honua QueryExpression.
    /// Maps OData filter syntax to Honua's expression tree model.
    /// </summary>
    private static QueryExpression? ConvertFilterExpression(FilterExpression? filterExpr)
    {
        if (filterExpr == null)
            return null;

        return filterExpr switch
        {
            ComparisonExpression comparison => ConvertComparisonExpression(comparison),
            LogicalExpression logical => ConvertLogicalExpression(logical),
            FunctionExpression func => ConvertFunctionExpression(func),
            _ => throw new NotSupportedException($"Unsupported filter expression type: {filterExpr.GetType().Name}")
        };
    }

    private static QueryExpression ConvertComparisonExpression(ComparisonExpression comparison)
    {
        var left = new QueryFieldReference(comparison.Property);
        var right = new QueryConstant(comparison.Value);

        var op = comparison.Operator switch
        {
            ComparisonOperator.Equals => QueryBinaryOperator.Equal,
            ComparisonOperator.NotEquals => QueryBinaryOperator.NotEqual,
            ComparisonOperator.GreaterThan => QueryBinaryOperator.GreaterThan,
            ComparisonOperator.GreaterThanOrEqual => QueryBinaryOperator.GreaterThanOrEqual,
            ComparisonOperator.LessThan => QueryBinaryOperator.LessThan,
            ComparisonOperator.LessThanOrEqual => QueryBinaryOperator.LessThanOrEqual,
            _ => throw new NotSupportedException($"Unsupported comparison operator: {comparison.Operator}")
        };

        return new QueryBinaryExpression(left, op, right);
    }

    private static QueryExpression ConvertLogicalExpression(LogicalExpression logical)
    {
        var op = logical.Operator.ToLowerInvariant() switch
        {
            "and" => QueryBinaryOperator.And,
            "or" => QueryBinaryOperator.Or,
            _ => throw new NotSupportedException($"Unsupported logical operator: {logical.Operator}")
        };

        var left = ConvertFilterExpression(logical.Left);
        var right = ConvertFilterExpression(logical.Right);

        if (left == null || right == null)
            throw new InvalidOperationException("Logical expression has null operand");

        return new QueryBinaryExpression(left, op, right);
    }

    private static QueryExpression ConvertFunctionExpression(FunctionExpression func)
    {
        // Convert arguments from object to QueryExpression
        var args = func.Arguments.Select(arg =>
        {
            if (arg is FilterExpression filterExpr)
                return ConvertFilterExpression(filterExpr);
            else if (arg is string str)
                return new QueryFieldReference(str);
            else
                return new QueryConstant(arg);
        }).Where(e => e != null).Cast<QueryExpression>().ToList();

        // Map OData function names to Honua function names
        var functionName = func.Name.ToLowerInvariant() switch
        {
            "contains" => "contains",
            "startswith" => "startswith",
            "endswith" => "endswith",
            "tolower" => "tolower",
            "toupper" => "toupper",
            "trim" => "trim",
            "concat" => "concat",
            "substring" => "substring",
            "indexof" => "indexof",
            "length" => "length",
            "year" => "year",
            "month" => "month",
            "day" => "day",
            "hour" => "hour",
            "minute" => "minute",
            "second" => "second",
            "round" => "round",
            "floor" => "floor",
            "ceiling" => "ceiling",
            "geo.intersects" => "st_intersects",
            "geo.distance" => "st_distance",
            "geo.length" => "st_length",
            _ => func.Name.ToLowerInvariant()
        };

        return new QueryFunctionExpression(functionName, args);
    }
}

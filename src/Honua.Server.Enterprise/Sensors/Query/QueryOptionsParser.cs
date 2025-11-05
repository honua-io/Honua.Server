namespace Honua.Server.Enterprise.Sensors.Query;

/// <summary>
/// Parses OData-style query parameters into QueryOptions model.
/// Supports $filter, $expand, $select, $orderby, $top, $skip, $count.
/// </summary>
public static class QueryOptionsParser
{
    public static QueryOptions Parse(
        string? filter,
        string? expand,
        string? select,
        string? orderby,
        int? top,
        int? skip,
        bool count)
    {
        return new QueryOptions
        {
            Filter = ParseFilter(filter),
            Expand = ParseExpand(expand),
            Select = ParseSelect(select),
            OrderBy = ParseOrderBy(orderby),
            Top = top,
            Skip = skip,
            Count = count
        };
    }

    private static FilterExpression? ParseFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        // Simple filter parser - supports basic equality and comparison operations
        // For production, consider using a proper OData parser library

        // Example: "name eq 'Temperature Sensor'"
        var parts = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return null;

        var property = parts[0];
        var operatorStr = parts[1];
        var value = string.Join(' ', parts.Skip(2)).Trim('\'', '"');

        var @operator = operatorStr.ToLowerInvariant() switch
        {
            "eq" => ComparisonOperator.Equals,
            "ne" => ComparisonOperator.NotEquals,
            "gt" => ComparisonOperator.GreaterThan,
            "ge" => ComparisonOperator.GreaterThanOrEqual,
            "lt" => ComparisonOperator.LessThan,
            "le" => ComparisonOperator.LessThanOrEqual,
            _ => ComparisonOperator.Equals
        };

        return new FilterExpression
        {
            Property = property,
            Operator = @operator,
            Value = value
        };
    }

    private static ExpandOptions? ParseExpand(string? expand)
    {
        if (string.IsNullOrWhiteSpace(expand))
            return null;

        return ExpandOptions.Parse(expand);
    }

    private static IReadOnlyList<string>? ParseSelect(string? select)
    {
        if (string.IsNullOrWhiteSpace(select))
            return null;

        return select.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();
    }

    private static IReadOnlyList<OrderBy>? ParseOrderBy(string? orderby)
    {
        if (string.IsNullOrWhiteSpace(orderby))
            return null;

        var parts = orderby.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var orderByList = new List<OrderBy>();

        foreach (var part in parts)
        {
            var tokens = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var property = tokens[0];
            var direction = tokens.Length > 1 && tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? SortDirection.Descending
                : SortDirection.Ascending;

            orderByList.Add(new OrderBy
            {
                Property = property,
                Direction = direction
            });
        }

        return orderByList;
    }
}

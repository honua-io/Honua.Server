// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Helpers;

/// <summary>
/// Helper class for applying column-specific filters to data collections.
/// </summary>
public static class FilterHelper
{
    /// <summary>
    /// Applies a list of column filters to a data collection.
    /// </summary>
    public static IEnumerable<T> ApplyFilters<T>(IEnumerable<T> data, List<ColumnFilter> filters, Dictionary<string, Func<T, object?>> propertySelectors)
    {
        var result = data;

        foreach (var filter in filters)
        {
            if (!propertySelectors.TryGetValue(filter.Column, out var selector))
            {
                continue; // Skip unknown columns
            }

            result = ApplySingleFilter(result, selector, filter);
        }

        return result;
    }

    private static IEnumerable<T> ApplySingleFilter<T>(IEnumerable<T> data, Func<T, object?> selector, ColumnFilter filter)
    {
        return filter.Operator switch
        {
            FilterOperator.Equals => data.Where(x => EqualsComparison(selector(x), filter.Value)),
            FilterOperator.NotEquals => data.Where(x => !EqualsComparison(selector(x), filter.Value)),
            FilterOperator.Contains => data.Where(x => ContainsComparison(selector(x), filter.Value)),
            FilterOperator.NotContains => data.Where(x => !ContainsComparison(selector(x), filter.Value)),
            FilterOperator.StartsWith => data.Where(x => StartsWithComparison(selector(x), filter.Value)),
            FilterOperator.EndsWith => data.Where(x => EndsWithComparison(selector(x), filter.Value)),
            FilterOperator.GreaterThan => data.Where(x => GreaterThanComparison(selector(x), filter.Value)),
            FilterOperator.LessThan => data.Where(x => LessThanComparison(selector(x), filter.Value)),
            FilterOperator.GreaterThanOrEqual => data.Where(x => GreaterThanOrEqualComparison(selector(x), filter.Value)),
            FilterOperator.LessThanOrEqual => data.Where(x => LessThanOrEqualComparison(selector(x), filter.Value)),
            FilterOperator.Between => data.Where(x => BetweenComparison(selector(x), filter.Value, filter.SecondValue)),
            FilterOperator.IsNull => data.Where(x => selector(x) == null || string.IsNullOrWhiteSpace(selector(x)?.ToString())),
            FilterOperator.IsNotNull => data.Where(x => selector(x) != null && !string.IsNullOrWhiteSpace(selector(x)?.ToString())),
            _ => data
        };
    }

    private static bool EqualsComparison(object? value, string? filterValue)
    {
        if (value == null || filterValue == null)
            return value == null && filterValue == null;

        return value.ToString()?.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool ContainsComparison(object? value, string? filterValue)
    {
        if (value == null || filterValue == null)
            return false;

        return value.ToString()?.Contains(filterValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool StartsWithComparison(object? value, string? filterValue)
    {
        if (value == null || filterValue == null)
            return false;

        return value.ToString()?.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool EndsWithComparison(object? value, string? filterValue)
    {
        if (value == null || filterValue == null)
            return false;

        return value.ToString()?.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool GreaterThanComparison(object? value, string? filterValue)
    {
        if (value == null || filterValue == null)
            return false;

        // Try numeric comparison
        if (TryParseNumeric(value.ToString(), out var numValue) && TryParseNumeric(filterValue, out var numFilter))
        {
            return numValue > numFilter;
        }

        // Try date comparison
        if (DateTime.TryParse(value.ToString(), out var dateValue) && DateTime.TryParse(filterValue, out var dateFilter))
        {
            return dateValue > dateFilter;
        }

        // Fallback to string comparison
        return string.Compare(value.ToString(), filterValue, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static bool LessThanComparison(object? value, string? filterValue)
    {
        if (value == null || filterValue == null)
            return false;

        // Try numeric comparison
        if (TryParseNumeric(value.ToString(), out var numValue) && TryParseNumeric(filterValue, out var numFilter))
        {
            return numValue < numFilter;
        }

        // Try date comparison
        if (DateTime.TryParse(value.ToString(), out var dateValue) && DateTime.TryParse(filterValue, out var dateFilter))
        {
            return dateValue < dateFilter;
        }

        // Fallback to string comparison
        return string.Compare(value.ToString(), filterValue, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static bool GreaterThanOrEqualComparison(object? value, string? filterValue)
    {
        return GreaterThanComparison(value, filterValue) || EqualsComparison(value, filterValue);
    }

    private static bool LessThanOrEqualComparison(object? value, string? filterValue)
    {
        return LessThanComparison(value, filterValue) || EqualsComparison(value, filterValue);
    }

    private static bool BetweenComparison(object? value, string? minValue, string? maxValue)
    {
        if (value == null || minValue == null || maxValue == null)
            return false;

        // Try numeric comparison
        if (TryParseNumeric(value.ToString(), out var numValue)
            && TryParseNumeric(minValue, out var numMin)
            && TryParseNumeric(maxValue, out var numMax))
        {
            return numValue >= numMin && numValue <= numMax;
        }

        // Try date comparison
        if (DateTime.TryParse(value.ToString(), out var dateValue)
            && DateTime.TryParse(minValue, out var dateMin)
            && DateTime.TryParse(maxValue, out var dateMax))
        {
            return dateValue >= dateMin && dateValue <= dateMax;
        }

        return false;
    }

    private static bool TryParseNumeric(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return double.TryParse(value, out result);
    }
}

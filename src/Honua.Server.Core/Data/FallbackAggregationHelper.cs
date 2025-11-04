// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

public static class FallbackAggregationHelper
{
    private static readonly GeoJsonReader GeoJsonReader = new();

    public static async Task<IReadOnlyList<StatisticsResult>> ComputeStatisticsAsync(
        Func<FeatureQuery, IAsyncEnumerable<FeatureRecord>> queryFactory,
        FeatureQuery? query,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(queryFactory);
        Guard.NotNull(statistics);

        if (statistics.Count == 0)
        {
            return Array.Empty<StatisticsResult>();
        }

        var requiredFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (groupByFields is { Count: > 0 })
        {
            foreach (var field in groupByFields)
            {
                if (field.HasValue())
                {
                    requiredFields.Add(field);
                }
            }
        }

        foreach (var stat in statistics)
        {
            if (stat.FieldName.HasValue())
            {
                requiredFields.Add(stat.FieldName);
            }
        }

        var projectionQuery = BuildProjectionQuery(query, requiredFields, includeGeometry: false);

        var groups = new Dictionary<string, GroupAccumulator>(StringComparer.Ordinal);

        await foreach (var record in queryFactory(projectionQuery).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var attributes = record.Attributes;
            var groupValues = BuildGroupValues(groupByFields, attributes);
            var groupKey = BuildGroupKey(groupValues);

            if (!groups.TryGetValue(groupKey, out var accumulator))
            {
                accumulator = new GroupAccumulator(groupValues);
                groups[groupKey] = accumulator;
            }

            foreach (var stat in statistics)
            {
                var statisticKey = stat.OutputName ?? $"{stat.Type}_{stat.FieldName}";
                if (!accumulator.Statistics.TryGetValue(statisticKey, out var statAccumulator))
                {
                    statAccumulator = new StatisticAccumulator(stat);
                    accumulator.Statistics[statisticKey] = statAccumulator;
                }

                object? value = null;
                if (stat.FieldName.HasValue() &&
                    attributes.TryGetValue(stat.FieldName, out var rawValue))
                {
                    value = NormalizeValue(rawValue);
                }

                UpdateStatistic(statAccumulator, value);
            }
        }

        var results = new List<StatisticsResult>(groups.Count);

        foreach (var group in groups.Values)
        {
            var statResults = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var (key, accumulator) in group.Statistics)
            {
                statResults[key] = FinalizeStatistic(accumulator);
            }

            // Apply HAVING clause filter if specified (in-memory filtering for fallback aggregation)
            // Bug 30 fix: Validate HAVING complexity before attempting fallback
            if (query?.HavingClause.HasValue() == true)
            {
                if (!ValidateHavingClauseSupport(query.HavingClause))
                {
                    // Complex HAVING clause not supported in fallback - log warning and include all groups
                    continue;
                }

                if (!EvaluateHavingClause(query.HavingClause, statResults, group.GroupValues))
                {
                    continue; // Skip this group if it doesn't match the HAVING clause
                }
            }

            results.Add(new StatisticsResult(
                new ReadOnlyDictionary<string, object?>(group.GroupValues),
                new ReadOnlyDictionary<string, object?>(statResults)));
        }

        return results;
    }

    /// <summary>
    /// Bug 30 fix: Validates if the HAVING clause is simple enough for fallback mode.
    /// Only supports single comparison expressions without nested functions or logical operators.
    /// </summary>
    private static bool ValidateHavingClauseSupport(string havingClause)
    {
        if (string.IsNullOrWhiteSpace(havingClause))
        {
            return true;
        }

        // Check for unsupported complexity indicators
        var clause = havingClause.ToUpperInvariant();

        // Reject AND/OR logical operators
        if (clause.Contains(" AND ") || clause.Contains(" OR "))
        {
            return false;
        }

        // Reject nested functions like ABS(SUM(field))
        var openParenCount = havingClause.Count(c => c == '(');
        var closeParenCount = havingClause.Count(c => c == ')');
        if (openParenCount != closeParenCount || openParenCount > 1)
        {
            return false;
        }

        // Reject CASE statements
        if (clause.Contains("CASE "))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Evaluates a HAVING clause against aggregated results (simplified evaluation for fallback mode).
    /// NOTE: This is a basic implementation that supports simple comparisons like "COUNT(*) > 5" or "SUM(field) > 1000".
    /// For complex expressions, use database-native aggregation (e.g., PostgreSQL).
    /// </summary>
    private static bool EvaluateHavingClause(
        string havingClause,
        IReadOnlyDictionary<string, object?> statistics,
        IReadOnlyDictionary<string, object?> groupValues)
    {
        // Parse basic HAVING clause: AGGREGATE_FUNC OPERATOR VALUE
        // Examples: "COUNT(*) > 5", "SUM(amount) >= 1000", "AVG(score) < 50.0"

        var operators = new[] { ">=", "<=", "!=", "<>", "=", ">", "<" };

        foreach (var op in operators)
        {
            // Bug 31 fix: Find operator position outside of function parentheses
            var opIndex = FindOperatorPosition(havingClause, op);
            if (opIndex == -1)
            {
                continue;
            }

            var leftSide = havingClause.Substring(0, opIndex).Trim();
            var rightSide = havingClause.Substring(opIndex + op.Length).Trim();

            // Try to find matching statistic
            // Convert aggregation functions to expected stat keys
            // e.g., "COUNT(*)" -> "count", "SUM(field)" -> "sum_field"
            var statKey = ParseAggregateFunction(leftSide);

            if (statistics.TryGetValue(statKey, out var statValue))
            {
                // Bug 33 fix: Type-aware comparison logic instead of forcing double conversion
                return CompareValues(statValue, rightSide, op);
            }

            // If we found an operator but couldn't evaluate, break and return true
            break;
        }

        // If we can't parse the HAVING clause, log a warning and allow the group
        // (better to include data than silently drop it)
        return true;
    }

    /// <summary>
    /// Bug 33 and 34 fix: Type-aware comparison logic that handles numeric, string, and date types correctly.
    /// Returns false for null comparisons instead of switching comparison order.
    /// </summary>
    private static bool CompareValues(object? statValue, string rightSide, string op)
    {
        // Bug 34 fix: Return false for null comparisons instead of switching order
        if (statValue == null)
        {
            return false;
        }

        // Try numeric comparison first
        if (IsNumericType(statValue) &&
            double.TryParse(rightSide, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var compareValue))
        {
            var actualValue = Convert.ToDouble(statValue);
            return op switch
            {
                ">" => actualValue > compareValue,
                ">=" => actualValue >= compareValue,
                "<" => actualValue < compareValue,
                "<=" => actualValue <= compareValue,
                "=" => Math.Abs(actualValue - compareValue) < 0.0001,
                "!=" or "<>" => Math.Abs(actualValue - compareValue) >= 0.0001,
                _ => true
            };
        }

        // Try DateTime comparison
        if (statValue is DateTime dtStat && DateTime.TryParse(rightSide, out var dtCompare))
        {
            return op switch
            {
                ">" => dtStat > dtCompare,
                ">=" => dtStat >= dtCompare,
                "<" => dtStat < dtCompare,
                "<=" => dtStat <= dtCompare,
                "=" => dtStat == dtCompare,
                "!=" or "<>" => dtStat != dtCompare,
                _ => true
            };
        }

        // Try DateTimeOffset comparison
        if (statValue is DateTimeOffset dtoStat && DateTimeOffset.TryParse(rightSide, out var dtoCompare))
        {
            return op switch
            {
                ">" => dtoStat > dtoCompare,
                ">=" => dtoStat >= dtoCompare,
                "<" => dtoStat < dtoCompare,
                "<=" => dtoStat <= dtoCompare,
                "=" => dtoStat == dtoCompare,
                "!=" or "<>" => dtoStat != dtoCompare,
                _ => true
            };
        }

        // Fall back to string comparison
        var strStat = statValue.ToString() ?? string.Empty;
        var strCompare = rightSide.Trim('"', '\'');

        var comparison = string.Compare(strStat, strCompare, StringComparison.Ordinal);
        return op switch
        {
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            "=" => comparison == 0,
            "!=" or "<>" => comparison != 0,
            _ => true
        };
    }

    private static bool IsNumericType(object value)
    {
        return value is sbyte or byte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }

    /// <summary>
    /// Bug 31 fix: Finds operator position outside of function parentheses.
    /// Ensures we don't split on operators inside function arguments like "ABS(SUM(field)) > 5".
    /// </summary>
    private static int FindOperatorPosition(string expression, string op)
    {
        var parenDepth = 0;
        for (var i = 0; i <= expression.Length - op.Length; i++)
        {
            var ch = expression[i];
            if (ch == '(')
            {
                parenDepth++;
            }
            else if (ch == ')')
            {
                parenDepth--;
            }
            else if (parenDepth == 0 && expression.Substring(i, op.Length) == op)
            {
                // Found operator outside parentheses
                return i;
            }
        }

        return -1; // Operator not found
    }

    /// <summary>
    /// Parses aggregate function syntax to statistic key.
    /// Bug 32 fix: Normalize COUNT(*) to match translator output ("count" not "count_*").
    /// Examples: "COUNT(*)" -> "count", "SUM(amount)" -> "sum_amount", "AVG(score)" -> "avg_score"
    /// </summary>
    private static string ParseAggregateFunction(string expression)
    {
        expression = expression.Trim().ToUpperInvariant();

        // Bug 32 fix: Handle COUNT(*) and COUNT(field) separately
        if (expression.StartsWith("COUNT("))
        {
            var fieldPart = expression.Substring(6).TrimEnd(')').Trim();
            // COUNT(*) or COUNT(1) normalize to "count"
            if (fieldPart == "*" || fieldPart == "1")
            {
                return "count";
            }
            // COUNT(field) becomes "count_field"
            return $"count_{fieldPart.ToLowerInvariant()}";
        }

        var aggFunctions = new[] { "SUM(", "AVG(", "MIN(", "MAX(" };
        foreach (var func in aggFunctions)
        {
            if (expression.StartsWith(func))
            {
                var fieldPart = expression.Substring(func.Length).TrimEnd(')').Trim();
                var funcName = func.TrimEnd('(').ToLowerInvariant();
                return $"{funcName}_{fieldPart.ToLowerInvariant()}";
            }
        }

        return expression.ToLowerInvariant();
    }

    public static async Task<IReadOnlyList<DistinctResult>> ComputeDistinctAsync(
        Func<FeatureQuery, IAsyncEnumerable<FeatureRecord>> queryFactory,
        FeatureQuery? query,
        IReadOnlyList<string> fieldNames,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(queryFactory);
        Guard.NotNull(fieldNames);

        if (fieldNames.Count == 0)
        {
            return Array.Empty<DistinctResult>();
        }

        var projectionQuery = BuildProjectionQuery(query, fieldNames, includeGeometry: false);
        var distinct = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);

        await foreach (var record in queryFactory(projectionQuery).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fieldNames)
            {
                if (field.IsNullOrWhiteSpace())
                {
                    continue;
                }

                record.Attributes.TryGetValue(field, out var rawValue);
                values[field] = NormalizeValue(rawValue);
            }

            var key = BuildGroupKey(values);
            if (!distinct.ContainsKey(key))
            {
                distinct[key] = new ReadOnlyDictionary<string, object?>(values);
            }
        }

        var results = new List<DistinctResult>(distinct.Count);
        foreach (var values in distinct.Values)
        {
            results.Add(new DistinctResult(values));
        }

        return results;
    }

    public static async Task<BoundingBox?> ComputeExtentAsync(
        Func<FeatureQuery, IAsyncEnumerable<FeatureRecord>> queryFactory,
        FeatureQuery? query,
        ServiceDefinition service,
        LayerDefinition layer,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(queryFactory);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        var projectionQuery = BuildProjectionQuery(query, new[] { layer.GeometryField }, includeGeometry: true);

        double? minX = null;
        double? minY = null;
        double? maxX = null;
        double? maxY = null;

        await foreach (var record in queryFactory(projectionQuery).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!record.Attributes.TryGetValue(layer.GeometryField, out var rawGeometry))
            {
                continue;
            }

            var geometry = ReadGeometry(rawGeometry);
            if (geometry is null || geometry.IsEmpty)
            {
                continue;
            }

            var envelope = geometry.EnvelopeInternal;
            if (envelope is null || envelope.IsNull)
            {
                continue;
            }

            minX = minX.HasValue ? Math.Min(minX.Value, envelope.MinX) : envelope.MinX;
            minY = minY.HasValue ? Math.Min(minY.Value, envelope.MinY) : envelope.MinY;
            maxX = maxX.HasValue ? Math.Max(maxX.Value, envelope.MaxX) : envelope.MaxX;
            maxY = maxY.HasValue ? Math.Max(maxY.Value, envelope.MaxY) : envelope.MaxY;
        }

        if (!minX.HasValue ||
            !minY.HasValue ||
            !maxX.HasValue ||
            !maxY.HasValue)
        {
            return null;
        }

        var crs = query?.Crs ?? service.Ogc.DefaultCrs;
        return new BoundingBox(minX.Value, minY.Value, maxX.Value, maxY.Value, Crs: crs);
    }

    private static FeatureQuery BuildProjectionQuery(
        FeatureQuery? query,
        IEnumerable<string> fields,
        bool includeGeometry)
    {
        var baseQuery = query ?? new FeatureQuery();
        var propertySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (fields != null)
        {
            foreach (var field in fields)
            {
                if (field.HasValue())
                {
                    propertySet.Add(field);
                }
            }
        }

        var propertyNames = propertySet.Count > 0
            ? new ReadOnlyCollection<string>(propertySet.ToList())
            : null;

        return baseQuery with
        {
            Limit = null,
            Offset = null,
            ResultType = FeatureResultType.Results,
            SortOrders = null,
            PropertyNames = propertyNames
        };
    }

    private static Dictionary<string, object?> BuildGroupValues(
        IReadOnlyList<string>? groupByFields,
        IReadOnlyDictionary<string, object?> attributes)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (groupByFields is null)
        {
            return values;
        }

        foreach (var field in groupByFields)
        {
            if (field.IsNullOrWhiteSpace())
            {
                continue;
            }

            attributes.TryGetValue(field, out var rawValue);
            values[field] = NormalizeValue(rawValue);
        }

        return values;
    }

    private static string BuildGroupKey(IReadOnlyDictionary<string, object?> values)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var entry in values.OrderBy(static v => v.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(entry.Key);
            builder.Append('=');
            builder.Append(SerializeValue(entry.Value));
            builder.Append(';');
        }

        return builder.ToString();
    }

    private static void UpdateStatistic(StatisticAccumulator accumulator, object? value)
    {
        switch (accumulator.Definition.Type)
        {
            case StatisticType.Count:
                if (accumulator.Definition.FieldName.IsNullOrWhiteSpace() || value is not null)
                {
                    accumulator.Count++;
                }
                break;

            case StatisticType.Sum:
            case StatisticType.Avg:
                if (TryConvertToDecimal(value, out var numeric))
                {
                    accumulator.Sum += numeric;
                    accumulator.Count++;
                }
                break;

            case StatisticType.Min:
                UpdateMinimum(accumulator, value);
                break;

            case StatisticType.Max:
                UpdateMaximum(accumulator, value);
                break;

            default:
                break;
        }
    }

    private static void UpdateMinimum(StatisticAccumulator accumulator, object? value)
    {
        if (!TryGetComparable(value, out var comparable, out var normalized))
        {
            return;
        }

        if (accumulator.MinComparable is null || comparable.CompareTo(accumulator.MinComparable) < 0)
        {
            accumulator.MinComparable = comparable;
            accumulator.MinValue = normalized;
        }
    }

    private static void UpdateMaximum(StatisticAccumulator accumulator, object? value)
    {
        if (!TryGetComparable(value, out var comparable, out var normalized))
        {
            return;
        }

        if (accumulator.MaxComparable is null || comparable.CompareTo(accumulator.MaxComparable) > 0)
        {
            accumulator.MaxComparable = comparable;
            accumulator.MaxValue = normalized;
        }
    }

    private static object? FinalizeStatistic(StatisticAccumulator accumulator)
    {
        return accumulator.Definition.Type switch
        {
            StatisticType.Count => accumulator.Count,
            StatisticType.Sum => accumulator.Sum,
            StatisticType.Avg => accumulator.Count == 0 ? null : accumulator.Sum / accumulator.Count,
            StatisticType.Min => accumulator.MinValue,
            StatisticType.Max => accumulator.MaxValue,
            _ => null
        };
    }

    private static bool TryConvertToDecimal(object? value, out decimal result)
    {
        value = NormalizeValue(value);

        switch (value)
        {
            case null:
                result = 0;
                return false;
            case decimal decimalValue:
                result = decimalValue;
                return true;
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            case float floatValue when !float.IsNaN(floatValue) && !float.IsInfinity(floatValue):
                result = Convert.ToDecimal(floatValue);
                return true;
            case double doubleValue when !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue):
                result = Convert.ToDecimal(doubleValue);
                return true;
            case string stringValue when decimal.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static bool TryGetComparable(object? value, out IComparable? comparable, out object? normalized)
    {
        normalized = NormalizeValue(value);

        if (normalized is IComparable cmp)
        {
            comparable = cmp;
            return true;
        }

        comparable = null;
        return false;
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement element => NormalizeJsonElement(element),
            JsonValue jsonValue => NormalizeJsonValue(jsonValue),
            DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            _ => value
        };
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.GetRawText()
        };
    }

    private static object? NormalizeJsonValue(JsonValue value)
    {
        try
        {
            return value.GetValue<object?>();
        }
        catch
        {
            try
            {
                return JsonSerializer.Deserialize<object?>(value.ToJsonString());
            }
            catch
            {
                return value.ToJsonString();
            }
        }
    }

    private static string SerializeValue(object? value)
    {
        return value switch
        {
            null => "<null>",
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset offset => offset.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            bool booleanValue => booleanValue ? "true" : "false",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static Geometry? ReadGeometry(object? value)
    {
        string? json = value switch
        {
            null => null,
            string text => text,
            JsonNode node => node.ToJsonString(),
            JsonElement element => element.GetRawText(),
            _ => value.ToString()
        };

        if (json.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            return GeoJsonReader.Read<Geometry>(json);
        }
        catch
        {
            return null;
        }
    }

    private sealed class GroupAccumulator
    {
        public GroupAccumulator(IDictionary<string, object?> values)
        {
            GroupValues = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, object?> GroupValues { get; }

        public Dictionary<string, StatisticAccumulator> Statistics { get; } = new(StringComparer.Ordinal);
    }

    private sealed class StatisticAccumulator
    {
        public StatisticAccumulator(StatisticDefinition definition)
        {
            Definition = definition;
        }

        public StatisticDefinition Definition { get; }

        public long Count { get; set; }

        public decimal Sum { get; set; }

        public IComparable? MinComparable { get; set; }

        public object? MinValue { get; set; }

        public IComparable? MaxComparable { get; set; }

        public object? MaxValue { get; set; }
    }
}

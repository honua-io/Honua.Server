using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// In-memory stub implementation of IFeatureRepository for testing.
/// This stub provides configurable test data with query filtering capabilities.
/// </summary>
/// <remarks>
/// This implementation supports:
/// - Multiple layers/datasets with independent feature collections
/// - Query filtering (WHERE clauses, spatial filters, temporal filters)
/// - Sorting and pagination
/// - Relationship queries (related records)
/// - Read-only operations (mutations throw NotSupportedException)
///
/// Default data includes:
/// - "roads-primary" layer: 3 road features with LineString geometry
/// - "roads-inspections" layer: 3 inspection features with Point geometry
/// </remarks>
public sealed class StubFeatureRepository : IFeatureRepository
{

    private static readonly IReadOnlyList<FeatureRecord> DefaultRoadRecords = new List<FeatureRecord>
    {
        CreateFeature(1, "Sunset Highway", "open", new DateTimeOffset(2024, 1, 15, 12, 30, 0, TimeSpan.Zero),
            (-122.56, 45.51), (-122.52, 45.55)),
        CreateFeature(2, "Pacific Avenue", "planned", new DateTimeOffset(2024, 2, 10, 9, 45, 0, TimeSpan.Zero),
            (-122.50, 45.60), (-122.46, 45.62)),
        CreateFeature(3, "Harbor Drive", "open", new DateTimeOffset(2024, 3, 5, 8, 15, 0, TimeSpan.Zero),
            (-122.44, 45.58), (-122.40, 45.59))
    };

    private static readonly IReadOnlyList<FeatureRecord> DefaultInspectionRecords = new List<FeatureRecord>
    {
        CreateInspection(100, 1, "Avery Johnson", "complete", new DateTimeOffset(2024, 1, 20, 14, 0, 0, TimeSpan.Zero), (-122.555, 45.53)),
        CreateInspection(101, 1, "Maria Chen", "follow-up", new DateTimeOffset(2024, 1, 28, 10, 15, 0, TimeSpan.Zero), (-122.535, 45.545)),
        CreateInspection(102, 2, "Hank Lewis", "scheduled", new DateTimeOffset(2024, 2, 18, 9, 45, 0, TimeSpan.Zero), (-122.48, 45.61))
    };

    private readonly Dictionary<string, IReadOnlyList<FeatureRecord>> _datasetIndex;

    /// <summary>
    /// Initializes a new instance with default test data.
    /// </summary>
    public StubFeatureRepository()
    {
        _datasetIndex = new Dictionary<string, IReadOnlyList<FeatureRecord>>(StringComparer.OrdinalIgnoreCase)
        {
            ["roads-primary"] = DefaultRoadRecords,
            ["roads-inspections"] = DefaultInspectionRecords
        };
    }

    /// <summary>
    /// Initializes a new instance with custom data.
    /// </summary>
    /// <param name="datasetIndex">Dictionary mapping layer IDs to feature collections.</param>
    public StubFeatureRepository(IDictionary<string, IReadOnlyList<FeatureRecord>> datasetIndex)
    {
        _datasetIndex = new Dictionary<string, IReadOnlyList<FeatureRecord>>(
            datasetIndex,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds or replaces features for a specific layer.
    /// </summary>
    public void SetFeatures(string layerId, params FeatureRecord[] features)
    {
        _datasetIndex[layerId] = features;
    }

    public IAsyncEnumerable<FeatureRecord> QueryAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyQuery(layerId, query);
        return ProduceAsync(filtered, cancellationToken);
    }

    public Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
    {
        var filtered = ApplyQuery(layerId, query);
        return Task.FromResult((long)filtered.Count);
    }

    public Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default)
    {
        var record = ApplyQuery(layerId, query).FirstOrDefault(candidate =>
            candidate.Attributes.TryGetValue(ResolvePrimaryKey(layerId), out var value) &&
            string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), featureId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(record);
    }

    public Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("StubFeatureRepository is read-only. Use InMemoryEditableFeatureRepository for write operations.");
    }

    public Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("StubFeatureRepository is read-only. Use InMemoryEditableFeatureRepository for write operations.");
    }

    public Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());

    public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(string serviceId, string layerId, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());

    public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(string serviceId, string layerId, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());

    public Task<BoundingBox?> QueryExtentAsync(string serviceId, string layerId, FeatureQuery? filter, CancellationToken cancellationToken = default)
        => Task.FromResult<BoundingBox?>(null);

    private IReadOnlyList<FeatureRecord> ApplyQuery(string layerId, FeatureQuery? query)
    {
        IEnumerable<FeatureRecord> candidates = ResolveDataset(layerId);

        if (query?.Filter?.Expression is not null)
        {
            candidates = candidates.Where(record => EvaluateBoolean(query.Filter.Expression, record));
        }

        if (query?.Temporal is { } temporalRange)
        {
            candidates = candidates.Where(record => IntersectsTemporal(record, temporalRange, layerId));
        }

        if (query?.Bbox is not null)
        {
            candidates = candidates.Where(record => Intersects(record, query.Bbox));
        }

        if (query?.SortOrders is { Count: > 0 })
        {
            var ordered = ApplySorts(candidates, query.SortOrders);
            candidates = ordered;
        }

        if (query?.Offset is int offset && offset > 0)
        {
            candidates = candidates.Skip(offset);
        }

        if (query?.Limit is int limit && limit >= 0)
        {
            candidates = candidates.Take(limit);
        }

        return candidates.ToList();
    }

    private IReadOnlyList<FeatureRecord> ResolveDataset(string layerId)
    {
        if (_datasetIndex.TryGetValue(layerId, out var dataset))
        {
            return dataset;
        }

        return DefaultRoadRecords;
    }

    private static string ResolvePrimaryKey(string layerId)
    {
        return layerId.Equals("roads-inspections", StringComparison.OrdinalIgnoreCase)
            ? "inspection_id"
            : "road_id";
    }

    private static string? ResolveTemporalColumn(string layerId)
    {
        return "observed_at";
    }

    private static bool IntersectsTemporal(FeatureRecord record, TemporalInterval temporal, string layerId)
    {
        var column = ResolveTemporalColumn(layerId);
        if (string.IsNullOrWhiteSpace(column))
        {
            return true;
        }

        if (!record.Attributes.TryGetValue(column, out var raw) || raw is null)
        {
            return false;
        }

        if (!TryConvertToDateTimeOffset(raw, out var timestamp))
        {
            return false;
        }

        if (temporal.Start is { } start && timestamp < start)
        {
            return false;
        }

        if (temporal.End is { } end && timestamp > end)
        {
            return false;
        }

        return true;
    }

    private static bool TryConvertToDateTimeOffset(object value, out DateTimeOffset result)
    {
        switch (value)
        {
            case DateTimeOffset dto:
                result = dto;
                return true;
            case DateTime dt when dt.Kind == DateTimeKind.Unspecified:
                result = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return true;
            case DateTime dt:
                result = dt;
                return true;
            case string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static IEnumerable<FeatureRecord> ApplySorts(IEnumerable<FeatureRecord> source, IReadOnlyList<FeatureSortOrder> sortOrders)
    {
        IOrderedEnumerable<FeatureRecord>? ordered = null;

        foreach (var sortOrder in sortOrders)
        {
            Func<FeatureRecord, object?> selector = record =>
                record.Attributes.TryGetValue(sortOrder.Field, out var value) ? value : null;

            ordered = ordered is null
                ? sortOrder.Direction == FeatureSortDirection.Ascending
                    ? source.OrderBy(selector, SortComparer.Instance)
                    : source.OrderByDescending(selector, SortComparer.Instance)
                : sortOrder.Direction == FeatureSortDirection.Ascending
                    ? ordered.ThenBy(selector, SortComparer.Instance)
                    : ordered.ThenByDescending(selector, SortComparer.Instance);
        }

        return ordered ?? source;
    }

    private sealed class SortComparer : IComparer<object?>
    {
        public static SortComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            if (x is IComparable comparable)
            {
                try
                {
                    var converted = Convert.ChangeType(y, x.GetType(), CultureInfo.InvariantCulture);
                    if (converted is not null)
                    {
                        return comparable.CompareTo(converted);
                    }
                }
                catch
                {
                    // Fall through to string comparison.
                }
            }

            var left = Convert.ToString(x, CultureInfo.InvariantCulture) ?? string.Empty;
            var right = Convert.ToString(y, CultureInfo.InvariantCulture) ?? string.Empty;
            return string.Compare(left, right, StringComparison.Ordinal);
        }
    }

    private static async IAsyncEnumerable<FeatureRecord> ProduceAsync(IEnumerable<FeatureRecord> records, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
            await Task.Yield();
        }
    }

    private static bool EvaluateBoolean(QueryExpression expression, FeatureRecord record)
    {
        return expression switch
        {
            QueryBinaryExpression binary => EvaluateBinary(binary, record),
            QueryUnaryExpression unary when unary.Operator == QueryUnaryOperator.Not => !EvaluateBoolean(unary.Operand, record),
            QueryFieldReference field => ToBoolean(GetValue(field.Name, record)),
            QueryConstant constant => ToBoolean(constant.Value),
            QueryFunctionExpression function => EvaluateFunction(function, record),
            _ => true
        };
    }

    private static bool EvaluateBinary(QueryBinaryExpression binary, FeatureRecord record)
    {
        return binary.Operator switch
        {
            QueryBinaryOperator.And => EvaluateBoolean(binary.Left, record) && EvaluateBoolean(binary.Right, record),
            QueryBinaryOperator.Or => EvaluateBoolean(binary.Left, record) || EvaluateBoolean(binary.Right, record),
            QueryBinaryOperator.Equal or QueryBinaryOperator.NotEqual or QueryBinaryOperator.GreaterThan or QueryBinaryOperator.GreaterThanOrEqual or QueryBinaryOperator.LessThan or QueryBinaryOperator.LessThanOrEqual
                => CompareValues(GetValue(binary.Left, record), GetValue(binary.Right, record), binary.Operator),
            _ => true
        };
    }

    private static object? GetValue(QueryExpression expression, FeatureRecord record)
    {
        return expression switch
        {
            QueryConstant constant => constant.Value,
            QueryFieldReference field => GetValue(field.Name, record),
            QueryBinaryExpression binary => EvaluateBoolean(binary, record),
            QueryUnaryExpression unary => EvaluateBoolean(unary, record),
            QueryFunctionExpression function => EvaluateFunction(function, record),
            _ => null
        };
    }

    private static object? GetValue(string fieldName, FeatureRecord record)
    {
        return record.Attributes.TryGetValue(fieldName, out var value) ? value : null;
    }

    private static bool CompareValues(object? left, object? right, QueryBinaryOperator op)
    {
        if (left is null || right is null)
        {
            return op switch
            {
                QueryBinaryOperator.Equal => left is null && right is null,
                QueryBinaryOperator.NotEqual => left is null ^ right is null,
                _ => false
            };
        }

        if (TryConvertToDouble(left, out var leftNumber) && TryConvertToDouble(right, out var rightNumber))
        {
            return EvaluateComparison(leftNumber.CompareTo(rightNumber), op);
        }

        if (TryConvertToDateTime(left, out var leftDate) && TryConvertToDateTime(right, out var rightDate))
        {
            return EvaluateComparison(leftDate.CompareTo(rightDate), op);
        }

        var leftText = Convert.ToString(left, CultureInfo.InvariantCulture) ?? string.Empty;
        var rightText = Convert.ToString(right, CultureInfo.InvariantCulture) ?? string.Empty;
        return EvaluateComparison(string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase), op);
    }

    private static bool EvaluateComparison(int comparison, QueryBinaryOperator op)
    {
        return op switch
        {
            QueryBinaryOperator.Equal => comparison == 0,
            QueryBinaryOperator.NotEqual => comparison != 0,
            QueryBinaryOperator.GreaterThan => comparison > 0,
            QueryBinaryOperator.GreaterThanOrEqual => comparison >= 0,
            QueryBinaryOperator.LessThan => comparison < 0,
            QueryBinaryOperator.LessThanOrEqual => comparison <= 0,
            _ => false
        };
    }

    private static bool TryConvertToDouble(object value, out double result)
    {
        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static bool TryConvertToDateTime(object value, out DateTimeOffset result)
    {
        switch (value)
        {
            case DateTimeOffset dto:
                result = dto;
                return true;
            case DateTime dt:
                result = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return true;
            case string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool Intersects(FeatureRecord record, BoundingBox box)
    {
        if (!record.Attributes.TryGetValue("geom", out var geometry) || geometry is not JsonObject obj)
        {
            return false;
        }

        if (!obj.TryGetPropertyValue("coordinates", out var coordinatesNode) || coordinatesNode is not JsonArray coordinates)
        {
            return false;
        }

        var crossesDateline = box.MinX > box.MaxX && box.MinX > 0 && box.MaxX < 0;

        foreach (var coordinate in coordinates)
        {
            if (coordinate is not JsonArray position || position.Count < 2)
            {
                continue;
            }

            var x = position[0]!.GetValue<double>();
            var y = position[1]!.GetValue<double>();
            var withinLatitude = y >= box.MinY && y <= box.MaxY;
            if (!withinLatitude)
            {
                continue;
            }

            if (!crossesDateline)
            {
                if (x >= box.MinX && x <= box.MaxX)
                {
                    return true;
                }
            }
            else
            {
                // Dateline crossing: accept points either on western or eastern segment
                if (x >= box.MinX || x <= box.MaxX)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ToBoolean(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            double d => Math.Abs(d) > double.Epsilon,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true
        };
    }

    private static bool EvaluateFunction(QueryFunctionExpression function, FeatureRecord record)
    {
        if (string.Equals(function.Name, "like", StringComparison.OrdinalIgnoreCase) && function.Arguments.Count == 2)
        {
            if (function.Arguments[0] is QueryFieldReference field && function.Arguments[1] is QueryConstant constant)
            {
                var candidate = Convert.ToString(GetValue(field.Name, record), CultureInfo.InvariantCulture) ?? string.Empty;
                var pattern = Convert.ToString(constant.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                return LikeMatch(candidate, pattern);
            }
        }

        return false;
    }

    private static bool LikeMatch(string value, string pattern)
    {
        if (pattern.Length == 0)
        {
            return value.Length == 0;
        }

        var escaped = Regex.Escape(pattern);
        var regexPattern = escaped
            .Replace("%", ".*", StringComparison.Ordinal)
            .Replace("_", ".", StringComparison.Ordinal);
        return Regex.IsMatch(value, "^" + regexPattern + "$", RegexOptions.IgnoreCase);
    }

    private static FeatureRecord CreateFeature(int id, string name, string status, DateTimeOffset observedAt, params (double X, double Y)[] coordinates)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["road_id"] = id,
            ["name"] = name,
            ["status"] = status,
            ["observed_at"] = observedAt.UtcDateTime,
            ["geom"] = BuildLineGeometry(coordinates)
        };

        return new FeatureRecord(attributes);
    }

    private static FeatureRecord CreateInspection(int inspectionId, int roadId, string inspector, string status, DateTimeOffset observedAt, (double X, double Y) coordinate)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["inspection_id"] = inspectionId,
            ["road_id"] = roadId,
            ["inspector"] = inspector,
            ["status"] = status,
            ["observed_at"] = observedAt.UtcDateTime,
            ["geom"] = BuildPointGeometry(coordinate)
        };

        return new FeatureRecord(attributes);
    }

    private static JsonObject BuildLineGeometry(params (double X, double Y)[] coordinates)
    {
        var coordinateArray = new JsonArray();
        foreach (var (x, y) in coordinates)
        {
            coordinateArray.Add(new JsonArray(x, y));
        }

        return new JsonObject
        {
            ["type"] = "LineString",
            ["coordinates"] = coordinateArray
        };
    }

    private static JsonObject BuildPointGeometry((double X, double Y) coordinate)
    {
        return new JsonObject
        {
            ["type"] = "Point",
            ["coordinates"] = new JsonArray(coordinate.X, coordinate.Y)
        };
    }
}

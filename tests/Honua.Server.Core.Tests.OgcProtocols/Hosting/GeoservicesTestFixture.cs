using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Tests.Shared;
using Honua.Server.Core.Raster.Analytics;
using Honua.Server.Host.Stac.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

/// <summary>
/// Shared test fixture for GeoservicesREST integration tests.
/// Provides a configured test web application with sample data for testing
/// MapServer, FeatureServer, and ImageServer endpoints.
/// </summary>
public sealed class GeoservicesLeafletFixture : HonuaTestWebApplicationFactory
{
    protected override string GetMetadataJson()
    {
        return BuildMetadata();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // Use StubFeatureRepository with pre-populated test data
        services.AddSingleton<IFeatureRepository>(_ => new StubFeatureRepository());
        services.RemoveAll<IRasterAnalyticsService>();
        services.AddSingleton<IRasterAnalyticsService, StubRasterAnalyticsService>();

        // Remove STAC services not needed for these tests
        services.RemoveAll<StacCollectionService>();
        services.RemoveAll<StacItemService>();
    }

    private static string BuildMetadata()
    {
        var root = JsonNode.Parse(HonuaWebApplicationFactory.SampleMetadata)!.AsObject();
        var rasterPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "Rasters", "cea.tif");
        var rasterUri = new Uri(rasterPath).AbsoluteUri;

        if (root.TryGetPropertyValue("layers", out var layersNode) && layersNode is JsonArray layers && layers.Count > 0)
        {
            var layer = layers[0]!.AsObject();
            var query = layer["query"] as JsonObject ?? new JsonObject();
            query["maxRecordCount"] = 100;
            layer["query"] = query;

            layer["minScale"] = 1;
            layer["maxScale"] = 0;

            var editing = layer["editing"] as JsonObject ?? new JsonObject();
            var capabilities = editing["capabilities"] as JsonObject ?? new JsonObject();
            capabilities["allowAdd"] = true;
            capabilities["allowUpdate"] = true;
            capabilities["allowDelete"] = true;
            capabilities["requireAuthentication"] = true;
            capabilities["allowedRoles"] = new JsonArray("DataPublisher");
            editing["capabilities"] = capabilities;

            var constraints = editing["constraints"] as JsonObject ?? new JsonObject();
            constraints["immutableFields"] = new JsonArray("road_id");
            constraints["requiredFields"] = new JsonArray("name");
            constraints["defaultValues"] = new JsonObject { ["status"] = "planned" };
            editing["constraints"] = constraints;

            layer["editing"] = editing;
        }

        if (root.TryGetPropertyValue("rasterDatasets", out var rasterNode) && rasterNode is JsonArray rasterDatasets)
        {
            foreach (var node in rasterDatasets)
            {
                if (node is JsonObject dataset && dataset.TryGetPropertyValue("source", out var sourceNode))
                {
                    var source = sourceNode as JsonObject ?? new JsonObject();
                    source["uri"] = rasterUri;
                    dataset["source"] = source;
                }
            }
        }

        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}

/// <summary>
/// In-memory stub implementation of IFeatureRepository for testing.
/// Contains pre-populated road and inspection data.
/// </summary>
internal sealed class StubFeatureRepository : IFeatureRepository
{
    private static readonly IReadOnlyList<FeatureRecord> RoadRecords = new List<FeatureRecord>
    {
        CreateFeature(1, "Sunset Highway", "open", new DateTimeOffset(2024, 1, 15, 12, 30, 0, TimeSpan.Zero),
            (-122.56, 45.51), (-122.52, 45.55)),
        CreateFeature(2, "Pacific Avenue", "planned", new DateTimeOffset(2024, 2, 10, 9, 45, 0, TimeSpan.Zero),
            (-122.50, 45.60), (-122.46, 45.62)),
        CreateFeature(3, "Harbor Drive", "open", new DateTimeOffset(2024, 3, 5, 8, 15, 0, TimeSpan.Zero),
            (-122.44, 45.58), (-122.40, 45.59))
    };

    private static readonly IReadOnlyList<FeatureRecord> InspectionRecords = new List<FeatureRecord>
    {
        CreateInspection(100, 1, "Avery Johnson", "complete", new DateTimeOffset(2024, 1, 20, 14, 0, 0, TimeSpan.Zero), (-122.555, 45.53)),
        CreateInspection(101, 1, "Maria Chen", "follow-up", new DateTimeOffset(2024, 1, 28, 10, 15, 0, TimeSpan.Zero), (-122.535, 45.545)),
        CreateInspection(102, 2, "Hank Lewis", "scheduled", new DateTimeOffset(2024, 2, 18, 9, 45, 0, TimeSpan.Zero), (-122.48, 45.61))
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<FeatureRecord>> DatasetIndex = new Dictionary<string, IReadOnlyList<FeatureRecord>>(StringComparer.OrdinalIgnoreCase)
    {
        ["roads-primary"] = RoadRecords,
        ["roads-inspections"] = InspectionRecords
    };

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
        throw new NotSupportedException("Stub repository is read-only.");
    }

    public Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Stub repository is read-only.");
    }

    public Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());

    public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        if (statistics is null || statistics.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());
        }

        var records = GetAggregationRecords(layerId, filter);
        if (records.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());
        }

        var groups = new Dictionary<string, GroupAccumulator>(StringComparer.Ordinal);
        var groupFields = groupByFields?.Where(static field => !string.IsNullOrWhiteSpace(field)).ToArray() ?? Array.Empty<string>();

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var keyComponents = new List<object?>(groupFields.Length);

            foreach (var field in groupFields)
            {
                if (TryGetAttribute(record, field, out var value))
                {
                    groupValues[field] = value;
                    keyComponents.Add(value);
                }
                else
                {
                    groupValues[field] = null;
                    keyComponents.Add(null);
                }
            }

            var key = groupFields.Length == 0
                ? "__all__"
                : JsonSerializer.Serialize(keyComponents);

            if (!groups.TryGetValue(key, out var accumulator))
            {
                var readOnlyGroupValues = new ReadOnlyDictionary<string, object?>(groupValues);
                var statAccumulators = statistics.Select(static definition => new StatisticAccumulator(definition)).ToList();
                accumulator = new GroupAccumulator(readOnlyGroupValues, statAccumulators);
                groups[key] = accumulator;
            }

            accumulator.Add(record);
        }

        var results = groups.Values
            .Select(static bucket => bucket.ToResult())
            .ToList();

        return Task.FromResult<IReadOnlyList<StatisticsResult>>(results);
    }

    public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        if (fieldNames is null || fieldNames.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());
        }

        var records = GetAggregationRecords(layerId, filter);
        if (records.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());
        }

        var distinct = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new Dictionary<string, object?>(fieldNames.Count, StringComparer.OrdinalIgnoreCase);
            var keyComponents = new List<object?>(fieldNames.Count);

            foreach (var fieldName in fieldNames)
            {
                if (TryGetAttribute(record, fieldName, out var value))
                {
                    values[fieldName] = value;
                    keyComponents.Add(value);
                }
                else
                {
                    values[fieldName] = null;
                    keyComponents.Add(null);
                }
            }

            var key = JsonSerializer.Serialize(keyComponents);
            if (!distinct.ContainsKey(key))
            {
                distinct[key] = new ReadOnlyDictionary<string, object?>(values);
            }
        }

        var results = distinct.Values
            .Select(static dict => new DistinctResult(dict))
            .ToList();

        return Task.FromResult<IReadOnlyList<DistinctResult>>(results);
    }

    public Task<BoundingBox?> QueryExtentAsync(
        string serviceId,
        string layerId,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        var records = GetAggregationRecords(layerId, filter);
        if (records.Count == 0)
        {
            return Task.FromResult<BoundingBox?>(null);
        }

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var hasValue = false;

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!record.Attributes.TryGetValue("geom", out var geometry) || geometry is null)
            {
                continue;
            }

            switch (geometry)
            {
                case JsonObject jsonObject:
                    hasValue |= TryExpandEnvelope(jsonObject, ref minX, ref minY, ref maxX, ref maxY);
                    break;
                case JsonNode node:
                    if (node is JsonObject nodeObject)
                    {
                        hasValue |= TryExpandEnvelope(nodeObject, ref minX, ref minY, ref maxX, ref maxY);
                    }
                    break;
                case string text when !string.IsNullOrWhiteSpace(text):
                    {
                        try
                        {
                            var parsed = JsonNode.Parse(text);
                            if (parsed is JsonObject parsedObject)
                            {
                                hasValue |= TryExpandEnvelope(parsedObject, ref minX, ref minY, ref maxX, ref maxY);
                            }
                        }
                        catch
                        {
                            // Ignore malformed geometry payloads for extent calculations
                        }

                        break;
                    }
            }
        }

        if (!hasValue)
        {
            return Task.FromResult<BoundingBox?>(null);
        }

        var bbox = new BoundingBox(minX, minY, maxX, maxY, Crs: "EPSG:4326");
        return Task.FromResult<BoundingBox?>(bbox);
    }

    private static IReadOnlyList<FeatureRecord> GetAggregationRecords(string layerId, FeatureQuery? query)
    {
        var workingQuery = query is null
            ? null
            : query with { Limit = null, Offset = null };

        return ApplyQuery(layerId, workingQuery);
    }

    private static IReadOnlyList<FeatureRecord> ApplyQuery(string layerId, FeatureQuery? query)
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

    private static bool TryExpandEnvelope(JsonObject geometry, ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        if (!geometry.TryGetPropertyValue("coordinates", out var coordinatesNode) || coordinatesNode is null)
        {
            return false;
        }

        return ExpandCoordinateNode(coordinatesNode, ref minX, ref minY, ref maxX, ref maxY);
    }

    private static bool ExpandCoordinateNode(JsonNode node, ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        switch (node)
        {
            case JsonArray array when array.Count >= 2 && array[0] is JsonValue:
                {
                    try
                    {
                        var x = array[0]!.GetValue<double>();
                        var y = array[1]!.GetValue<double>();
                        UpdateBounds(x, y, ref minX, ref minY, ref maxX, ref maxY);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            case JsonArray array:
                {
                    var updated = false;
                    foreach (var element in array)
                    {
                        if (element is JsonNode child && ExpandCoordinateNode(child, ref minX, ref minY, ref maxX, ref maxY))
                        {
                            updated = true;
                        }
                    }

                    return updated;
                }
            default:
                return false;
        }
    }

    private static void UpdateBounds(double x, double y, ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
    }

    private static bool TryGetAttribute(FeatureRecord record, string fieldName, out object? value)
    {
        if (record.Attributes.TryGetValue(fieldName, out value))
        {
            return true;
        }

        foreach (var pair in record.Attributes)
        {
            if (pair.Key.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private sealed class GroupAccumulator
    {
        private readonly List<StatisticAccumulator> _statistics;

        public GroupAccumulator(IReadOnlyDictionary<string, object?> groupValues, List<StatisticAccumulator> statistics)
        {
            GroupValues = groupValues;
            _statistics = statistics;
        }

        public IReadOnlyDictionary<string, object?> GroupValues { get; }

        public void Add(FeatureRecord record)
        {
            foreach (var accumulator in _statistics)
            {
                accumulator.Add(record);
            }
        }

        public StatisticsResult ToResult()
        {
            var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var accumulator in _statistics)
            {
                dictionary[accumulator.OutputName] = accumulator.GetValue();
            }

            return new StatisticsResult(GroupValues, new ReadOnlyDictionary<string, object?>(dictionary));
        }
    }

    private sealed class StatisticAccumulator
    {
        private readonly StatisticDefinition _definition;
        private long _nonNullCount;
        private double _sum;
        private double? _min;
        private double? _max;

        public StatisticAccumulator(StatisticDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public string OutputName => _definition.OutputName ?? $"{_definition.Type}_{_definition.FieldName}";

        public void Add(FeatureRecord record)
        {
            if (_definition.Type == StatisticType.Count)
            {
                if (TryGetAttribute(record, _definition.FieldName, out var value) && value is not null)
                {
                    _nonNullCount++;
                }
                return;
            }

            if (!TryGetAttribute(record, _definition.FieldName, out var raw) || raw is null)
            {
                return;
            }

            if (!TryConvertToDouble(raw, out var number))
            {
                return;
            }

            switch (_definition.Type)
            {
                case StatisticType.Sum:
                    _sum += number;
                    _nonNullCount++;
                    break;
                case StatisticType.Avg:
                    _sum += number;
                    _nonNullCount++;
                    break;
                case StatisticType.Min:
                    _min = _min.HasValue ? Math.Min(_min.Value, number) : number;
                    _nonNullCount++;
                    break;
                case StatisticType.Max:
                    _max = _max.HasValue ? Math.Max(_max.Value, number) : number;
                    _nonNullCount++;
                    break;
            }
        }

        public object? GetValue()
        {
            return _definition.Type switch
            {
                StatisticType.Count => (int)_nonNullCount,
                StatisticType.Sum => _nonNullCount > 0 ? _sum : 0d,
                StatisticType.Avg => _nonNullCount > 0 ? _sum / _nonNullCount : (double?)null,
                StatisticType.Min => _nonNullCount > 0 ? _min : null,
                StatisticType.Max => _nonNullCount > 0 ? _max : null,
                _ => null
            };
        }
    }

    private static IReadOnlyList<FeatureRecord> ResolveDataset(string layerId)
    {
        if (DatasetIndex.TryGetValue(layerId, out var dataset))
        {
            return dataset;
        }

        return RoadRecords;
    }

    private static string ResolvePrimaryKey(string layerId)
    {
        return layerId.Equals("roads-inspections", StringComparison.OrdinalIgnoreCase)
            ? "inspection_id"
            : "road_id";
    }

    private static string? ResolveTemporalColumn(string layerId)
    {
        return layerId.Equals("roads-inspections", StringComparison.OrdinalIgnoreCase)
            ? "observed_at"
            : "observed_at";
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

        foreach (var coordinate in coordinates)
        {
            if (coordinate is not JsonArray position || position.Count < 2)
            {
                continue;
            }

            var x = position[0]!.GetValue<double>();
            var y = position[1]!.GetValue<double>();
            if (x >= box.MinX && x <= box.MaxX && y >= box.MinY && y <= box.MaxY)
            {
                return true;
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

internal sealed class StubRasterAnalyticsService : IRasterAnalyticsService
{
    private static readonly RasterAnalyticsCapabilities Capabilities = new(
        SupportedAlgebraOperators: new[] { "+", "-", "*", "/" },
        SupportedAlgebraFunctions: new[] { "mean", "sum", "min", "max" },
        SupportedTerrainAnalyses: new[]
        {
            TerrainAnalysisType.Hillshade.ToString(),
            TerrainAnalysisType.Slope.ToString(),
            TerrainAnalysisType.Aspect.ToString()
        },
        MaxAlgebraDatasets: 4,
        MaxExtractionPoints: 250,
        MaxHistogramBins: 512,
        MaxZonalPolygons: 1000);

    public Task<RasterStatistics> CalculateStatisticsAsync(RasterStatisticsRequest request, CancellationToken cancellationToken = default)
    {
        var bands = Enumerable.Range(0, 3)
            .Select(index => new BandStatistics(
                BandIndex: index,
                Min: 0,
                Max: 255,
                Mean: 120 + index,
                StdDev: 30,
                Median: 128,
                ValidPixelCount: 10_000,
                NoDataPixelCount: 0,
                NoDataValue: null))
            .ToArray();

        return Task.FromResult(new RasterStatistics(request.Dataset.Id, bands.Length, bands, request.BoundingBox));
    }

    public Task<RasterAlgebraResult> CalculateAlgebraAsync(RasterAlgebraRequest request, CancellationToken cancellationToken = default)
    {
        var statistics = CalculateStatisticsAsync(new RasterStatisticsRequest(request.Datasets.First()), cancellationToken).Result;
        var data = new byte[Math.Max(1, request.Width) * Math.Max(1, request.Height)];
        return Task.FromResult(new RasterAlgebraResult(data, "application/octet-stream", request.Width, request.Height, statistics));
    }

    public Task<RasterValueExtractionResult> ExtractValuesAsync(RasterValueExtractionRequest request, CancellationToken cancellationToken = default)
    {
        var values = request.Points.Select((point, index) => new PointValue(point.X, point.Y, 100 + index, request.BandIndex ?? 0)).ToArray();
        return Task.FromResult(new RasterValueExtractionResult(request.Dataset.Id, values));
    }

    public Task<RasterHistogram> CalculateHistogramAsync(RasterHistogramRequest request, CancellationToken cancellationToken = default)
    {
        var bins = Enumerable.Range(0, Math.Max(1, request.BinCount))
            .Select(index => new HistogramBin(index, index + 1, 25 + index))
            .ToArray();

        return Task.FromResult(new RasterHistogram(request.Dataset.Id, request.BandIndex ?? 0, bins, 0, bins.Length));
    }

    public Task<ZonalStatisticsResult> CalculateZonalStatisticsAsync(ZonalStatisticsRequest request, CancellationToken cancellationToken = default)
    {
        var zones = request.Zones.Select((zone, index) => new ZoneStatistics(
            ZoneId: zone.ZoneId ?? $"zone-{index}",
            BandIndex: request.BandIndex ?? 0,
            Mean: 120 + index,
            Min: 0,
            Max: 255,
            Sum: 10_000 + index * 250,
            StdDev: 25,
            PixelCount: 500 + index * 10,
            Median: 128,
            Properties: zone.Properties)).ToArray();

        return Task.FromResult(new ZonalStatisticsResult(request.Dataset.Id, zones));
    }

    public Task<TerrainAnalysisResult> CalculateTerrainAsync(TerrainAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var data = new byte[Math.Max(1, request.Width) * Math.Max(1, request.Height)];
        var stats = new TerrainAnalysisStatistics(0, 1, 0.5, 0.1, request.AnalysisType == TerrainAnalysisType.Hillshade ? "unitless" : "degrees");
        return Task.FromResult(new TerrainAnalysisResult(data, "application/octet-stream", request.Width, request.Height, request.AnalysisType, stats));
    }

    public RasterAnalyticsCapabilities GetCapabilities() => Capabilities;
}

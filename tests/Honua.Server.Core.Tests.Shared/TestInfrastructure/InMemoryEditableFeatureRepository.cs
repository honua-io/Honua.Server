using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Tests.Shared;

public sealed class InMemoryEditableFeatureRepository : IFeatureRepository
{
    private const string GlobalIdFieldName = "globalId";
    private static readonly string[] SeedGlobalIds =
    {
        "00000000-0000-0000-0000-000000000001",
        "00000000-0000-0000-0000-000000000002",
        "00000000-0000-0000-0000-000000000003"
    };

    private readonly List<FeatureRecord> _features = new();
    private int _nextId;
    private readonly object _sync = new();

    public InMemoryEditableFeatureRepository()
    {
        Reset();
    }

    public IReadOnlyList<FeatureRecord> Features
    {
        get
        {
            lock (_sync)
            {
                return _features.Select(CloneRecord).ToList();
            }
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _features.Clear();
            _features.AddRange(new[]
            {
                CreateFeature(1, "NE Sandy Blvd", "open", new[]
                {
                    (-122.575, 45.523),
                    (-122.560, 45.525)
                }, SeedGlobalIds[0]),
                CreateFeature(2, "SE Division St", "closed", new[]
                {
                    (-122.540, 45.512),
                    (-122.530, 45.508)
                }, SeedGlobalIds[1]),
                CreateFeature(3, "N Williams Ave", "open", new[]
                {
                    (-122.675, 45.552),
                    (-122.669, 45.548)
                }, SeedGlobalIds[2])
            });

            _nextId = _features
                .Select(f => Convert.ToInt32(f.Attributes["road_id"], CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0)
                .Max() + 1;
        }
    }

    public Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var attributes = CloneAttributes(record.Attributes);
            if (!attributes.TryGetValue("road_id", out var value) || value is null || string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture)))
            {
                attributes["road_id"] = _nextId++;
            }

            if (!attributes.TryGetValue(GlobalIdFieldName, out var globalValue) || string.IsNullOrWhiteSpace(Convert.ToString(globalValue, CultureInfo.InvariantCulture)))
            {
                attributes[GlobalIdFieldName] = Guid.NewGuid().ToString();
            }

            var stored = new FeatureRecord(attributes);
            _features.Add(stored);
            return Task.FromResult(CloneRecord(stored));
        }
    }

    public Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var existing = FindById(featureId);
            if (existing is null)
            {
                return Task.FromResult<FeatureRecord?>(null);
            }

            var updatedAttributes = CloneAttributes(existing.Attributes);
            foreach (var pair in record.Attributes)
            {
                updatedAttributes[pair.Key] = pair.Value;
            }

            var updated = new FeatureRecord(updatedAttributes);
            var index = _features.IndexOf(existing);
            _features[index] = updated;
            return Task.FromResult<FeatureRecord?>(CloneRecord(updated));
        }
    }

    public Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var index = _features.FindIndex(f => string.Equals(Convert.ToString(f.Attributes["road_id"], CultureInfo.InvariantCulture), featureId, StringComparison.Ordinal));
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            _features.RemoveAt(index);
            return Task.FromResult(true);
        }
    }

    public Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var record = FindById(featureId);
            return Task.FromResult(record is null ? null : CloneRecord(record));
        }
    }

    public async IAsyncEnumerable<FeatureRecord> QueryAsync(string serviceId, string layerId, FeatureQuery? query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<FeatureRecord> snapshot;
        lock (_sync)
        {
            snapshot = ApplyQuery(_features, query).Select(CloneRecord).ToList();
        }

        foreach (var record in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
            await Task.Yield();
        }
    }

    public Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var count = ApplyQuery(_features, query).Count;
            return Task.FromResult((long)count);
        }
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
        // Stub implementation for tests
        return Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());
    }

    public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        // Stub implementation for tests
        return Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());
    }

    public Task<BoundingBox?> QueryExtentAsync(
        string serviceId,
        string layerId,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        // Stub implementation for tests - return a bounding box covering Portland, OR
        return Task.FromResult<BoundingBox?>(new BoundingBox(-122.7, 45.5, -122.5, 45.6));
    }

    private FeatureRecord? FindById(string featureId)
    {
        return _features.FirstOrDefault(f =>
            string.Equals(Convert.ToString(f.Attributes["road_id"], CultureInfo.InvariantCulture), featureId, StringComparison.Ordinal));
    }

    private static FeatureRecord CloneRecord(FeatureRecord record)
    {
        return new FeatureRecord(CloneAttributes(record.Attributes));
    }

    private static Dictionary<string, object?> CloneAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        var copy = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in attributes)
        {
            copy[pair.Key] = CloneValue(pair.Value);
        }

        return copy;
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode node => JsonNode.Parse(node.ToJsonString()),
            JsonElement element => JsonNode.Parse(element.GetRawText()),
            ICloneable cloneable => cloneable.Clone(),
            _ => value
        };
    }

    private static List<FeatureRecord> ApplyQuery(IReadOnlyList<FeatureRecord> source, FeatureQuery? query)
    {
        IEnumerable<FeatureRecord> candidates = source;

        if (query?.Filter?.Expression is not null)
        {
            candidates = candidates.Where(feature => EvaluateBoolean(query.Filter.Expression, feature));
        }

        if (query?.Bbox is not null)
        {
            candidates = candidates.Where(feature => Intersects(feature, query.Bbox));
        }

        if (query?.Offset is int offset and > 0)
        {
            candidates = candidates.Skip(offset);
        }

        if (query?.Limit is int limit and >= 0)
        {
            candidates = candidates.Take(limit);
        }

        return candidates.ToList();
    }

    private static bool EvaluateBoolean(QueryExpression expression, FeatureRecord record)
    {
        return expression switch
        {
            QueryBinaryExpression binary => EvaluateBinary(binary, record),
            QueryUnaryExpression unary when unary.Operator == QueryUnaryOperator.Not => !EvaluateBoolean(unary.Operand, record),
            QueryFieldReference field => Convert.ToBoolean(NormalizeValue(record.Attributes.TryGetValue(field.Name, out var value) ? value : null) ?? false, CultureInfo.InvariantCulture),
            QueryConstant constant => constant.Value is bool b && b,
            QueryFunctionExpression function => EvaluateFunction(function, record),
            _ => false
        };
    }

    private static bool EvaluateBinary(QueryBinaryExpression binary, FeatureRecord record)
    {
        if (binary.Operator == QueryBinaryOperator.And)
        {
            return EvaluateBoolean(binary.Left, record) && EvaluateBoolean(binary.Right, record);
        }

        if (binary.Operator == QueryBinaryOperator.Or)
        {
            return EvaluateBoolean(binary.Left, record) || EvaluateBoolean(binary.Right, record);
        }

        var left = NormalizeValue(GetValue(binary.Left, record));
        var right = NormalizeValue(GetValue(binary.Right, record));

        return binary.Operator switch
        {
            QueryBinaryOperator.Equal => AreEqual(left, right),
            QueryBinaryOperator.NotEqual => !AreEqual(left, right),
            QueryBinaryOperator.GreaterThan => CompareValues(left, right) > 0,
            QueryBinaryOperator.GreaterThanOrEqual => CompareValues(left, right) >= 0,
            QueryBinaryOperator.LessThan => CompareValues(left, right) < 0,
            QueryBinaryOperator.LessThanOrEqual => CompareValues(left, right) <= 0,
            _ => false
        };
    }

    private static object? GetValue(QueryExpression expression, FeatureRecord record)
    {
        return expression switch
        {
            QueryFieldReference field => record.Attributes.TryGetValue(field.Name, out var value) ? value : null,
            QueryConstant constant => constant.Value,
            _ => null
        };
    }

    private static bool EvaluateFunction(QueryFunctionExpression function, FeatureRecord record)
    {
        if (function.Arguments.Count != 2)
        {
            return false;
        }

        var field = function.Arguments[0] as QueryFieldReference ?? function.Arguments[1] as QueryFieldReference;
        var constant = function.Arguments[0] as QueryConstant ?? function.Arguments[1] as QueryConstant;

        if (field is null || constant?.Value is not QueryGeometryValue geometry)
        {
            return false;
        }

        if (!record.Attributes.TryGetValue(field.Name, out var value) || value is not JsonObject geom)
        {
            return false;
        }

        if (geometry.WellKnownText is null)
        {
            return false;
        }

        var reader = new WKTReader();
        var geometryFilter = reader.Read(geometry.WellKnownText);
        var writer = new GeoJsonWriter();
        var stored = JsonDocument.Parse(geom.ToJsonString());
        var storedGeometry = new GeoJsonReader().Read<NetTopologySuite.Geometries.Geometry>(stored.RootElement.GetRawText());

        if (storedGeometry is null)
        {
            return false;
        }

        return function.Name.ToLowerInvariant() switch
        {
            "geo.intersects" => storedGeometry.Intersects(geometryFilter),
            "geo.contains" => storedGeometry.Contains(geometryFilter),
            "geo.within" => storedGeometry.Within(geometryFilter),
            "geo.crosses" => storedGeometry.Crosses(geometryFilter),
            "geo.overlaps" => storedGeometry.Overlaps(geometryFilter),
            "geo.touches" => storedGeometry.Touches(geometryFilter),
            "geo.disjoint" => storedGeometry.Disjoint(geometryFilter),
            "geo.equals" => storedGeometry.EqualsExact(geometryFilter),
            _ => false
        };
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            JsonElement element => NormalizeJsonElement(element),
            JsonValue jsonValue => jsonValue.GetValue<object?>(),
            _ => value
        };
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left is IComparable comparable)
        {
            try
            {
                var rightConverted = Convert.ChangeType(right, left.GetType(), CultureInfo.InvariantCulture);
                return comparable.CompareTo(rightConverted) == 0;
            }
            catch
            {
            }
        }

        return string.Equals(Convert.ToString(left, CultureInfo.InvariantCulture), Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static int CompareValues(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is IComparable comparable)
        {
            try
            {
                var converted = Convert.ChangeType(right, left.GetType(), CultureInfo.InvariantCulture);
                return comparable.CompareTo(converted);
            }
            catch
            {
            }
        }

        var leftString = Convert.ToString(left, CultureInfo.InvariantCulture) ?? string.Empty;
        var rightString = Convert.ToString(right, CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Compare(leftString, rightString, StringComparison.Ordinal);
    }

    private static bool Intersects(FeatureRecord record, BoundingBox box)
    {
        if (!record.Attributes.TryGetValue("geom", out var geometry) || geometry is not JsonObject geomObject)
        {
            return false;
        }

        if (!geomObject.TryGetPropertyValue("coordinates", out var node) || node is not JsonArray coordinates)
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

    private static FeatureRecord CreateFeature(int id, string name, string status, IReadOnlyList<(double X, double Y)> coordinates, string? globalId = null)
    {
        var coordinateArray = new JsonArray();
        foreach (var (x, y) in coordinates)
        {
            coordinateArray.Add(new JsonArray(x, y));
        }

        var geometry = new JsonObject
        {
            ["type"] = "LineString",
            ["coordinates"] = coordinateArray
        };

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["road_id"] = id,
            ["name"] = name,
            ["status"] = status,
            ["geom"] = geometry,
            [GlobalIdFieldName] = globalId ?? Guid.NewGuid().ToString()
        };

        return new FeatureRecord(attributes);
    }
}

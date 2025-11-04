// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Enterprise.Data.Elasticsearch;

public sealed partial class ElasticsearchDataStoreProvider
{
    private static JsonObject BuildSearchBody(FeatureQuery query, LayerDefinition layer, bool includePaging)
    {
        var body = new JsonObject();

        if (includePaging)
        {
            var size = query.ResultType == FeatureResultType.Hits
                ? 0
                : query.Limit ?? layer.Query?.MaxRecordCount ?? 100;

            if (query.Offset.HasValue && query.Offset.Value > 0)
            {
                body["from"] = query.Offset.Value;
            }

            body["size"] = size;
        }

        var queryObject = BuildQueryObject(query, layer);
        if (queryObject is not null)
        {
            body["query"] = queryObject;
        }

        if (query.PropertyNames is { Count: > 0 })
        {
            var array = new JsonArray();
            foreach (var property in query.PropertyNames)
            {
                array.Add(property);
            }

            body["_source"] = array;
        }

        if (query.SortOrders is { Count: > 0 })
        {
            body["sort"] = BuildSortArray(query.SortOrders);
        }

        return body;
    }

    private static JsonObject? BuildQueryObject(FeatureQuery query, LayerDefinition layer)
    {
        var filters = new List<JsonNode>();

        if (query.Filter?.Expression is not null)
        {
            filters.Add(BuildExpressionNode(query.Filter.Expression));
        }

        // Use LayerMetadataHelper to get geometry column
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);
        if (query.Bbox is not null && geometryField.HasValue())
        {
            filters.Add(BuildBoundingBoxNode(geometryField, query.Bbox));
        }

        if (query.Temporal is not null && layer.Storage?.TemporalColumn.HasValue() == true)
        {
            filters.Add(BuildTemporalNode(layer.Storage!.TemporalColumn!, query.Temporal));
        }

        if (filters.Count == 0)
        {
            return null;
        }

        var filterArray = new JsonArray(filters.ToArray());
        var boolObject = new JsonObject
        {
            ["filter"] = filterArray
        };

        return new JsonObject
        {
            ["bool"] = boolObject
        };
    }

    private static JsonArray BuildSortArray(IReadOnlyList<FeatureSortOrder> sortOrders)
    {
        var array = new JsonArray();

        foreach (var sort in sortOrders)
        {
            var direction = sort.Direction == FeatureSortDirection.Descending ? "desc" : "asc";
            var sortObject = new JsonObject
            {
                [sort.Field] = new JsonObject
                {
                    ["order"] = direction
                }
            };

            array.Add(sortObject);
        }

        return array;
    }

    private static JsonObject BuildBoundingBoxNode(string geometryField, BoundingBox bbox)
    {
        return new JsonObject
        {
            ["geo_bounding_box"] = new JsonObject
            {
                [geometryField] = new JsonObject
                {
                    ["top_left"] = new JsonObject
                    {
                        ["lat"] = bbox.MaxY,
                        ["lon"] = bbox.MinX
                    },
                    ["bottom_right"] = new JsonObject
                    {
                        ["lat"] = bbox.MinY,
                        ["lon"] = bbox.MaxX
                    }
                }
            }
        };
    }

    private static JsonObject BuildTemporalNode(string field, TemporalInterval temporal)
    {
        var range = new JsonObject();

        if (temporal.Start.HasValue)
        {
            // Use FeatureRecordNormalizer to ensure UTC conversion
            var normalized = FeatureRecordNormalizer.NormalizeDateTimeOffsetValue(temporal.Start.Value);
            range["gte"] = normalized.ToString("o", CultureInfo.InvariantCulture);
        }

        if (temporal.End.HasValue)
        {
            // Use FeatureRecordNormalizer to ensure UTC conversion
            var normalized = FeatureRecordNormalizer.NormalizeDateTimeOffsetValue(temporal.End.Value);
            range["lte"] = normalized.ToString("o", CultureInfo.InvariantCulture);
        }

        return new JsonObject
        {
            ["range"] = new JsonObject
            {
                [field] = range
            }
        };
    }

    private static JsonObject BuildExpressionNode(QueryExpression expression)
    {
        return expression switch
        {
            QueryBinaryExpression binary => BuildBinaryExpression(binary),
            QueryUnaryExpression unary => BuildUnaryExpression(unary),
            QueryFunctionExpression function => BuildFunctionExpression(function),
            _ => throw new NotSupportedException($"Query expression type '{expression.GetType().Name}' is not supported by the Elasticsearch provider.")
        };
    }

    private static JsonObject BuildBinaryExpression(QueryBinaryExpression binary)
    {
        if (binary.Operator is QueryBinaryOperator.And or QueryBinaryOperator.Or)
        {
            var left = BuildExpressionNode(binary.Left);
            var right = BuildExpressionNode(binary.Right);

            var clauseName = binary.Operator == QueryBinaryOperator.And ? "must" : "should";
            var boolObject = new JsonObject
            {
                [clauseName] = new JsonArray(left, right)
            };

            if (binary.Operator == QueryBinaryOperator.Or)
            {
                boolObject["minimum_should_match"] = 1;
            }

            return new JsonObject
            {
                ["bool"] = boolObject
            };
        }

        if (binary.Left is QueryFunctionExpression leftFunction)
        {
            return BuildFunctionComparison(binary, leftFunction, null);
        }

        if (binary.Right is QueryFunctionExpression rightFunction)
        {
            return BuildFunctionComparison(binary, null, rightFunction);
        }

        if (!TryExtractFieldComparison(binary.Left, binary.Right, out var field, out var value, out var fieldOnLeft))
        {
            throw new NotSupportedException("Elasticsearch provider only supports comparisons between fields and constant values.");
        }

        var operatorToUse = AdjustOperator(binary.Operator, fieldOnLeft);

        return operatorToUse switch
        {
            QueryBinaryOperator.Equal => BuildTermQuery(field, value),
            QueryBinaryOperator.NotEqual => BuildBoolMustNot(BuildTermQuery(field, value)),
            QueryBinaryOperator.GreaterThan => BuildRangeQuery(field, "gt", value),
            QueryBinaryOperator.GreaterThanOrEqual => BuildRangeQuery(field, "gte", value),
            QueryBinaryOperator.LessThan => BuildRangeQuery(field, "lt", value),
            QueryBinaryOperator.LessThanOrEqual => BuildRangeQuery(field, "lte", value),
            _ => throw new NotSupportedException($"Unsupported binary operator '{binary.Operator}' for Elasticsearch provider.")
        };
    }

    private static JsonObject BuildFunctionComparison(
        QueryBinaryExpression binary,
        QueryFunctionExpression? leftFunction,
        QueryFunctionExpression? rightFunction)
    {
        var function = leftFunction ?? rightFunction
            ?? throw new InvalidOperationException("Function comparison requires at least one function expression.");

        var isFunctionOnLeft = leftFunction is not null;
        var otherExpression = isFunctionOnLeft ? binary.Right : binary.Left;

        return function.Name.ToLowerInvariant() switch
        {
            "geo.distance" => BuildGeoDistanceComparison(binary.Operator, function, otherExpression, isFunctionOnLeft),
            "geo.length" => throw new NotSupportedException("geo.length comparisons are not yet supported by the Elasticsearch provider."),
            _ => throw new NotSupportedException($"Function '{function.Name}' is not supported in comparisons by the Elasticsearch provider.")
        };
    }

    private static JsonObject BuildGeoIntersects(QueryFunctionExpression function)
    {
        if (function.Arguments.Count != 2)
        {
            throw new NotSupportedException("geo.intersects requires two arguments: field and geometry constant.");
        }

        var field = ExtractFieldName(function.Arguments[0]);
        var geometryNode = ConvertGeometryArgument(function.Arguments[1]);

        return new JsonObject
        {
            ["geo_shape"] = new JsonObject
            {
                [field] = new JsonObject
                {
                    ["relation"] = "intersects",
                    ["shape"] = geometryNode
                }
            }
        };
    }

    private static JsonObject BuildSubstring(QueryFunctionExpression function)
    {
        if (function.Arguments.Count != 2)
        {
            throw new NotSupportedException("substringof requires exactly two arguments.");
        }

        // OData order: substringof(search, field)
        var (field, value) = ExtractFieldAndValue(function.Arguments[1], function.Arguments[0]);
        return BuildWildcardQuery(field, value, leadingWildcard: true, trailingWildcard: true);
    }

    private static JsonObject BuildContains(QueryFunctionExpression function)
    {
        if (function.Arguments.Count != 2)
        {
            throw new NotSupportedException("contains requires exactly two arguments.");
        }

        // Modern OData contains(field, search)
        var (field, value) = ExtractFieldAndValue(function.Arguments[0], function.Arguments[1]);
        return BuildWildcardQuery(field, value, leadingWildcard: true, trailingWildcard: true);
    }

    private static JsonObject BuildStartsWith(QueryFunctionExpression function)
    {
        if (function.Arguments.Count != 2)
        {
            throw new NotSupportedException("startswith requires exactly two arguments.");
        }

        var (field, value) = ExtractFieldAndValue(function.Arguments[0], function.Arguments[1]);
        return BuildPrefixQuery(field, value);
    }

    private static JsonObject BuildEndsWith(QueryFunctionExpression function)
    {
        if (function.Arguments.Count != 2)
        {
            throw new NotSupportedException("endswith requires exactly two arguments.");
        }

        var (field, value) = ExtractFieldAndValue(function.Arguments[0], function.Arguments[1]);
        return BuildWildcardQuery(field, value, leadingWildcard: true, trailingWildcard: false);
    }

    private static JsonObject BuildGeoDistanceComparison(
        QueryBinaryOperator op,
        QueryFunctionExpression function,
        QueryExpression otherExpression,
        bool functionOnLeft)
    {
        if (function.Arguments.Count != 2)
        {
            throw new NotSupportedException("geo.distance requires two arguments: field and point geometry.");
        }

        var field = ExtractFieldName(function.Arguments[0]);
        var pointNode = ConvertGeometryArgument(function.Arguments[1], requirePoint: true) as JsonObject
            ?? throw new InvalidOperationException("geo.distance requires a POINT geometry expressed as GeoJSON or WKT.");

        var constantValue = ExtractNumericValue(otherExpression);
        var adjustedOperator = functionOnLeft ? op : AdjustOperator(op, fieldOnLeft: false);

        return BuildGeoDistanceQuery(field, pointNode, constantValue, adjustedOperator);
    }

    private static JsonObject BuildWildcardQuery(string field, string value, bool leadingWildcard, bool trailingWildcard)
    {
        var pattern = new StringBuilder();
        if (leadingWildcard)
        {
            pattern.Append('*');
        }

        pattern.Append(EscapeWildcard(value));

        if (trailingWildcard)
        {
            pattern.Append('*');
        }

        return new JsonObject
        {
            ["wildcard"] = new JsonObject
            {
                [field] = new JsonObject
                {
                    ["value"] = pattern.ToString(),
                    ["case_insensitive"] = true
                }
            }
        };
    }

    private static JsonObject BuildPrefixQuery(string field, string value)
    {
        return new JsonObject
        {
            ["prefix"] = new JsonObject
            {
                [field] = new JsonObject
                {
                    ["value"] = value,
                    ["case_insensitive"] = true
                }
            }
        };
    }

    private static string EscapeWildcard(string value)
    {
        if (value.IsNullOrEmpty())
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(ch switch
            {
                '*' => "\\*",
                '?' => "\\?",
                '\\' => "\\\\",
                _ => ch.ToString()
            });
        }

        return builder.ToString();
    }

    private static (string Field, string Value) ExtractFieldAndValue(QueryExpression fieldExpression, QueryExpression valueExpression)
    {
        var field = ExtractFieldName(fieldExpression);
        var value = ExtractStringValue(valueExpression);
        return (field, value);
    }

    private static JsonObject BuildGeoDistanceQuery(string field, JsonObject pointNode, double distanceMeters, QueryBinaryOperator op)
    {
        if (distanceMeters < 0)
        {
            throw new InvalidOperationException("geo.distance comparisons require a non-negative distance value.");
        }

        var distanceString = distanceMeters.ToString("0.###", CultureInfo.InvariantCulture);

        var geoDistance = new JsonObject
        {
            ["distance"] = distanceString + "m",
            [field] = pointNode
        };

        var baseQuery = new JsonObject
        {
            ["geo_distance"] = geoDistance
        };

        return op switch
        {
            QueryBinaryOperator.LessThan => baseQuery,
            QueryBinaryOperator.LessThanOrEqual => baseQuery,
            QueryBinaryOperator.GreaterThan => BuildBoolMustNot(baseQuery),
            QueryBinaryOperator.GreaterThanOrEqual => BuildBoolMustNot(baseQuery),
            _ => throw new NotSupportedException($"Comparison '{op}' is not supported for geo.distance().")
        };
    }

    private static QueryBinaryOperator AdjustOperator(QueryBinaryOperator op, bool fieldOnLeft)
    {
        if (fieldOnLeft)
        {
            return op;
        }

        return op switch
        {
            QueryBinaryOperator.GreaterThan => QueryBinaryOperator.LessThan,
            QueryBinaryOperator.GreaterThanOrEqual => QueryBinaryOperator.LessThanOrEqual,
            QueryBinaryOperator.LessThan => QueryBinaryOperator.GreaterThan,
            QueryBinaryOperator.LessThanOrEqual => QueryBinaryOperator.GreaterThanOrEqual,
            _ => op
        };
    }

    private static JsonObject BuildTermQuery(string field, object? value)
    {
        return new JsonObject
        {
            ["term"] = new JsonObject
            {
                [field] = ConvertValueToJsonNode(value) ?? JsonValue.Create((string?)null)
            }
        };
    }

    private static JsonObject BuildRangeQuery(string field, string comparison, object? value)
    {
        return new JsonObject
        {
            ["range"] = new JsonObject
            {
                [field] = new JsonObject
                {
                    [comparison] = ConvertValueToJsonNode(value) ?? JsonValue.Create((string?)null)
                }
            }
        };
    }

    private static JsonObject BuildBoolMustNot(JsonObject innerQuery)
    {
        return new JsonObject
        {
            ["bool"] = new JsonObject
            {
                ["must_not"] = new JsonArray(innerQuery)
            }
        };
    }

    private static JsonObject BuildUnaryExpression(QueryUnaryExpression unary)
    {
        if (unary.Operator != QueryUnaryOperator.Not)
        {
            throw new NotSupportedException($"Unary operator '{unary.Operator}' is not supported by the Elasticsearch provider.");
        }

        var inner = BuildExpressionNode(unary.Operand);

        return new JsonObject
        {
            ["bool"] = new JsonObject
            {
                ["must_not"] = new JsonArray(inner)
            }
        };
    }

    private static JsonObject BuildFunctionExpression(QueryFunctionExpression function)
    {
        return function.Name.ToLowerInvariant() switch
        {
            "geo.intersects" => BuildGeoIntersects(function),
            "substringof" => BuildSubstring(function),
            "contains" => BuildContains(function),
            "startswith" => BuildStartsWith(function),
            "endswith" => BuildEndsWith(function),
            _ => throw new NotSupportedException($"Query function '{function.Name}' is not supported by the Elasticsearch provider.")
        };
    }

    private static bool TryExtractFieldComparison(
        QueryExpression left,
        QueryExpression right,
        out string field,
        out object? value,
        out bool fieldOnLeft)
    {
        if (left is QueryFieldReference leftField && right is QueryConstant rightConstant)
        {
            field = leftField.Name;
            value = rightConstant.Value;
            fieldOnLeft = true;
            return true;
        }

        if (right is QueryFieldReference rightField && left is QueryConstant leftConstant)
        {
            field = rightField.Name;
            value = leftConstant.Value;
            fieldOnLeft = false;
            return true;
        }

        field = string.Empty;
        value = null;
        fieldOnLeft = true;
        return false;
    }

    private static string ExtractFieldName(QueryExpression expression)
    {
        return expression switch
        {
            QueryFieldReference field => field.Name,
            _ => throw new NotSupportedException("Expected a field reference in Elasticsearch filter expression.")
        };
    }

    private static object? ExtractConstantValue(QueryExpression expression)
    {
        return expression switch
        {
            QueryConstant constant => constant.Value,
            _ => throw new NotSupportedException("Elasticsearch filter expressions require constant values for this operation.")
        };
    }

    private static string ExtractStringValue(QueryExpression expression)
    {
        var value = ExtractConstantValue(expression);
        return value is null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static double ExtractNumericValue(QueryExpression expression)
    {
        var value = ExtractConstantValue(expression) ?? throw new InvalidOperationException("Numeric comparison requires a constant value.");

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidOperationException("Numeric comparison requires a numeric constant value.", ex);
        }
    }

    private static JsonNode ConvertGeometryArgument(QueryExpression expression, bool requirePoint = false)
    {
        var value = ExtractConstantValue(expression);
        var node = ConvertGeometryValue(value, requirePoint);
        if (node is null)
        {
            throw new InvalidOperationException("Unable to parse geometry constant for Elasticsearch query.");
        }

        return node;
    }
}

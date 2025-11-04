// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query.Filter;

/// <summary>
/// Represents a temporal interval with start and end times.
/// </summary>
internal record TemporalInterval(DateTimeOffset Start, DateTimeOffset End);

public static class Cql2JsonParser
{
    public static QueryFilter Parse(string json, LayerDefinition layer, string? filterCrs)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("CQL2 JSON filter payload is empty.");
        }

        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement, layer, filterCrs);
    }

    public static QueryFilter Parse(JsonElement element, LayerDefinition layer, string? filterCrs)
    {
        var expression = ParseExpression(element, layer, filterCrs);
        return new QueryFilter(expression);
    }

    private static QueryExpression ParseExpression(JsonElement element, LayerDefinition layer, string? filterCrs)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("op", out var opElement))
            {
                var opName = opElement.GetString();
                if (string.IsNullOrWhiteSpace(opName))
                {
                    throw new InvalidOperationException("CQL2 JSON filter 'op' must be a non-empty string.");
                }

                if (!element.TryGetProperty("args", out var argsElement) || argsElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("CQL2 JSON filter 'args' must be an array.");
                }

                return ParseOperation(opName, argsElement, layer, filterCrs);
            }

            if (element.TryGetProperty("property", out var propertyElement) && propertyElement.ValueKind == JsonValueKind.String)
            {
                var candidate = propertyElement.GetString() ?? string.Empty;
                var (fieldName, _) = CqlFilterParserUtils.ResolveField(layer, candidate);
                return new QueryFieldReference(fieldName);
            }

            if (element.TryGetProperty("value", out var literalElement))
            {
                return new QueryConstant(ReadLiteral(literalElement));
            }

            if (element.TryGetProperty("interval", out var intervalElement))
            {
                // Handle spec-conformant temporal interval objects: { "interval": [start, end] }
                return new QueryConstant(ParseTemporalInterval(intervalElement));
            }

            if (IsGeometryObject(element))
            {
                return new QueryConstant(ParseGeometry(element, filterCrs));
            }

            throw new InvalidOperationException("Unsupported structure in CQL2 JSON expression.");
        }

        return new QueryConstant(ReadLiteral(element));
    }

    private static QueryExpression ParseOperation(string op, JsonElement argsElement, LayerDefinition layer, string? filterCrs)
    {
        var normalizedOp = op.Trim().ToLowerInvariant();

        // Special handling for IN operator - needs raw JsonElement to parse array
        if (normalizedOp == "in")
        {
            return BuildInExpression(argsElement, layer, filterCrs);
        }

        var arguments = ParseArguments(argsElement, layer, filterCrs);

        return normalizedOp switch
        {
            "=" or "eq" => BuildComparisonExpression(QueryBinaryOperator.Equal, arguments, layer),
            "<>" or "!=" or "ne" => BuildComparisonExpression(QueryBinaryOperator.NotEqual, arguments, layer),
            "<" or "lt" => BuildComparisonExpression(QueryBinaryOperator.LessThan, arguments, layer),
            "<=" or "le" => BuildComparisonExpression(QueryBinaryOperator.LessThanOrEqual, arguments, layer),
            ">" or "gt" => BuildComparisonExpression(QueryBinaryOperator.GreaterThan, arguments, layer),
            ">=" or "ge" => BuildComparisonExpression(QueryBinaryOperator.GreaterThanOrEqual, arguments, layer),
            "and" => BuildLogicalExpression(QueryBinaryOperator.And, arguments, layer),
            "or" => BuildLogicalExpression(QueryBinaryOperator.Or, arguments, layer),
            "not" => BuildNotExpression(arguments),
            "between" => BuildBetweenExpression(arguments, layer),
            "isnull" => BuildIsNullExpression(arguments, layer),
            "s_intersects" or "intersects" => BuildSpatialFunction("geo.intersects", arguments, layer, filterCrs),
            "s_contains" or "contains" => BuildSpatialFunction("geo.contains", arguments, layer, filterCrs),
            "s_within" or "within" => BuildSpatialFunction("geo.within", arguments, layer, filterCrs),
            "s_crosses" or "crosses" => BuildSpatialFunction("geo.crosses", arguments, layer, filterCrs),
            "s_overlaps" or "overlaps" => BuildSpatialFunction("geo.overlaps", arguments, layer, filterCrs),
            "s_touches" or "touches" => BuildSpatialFunction("geo.touches", arguments, layer, filterCrs),
            "s_disjoint" or "disjoint" => BuildSpatialFunction("geo.disjoint", arguments, layer, filterCrs),
            "s_equals" or "equals" => BuildSpatialFunction("geo.equals", arguments, layer, filterCrs),
            "t_after" or "after" => BuildTemporalComparison(QueryBinaryOperator.GreaterThan, arguments, layer),
            "t_before" or "before" => BuildTemporalComparison(QueryBinaryOperator.LessThan, arguments, layer),
            "t_during" or "during" => BuildTemporalDuring(arguments, layer),
            "t_equals" or "t-equals" => BuildTemporalComparison(QueryBinaryOperator.Equal, arguments, layer),
            _ => throw new InvalidOperationException($"CQL2 operation '{op}' is not supported.")
        };
    }

    private static IReadOnlyList<QueryExpression> ParseArguments(JsonElement argsElement, LayerDefinition layer, string? filterCrs)
    {
        var list = new List<QueryExpression>();
        foreach (var item in argsElement.EnumerateArray())
        {
            list.Add(ParseExpression(item, layer, filterCrs));
        }

        return list;
    }

    private static QueryExpression BuildComparisonExpression(QueryBinaryOperator op, IReadOnlyList<QueryExpression> arguments, LayerDefinition layer)
    {
        if (arguments.Count != 2)
        {
            throw new InvalidOperationException($"Comparison operator requires exactly two arguments; received {arguments.Count}.");
        }

        var left = arguments[0];
        var right = arguments[1];

        if (left is QueryFieldReference leftField && right is QueryConstant rightConstant)
        {
            var coerced = CoerceConstant(layer, leftField.Name, rightConstant);
            return new QueryBinaryExpression(left, op, coerced);
        }

        if (right is QueryFieldReference rightField && left is QueryConstant leftConstant)
        {
            var coerced = CoerceConstant(layer, rightField.Name, leftConstant);
            return new QueryBinaryExpression(new QueryFieldReference(rightField.Name), op, coerced);
        }

        throw new InvalidOperationException("Comparison operators require a property reference and a literal value.");
    }

    private static QueryExpression BuildLogicalExpression(QueryBinaryOperator op, IReadOnlyList<QueryExpression> arguments, LayerDefinition layer)
    {
        if (arguments.Count < 2)
        {
            throw new InvalidOperationException($"Logical operator requires at least two arguments; received {arguments.Count}.");
        }

        var expression = arguments[0];
        for (var i = 1; i < arguments.Count; i++)
        {
            expression = new QueryBinaryExpression(expression, op, arguments[i]);
        }

        return expression;
    }

    private static QueryExpression BuildNotExpression(IReadOnlyList<QueryExpression> arguments)
    {
        if (arguments.Count != 1)
        {
            throw new InvalidOperationException("NOT operator requires a single argument.");
        }

        return new QueryUnaryExpression(QueryUnaryOperator.Not, arguments[0]);
    }

    private static QueryExpression BuildSpatialFunction(string functionName, IReadOnlyList<QueryExpression> arguments, LayerDefinition layer, string? filterCrs)
    {
        if (arguments.Count != 2)
        {
            throw new InvalidOperationException($"Spatial function requires exactly two arguments; received {arguments.Count}.");
        }

        QueryFieldReference field;
        QueryGeometryValue geometry;

        if (arguments[0] is QueryFieldReference firstField &&
            arguments[1] is QueryConstant constant1 &&
            constant1.Value is QueryGeometryValue geometryValue1)
        {
            field = firstField;
            geometry = ApplyFilterCrs(geometryValue1, filterCrs);
        }
        else if (arguments[1] is QueryFieldReference secondField &&
                 arguments[0] is QueryConstant constant2 &&
                 constant2.Value is QueryGeometryValue geometryValue2)
        {
            field = secondField;
            geometry = ApplyFilterCrs(geometryValue2, filterCrs);
        }
        else
        {
            throw new InvalidOperationException("Spatial function requires a property reference and a geometry argument.");
        }

        return new QueryFunctionExpression(functionName, new QueryExpression[]
        {
            field,
            new QueryConstant(geometry)
        });
    }

    private static QueryGeometryValue ApplyFilterCrs(QueryGeometryValue geometry, string? filterCrs)
    {
        if (geometry.Srid.HasValue || string.IsNullOrWhiteSpace(filterCrs))
        {
            return geometry;
        }

        return new QueryGeometryValue(geometry.WellKnownText, CrsHelper.ParseCrs(filterCrs));
    }

    private static QueryExpression BuildTemporalComparison(QueryBinaryOperator op, IReadOnlyList<QueryExpression> arguments, LayerDefinition layer)
    {
        if (arguments.Count != 2)
        {
            throw new InvalidOperationException($"Temporal comparison requires exactly two arguments; received {arguments.Count}.");
        }

        var left = arguments[0];
        var right = arguments[1];

        // Ensure we have a field and a temporal value
        if (left is QueryFieldReference leftField && right is QueryConstant rightConstant)
        {
            var coerced = CoerceTemporalConstant(rightConstant);
            return new QueryBinaryExpression(leftField, op, coerced);
        }

        if (right is QueryFieldReference rightField && left is QueryConstant leftConstant)
        {
            var coerced = CoerceTemporalConstant(leftConstant);
            return new QueryBinaryExpression(rightField, op, coerced);
        }

        throw new InvalidOperationException("Temporal comparison requires a property reference and a temporal literal.");
    }

    private static QueryExpression BuildTemporalDuring(IReadOnlyList<QueryExpression> arguments, LayerDefinition layer)
    {
        if (arguments.Count != 2)
        {
            throw new InvalidOperationException($"Temporal 'during' requires exactly two arguments; received {arguments.Count}.");
        }

        if (arguments[0] is not QueryFieldReference field)
        {
            throw new InvalidOperationException("First argument to 'during' must be a property reference.");
        }

        if (arguments[1] is not QueryConstant intervalConstant)
        {
            throw new InvalidOperationException("Second argument to 'during' must be a temporal interval.");
        }

        DateTimeOffset start;
        DateTimeOffset end;

        // Handle both spec-conformant interval objects and stringified intervals
        if (intervalConstant.Value is TemporalInterval interval)
        {
            start = interval.Start;
            end = interval.End;
        }
        else if (intervalConstant.Value is string intervalJson)
        {
            // Legacy support for stringified intervals
            using var doc = JsonDocument.Parse(intervalJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() != 2)
            {
                throw new InvalidOperationException("Temporal interval must be an array with start and end values.");
            }

            start = ParseTemporalValue(doc.RootElement[0]);
            end = ParseTemporalValue(doc.RootElement[1]);
        }
        else
        {
            throw new InvalidOperationException($"Second argument to 'during' must be a temporal interval, but got {intervalConstant.Value?.GetType().Name ?? "null"}.");
        }

        // Build: field >= start AND field <= end
        var afterStart = new QueryBinaryExpression(field, QueryBinaryOperator.GreaterThanOrEqual, new QueryConstant(start));
        var beforeEnd = new QueryBinaryExpression(field, QueryBinaryOperator.LessThanOrEqual, new QueryConstant(end));
        return new QueryBinaryExpression(afterStart, QueryBinaryOperator.And, beforeEnd);
    }

    private static QueryExpression BuildBetweenExpression(IReadOnlyList<QueryExpression> arguments, LayerDefinition layer)
    {
        if (arguments.Count != 3)
        {
            throw new InvalidOperationException($"BETWEEN operator requires exactly three arguments (property, lower, upper); received {arguments.Count}.");
        }

        if (arguments[0] is not QueryFieldReference field)
        {
            throw new InvalidOperationException("First argument to BETWEEN must be a property reference.");
        }

        // Coerce the lower and upper bounds if they are constants
        QueryExpression lowerBound = arguments[1];
        QueryExpression upperBound = arguments[2];

        if (lowerBound is QueryConstant lowerConstant)
        {
            lowerBound = CoerceConstant(layer, field.Name, lowerConstant);
        }

        if (upperBound is QueryConstant upperConstant)
        {
            upperBound = CoerceConstant(layer, field.Name, upperConstant);
        }

        // Build: property >= lowerBound AND property <= upperBound
        var lowerComparison = new QueryBinaryExpression(field, QueryBinaryOperator.GreaterThanOrEqual, lowerBound);
        var upperComparison = new QueryBinaryExpression(field, QueryBinaryOperator.LessThanOrEqual, upperBound);
        return new QueryBinaryExpression(lowerComparison, QueryBinaryOperator.And, upperComparison);
    }

    private static QueryExpression BuildInExpression(JsonElement argsElement, LayerDefinition layer, string? filterCrs)
    {
        // Parse the args array to get exactly 2 arguments
        var argsList = argsElement.EnumerateArray().ToList();
        if (argsList.Count != 2)
        {
            throw new InvalidOperationException($"IN operator requires exactly two arguments (property, values); received {argsList.Count}.");
        }

        // First argument must be a property reference
        var firstArg = ParseExpression(argsList[0], layer, filterCrs);
        if (firstArg is not QueryFieldReference field)
        {
            throw new InvalidOperationException("First argument to IN must be a property reference.");
        }

        // Second argument must be an array
        var secondArg = argsList[1];
        if (secondArg.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Second argument to IN must be an array.");
        }

        var values = new List<QueryExpression>();
        foreach (var item in secondArg.EnumerateArray())
        {
            var literalValue = ReadLiteral(item);
            var literalConstant = new QueryConstant(literalValue);
            values.Add(CoerceConstant(layer, field.Name, literalConstant));
        }

        if (values.Count == 0)
        {
            throw new InvalidOperationException("IN array cannot be empty.");
        }

        return BuildOrChain(field, values);
    }

    private static QueryExpression BuildIsNullExpression(IReadOnlyList<QueryExpression> arguments, LayerDefinition layer)
    {
        if (arguments.Count != 1)
        {
            throw new InvalidOperationException($"IS NULL operator requires exactly one argument; received {arguments.Count}.");
        }

        if (arguments[0] is not QueryFieldReference field)
        {
            throw new InvalidOperationException("Argument to IS NULL must be a property reference.");
        }

        // Create a comparison with null: field = NULL
        // The SqlFilterTranslator will convert this to IS NULL
        return new QueryBinaryExpression(field, QueryBinaryOperator.Equal, new QueryConstant(null));
    }

    private static QueryExpression BuildOrChain(QueryFieldReference field, IReadOnlyList<QueryExpression> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException("Cannot build OR chain with empty values list.");
        }

        if (values.Count == 1)
        {
            // Optimize single value to simple equality
            return new QueryBinaryExpression(field, QueryBinaryOperator.Equal, values[0]);
        }

        // Build OR chain: field = value1 OR field = value2 OR ...
        var expression = new QueryBinaryExpression(field, QueryBinaryOperator.Equal, values[0]);
        for (var i = 1; i < values.Count; i++)
        {
            var equality = new QueryBinaryExpression(field, QueryBinaryOperator.Equal, values[i]);
            expression = new QueryBinaryExpression(expression, QueryBinaryOperator.Or, equality);
        }

        return expression;
    }

    private static QueryConstant CoerceTemporalConstant(QueryConstant constant)
    {
        if (constant.Value is DateTimeOffset)
        {
            return constant;
        }

        if (constant.Value is string str)
        {
            if (DateTimeOffset.TryParse(str, out var parsed))
            {
                return new QueryConstant(parsed);
            }
        }

        throw new InvalidOperationException($"Cannot coerce value '{constant.Value}' to temporal type.");
    }

    private static DateTimeOffset ParseTemporalValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            if (!string.IsNullOrWhiteSpace(str) && DateTimeOffset.TryParse(str, out var parsed))
            {
                return parsed;
            }
        }

        throw new InvalidOperationException($"Cannot parse temporal value from '{element.GetRawText()}'.");
    }

    private static TemporalInterval ParseTemporalInterval(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 2)
        {
            throw new InvalidOperationException("Temporal interval must be an array with exactly two elements [start, end].");
        }

        var start = ParseTemporalValue(element[0]);
        var end = ParseTemporalValue(element[1]);

        return new TemporalInterval(start, end);
    }

    private static QueryConstant CoerceConstant(LayerDefinition layer, string fieldName, QueryConstant constant)
    {
        if (constant.Value is QueryGeometryValue)
        {
            return constant;
        }

        var (resolvedField, fieldType) = CqlFilterParserUtils.ResolveField(layer, fieldName);
        if (constant.Value is string s && fieldType is not null)
        {
            var converted = CqlFilterParserUtils.ConvertToFieldValue(fieldType, s);
            return new QueryConstant(converted);
        }

        return constant;
    }

    private static object? ReadLiteral(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out var l)
                ? l
                : element.GetDouble(),
            JsonValueKind.Object when IsGeometryObject(element) => ParseGeometry(element, null),
            _ => throw new InvalidOperationException("Unsupported literal value in CQL2 JSON filter.")
        };
    }

    private static QueryGeometryValue ParseGeometry(JsonElement element, string? filterCrs)
    {
        var reader = new GeoJsonReader();
        Geometry geometry;
        try
        {
            geometry = reader.Read<Geometry>(element.GetRawText());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse geometry literal: {ex.Message}");
        }

        var srid = ResolveGeometrySrid(element, geometry, filterCrs);
        var writer = new WKTWriter();
        var wkt = writer.Write(geometry);
        return new QueryGeometryValue(wkt, srid);
    }

    private static int? ResolveGeometrySrid(JsonElement element, Geometry geometry, string? filterCrs)
    {
        if (geometry.SRID != 0)
        {
            return geometry.SRID;
        }

        var crsFromGeometry = ExtractCrsName(element);
        if (!string.IsNullOrWhiteSpace(crsFromGeometry))
        {
            return CrsHelper.ParseCrs(crsFromGeometry);
        }

        if (!string.IsNullOrWhiteSpace(filterCrs))
        {
            return CrsHelper.ParseCrs(filterCrs);
        }

        return null;
    }

    private static bool IsGeometryObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return element.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String;
    }

    private static string? ExtractCrsName(JsonElement element)
    {
        if (!element.TryGetProperty("crs", out var crsElement))
        {
            return null;
        }

        if (crsElement.ValueKind == JsonValueKind.String)
        {
            return crsElement.GetString();
        }

        if (crsElement.ValueKind == JsonValueKind.Object)
        {
            if (crsElement.TryGetProperty("type", out var typeElement) &&
                string.Equals(typeElement.GetString(), "name", StringComparison.OrdinalIgnoreCase) &&
                crsElement.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object &&
                props.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
            {
                return nameElement.GetString();
            }
        }

        return null;
    }
}

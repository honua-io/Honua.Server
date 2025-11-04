// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Microsoft.OData.UriParser;
using Microsoft.Spatial;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query.Filter;

public sealed class ODataFilterParser
{
    private static readonly WellKnownTextSqlFormatter WktFormatter = WellKnownTextSqlFormatter.Create();

    private readonly QueryEntityDefinition _entityDefinition;

    public ODataFilterParser(QueryEntityDefinition entityDefinition)
    {
        _entityDefinition = entityDefinition ?? throw new ArgumentNullException(nameof(entityDefinition));
    }

    public QueryFilter Parse(FilterClause? filterClause)
    {
        if (filterClause is null)
        {
            return new QueryFilter(null);
        }

        var expression = TranslateNode(filterClause.Expression);
        return new QueryFilter(expression);
    }

    private QueryExpression TranslateNode(QueryNode node)
    {
        return node switch
        {
            BinaryOperatorNode binary => TranslateBinary(binary),
            UnaryOperatorNode unary => TranslateUnary(unary),
            SingleValuePropertyAccessNode property => TranslateProperty(property),
            ConstantNode constant => TranslateConstant(constant),
            ConvertNode convert => TranslateNode(convert.Source),
            SingleValueFunctionCallNode function => TranslateFunction(function),
            _ => throw new NotSupportedException($"Filter expression node kind '{node.Kind}' is not supported yet.")
        };
    }

    private QueryExpression TranslateBinary(BinaryOperatorNode node)
    {
        var left = TranslateNode(node.Left);
        var right = TranslateNode(node.Right);

        var op = node.OperatorKind switch
        {
            // Comparison operators
            BinaryOperatorKind.Equal => QueryBinaryOperator.Equal,
            BinaryOperatorKind.NotEqual => QueryBinaryOperator.NotEqual,
            BinaryOperatorKind.GreaterThan => QueryBinaryOperator.GreaterThan,
            BinaryOperatorKind.GreaterThanOrEqual => QueryBinaryOperator.GreaterThanOrEqual,
            BinaryOperatorKind.LessThan => QueryBinaryOperator.LessThan,
            BinaryOperatorKind.LessThanOrEqual => QueryBinaryOperator.LessThanOrEqual,

            // Logical operators
            BinaryOperatorKind.And => QueryBinaryOperator.And,
            BinaryOperatorKind.Or => QueryBinaryOperator.Or,

            // Arithmetic operators
            BinaryOperatorKind.Add => QueryBinaryOperator.Add,
            BinaryOperatorKind.Subtract => QueryBinaryOperator.Subtract,
            BinaryOperatorKind.Multiply => QueryBinaryOperator.Multiply,
            BinaryOperatorKind.Divide => QueryBinaryOperator.Divide,
            BinaryOperatorKind.Modulo => QueryBinaryOperator.Modulo,

            _ => throw new NotSupportedException($"Binary operator '{node.OperatorKind}' is not supported yet.")
        };

        return new QueryBinaryExpression(left, op, right);
    }

    private QueryExpression TranslateUnary(UnaryOperatorNode node)
    {
        if (node.OperatorKind != UnaryOperatorKind.Not)
        {
            throw new NotSupportedException($"Unary operator '{node.OperatorKind}' is not supported yet.");
        }

        var operand = TranslateNode(node.Operand);
        return new QueryUnaryExpression(QueryUnaryOperator.Not, operand);
    }

    private QueryExpression TranslateProperty(SingleValuePropertyAccessNode property)
    {
        var fieldName = property.Property.Name;
        if (!_entityDefinition.Fields.ContainsKey(fieldName))
        {
            throw new KeyNotFoundException($"Field '{fieldName}' is not defined on entity '{_entityDefinition.Name}'.");
        }

        return new QueryFieldReference(fieldName);
    }

    private QueryExpression TranslateFunction(SingleValueFunctionCallNode node)
    {
        var arguments = new List<QueryExpression>();
        foreach (var parameter in node.Parameters)
        {
            arguments.Add(TranslateNode(parameter));
        }

        var name = node.Name?.Trim() ?? string.Empty;
        return name.ToLowerInvariant() switch
        {
            // Geospatial functions
            "geo.intersects" => new QueryFunctionExpression("geo.intersects", arguments),
            "geo.distance" => new QueryFunctionExpression("geo.distance", arguments),
            "geo.length" => new QueryFunctionExpression("geo.length", arguments),

            // String functions (OData v4)
            "contains" => new QueryFunctionExpression("contains", arguments),
            "startswith" => new QueryFunctionExpression("startswith", arguments),
            "endswith" => new QueryFunctionExpression("endswith", arguments),
            "length" => new QueryFunctionExpression("length", arguments),
            "indexof" => new QueryFunctionExpression("indexof", arguments),
            "substring" => new QueryFunctionExpression("substring", arguments),
            "tolower" => new QueryFunctionExpression("tolower", arguments),
            "toupper" => new QueryFunctionExpression("toupper", arguments),
            "trim" => new QueryFunctionExpression("trim", arguments),
            "concat" => new QueryFunctionExpression("concat", arguments),

            // String functions (OData v3 compatibility)
            "substringof" => new QueryFunctionExpression("substringof", arguments),

            // Date/Time functions (OData v4)
            "year" => new QueryFunctionExpression("year", arguments),
            "month" => new QueryFunctionExpression("month", arguments),
            "day" => new QueryFunctionExpression("day", arguments),
            "hour" => new QueryFunctionExpression("hour", arguments),
            "minute" => new QueryFunctionExpression("minute", arguments),
            "second" => new QueryFunctionExpression("second", arguments),
            "fractionalseconds" => new QueryFunctionExpression("fractionalseconds", arguments),
            "date" => new QueryFunctionExpression("date", arguments),
            "time" => new QueryFunctionExpression("time", arguments),
            "totaloffsetminutes" => new QueryFunctionExpression("totaloffsetminutes", arguments),
            "now" => new QueryFunctionExpression("now", arguments),
            "mindatetime" => new QueryFunctionExpression("mindatetime", arguments),
            "maxdatetime" => new QueryFunctionExpression("maxdatetime", arguments),

            // Math functions (OData v4)
            "round" => new QueryFunctionExpression("round", arguments),
            "floor" => new QueryFunctionExpression("floor", arguments),
            "ceiling" => new QueryFunctionExpression("ceiling", arguments),

            _ => throw new NotSupportedException($"Filter function '{node.Name}' is not supported yet.")
        };
    }

    private QueryExpression TranslateConstant(ConstantNode constant)
    {
        if (constant.Value is ISpatial spatial)
        {
            return new QueryConstant(CreateGeometryValue(spatial, constant.LiteralText));
        }

        return new QueryConstant(constant.Value);
    }

    private static QueryGeometryValue CreateGeometryValue(ISpatial spatial, string? literalText)
    {
        var text = WktFormatter.Write(spatial);
        int? srid = spatial.CoordinateSystem?.EpsgId ?? TryExtractSrid(literalText);

        if (text.StartsWith("SRID=", StringComparison.OrdinalIgnoreCase))
        {
            var separator = text.IndexOf(';');
            if (separator > 5)
            {
                if (srid is null && int.TryParse(text.AsSpan(5, separator - 5), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    srid = parsed;
                }

                text = text.Substring(separator + 1);
            }
        }

        return new QueryGeometryValue(text, srid);
    }

    private static int? TryExtractSrid(string? literalText)
    {
        if (string.IsNullOrWhiteSpace(literalText))
        {
            return null;
        }

        var index = literalText.IndexOf("SRID=", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        index += 5;
        var end = index;
        while (end < literalText.Length && char.IsDigit(literalText[end]))
        {
            end++;
        }

        if (end <= index)
        {
            return null;
        }

        if (int.TryParse(literalText.AsSpan(index, end - index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

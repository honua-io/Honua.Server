// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Honua.Server.Core.Stac.Cql2;

/// <summary>
/// Parser for CQL2-JSON filter expressions.
/// Converts JSON filter expressions into strongly-typed CQL2 expression trees.
/// </summary>
public static class Cql2Parser
{
    private static readonly HashSet<string> LogicalOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or"
    };

    private static readonly HashSet<string> ComparisonOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "<>", "<", "<=", ">", ">="
    };

    private static readonly HashSet<string> SpatialOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "s_intersects", "s_equals", "s_disjoint", "s_touches", "s_within", "s_overlaps", "s_crosses", "s_contains"
    };

    private static readonly HashSet<string> TemporalOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "t_intersects", "t_equals", "t_disjoint", "t_before", "t_after", "t_meets", "t_during", "t_overlaps",
        "anyinteracts"
    };

    /// <summary>
    /// Parses a CQL2-JSON filter expression from a JSON string.
    /// </summary>
    /// <param name="filterJson">The JSON string containing the filter expression.</param>
    /// <returns>The parsed CQL2 expression tree.</returns>
    /// <exception cref="Cql2ParseException">Thrown when the filter expression is invalid.</exception>
    public static Cql2Expression Parse(string filterJson)
    {
        if (string.IsNullOrWhiteSpace(filterJson))
        {
            throw new Cql2ParseException("Filter expression cannot be null or empty.");
        }

        try
        {
            var jsonNode = JsonNode.Parse(filterJson);
            if (jsonNode is null)
            {
                throw new Cql2ParseException("Failed to parse filter JSON: result was null.");
            }

            return ParseExpression(jsonNode);
        }
        catch (JsonException ex)
        {
            throw new Cql2ParseException($"Invalid JSON in filter expression: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses a CQL2-JSON filter expression from a JsonNode.
    /// </summary>
    /// <param name="node">The JSON node containing the filter expression.</param>
    /// <returns>The parsed CQL2 expression tree.</returns>
    /// <exception cref="Cql2ParseException">Thrown when the filter expression is invalid.</exception>
    public static Cql2Expression ParseExpression(JsonNode node)
    {
        if (node is not JsonObject obj)
        {
            throw new Cql2ParseException("Filter expression must be a JSON object.");
        }

        // Extract operator
        if (!obj.TryGetPropertyValue("op", out var opNode) || opNode is null)
        {
            throw new Cql2ParseException("Filter expression must have an 'op' property.");
        }

        var op = opNode.GetValue<string>();
        if (string.IsNullOrWhiteSpace(op))
        {
            throw new Cql2ParseException("Operator cannot be null or empty.");
        }

        op = op.ToLowerInvariant();

        // Extract arguments
        if (!obj.TryGetPropertyValue("args", out var argsNode) || argsNode is not JsonArray argsArray)
        {
            throw new Cql2ParseException($"Operator '{op}' must have an 'args' array.");
        }

        // Parse based on operator type
        return op switch
        {
            "and" or "or" => ParseLogicalExpression(op, argsArray),
            "not" => ParseNotExpression(argsArray),
            "=" or "<>" or "<" or "<=" or ">" or ">=" => ParseComparisonExpression(op, argsArray),
            "isnull" => ParseIsNullExpression(argsArray),
            "like" => ParseLikeExpression(argsArray),
            "between" => ParseBetweenExpression(argsArray),
            "in" => ParseInExpression(argsArray),
            var s when SpatialOperators.Contains(s) => ParseSpatialExpression(op, argsArray),
            var t when TemporalOperators.Contains(t) => ParseTemporalExpression(op, argsArray),
            _ => throw new Cql2ParseException($"Unsupported operator: '{op}'")
        };
    }

    private static Cql2LogicalExpression ParseLogicalExpression(string op, JsonArray argsArray)
    {
        if (argsArray.Count < 2)
        {
            throw new Cql2ParseException($"Logical operator '{op}' requires at least 2 arguments.");
        }

        var arguments = new List<Cql2Expression>();
        foreach (var argNode in argsArray)
        {
            if (argNode is null)
            {
                throw new Cql2ParseException($"Logical operator '{op}' contains null argument.");
            }

            arguments.Add(ParseExpression(argNode));
        }

        return new Cql2LogicalExpression
        {
            Op = op,
            Arguments = arguments
        };
    }

    private static Cql2NotExpression ParseNotExpression(JsonArray argsArray)
    {
        if (argsArray.Count != 1)
        {
            throw new Cql2ParseException("NOT operator requires exactly 1 argument.");
        }

        var argNode = argsArray[0];
        if (argNode is null)
        {
            throw new Cql2ParseException("NOT operator argument cannot be null.");
        }

        return new Cql2NotExpression
        {
            Arguments = new[] { ParseExpression(argNode) }
        };
    }

    private static Cql2ComparisonExpression ParseComparisonExpression(string op, JsonArray argsArray)
    {
        if (argsArray.Count != 2)
        {
            throw new Cql2ParseException($"Comparison operator '{op}' requires exactly 2 arguments.");
        }

        var leftNode = argsArray[0];
        var rightNode = argsArray[1];

        if (leftNode is null || rightNode is null)
        {
            throw new Cql2ParseException($"Comparison operator '{op}' arguments cannot be null.");
        }

        var operands = new List<Cql2Operand>
        {
            ParseOperand(leftNode),
            ParseOperand(rightNode)
        };

        return new Cql2ComparisonExpression
        {
            Op = op,
            Arguments = operands
        };
    }

    private static Cql2IsNullExpression ParseIsNullExpression(JsonArray argsArray)
    {
        if (argsArray.Count != 1)
        {
            throw new Cql2ParseException("IS NULL operator requires exactly 1 argument.");
        }

        var argNode = argsArray[0];
        if (argNode is null)
        {
            throw new Cql2ParseException("IS NULL operator argument cannot be null.");
        }

        return new Cql2IsNullExpression
        {
            Arguments = new[] { ParseOperand(argNode) }
        };
    }

    private static Cql2LikeExpression ParseLikeExpression(JsonArray argsArray)
    {
        if (argsArray.Count != 2)
        {
            throw new Cql2ParseException("LIKE operator requires exactly 2 arguments.");
        }

        var propertyNode = argsArray[0];
        var patternNode = argsArray[1];

        if (propertyNode is null || patternNode is null)
        {
            throw new Cql2ParseException("LIKE operator arguments cannot be null.");
        }

        return new Cql2LikeExpression
        {
            Arguments = new[]
            {
                ParseOperand(propertyNode),
                ParseOperand(patternNode)
            }
        };
    }

    private static Cql2BetweenExpression ParseBetweenExpression(JsonArray argsArray)
    {
        if (argsArray.Count != 3)
        {
            throw new Cql2ParseException("BETWEEN operator requires exactly 3 arguments (property, lower, upper).");
        }

        var propertyNode = argsArray[0];
        var lowerNode = argsArray[1];
        var upperNode = argsArray[2];

        if (propertyNode is null || lowerNode is null || upperNode is null)
        {
            throw new Cql2ParseException("BETWEEN operator arguments cannot be null.");
        }

        return new Cql2BetweenExpression
        {
            Arguments = new[]
            {
                ParseOperand(propertyNode),
                ParseOperand(lowerNode),
                ParseOperand(upperNode)
            }
        };
    }

    private static Cql2InExpression ParseInExpression(JsonArray argsArray)
    {
        if (argsArray.Count < 2)
        {
            throw new Cql2ParseException("IN operator requires at least 2 arguments (property and values).");
        }

        var operands = new List<Cql2Operand>();
        foreach (var argNode in argsArray)
        {
            if (argNode is null)
            {
                throw new Cql2ParseException("IN operator contains null argument.");
            }

            operands.Add(ParseOperand(argNode));
        }

        return new Cql2InExpression
        {
            Arguments = operands
        };
    }

    private static Cql2SpatialExpression ParseSpatialExpression(string op, JsonArray argsArray)
    {
        if (argsArray.Count != 2)
        {
            throw new Cql2ParseException($"Spatial operator '{op}' requires exactly 2 arguments.");
        }

        var leftNode = argsArray[0];
        var rightNode = argsArray[1];

        if (leftNode is null || rightNode is null)
        {
            throw new Cql2ParseException($"Spatial operator '{op}' arguments cannot be null.");
        }

        return new Cql2SpatialExpression
        {
            Op = op,
            Arguments = new[]
            {
                ParseOperand(leftNode),
                ParseOperand(rightNode)
            }
        };
    }

    private static Cql2TemporalExpression ParseTemporalExpression(string op, JsonArray argsArray)
    {
        if (argsArray.Count != 2)
        {
            throw new Cql2ParseException($"Temporal operator '{op}' requires exactly 2 arguments.");
        }

        var leftNode = argsArray[0];
        var rightNode = argsArray[1];

        if (leftNode is null || rightNode is null)
        {
            throw new Cql2ParseException($"Temporal operator '{op}' arguments cannot be null.");
        }

        return new Cql2TemporalExpression
        {
            Op = op,
            Arguments = new[]
            {
                ParseOperand(leftNode),
                ParseOperand(rightNode)
            }
        };
    }

    private static Cql2Operand ParseOperand(JsonNode node)
    {
        // Check if it's a property reference
        if (node is JsonObject obj && obj.TryGetPropertyValue("property", out var propertyNode) && propertyNode is not null)
        {
            var propertyName = propertyNode.GetValue<string>();
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new Cql2ParseException("Property name cannot be null or empty.");
            }

            return new Cql2PropertyRef
            {
                Property = propertyName
            };
        }

        // Otherwise, it's a literal value
        return new Cql2Literal(ExtractLiteralValue(node));
    }

    private static object? ExtractLiteralValue(JsonNode node)
    {
        return node switch
        {
            JsonValue value => ExtractJsonValue(value),
            JsonArray array => ExtractJsonArray(array),
            JsonObject obj => ExtractJsonObject(obj),
            _ => null
        };
    }

    private static object? ExtractJsonValue(JsonValue value)
    {
        // Try to get the value in various types
        if (value.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        if (value.TryGetValue<long>(out var longValue))
        {
            return longValue;
        }

        if (value.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue;
        }

        if (value.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        return value.ToString();
    }

    private static object ExtractJsonArray(JsonArray array)
    {
        var list = new List<object?>();
        foreach (var item in array)
        {
            if (item is not null)
            {
                list.Add(ExtractLiteralValue(item));
            }
        }

        return list;
    }

    private static object ExtractJsonObject(JsonObject obj)
    {
        // For GeoJSON geometries and other objects, return the raw JSON string
        return obj.ToJsonString();
    }
}

/// <summary>
/// Exception thrown when CQL2 parsing fails.
/// </summary>
public sealed class Cql2ParseException : Exception
{
    public Cql2ParseException(string message) : base(message)
    {
    }

    public Cql2ParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

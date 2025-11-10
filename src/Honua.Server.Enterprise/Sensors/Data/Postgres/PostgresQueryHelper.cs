// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// Shared query building and parameter handling logic for PostgreSQL SensorThings repositories.
/// Provides reusable methods for filter translation, parameter building, and result mapping.
/// </summary>
internal static class PostgresQueryHelper
{
    /// <summary>
    /// Translates a SensorThings filter expression to PostgreSQL WHERE clause.
    /// </summary>
    public static string TranslateFilter(FilterExpression filter, DynamicParameters parameters)
    {
        return filter switch
        {
            ComparisonExpression comparison => TranslateComparison(comparison, parameters),
            LogicalExpression logical => TranslateLogical(logical, parameters),
            FunctionExpression function => TranslateFunction(function, parameters),
            _ => throw new NotSupportedException($"Filter expression type {filter.GetType().Name} not supported")
        };
    }

    private static string TranslateComparison(ComparisonExpression expr, DynamicParameters parameters)
    {
        var paramName = $"p{parameters.ParameterNames.Count()}";
        parameters.Add(paramName, expr.Value);

        var op = expr.Operator switch
        {
            ComparisonOperator.Equals => "=",
            ComparisonOperator.NotEquals => "!=",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            _ => throw new NotSupportedException($"Operator {expr.Operator} not supported")
        };

        return $"{expr.Property} {op} @{paramName}";
    }

    private static string TranslateLogical(LogicalExpression expr, DynamicParameters parameters)
    {
        if (expr.Operator == "not" && expr.Left != null)
        {
            return $"NOT ({TranslateFilter(expr.Left, parameters)})";
        }

        if (expr.Left == null || expr.Right == null)
            throw new ArgumentException("Logical expression requires both left and right operands");

        var left = TranslateFilter(expr.Left, parameters);
        var right = TranslateFilter(expr.Right, parameters);

        return expr.Operator.ToUpper() switch
        {
            "AND" => $"({left} AND {right})",
            "OR" => $"({left} OR {right})",
            _ => throw new NotSupportedException($"Logical operator {expr.Operator} not supported")
        };
    }

    private static string TranslateFunction(FunctionExpression expr, DynamicParameters parameters)
    {
        return expr.Name switch
        {
            "geo.intersects" => TranslateGeoIntersects(expr, parameters),
            "substringof" => TranslateSubstringOf(expr, parameters),
            _ => throw new NotSupportedException($"Function {expr.Name} not supported")
        };
    }

    private static string TranslateGeoIntersects(FunctionExpression expr, DynamicParameters parameters)
    {
        if (expr.Arguments.Count < 2)
            throw new ArgumentException("geo.intersects requires 2 arguments");

        var property = expr.Arguments[0].ToString();
        var geometry = expr.Arguments[1];
        var paramName = $"geom{parameters.ParameterNames.Count()}";

        parameters.Add(paramName, geometry);

        return $"ST_Intersects({property}, ST_GeomFromGeoJSON(@{paramName}))";
    }

    private static string TranslateSubstringOf(FunctionExpression expr, DynamicParameters parameters)
    {
        if (expr.Arguments.Count < 2)
            throw new ArgumentException("substringof requires 2 arguments");

        var substring = expr.Arguments[0].ToString();
        var property = expr.Arguments[1].ToString();
        var paramName = $"str{parameters.ParameterNames.Count()}";

        parameters.Add(paramName, $"%{substring}%");

        return $"{property} LIKE @{paramName}";
    }

    /// <summary>
    /// Parses JSON properties dictionary from database string.
    /// </summary>
    public static Dictionary<string, object>? ParseProperties(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return JsonSerializer.Deserialize<Dictionary<string, object>>(value);
    }

    /// <summary>
    /// Adds a parameter to a database command.
    /// </summary>
    public static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

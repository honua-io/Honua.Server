// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data;
using System.Linq;
using Dapper;
using Honua.Server.Enterprise.Data;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// PostgreSQL implementation of the SensorThings repository.
/// Uses Dapper for efficient data access and PostGIS for spatial operations.
/// This is the main partial class containing core infrastructure and helper methods.
/// </summary>
public sealed partial class PostgresSensorThingsRepository : ISensorThingsRepository
{
    private readonly IDbConnection _connection;
    private readonly SensorThingsServiceDefinition _config;
    private readonly ILogger<PostgresSensorThingsRepository> _logger;
    private readonly GeoJsonReader _geoJsonReader;
    private readonly GeoJsonWriter _geoJsonWriter;

    public PostgresSensorThingsRepository(
        IDbConnection connection,
        SensorThingsServiceDefinition config,
        ILogger<PostgresSensorThingsRepository> logger)
    {
        DapperBootstrapper.EnsureConfigured();
        _connection = connection;
        _config = config;
        _logger = logger;
        _geoJsonReader = new GeoJsonReader();
        _geoJsonWriter = new GeoJsonWriter();
    }

    // ============================================================================
    // Helper methods for query translation
    // ============================================================================

    private string TranslateFilter(FilterExpression filter, DynamicParameters parameters)
    {
        return filter switch
        {
            ComparisonExpression comparison => TranslateComparison(comparison, parameters),
            LogicalExpression logical => TranslateLogical(logical, parameters),
            FunctionExpression function => TranslateFunction(function, parameters),
            _ => throw new NotSupportedException($"Filter expression type {filter.GetType().Name} not supported")
        };
    }

    private string TranslateComparison(ComparisonExpression expr, DynamicParameters parameters)
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

    private string TranslateLogical(LogicalExpression expr, DynamicParameters parameters)
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

    private string TranslateFunction(FunctionExpression expr, DynamicParameters parameters)
    {
        return expr.Name switch
        {
            "geo.intersects" => TranslateGeoIntersects(expr, parameters),
            "substringof" => TranslateSubstringOf(expr, parameters),
            _ => throw new NotSupportedException($"Function {expr.Name} not supported")
        };
    }

    private string TranslateGeoIntersects(FunctionExpression expr, DynamicParameters parameters)
    {
        if (expr.Arguments.Count < 2)
            throw new ArgumentException("geo.intersects requires 2 arguments");

        var property = expr.Arguments[0].ToString();
        var geometry = expr.Arguments[1];
        var paramName = $"geom{parameters.ParameterNames.Count()}";

        parameters.Add(paramName, geometry);

        return $"ST_Intersects({property}, ST_GeomFromGeoJSON(@{paramName}))";
    }

    private string TranslateSubstringOf(FunctionExpression expr, DynamicParameters parameters)
    {
        if (expr.Arguments.Count < 2)
            throw new ArgumentException("substringof requires 2 arguments");

        var substring = expr.Arguments[0].ToString();
        var property = expr.Arguments[1].ToString();
        var paramName = $"str{parameters.ParameterNames.Count()}";

        parameters.Add(paramName, $"%{substring}%");

        return $"{property} LIKE @{paramName}";
    }
}

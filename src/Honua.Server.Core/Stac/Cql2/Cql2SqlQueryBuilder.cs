// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Honua.Server.Core.Stac.Cql2;

/// <summary>
/// Builds parameterized SQL WHERE clauses from CQL2 expressions.
/// Supports multiple database providers (PostgreSQL, MySQL, SQL Server, SQLite).
/// </summary>
public sealed class Cql2SqlQueryBuilder
{
    private readonly DbCommand _command;
    private readonly DatabaseProvider _provider;
    private int _parameterCounter;

    /// <summary>
    /// Database provider type for SQL syntax customization.
    /// </summary>
    public enum DatabaseProvider
    {
        PostgreSQL,
        MySQL,
        SqlServer,
        SQLite
    }

    public Cql2SqlQueryBuilder(DbCommand command, DatabaseProvider provider)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _provider = provider;
        _parameterCounter = 0;
    }

    /// <summary>
    /// Builds a parameterized SQL WHERE clause from a CQL2 expression.
    /// </summary>
    /// <param name="expression">The CQL2 expression to convert.</param>
    /// <returns>The SQL WHERE clause (without the WHERE keyword).</returns>
    /// <exception cref="Cql2BuildException">Thrown when the expression cannot be converted to SQL.</exception>
    public string BuildWhereClause(Cql2Expression expression)
    {
        return BuildExpression(expression);
    }

    private string BuildExpression(Cql2Expression expression)
    {
        return expression switch
        {
            Cql2LogicalExpression logical => BuildLogicalExpression(logical),
            Cql2NotExpression not => BuildNotExpression(not),
            Cql2ComparisonExpression comparison => BuildComparisonExpression(comparison),
            Cql2IsNullExpression isNull => BuildIsNullExpression(isNull),
            Cql2LikeExpression like => BuildLikeExpression(like),
            Cql2BetweenExpression between => BuildBetweenExpression(between),
            Cql2InExpression @in => BuildInExpression(@in),
            Cql2SpatialExpression spatial => BuildSpatialExpression(spatial),
            Cql2TemporalExpression temporal => BuildTemporalExpression(temporal),
            _ => throw new Cql2BuildException($"Unsupported expression type: {expression.GetType().Name}")
        };
    }

    private string BuildLogicalExpression(Cql2LogicalExpression expression)
    {
        if (expression.Arguments.Count == 0)
        {
            throw new Cql2BuildException($"Logical operator '{expression.Operator}' requires at least one argument.");
        }

        var sqlOperator = expression.Operator.ToUpperInvariant();
        var clauses = expression.Arguments.Select(BuildExpression).ToList();

        return $"({string.Join($" {sqlOperator} ", clauses)})";
    }

    private string BuildNotExpression(Cql2NotExpression expression)
    {
        if (expression.Arguments.Count != 1)
        {
            throw new Cql2BuildException("NOT operator requires exactly 1 argument.");
        }

        var innerExpression = BuildExpression(expression.Arguments[0]);
        return $"NOT ({innerExpression})";
    }

    private string BuildComparisonExpression(Cql2ComparisonExpression expression)
    {
        if (expression.Arguments.Count != 2)
        {
            throw new Cql2BuildException($"Comparison operator '{expression.Operator}' requires exactly 2 arguments.");
        }

        var left = BuildOperand(expression.Arguments[0]);
        var right = BuildOperand(expression.Arguments[1]);

        var sqlOperator = expression.Operator switch
        {
            "=" => "=",
            "<>" => "<>",
            "<" => "<",
            "<=" => "<=",
            ">" => ">",
            ">=" => ">=",
            _ => throw new Cql2BuildException($"Unsupported comparison operator: '{expression.Operator}'")
        };

        return $"{left} {sqlOperator} {right}";
    }

    private string BuildIsNullExpression(Cql2IsNullExpression expression)
    {
        if (expression.Arguments.Count != 1)
        {
            throw new Cql2BuildException("IS NULL operator requires exactly 1 argument.");
        }

        var operand = BuildOperand(expression.Arguments[0]);
        return $"{operand} IS NULL";
    }

    private string BuildLikeExpression(Cql2LikeExpression expression)
    {
        if (expression.Arguments.Count != 2)
        {
            throw new Cql2BuildException("LIKE operator requires exactly 2 arguments.");
        }

        var property = BuildOperand(expression.Arguments[0]);
        var pattern = BuildOperand(expression.Arguments[1]);

        return $"{property} LIKE {pattern}";
    }

    private string BuildBetweenExpression(Cql2BetweenExpression expression)
    {
        if (expression.Arguments.Count != 3)
        {
            throw new Cql2BuildException("BETWEEN operator requires exactly 3 arguments.");
        }

        var property = BuildOperand(expression.Arguments[0]);
        var lower = BuildOperand(expression.Arguments[1]);
        var upper = BuildOperand(expression.Arguments[2]);

        return $"{property} BETWEEN {lower} AND {upper}";
    }

    private string BuildInExpression(Cql2InExpression expression)
    {
        if (expression.Arguments.Count < 2)
        {
            throw new Cql2BuildException("IN operator requires at least 2 arguments.");
        }

        var property = BuildOperand(expression.Arguments[0]);
        var values = expression.Arguments.Skip(1).Select(BuildOperand).ToList();

        return $"{property} IN ({string.Join(", ", values)})";
    }

    private string BuildSpatialExpression(Cql2SpatialExpression expression)
    {
        // For now, only support S_INTERSECTS
        if (!string.Equals(expression.Operator, "s_intersects", StringComparison.OrdinalIgnoreCase))
        {
            throw new Cql2BuildException($"Spatial operator '{expression.Operator}' is not yet supported. Only 's_intersects' is currently implemented.");
        }

        if (expression.Arguments.Count != 2)
        {
            throw new Cql2BuildException("S_INTERSECTS operator requires exactly 2 arguments.");
        }

        // Extract property and geometry
        var propertyArg = expression.Arguments[0];
        var geometryArg = expression.Arguments[1];

        if (propertyArg is not Cql2PropertyRef propertyRef)
        {
            throw new Cql2BuildException("First argument to S_INTERSECTS must be a property reference.");
        }

        if (geometryArg is not Cql2Literal geometryLiteral)
        {
            throw new Cql2BuildException("Second argument to S_INTERSECTS must be a GeoJSON geometry.");
        }

        // Build spatial query based on database provider
        return _provider switch
        {
            DatabaseProvider.PostgreSQL => BuildPostGISSpatialIntersects(propertyRef.Property, geometryLiteral.Value),
            DatabaseProvider.MySQL => BuildMySQLSpatialIntersects(propertyRef.Property, geometryLiteral.Value),
            DatabaseProvider.SqlServer => BuildSqlServerSpatialIntersects(propertyRef.Property, geometryLiteral.Value),
            DatabaseProvider.SQLite => BuildSQLiteSpatialIntersects(propertyRef.Property, geometryLiteral.Value),
            _ => throw new Cql2BuildException($"Spatial operations not supported for provider: {_provider}")
        };
    }

    private string BuildTemporalExpression(Cql2TemporalExpression expression)
    {
        // For now, only support T_INTERSECTS and ANYINTERACTS
        var op = expression.Operator.ToLowerInvariant();
        if (op != "t_intersects" && op != "anyinteracts")
        {
            throw new Cql2BuildException($"Temporal operator '{expression.Operator}' is not yet supported. Only 't_intersects' and 'anyinteracts' are currently implemented.");
        }

        if (expression.Arguments.Count != 2)
        {
            throw new Cql2BuildException($"Temporal operator '{expression.Operator}' requires exactly 2 arguments.");
        }

        var propertyArg = expression.Arguments[0];
        var intervalArg = expression.Arguments[1];

        if (propertyArg is not Cql2PropertyRef propertyRef)
        {
            throw new Cql2BuildException($"First argument to {expression.Operator} must be a property reference.");
        }

        if (intervalArg is not Cql2Literal intervalLiteral)
        {
            throw new Cql2BuildException($"Second argument to {expression.Operator} must be a temporal interval.");
        }

        // Parse temporal interval (can be array [start, end] or object {"interval": [start, end]})
        var interval = ParseTemporalInterval(intervalLiteral.Value);

        // Build temporal intersection query
        return BuildTemporalIntersects(propertyRef.Property, interval.Start, interval.End);
    }

    private (DateTimeOffset? Start, DateTimeOffset? End) ParseTemporalInterval(object? value)
    {
        if (value is null)
        {
            throw new Cql2BuildException("Temporal interval cannot be null.");
        }

        // Handle array format: ["2020-01-01T00:00:00Z", "2020-12-31T23:59:59Z"]
        if (value is List<object?> list)
        {
            if (list.Count != 2)
            {
                throw new Cql2BuildException("Temporal interval array must have exactly 2 elements [start, end].");
            }

            var start = ParseDateTime(list[0]);
            var end = ParseDateTime(list[1]);
            return (start, end);
        }

        // Handle object format: {"interval": ["2020-01-01T00:00:00Z", "2020-12-31T23:59:59Z"]}
        if (value is string jsonString)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                if (doc.RootElement.TryGetProperty("interval", out var intervalProp) && intervalProp.ValueKind == JsonValueKind.Array)
                {
                    var arr = intervalProp.EnumerateArray().ToList();
                    if (arr.Count != 2)
                    {
                        throw new Cql2BuildException("Temporal interval array must have exactly 2 elements [start, end].");
                    }

                    var start = ParseDateTime(arr[0].GetString());
                    var end = ParseDateTime(arr[1].GetString());
                    return (start, end);
                }
            }
            catch (JsonException ex)
            {
                throw new Cql2BuildException($"Failed to parse temporal interval JSON: {ex.Message}", ex);
            }
        }

        throw new Cql2BuildException($"Unsupported temporal interval format: {value.GetType().Name}");
    }

    private DateTimeOffset? ParseDateTime(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str) || str == "..")
            {
                return null;
            }

            if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
            {
                return result;
            }

            throw new Cql2BuildException($"Failed to parse datetime value: '{str}'");
        }

        throw new Cql2BuildException($"Unsupported datetime value type: {value.GetType().Name}");
    }

    private string BuildTemporalIntersects(string property, DateTimeOffset? start, DateTimeOffset? end)
    {
        // Map property to database columns
        // STAC items can have datetime, start_datetime, end_datetime
        var startColumn = property == "datetime" ? "COALESCE(start_datetime, datetime)" : property;
        var endColumn = property == "datetime" ? "COALESCE(end_datetime, datetime)" : property;

        var clauses = new List<string>();

        if (start.HasValue)
        {
            var paramName = AddParameter(start.Value.UtcDateTime);
            clauses.Add($"({endColumn} IS NULL OR {endColumn} >= {paramName})");
        }

        if (end.HasValue)
        {
            var paramName = AddParameter(end.Value.UtcDateTime);
            clauses.Add($"({startColumn} IS NULL OR {startColumn} <= {paramName})");
        }

        if (clauses.Count == 0)
        {
            return "1=1"; // No temporal constraints
        }

        return string.Join(" AND ", clauses);
    }

    private string BuildPostGISSpatialIntersects(string property, object? geometry)
    {
        // PostGIS: ST_Intersects(geometry_column, ST_GeomFromGeoJSON(?))
        if (geometry is not string geoJson)
        {
            throw new Cql2BuildException("Geometry must be a GeoJSON string.");
        }

        var paramName = AddParameter(geoJson);

        // Use geometry_json column for spatial queries
        return $"ST_Intersects(ST_GeomFromGeoJSON(geometry_json), ST_GeomFromGeoJSON({paramName}))";
    }

    private string BuildMySQLSpatialIntersects(string property, object? geometry)
    {
        // MySQL: ST_Intersects(geometry_column, ST_GeomFromGeoJSON(?))
        if (geometry is not string geoJson)
        {
            throw new Cql2BuildException("Geometry must be a GeoJSON string.");
        }

        var paramName = AddParameter(geoJson);

        // Use geometry_json column for spatial queries
        return $"ST_Intersects(ST_GeomFromGeoJSON(geometry_json), ST_GeomFromGeoJSON({paramName}))";
    }

    private string BuildSqlServerSpatialIntersects(string property, object? geometry)
    {
        // SQL Server: geometry_column.STIntersects(geometry::STGeomFromGeoJSON(?)) = 1
        if (geometry is not string geoJson)
        {
            throw new Cql2BuildException("Geometry must be a GeoJSON string.");
        }

        var paramName = AddParameter(geoJson);

        // SQL Server uses different syntax
        return $"geography::STGeomFromText((SELECT geometry::Parse({paramName}).MakeValid().STAsText()), 4326).STIntersects(geography::STGeomFromText((SELECT geometry::Parse(geometry_json).MakeValid().STAsText()), 4326)) = 1";
    }

    private string BuildSQLiteSpatialIntersects(string property, object? geometry)
    {
        // SQLite with SpatiaLite: ST_Intersects(geometry_column, GeomFromGeoJSON(?))
        if (geometry is not string geoJson)
        {
            throw new Cql2BuildException("Geometry must be a GeoJSON string.");
        }

        var paramName = AddParameter(geoJson);

        // Use geometry_json column for spatial queries
        return $"ST_Intersects(GeomFromGeoJSON(geometry_json), GeomFromGeoJSON({paramName}))";
    }

    private string BuildOperand(Cql2Operand operand)
    {
        return operand switch
        {
            Cql2PropertyRef propertyRef => BuildPropertyRef(propertyRef),
            Cql2Literal literal => BuildLiteral(literal),
            _ => throw new Cql2BuildException($"Unsupported operand type: {operand.GetType().Name}")
        };
    }

    private string BuildPropertyRef(Cql2PropertyRef propertyRef)
    {
        var property = propertyRef.Property;

        // Map STAC property names to database columns
        // Standard STAC properties are stored in dedicated columns
        // Custom properties are stored in properties_json
        return property switch
        {
            "id" => "id",
            "collection" or "collection_id" => "collection_id",
            "datetime" => "datetime",
            "start_datetime" => "start_datetime",
            "end_datetime" => "end_datetime",
            "geometry" => "geometry_json",
            _ => BuildJsonPropertyAccess(property)
        };
    }

    private string BuildJsonPropertyAccess(string property)
    {
        // Access properties from the properties_json column
        // Syntax varies by database provider
        return _provider switch
        {
            DatabaseProvider.PostgreSQL => $"(properties_json->>{QuoteString(property)})",
            DatabaseProvider.MySQL => $"JSON_UNQUOTE(JSON_EXTRACT(properties_json, '$.{EscapeJsonPath(property)}'))",
            DatabaseProvider.SqlServer => $"JSON_VALUE(properties_json, '$.{EscapeJsonPath(property)}')",
            DatabaseProvider.SQLite => $"JSON_EXTRACT(properties_json, '$.{EscapeJsonPath(property)}')",
            _ => throw new Cql2BuildException($"JSON property access not supported for provider: {_provider}")
        };
    }

    private string BuildLiteral(Cql2Literal literal)
    {
        // Add as parameter to prevent SQL injection
        return AddParameter(literal.Value);
    }

    private string AddParameter(object? value)
    {
        var paramName = $"@cql2_p{_parameterCounter++}";
        var parameter = _command.CreateParameter();
        parameter.ParameterName = paramName;
        parameter.Value = value ?? DBNull.Value;
        _command.Parameters.Add(parameter);
        return paramName;
    }

    private string QuoteString(string value)
    {
        // Escape single quotes for SQL
        return value.Replace("'", "''");
    }

    private string EscapeJsonPath(string value)
    {
        // Escape special characters in JSON path
        return value.Replace(".", "\\.").Replace("$", "\\$");
    }
}

/// <summary>
/// Exception thrown when CQL2 expression cannot be converted to SQL.
/// </summary>
public sealed class Cql2BuildException : Exception
{
    public Cql2BuildException(string message) : base(message)
    {
    }

    public Cql2BuildException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

using System.Text;
using Npgsql;

namespace Honua.Server.Enterprise.Sensors.Query;

/// <summary>
/// Builds SQL WHERE clauses from advanced FilterExpression trees.
/// Supports comparison, logical, and function expressions.
/// </summary>
public static class AdvancedFilterSqlBuilder
{
    /// <summary>
    /// Builds a SQL WHERE clause from a filter expression.
    /// Returns the SQL string and parameters to use with Dapper.
    /// </summary>
    public static (string Sql, Dictionary<string, object> Parameters) BuildWhereClause(FilterExpression? filter)
    {
        if (filter == null)
            return (string.Empty, new Dictionary<string, object>());

        var parameters = new Dictionary<string, object>();
        var paramCounter = 0;

        var sql = BuildExpression(filter, parameters, ref paramCounter);
        return (sql, parameters);
    }

    private static string BuildExpression(FilterExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        return expr switch
        {
            ComparisonExpression comp => BuildComparison(comp, parameters, ref paramCounter),
            LogicalExpression logical => BuildLogical(logical, parameters, ref paramCounter),
            FunctionExpression func => BuildFunction(func, parameters, ref paramCounter),
            _ => throw new NotSupportedException($"Filter expression type {expr.GetType().Name} is not supported")
        };
    }

    private static string BuildComparison(ComparisonExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        var column = MapPropertyToColumn(expr.Property);
        var paramName = $"p{paramCounter++}";
        parameters[paramName] = expr.Value;

        var sqlOperator = expr.Operator switch
        {
            ComparisonOperator.Equals => "=",
            ComparisonOperator.NotEquals => "!=",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            _ => "="
        };

        return $"{column} {sqlOperator} @{paramName}";
    }

    private static string BuildLogical(LogicalExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        return expr.Operator.ToLowerInvariant() switch
        {
            "and" => $"({BuildExpression(expr.Left!, parameters, ref paramCounter)} AND {BuildExpression(expr.Right!, parameters, ref paramCounter)})",
            "or" => $"({BuildExpression(expr.Left!, parameters, ref paramCounter)} OR {BuildExpression(expr.Right!, parameters, ref paramCounter)})",
            "not" => $"(NOT {BuildExpression(expr.Left!, parameters, ref paramCounter)})",
            _ => throw new NotSupportedException($"Logical operator '{expr.Operator}' is not supported")
        };
    }

    private static string BuildFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        var functionName = expr.Name.ToLowerInvariant();

        return functionName switch
        {
            // String functions
            "contains" => BuildContainsFunction(expr, parameters, ref paramCounter),
            "startswith" => BuildStartsWithFunction(expr, parameters, ref paramCounter),
            "endswith" => BuildEndsWithFunction(expr, parameters, ref paramCounter),
            "length" => BuildLengthFunction(expr, parameters, ref paramCounter),
            "tolower" => BuildToLowerFunction(expr, parameters, ref paramCounter),
            "toupper" => BuildToUpperFunction(expr, parameters, ref paramCounter),
            "trim" => BuildTrimFunction(expr, parameters, ref paramCounter),
            "concat" => BuildConcatFunction(expr, parameters, ref paramCounter),
            "substring" => BuildSubstringFunction(expr, parameters, ref paramCounter),
            "indexof" => BuildIndexOfFunction(expr, parameters, ref paramCounter),

            // Math functions
            "round" => BuildRoundFunction(expr, parameters, ref paramCounter),
            "floor" => BuildFloorFunction(expr, parameters, ref paramCounter),
            "ceiling" => BuildCeilingFunction(expr, parameters, ref paramCounter),
            "add" => BuildMathOperation(expr, "+", parameters, ref paramCounter),
            "sub" => BuildMathOperation(expr, "-", parameters, ref paramCounter),
            "mul" => BuildMathOperation(expr, "*", parameters, ref paramCounter),
            "div" => BuildMathOperation(expr, "/", parameters, ref paramCounter),
            "mod" => BuildMathOperation(expr, "%", parameters, ref paramCounter),

            // Spatial functions
            "geo.distance" => BuildGeoDistanceFunction(expr, parameters, ref paramCounter),
            "geo.intersects" => BuildGeoIntersectsFunction(expr, parameters, ref paramCounter),
            "geo.length" => BuildGeoLengthFunction(expr, parameters, ref paramCounter),
            "geo.within" => BuildGeoWithinFunction(expr, parameters, ref paramCounter),

            // Temporal functions
            "year" => BuildTemporalFunction(expr, "EXTRACT(YEAR FROM", parameters, ref paramCounter),
            "month" => BuildTemporalFunction(expr, "EXTRACT(MONTH FROM", parameters, ref paramCounter),
            "day" => BuildTemporalFunction(expr, "EXTRACT(DAY FROM", parameters, ref paramCounter),
            "hour" => BuildTemporalFunction(expr, "EXTRACT(HOUR FROM", parameters, ref paramCounter),
            "minute" => BuildTemporalFunction(expr, "EXTRACT(MINUTE FROM", parameters, ref paramCounter),
            "second" => BuildTemporalFunction(expr, "EXTRACT(SECOND FROM", parameters, ref paramCounter),

            // Comparison wrapper (used when function is followed by comparison operator)
            "comparison" => BuildComparisonWrapper(expr, parameters, ref paramCounter),

            _ => throw new NotSupportedException($"Function '{expr.Name}' is not supported")
        };
    }

    // String functions
    private static string BuildContainsFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 2)
            throw new ArgumentException("contains requires 2 arguments");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        var paramName = $"p{paramCounter++}";
        parameters[paramName] = $"%{expr.Arguments[1]}%";

        return $"{column} LIKE @{paramName}";
    }

    private static string BuildStartsWithFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 2)
            throw new ArgumentException("startswith requires 2 arguments");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        var paramName = $"p{paramCounter++}";
        parameters[paramName] = $"{expr.Arguments[1]}%";

        return $"{column} LIKE @{paramName}";
    }

    private static string BuildEndsWithFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 2)
            throw new ArgumentException("endswith requires 2 arguments");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        var paramName = $"p{paramCounter++}";
        parameters[paramName] = $"%{expr.Arguments[1]}";

        return $"{column} LIKE @{paramName}";
    }

    private static string BuildLengthFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 1)
            throw new ArgumentException("length requires 1 argument");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        return $"LENGTH({column})";
    }

    private static string BuildToLowerFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 1)
            throw new ArgumentException("tolower requires 1 argument");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        return $"LOWER({column})";
    }

    private static string BuildToUpperFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 1)
            throw new ArgumentException("toupper requires 1 argument");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        return $"UPPER({column})";
    }

    private static string BuildTrimFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 1)
            throw new ArgumentException("trim requires 1 argument");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        return $"TRIM({column})";
    }

    private static string BuildConcatFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count < 2)
            throw new ArgumentException("concat requires at least 2 arguments");

        var parts = new List<string>();
        foreach (var arg in expr.Arguments)
        {
            if (arg is string str && !str.StartsWith("@"))
            {
                var paramName = $"p{paramCounter++}";
                parameters[paramName] = str;
                parts.Add($"@{paramName}");
            }
            else
            {
                parts.Add(MapPropertyToColumn(arg.ToString()!));
            }
        }

        return $"CONCAT({string.Join(", ", parts)})";
    }

    private static string BuildSubstringFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count < 2 || expr.Arguments.Count > 3)
            throw new ArgumentException("substring requires 2 or 3 arguments");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        var startParam = $"p{paramCounter++}";
        parameters[startParam] = Convert.ToInt32(expr.Arguments[1]) + 1; // PostgreSQL substring is 1-indexed

        if (expr.Arguments.Count == 3)
        {
            var lengthParam = $"p{paramCounter++}";
            parameters[lengthParam] = expr.Arguments[2];
            return $"SUBSTRING({column}, @{startParam}, @{lengthParam})";
        }

        return $"SUBSTRING({column}, @{startParam})";
    }

    private static string BuildIndexOfFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 2)
            throw new ArgumentException("indexof requires 2 arguments");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        var paramName = $"p{paramCounter++}";
        parameters[paramName] = expr.Arguments[1];

        return $"(POSITION(@{paramName} IN {column}) - 1)"; // Convert to 0-indexed
    }

    // Math functions
    private static string BuildRoundFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 1)
            throw new ArgumentException("round requires 1 argument");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        return $"ROUND({column})";
    }

    private static string BuildFloorFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 1)
            throw new ArgumentException("floor requires 1 argument");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        return $"FLOOR({column})";
    }

    private static string BuildCeilingFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 1)
            throw new ArgumentException("ceiling requires 1 argument");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        return $"CEILING({column})";
    }

    private static string BuildMathOperation(FunctionExpression expr, string op, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 2)
            throw new ArgumentException($"Math operation {op} requires 2 arguments");

        var left = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        var right = MapPropertyToColumn(expr.Arguments[1].ToString()!);

        return $"({left} {op} {right})";
    }

    // Spatial functions
    private static string BuildGeoDistanceFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 2)
            throw new ArgumentException("geo.distance requires 2 arguments");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        var geomParam = $"p{paramCounter++}";

        // Extract geometry from WKT or GeoJSON
        var geomArg = expr.Arguments[1].ToString()!;
        if (geomArg.StartsWith("geometry'"))
        {
            var wkt = geomArg.Substring(9, geomArg.Length - 10);
            parameters[geomParam] = wkt;
            return $"ST_Distance({column}, ST_GeomFromText(@{geomParam}, 4326))";
        }

        parameters[geomParam] = geomArg;
        return $"ST_Distance({column}, ST_GeomFromGeoJSON(@{geomParam}))";
    }

    private static string BuildGeoIntersectsFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 2)
            throw new ArgumentException("geo.intersects requires 2 arguments");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        var geomParam = $"p{paramCounter++}";

        var geomArg = expr.Arguments[1].ToString()!;
        if (geomArg.StartsWith("geometry'"))
        {
            var wkt = geomArg.Substring(9, geomArg.Length - 10);
            parameters[geomParam] = wkt;
            return $"ST_Intersects({column}, ST_GeomFromText(@{geomParam}, 4326))";
        }

        parameters[geomParam] = geomArg;
        return $"ST_Intersects({column}, ST_GeomFromGeoJSON(@{geomParam}))";
    }

    private static string BuildGeoLengthFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 1)
            throw new ArgumentException("geo.length requires 1 argument");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        return $"ST_Length({column}::geography)";
    }

    private static string BuildGeoWithinFunction(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 2)
            throw new ArgumentException("geo.within requires 2 arguments");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        var geomParam = $"p{paramCounter++}";

        var geomArg = expr.Arguments[1].ToString()!;
        if (geomArg.StartsWith("geometry'"))
        {
            var wkt = geomArg.Substring(9, geomArg.Length - 10);
            parameters[geomParam] = wkt;
            return $"ST_Within({column}, ST_GeomFromText(@{geomParam}, 4326))";
        }

        parameters[geomParam] = geomArg;
        return $"ST_Within({column}, ST_GeomFromGeoJSON(@{geomParam}))";
    }

    // Temporal functions
    private static string BuildTemporalFunction(FunctionExpression expr, string sqlFunc, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 1)
            throw new ArgumentException($"Temporal function requires 1 argument");

        var column = MapPropertyToColumn(expr.Arguments[0].ToString()!);
        return $"{sqlFunc} {column})";
    }

    // Comparison wrapper
    private static string BuildComparisonWrapper(FunctionExpression expr, Dictionary<string, object> parameters, ref int paramCounter)
    {
        if (expr.Arguments.Count != 3)
            throw new ArgumentException("comparison wrapper requires 3 arguments");

        var leftFunc = expr.Arguments[0] as FunctionExpression;
        if (leftFunc == null)
            throw new ArgumentException("First argument must be a function expression");

        var leftSql = BuildFunction(leftFunc, parameters, ref paramCounter);
        var operatorStr = expr.Arguments[1].ToString()!;
        var rightValue = expr.Arguments[2];

        var paramName = $"p{paramCounter++}";
        parameters[paramName] = rightValue;

        var sqlOperator = operatorStr.ToLowerInvariant() switch
        {
            "equals" => "=",
            "notequals" => "!=",
            "greaterthan" => ">",
            "greaterthanorequal" => ">=",
            "lessthan" => "<",
            "lessthanorequal" => "<=",
            _ => "="
        };

        return $"{leftSql} {sqlOperator} @{paramName}";
    }

    /// <summary>
    /// Maps OData property names to PostgreSQL column names.
    /// Handles both entity properties and navigation properties.
    /// </summary>
    private static string MapPropertyToColumn(string property)
    {
        // Handle navigation properties (e.g., "Datastream/id" -> "datastream_id")
        if (property.Contains('/'))
        {
            var parts = property.Split('/');
            return string.Join("_", parts.Select(p => ToSnakeCase(p)));
        }

        // Standard property mapping
        return property.ToLowerInvariant() switch
        {
            "id" or "@iot.id" => "id",
            "name" => "name",
            "description" => "description",
            "properties" => "properties",
            "result" => "result",
            "resulttime" => "result_time",
            "phenomenontime" => "phenomenon_time",
            "validtime" => "valid_time",
            "resultquality" => "result_quality",
            "parameters" => "parameters",
            "location" => "location",
            "encodingtype" => "encoding_type",
            "metadata" => "metadata",
            "observationtype" => "observation_type",
            "unitofmeasurement" => "unit_of_measurement",
            "observedarea" => "observed_area",
            "phenomenontimestart" => "phenomenon_time_start",
            "phenomenontimeend" => "phenomenon_time_end",
            "resulttimestart" => "result_time_start",
            "resulttimeend" => "result_time_end",
            "definition" => "definition",
            "feature" => "feature",
            "time" => "time",
            _ => ToSnakeCase(property)
        };
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder();
        sb.Append(char.ToLower(input[0]));

        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
            {
                sb.Append('_');
                sb.Append(char.ToLower(input[i]));
            }
            else
            {
                sb.Append(input[i]);
            }
        }

        return sb.ToString();
    }
}

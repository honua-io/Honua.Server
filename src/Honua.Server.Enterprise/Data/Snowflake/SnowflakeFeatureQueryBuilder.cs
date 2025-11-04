// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Linq;
using System.Text;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Snowflake;

internal sealed class SnowflakeFeatureQueryBuilder
{
    private readonly LayerDefinition _layer;

    public SnowflakeFeatureQueryBuilder(LayerDefinition layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildSelect(FeatureQuery query)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var geometryField = _layer.GeometryField;
        var parameters = new Dictionary<string, object?>();

        // Select all columns except geometry (avoid duplication), then add GeoJSON geometry
        // This reduces bandwidth by not transferring raw geometry twice
        sql.Append("SELECT * EXCLUDE (");
        sql.Append(QuoteIdentifier(geometryField));
        sql.Append("), ST_ASGEOJSON(");
        sql.Append(QuoteIdentifier(geometryField));
        sql.Append(") as _geojson FROM ");
        sql.Append(GetTableExpression());

        AppendWhereClause(sql, query, parameters);
        AppendOrderBy(sql, query);
        AppendPagination(sql, query, parameters);

        return (sql.ToString(), parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildCount(FeatureQuery query)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>();

        sql.Append("SELECT COUNT(*) FROM ");
        sql.Append(GetTableExpression());

        AppendWhereClause(sql, query, parameters);

        return (sql.ToString(), parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildById(string featureId)
    {
        Guard.NotNullOrWhiteSpace(featureId);

        var sql = new StringBuilder();
        var geometryField = _layer.GeometryField;
        var parameters = new Dictionary<string, object?>();

        // Select all columns except geometry (avoid duplication), then add GeoJSON geometry
        sql.Append("SELECT * EXCLUDE (");
        sql.Append(QuoteIdentifier(geometryField));
        sql.Append("), ST_ASGEOJSON(");
        sql.Append(QuoteIdentifier(geometryField));
        sql.Append(") as _geojson FROM ");
        sql.Append(GetTableExpression());
        sql.Append(" WHERE ");
        sql.Append(QuoteIdentifier(GetPrimaryKeyColumn()));
        sql.Append(" = :id");
        sql.Append(" LIMIT 1");

        parameters["id"] = featureId;
        return (sql.ToString(), parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildStatistics(
        FeatureQuery query,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields)
    {
        Guard.NotNull(query);
        Guard.NotNull(statistics);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>();

        sql.Append("SELECT ");
        var selectParts = new List<string>();

        if (groupByFields is { Count: > 0 })
        {
            foreach (var groupField in groupByFields)
            {
                selectParts.Add(QuoteIdentifier(groupField));
            }
        }

        foreach (var statistic in statistics)
        {
            var outputName = statistic.OutputName ?? $"{statistic.Type}_{statistic.FieldName}";
            var aggregateExpression = BuildAggregateExpression(statistic);
            selectParts.Add($"{aggregateExpression} AS {QuoteAlias(outputName)}");
        }

        if (selectParts.Count == 0)
        {
            selectParts.Add("1");
        }

        sql.Append(string.Join(", ", selectParts));
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());

        AppendWhereClause(sql, query, parameters);

        if (groupByFields is { Count: > 0 })
        {
            sql.Append(" GROUP BY ");
            sql.Append(string.Join(", ", groupByFields.Select(QuoteIdentifier)));
        }

        return (sql.ToString(), parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildDistinct(
        FeatureQuery query,
        IReadOnlyList<string> fieldNames)
    {
        Guard.NotNull(query);
        Guard.NotNull(fieldNames);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>();

        sql.Append("SELECT DISTINCT ");
        sql.Append(string.Join(", ", fieldNames.Select(QuoteIdentifier)));
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());

        AppendWhereClause(sql, query, parameters);
        sql.Append(" LIMIT 10000");

        return (sql.ToString(), parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildExtent(FeatureQuery query)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>();
        var geometryColumn = QuoteIdentifier(_layer.GeometryField);

        sql.Append("SELECT ");
        sql.Append($"MIN(ST_XMIN({geometryColumn})) AS {QuoteAlias("minx")}, ");
        sql.Append($"MIN(ST_YMIN({geometryColumn})) AS {QuoteAlias("miny")}, ");
        sql.Append($"MAX(ST_XMAX({geometryColumn})) AS {QuoteAlias("maxx")}, ");
        sql.Append($"MAX(ST_YMAX({geometryColumn})) AS {QuoteAlias("maxy")}");
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());

        AppendWhereClause(sql, query, parameters);

        return (sql.ToString(), parameters);
    }

    private string BuildAggregateExpression(StatisticDefinition statistic)
    {
        var fieldReference = statistic.FieldName.IsNullOrWhiteSpace()
            ? null
            : QuoteIdentifier(statistic.FieldName);

        return statistic.Type switch
        {
            StatisticType.Count => "COUNT(*)",
            StatisticType.Sum => EnsureFieldForAggregate("SUM", fieldReference, statistic.Type),
            StatisticType.Avg => EnsureFieldForAggregate("AVG", fieldReference, statistic.Type),
            StatisticType.Min => EnsureFieldForAggregate("MIN", fieldReference, statistic.Type),
            StatisticType.Max => EnsureFieldForAggregate("MAX", fieldReference, statistic.Type),
            _ => throw new NotSupportedException($"Statistic type '{statistic.Type}' is not supported.")
        };
    }

    private void AppendWhereClause(StringBuilder sql, FeatureQuery query, Dictionary<string, object?> parameters)
    {
        var conditions = new List<string>();

        // Spatial filter (bbox)
        if (query.Bbox is not null)
        {
            var bbox = query.Bbox;

            // Validate bbox coordinates are finite numbers
            ValidateCoordinate(bbox.MinX, nameof(bbox.MinX));
            ValidateCoordinate(bbox.MinY, nameof(bbox.MinY));
            ValidateCoordinate(bbox.MaxX, nameof(bbox.MaxX));
            ValidateCoordinate(bbox.MaxY, nameof(bbox.MaxY));

            var geometryField = QuoteIdentifier(_layer.GeometryField);

            // Build WKT polygon in C# code to prevent SQL injection
            // Previously used SQL string concatenation which was vulnerable to injection
            var bboxWkt = $"POLYGON(({bbox.MinX} {bbox.MinY}, {bbox.MaxX} {bbox.MinY}, {bbox.MaxX} {bbox.MaxY}, {bbox.MinX} {bbox.MaxY}, {bbox.MinX} {bbox.MinY}))";
            parameters["bbox_wkt"] = bboxWkt;

            // Snowflake ST_INTERSECTS with fully parameterized WKT
            conditions.Add($"ST_INTERSECTS({geometryField}, TO_GEOGRAPHY(:bbox_wkt))");
        }

        // Temporal filter
        var temporalColumn = _layer.Storage?.TemporalColumn;
        if (query.Temporal is not null && temporalColumn.HasValue())
        {
            var temporal = query.Temporal;

            if (temporal.Start.HasValue)
            {
                // Validate temporal start date
                ValidateTemporalDate(temporal.Start.Value, nameof(temporal.Start));
                parameters["temporal_start"] = temporal.Start.Value;
                conditions.Add($"{QuoteIdentifier(temporalColumn)} >= :temporal_start");
            }

            if (temporal.End.HasValue)
            {
                // Validate temporal end date
                ValidateTemporalDate(temporal.End.Value, nameof(temporal.End));
                parameters["temporal_end"] = temporal.End.Value;
                conditions.Add($"{QuoteIdentifier(temporalColumn)} <= :temporal_end");
            }
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ");
            sql.Append(string.Join(" AND ", conditions));
        }
    }

    private void AppendOrderBy(StringBuilder sql, FeatureQuery query)
    {
        if (query.SortOrders is not { Count: > 0 })
        {
            return;
        }

        var defaultSort = GetPrimaryKeyColumn();
        var orderClauses = new List<string>();

        foreach (var sort in query.SortOrders)
        {
            var direction = sort.Direction == FeatureSortDirection.Descending ? "DESC" : "ASC";
            orderClauses.Add($"{QuoteIdentifier(sort.Field)} {direction}");
        }

        sql.Append(" ORDER BY ");
        sql.Append(string.Join(", ", orderClauses));
    }

    private void AppendPagination(StringBuilder sql, FeatureQuery query, Dictionary<string, object?> parameters)
    {
        // Validate limit and offset if provided
        if (query.Limit.HasValue)
        {
            ValidatePositiveInteger(query.Limit.Value, nameof(query.Limit));
        }

        if (query.Offset.HasValue && query.Offset.Value > 0)
        {
            ValidateNonNegativeInteger(query.Offset.Value, nameof(query.Offset));
        }

        PaginationHelper.BuildOffsetLimitClause(
            sql,
            query.Offset,
            query.Limit,
            parameters,
            PaginationHelper.DatabaseVendor.Snowflake,
            ":");
    }

    /// <summary>
    /// Gets the quoted table expression for use in SQL queries.
    /// Public method to support batched operations in SnowflakeDataStoreProvider.
    /// Uses consolidated LayerMetadataHelper for metadata extraction.
    /// </summary>
    /// <returns>Quoted table name</returns>
    public string GetTableExpression()
    {
        // Use consolidated LayerMetadataHelper for table name extraction
        return LayerMetadataHelper.GetTableExpression(_layer, QuoteIdentifier);
    }

    /// <summary>
    /// Gets the primary key column name (unquoted).
    /// Public method to support batched operations in SnowflakeDataStoreProvider.
    /// Uses consolidated LayerMetadataHelper for metadata extraction.
    /// </summary>
    /// <returns>Primary key column name</returns>
    public string GetPrimaryKeyColumn()
    {
        // Use consolidated LayerMetadataHelper for primary key extraction
        return LayerMetadataHelper.GetPrimaryKeyColumn(_layer);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildInsert(FeatureRecord record)
    {
        var table = GetTableExpression();
        var columns = new List<string>();
        var parameters = new Dictionary<string, object?>();
        var parameterIndex = 0;

        foreach (var (key, value) in record.Attributes)
        {
            if (string.Equals(key, "_geojson", StringComparison.OrdinalIgnoreCase))
                continue;

            columns.Add(QuoteIdentifier(key));
            var paramName = $"p{parameterIndex++}";
            parameters[paramName] = value;
        }

        var sql = $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters.Keys.Select(p => $":{p}"))})";
        return (sql, parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildUpdate(string featureId, FeatureRecord record)
    {
        var table = GetTableExpression();
        var primaryKey = GetPrimaryKeyColumn();
        var setClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();
        var parameterIndex = 0;

        foreach (var (key, value) in record.Attributes)
        {
            if (string.Equals(key, "_geojson", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(key, primaryKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var paramName = $"p{parameterIndex++}";
            setClauses.Add($"{QuoteIdentifier(key)} = :{paramName}");
            parameters[paramName] = value;
        }

        parameters["id"] = featureId;
        var sql = $"UPDATE {table} SET {string.Join(", ", setClauses)} WHERE {QuoteIdentifier(primaryKey)} = :id";
        return (sql, parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildDelete(string featureId)
    {
        Guard.NotNullOrWhiteSpace(featureId);

        var table = GetTableExpression();
        var primaryKey = GetPrimaryKeyColumn();
        var parameters = new Dictionary<string, object?>();

        parameters["id"] = featureId;
        var sql = $"DELETE FROM {table} WHERE {QuoteIdentifier(primaryKey)} = :id";
        return (sql, parameters);
    }

    /// <summary>
    /// Quotes a Snowflake identifier using double quotes after validating it for SQL injection protection.
    /// Snowflake uses the same quoting style as PostgreSQL.
    /// Handles qualified names (e.g., schema.table) by quoting each part individually.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote</param>
    /// <returns>The validated and quoted identifier safe for use in Snowflake SQL</returns>
    /// <exception cref="ArgumentException">Thrown if the identifier is invalid or contains dangerous characters</exception>
    private static string QuoteIdentifier(string identifier)
    {
        // Validate identifier for SQL injection protection before quoting
        SqlIdentifierValidator.ValidateIdentifier(identifier, nameof(identifier));

        // Split on dots to handle qualified names (schema.table, database.schema.table)
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

        // Quote each part individually
        for (var i = 0; i < parts.Length; i++)
        {
            var unquoted = parts[i].Trim('"'); // Remove existing quotes if any
            parts[i] = $"\"{unquoted.Replace("\"", "\"\"")}\"";
        }

        return string.Join('.', parts);
    }

    private static string EnsureFieldForAggregate(string functionName, string? fieldReference, StatisticType type)
    {
        if (fieldReference.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Statistic '{type}' requires a valid field name.");
        }

        return $"{functionName}({fieldReference})";
    }

    private static string QuoteAlias(string alias)
    {
        if (alias.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Alias must not be null or whitespace.", nameof(alias));
        }

        var trimmed = alias.Trim('"');
        return $"\"{trimmed.Replace("\"", "\"\"")}\"";
    }

    private static string QuoteString(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static void ValidateCoordinate(double coordinate, string parameterName)
    {
        if (double.IsNaN(coordinate))
        {
            throw new ArgumentException($"Coordinate '{parameterName}' cannot be NaN.", parameterName);
        }

        if (double.IsInfinity(coordinate))
        {
            throw new ArgumentException($"Coordinate '{parameterName}' cannot be infinity.", parameterName);
        }

        // Validate reasonable coordinate ranges (longitude: -180 to 180, latitude: -90 to 90)
        // Using extended range to support different coordinate systems
        if (coordinate < -180 || coordinate > 180)
        {
            throw new ArgumentException($"Coordinate '{parameterName}' must be between -180 and 180.", parameterName);
        }
    }

    private static void ValidatePositiveInteger(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be a positive integer.", parameterName);
        }

        // Prevent unreasonably large values that could cause performance issues
        if (value > 100000)
        {
            throw new ArgumentException($"Parameter '{parameterName}' exceeds maximum allowed value of 100000.", parameterName);
        }
    }

    private static void ValidateNonNegativeInteger(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be a non-negative integer.", parameterName);
        }

        // Prevent unreasonably large values
        if (value > 1000000)
        {
            throw new ArgumentException($"Parameter '{parameterName}' exceeds maximum allowed value of 1000000.", parameterName);
        }
    }

    private static void ValidateTemporalDate(DateTimeOffset date, string parameterName)
    {
        // Ensure date is within reasonable range (1900 to 2200)
        if (date.Year < 1900 || date.Year > 2200)
        {
            throw new ArgumentException($"Temporal date '{parameterName}' must be between year 1900 and 2200.", parameterName);
        }

        // Ensure date is not DateTimeOffset.MinValue or DateTimeOffset.MaxValue which could indicate uninitialized values
        if (date == DateTimeOffset.MinValue || date == DateTimeOffset.MaxValue)
        {
            throw new ArgumentException($"Temporal date '{parameterName}' cannot be DateTimeOffset.MinValue or DateTimeOffset.MaxValue.", parameterName);
        }
    }
}

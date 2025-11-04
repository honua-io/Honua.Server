// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Linq;
using System.Text;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Google.Cloud.BigQuery.V2;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Enterprise.Data.BigQuery;

internal sealed class BigQueryFeatureQueryBuilder
{
    private const string DefaultGeometryColumn = "geom";
    private readonly LayerDefinition _layer;

    public BigQueryFeatureQueryBuilder(LayerDefinition layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }

    public sealed class QueryResult
    {
        public required string Sql { get; init; }
        public BigQueryParameter[]? Parameters { get; init; }
    }

    public QueryResult BuildSelect(FeatureQuery query)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var parameters = new List<BigQueryParameter>();

        sql.Append("SELECT ");
        sql.Append(BuildSelectList(query));
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());

        AppendWhereClause(sql, query, parameters);
        AppendOrderBy(sql, query);
        AppendPagination(sql, query, parameters);

        return new QueryResult
        {
            Sql = sql.ToString(),
            Parameters = parameters.Count > 0 ? parameters.ToArray() : null
        };
    }

    public QueryResult BuildStatistics(
        FeatureQuery query,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields)
    {
        Guard.NotNull(query);
        Guard.NotNull(statistics);

        var sql = new StringBuilder();
        var parameters = new List<BigQueryParameter>();

        sql.Append("SELECT ");

        var selectParts = new List<string>();

        if (groupByFields is { Count: > 0 })
        {
            selectParts.AddRange(groupByFields.Select(QuoteIdentifier));
        }

        foreach (var stat in statistics)
        {
            var outputName = stat.OutputName ?? $"{stat.Type}_{stat.FieldName}";
            var aggregateExpression = BuildAggregateExpression(stat);
            selectParts.Add($"{aggregateExpression} AS {QuoteAlias(outputName)}");
        }

        if (selectParts.Count == 0)
        {
            // Always select at least one value to keep BigQuery happy even if no statistics provided.
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

        return new QueryResult
        {
            Sql = sql.ToString(),
            Parameters = parameters.Count > 0 ? parameters.ToArray() : null
        };
    }

    public QueryResult BuildDistinct(
        FeatureQuery query,
        IReadOnlyList<string> fieldNames)
    {
        Guard.NotNull(query);
        Guard.NotNull(fieldNames);

        var sql = new StringBuilder();
        var parameters = new List<BigQueryParameter>();

        sql.Append("SELECT DISTINCT ");
        sql.Append(string.Join(", ", fieldNames.Select(QuoteIdentifier)));
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());

        AppendWhereClause(sql, query, parameters);
        sql.Append(" LIMIT 10000");

        return new QueryResult
        {
            Sql = sql.ToString(),
            Parameters = parameters.Count > 0 ? parameters.ToArray() : null
        };
    }

    public QueryResult BuildExtent(FeatureQuery query)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var parameters = new List<BigQueryParameter>();
        var geometryColumn = QuoteIdentifier(GetGeometryColumn());

        sql.Append("SELECT ST_ASGEOJSON(ST_EXTENT(");
        sql.Append(geometryColumn);
        sql.Append(")) AS extent_geojson FROM ");
        sql.Append(GetTableExpression());

        AppendWhereClause(sql, query, parameters);

        return new QueryResult
        {
            Sql = sql.ToString(),
            Parameters = parameters.Count > 0 ? parameters.ToArray() : null
        };
    }

    public QueryResult BuildCount(FeatureQuery query)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var parameters = new List<BigQueryParameter>();

        sql.Append("SELECT COUNT(*) as count FROM ");
        sql.Append(GetTableExpression());

        AppendWhereClause(sql, query, parameters);

        return new QueryResult
        {
            Sql = sql.ToString(),
            Parameters = parameters.Count > 0 ? parameters.ToArray() : null
        };
    }

    public QueryResult BuildById(string featureId)
    {
        Guard.NotNullOrWhiteSpace(featureId);

        var sql = new StringBuilder();
        var parameters = new List<BigQueryParameter>();

        sql.Append("SELECT ");
        sql.Append(BuildSelectList(new FeatureQuery()));
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());
        sql.Append(" WHERE ");
        sql.Append(QuoteIdentifier(GetPrimaryKeyColumn()));
        sql.Append(" = @feature_id");
        sql.Append(" LIMIT 1");

        parameters.Add(new BigQueryParameter("feature_id", BigQueryDbType.String, featureId));

        return new QueryResult
        {
            Sql = sql.ToString(),
            Parameters = parameters.ToArray()
        };
    }

    private string BuildSelectList(FeatureQuery query)
    {
        var geometryField = _layer.GeometryField;
        var columns = new List<string>();

        // If specific properties requested, use those
        if (query.PropertyNames is { Count: > 0 })
        {
            foreach (var prop in query.PropertyNames)
            {
                if (string.Equals(prop, geometryField, StringComparison.OrdinalIgnoreCase))
                {
                    // BigQuery GEOGRAPHY to GeoJSON
                    columns.Add($"ST_ASGEOJSON({QuoteIdentifier(geometryField)}) as {QuoteIdentifier(geometryField)}");
                }
                else
                {
                    columns.Add(QuoteIdentifier(prop));
                }
            }
        }
        else
        {
            // Select all columns, convert GEOGRAPHY to GeoJSON
            columns.Add("*");
            columns.Add($"ST_ASGEOJSON({QuoteIdentifier(geometryField)}) as _geojson");
        }

        return string.Join(", ", columns);
    }

    private void AppendWhereClause(StringBuilder sql, FeatureQuery query, List<BigQueryParameter> parameters)
    {
        var conditions = new List<string>();

        // Spatial filter (bbox)
        if (query.Bbox is not null)
        {
            var bbox = query.Bbox;

            // Validate bbox coordinates
            ValidateCoordinate(bbox.MinX, nameof(bbox.MinX));
            ValidateCoordinate(bbox.MinY, nameof(bbox.MinY));
            ValidateCoordinate(bbox.MaxX, nameof(bbox.MaxX));
            ValidateCoordinate(bbox.MaxY, nameof(bbox.MaxY));

            var geometryField = QuoteIdentifier(_layer.GeometryField);

            // Create bounding box polygon in WKT format using parameterized query
            var bboxWkt = $"POLYGON(({bbox.MinX} {bbox.MinY}, {bbox.MaxX} {bbox.MinY}, {bbox.MaxX} {bbox.MaxY}, {bbox.MinX} {bbox.MaxY}, {bbox.MinX} {bbox.MinY}))";

            conditions.Add($"ST_INTERSECTS({geometryField}, ST_GEOGFROMTEXT(@bbox_wkt))");
            parameters.Add(new BigQueryParameter("bbox_wkt", BigQueryDbType.String, bboxWkt));
        }

        // Temporal filter
        var temporalColumn = _layer.Storage?.TemporalColumn;
        if (query.Temporal is not null && temporalColumn.HasValue())
        {
            var temporal = query.Temporal;
            var temporalField = QuoteIdentifier(temporalColumn);

            if (temporal.Start.HasValue)
            {
                ValidateDateTimeOffset(temporal.Start.Value, nameof(temporal.Start));
                conditions.Add($"{temporalField} >= @temporal_start");
                parameters.Add(new BigQueryParameter("temporal_start", BigQueryDbType.Timestamp, temporal.Start.Value.UtcDateTime));
            }

            if (temporal.End.HasValue)
            {
                ValidateDateTimeOffset(temporal.End.Value, nameof(temporal.End));
                conditions.Add($"{temporalField} <= @temporal_end");
                parameters.Add(new BigQueryParameter("temporal_end", BigQueryDbType.Timestamp, temporal.End.Value.UtcDateTime));
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

        sql.Append(" ORDER BY ");
        var orderClauses = new List<string>();

        foreach (var sort in query.SortOrders)
        {
            var direction = sort.Direction == FeatureSortDirection.Descending ? "DESC" : "ASC";
            orderClauses.Add($"{QuoteIdentifier(sort.Field)} {direction}");
        }

        sql.Append(string.Join(", ", orderClauses));
    }

    private void AppendPagination(StringBuilder sql, FeatureQuery query, List<BigQueryParameter> parameters)
    {
        if (query.Limit.HasValue)
        {
            ValidatePositiveInteger(query.Limit.Value, nameof(query.Limit));
            sql.Append(" LIMIT @limit_value");
            parameters.Add(new BigQueryParameter("limit_value", BigQueryDbType.Int64, query.Limit.Value));
        }

        if (query.Offset.HasValue && query.Offset.Value > 0)
        {
            ValidateNonNegativeInteger(query.Offset.Value, nameof(query.Offset));
            sql.Append(" OFFSET @offset_value");
            parameters.Add(new BigQueryParameter("offset_value", BigQueryDbType.Int64, query.Offset.Value));
        }
    }

    private string GetTableExpression()
    {
        // BigQuery table format: `project.dataset.table`
        // Use LayerMetadataHelper to get the table name, then quote it
        var table = LayerMetadataHelper.GetTableName(_layer);
        return $"`{table}`";
    }

    private string GetPrimaryKeyColumn()
    {
        return LayerMetadataHelper.GetPrimaryKeyColumn(_layer);
    }

    private string GetGeometryColumn()
    {
        var column = LayerMetadataHelper.GetGeometryColumn(_layer);
        return column.IsNullOrWhiteSpace() ? DefaultGeometryColumn : column;
    }

    private static string BuildAggregateExpression(StatisticDefinition statistic)
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
            _ => throw new NotSupportedException($"Statistic type '{statistic.Type}' is not supported for BigQuery.")
        };
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

        var trimmed = alias.Trim('`');
        return $"`{trimmed.Replace("`", "``")}`";
    }

    /// <summary>
    /// Builds a parameterized INSERT query to prevent SQL injection vulnerabilities.
    /// All attribute values are passed as BigQuery parameters instead of being concatenated into the SQL string.
    /// </summary>
    public QueryResult BuildInsert(FeatureRecord record)
    {
        var table = GetTableExpression();
        var columns = new List<string>();
        var parameterNames = new List<string>();
        var parameters = new List<BigQueryParameter>();

        int paramIndex = 0;
        foreach (var (key, value) in record.Attributes)
        {
            if (string.Equals(key, "_geojson", StringComparison.OrdinalIgnoreCase))
                continue;

            columns.Add(QuoteIdentifier(key));

            var paramName = $"p{paramIndex}";
            parameterNames.Add($"@{paramName}");
            parameters.Add(CreateParameter(paramName, value));
            paramIndex++;
        }

        var sql = $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameterNames)})";

        return new QueryResult
        {
            Sql = sql,
            Parameters = parameters.ToArray()
        };
    }

    /// <summary>
    /// Builds a parameterized UPDATE query to prevent SQL injection vulnerabilities.
    /// All attribute values and the feature ID are passed as BigQuery parameters instead of being concatenated into the SQL string.
    /// </summary>
    public QueryResult BuildUpdate(string featureId, FeatureRecord record)
    {
        var table = GetTableExpression();
        var primaryKey = GetPrimaryKeyColumn();
        var setClauses = new List<string>();
        var parameters = new List<BigQueryParameter>();

        int paramIndex = 0;
        foreach (var (key, value) in record.Attributes)
        {
            if (string.Equals(key, "_geojson", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(key, primaryKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var paramName = $"p{paramIndex}";
            setClauses.Add($"{QuoteIdentifier(key)} = @{paramName}");
            parameters.Add(CreateParameter(paramName, value));
            paramIndex++;
        }

        parameters.Add(new BigQueryParameter("feature_id", BigQueryDbType.String, featureId));

        var sql = $"UPDATE {table} SET {string.Join(", ", setClauses)} WHERE {QuoteIdentifier(primaryKey)} = @feature_id";

        return new QueryResult
        {
            Sql = sql,
            Parameters = parameters.ToArray()
        };
    }

    /// <summary>
    /// Builds a parameterized DELETE query to prevent SQL injection vulnerabilities.
    /// The feature ID is passed as a BigQuery parameter instead of being concatenated into the SQL string.
    /// </summary>
    public QueryResult BuildDelete(string featureId)
    {
        var table = GetTableExpression();
        var primaryKey = GetPrimaryKeyColumn();
        var parameters = new List<BigQueryParameter>
        {
            new BigQueryParameter("feature_id", BigQueryDbType.String, featureId)
        };

        var sql = $"DELETE FROM {table} WHERE {QuoteIdentifier(primaryKey)} = @feature_id";

        return new QueryResult
        {
            Sql = sql,
            Parameters = parameters.ToArray()
        };
    }

    /// <summary>
    /// Creates a BigQueryParameter with the appropriate type based on the value.
    /// This ensures type safety and prevents SQL injection by letting BigQuery handle value serialization.
    /// </summary>
    private static BigQueryParameter CreateParameter(string name, object? value)
    {
        return value switch
        {
            null => new BigQueryParameter(name, BigQueryDbType.String, DBNull.Value),
            string s => new BigQueryParameter(name, BigQueryDbType.String, s),
            bool b => new BigQueryParameter(name, BigQueryDbType.Bool, b),
            int i => new BigQueryParameter(name, BigQueryDbType.Int64, (long)i),
            long l => new BigQueryParameter(name, BigQueryDbType.Int64, l),
            short sh => new BigQueryParameter(name, BigQueryDbType.Int64, (long)sh),
            byte by => new BigQueryParameter(name, BigQueryDbType.Int64, (long)by),
            float f => new BigQueryParameter(name, BigQueryDbType.Float64, (double)f),
            double d => new BigQueryParameter(name, BigQueryDbType.Float64, d),
            decimal dec => new BigQueryParameter(name, BigQueryDbType.Numeric, dec),
            DateTime dt => new BigQueryParameter(name, BigQueryDbType.Timestamp, dt),
            DateTimeOffset dto => new BigQueryParameter(name, BigQueryDbType.Timestamp, dto.UtcDateTime),
            _ => new BigQueryParameter(name, BigQueryDbType.String, value.ToString() ?? "")
        };
    }

    /// <summary>
    /// Quotes a BigQuery identifier using backticks, with SQL injection protection.
    /// Validates the identifier for length, valid characters, and potential injection attacks before quoting.
    /// Handles qualified names (e.g., dataset.table, project.dataset.table) by quoting each part individually.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote (table, column, or schema name)</param>
    /// <returns>The safely quoted identifier for use in BigQuery SQL</returns>
    /// <exception cref="ArgumentException">Thrown if the identifier is invalid or potentially malicious</exception>
    private static string QuoteIdentifier(string identifier)
    {
        SqlIdentifierValidator.ValidateIdentifier(identifier, nameof(identifier));

        // Split on dots to handle qualified names (dataset.table, project.dataset.table)
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

        // Quote each part individually
        for (var i = 0; i < parts.Length; i++)
        {
            var unquoted = parts[i].Trim('`'); // Remove existing quotes if any
            parts[i] = $"`{unquoted.Replace("`", "``")}`";
        }

        return string.Join('.', parts);
    }

    private static void ValidateCoordinate(double coordinate, string parameterName)
    {
        if (double.IsNaN(coordinate) || double.IsInfinity(coordinate))
        {
            throw new ArgumentException($"Coordinate value must be a valid number.", parameterName);
        }

        // Validate longitude range
        if (parameterName.Contains("X", StringComparison.OrdinalIgnoreCase))
        {
            if (coordinate < -180 || coordinate > 180)
            {
                throw new ArgumentOutOfRangeException(parameterName, coordinate, "Longitude must be between -180 and 180.");
            }
        }
        // Validate latitude range
        else if (parameterName.Contains("Y", StringComparison.OrdinalIgnoreCase))
        {
            if (coordinate < -90 || coordinate > 90)
            {
                throw new ArgumentOutOfRangeException(parameterName, coordinate, "Latitude must be between -90 and 90.");
            }
        }
    }

    private static void ValidateDateTimeOffset(DateTimeOffset dateTime, string parameterName)
    {
        if (dateTime == default)
        {
            throw new ArgumentException("DateTimeOffset value cannot be default.", parameterName);
        }

        // Ensure the date is within a reasonable range (BigQuery supports dates from 0001-01-01 to 9999-12-31)
        if (dateTime.Year < 1 || dateTime.Year > 9999)
        {
            throw new ArgumentOutOfRangeException(parameterName, dateTime, "DateTimeOffset year must be between 1 and 9999.");
        }
    }

    private static void ValidatePositiveInteger(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be greater than 0.");
        }
    }

    private static void ValidateNonNegativeInteger(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be greater than or equal to 0.");
        }
    }
}

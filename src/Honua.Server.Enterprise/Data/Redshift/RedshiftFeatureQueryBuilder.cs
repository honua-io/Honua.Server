// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text;
using Amazon.RedshiftDataAPIService.Model;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Redshift;

/// <summary>
/// Builds SQL queries for AWS Redshift (PostgreSQL-compatible)
/// </summary>
internal sealed class RedshiftFeatureQueryBuilder
{
    private readonly LayerDefinition _layer;
    private const int MaxLimit = 10000;

    public RedshiftFeatureQueryBuilder(LayerDefinition layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }

    public (string sql, List<SqlParameter> parameters) BuildSelect(FeatureQuery query)
    {
        var sql = new StringBuilder();
        var parameters = new List<SqlParameter>();
        var geometryField = _layer.GeometryField;
        var table = GetTableExpression();

        // Build explicit column list to avoid SELECT * bandwidth waste
        // Note: In production, enumerate actual table columns dynamically or from metadata
        // For now, using * with a comment explaining the optimization opportunity
        sql.Append("SELECT t.*, ST_AsGeoJSON(");
        sql.Append(QuoteIdentifier(geometryField));
        sql.Append(") as _geojson FROM ");
        sql.Append(table);
        sql.Append(" t");

        // WHERE clause for bounding box
        if (query.Bbox != null)
        {
            ValidateBbox(query.Bbox);

            sql.Append(" WHERE ST_Intersects(t.");
            sql.Append(QuoteIdentifier(geometryField));
            sql.Append(", ST_MakeEnvelope(:minx, :miny, :maxx, :maxy, 4326))");

            parameters.Add(new SqlParameter { Name = "minx", Value = query.Bbox.MinX.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            parameters.Add(new SqlParameter { Name = "miny", Value = query.Bbox.MinY.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            parameters.Add(new SqlParameter { Name = "maxx", Value = query.Bbox.MaxX.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            parameters.Add(new SqlParameter { Name = "maxy", Value = query.Bbox.MaxY.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        // ORDER BY
        if (query.SortOrders?.Count > 0)
        {
            // Validate all sort field names to prevent SQL injection
            foreach (var sortOrder in query.SortOrders)
            {
                SqlIdentifierValidator.ValidateIdentifier(sortOrder.Field, nameof(sortOrder.Field));
            }

            sql.Append(" ORDER BY ");
            sql.Append(string.Join(", ", query.SortOrders.Select(so =>
                $"{QuoteIdentifier(so.Field)} {(so.Direction == FeatureSortDirection.Ascending ? "ASC" : "DESC")}")));
        }

        // LIMIT and OFFSET with validation
        var limit = ValidateLimit(query.Limit);
        var offset = ValidateOffset(query.Offset);

        if (limit > 0)
        {
            sql.Append(" LIMIT :limit");
            parameters.Add(new SqlParameter { Name = "limit", Value = limit.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        if (offset > 0)
        {
            sql.Append(" OFFSET :offset");
            parameters.Add(new SqlParameter { Name = "offset", Value = offset.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        return (sql.ToString(), parameters);
    }

    public (string sql, List<SqlParameter> parameters) BuildCount(FeatureQuery query)
    {
        var sql = new StringBuilder();
        var parameters = new List<SqlParameter>();
        var table = GetTableExpression();

        sql.Append("SELECT COUNT(*) as count FROM ");
        sql.Append(table);

        if (query.Bbox != null)
        {
            ValidateBbox(query.Bbox);

            var geometryField = _layer.GeometryField;
            sql.Append(" WHERE ST_Intersects(");
            sql.Append(QuoteIdentifier(geometryField));
            sql.Append(", ST_MakeEnvelope(:minx, :miny, :maxx, :maxy, 4326))");

            parameters.Add(new SqlParameter { Name = "minx", Value = query.Bbox.MinX.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            parameters.Add(new SqlParameter { Name = "miny", Value = query.Bbox.MinY.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            parameters.Add(new SqlParameter { Name = "maxx", Value = query.Bbox.MaxX.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            parameters.Add(new SqlParameter { Name = "maxy", Value = query.Bbox.MaxY.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        return (sql.ToString(), parameters);
    }

    public (string sql, List<SqlParameter> parameters) BuildById(string featureId)
    {
        var sql = new StringBuilder();
        var parameters = new List<SqlParameter>();
        var geometryField = _layer.GeometryField;
        var table = GetTableExpression();
        var primaryKey = GetPrimaryKeyColumn();

        // Build explicit column list to avoid SELECT * bandwidth waste
        // Using t.* for now - consider enumerating columns from layer metadata in future
        sql.Append("SELECT t.*, ST_AsGeoJSON(t.");
        sql.Append(QuoteIdentifier(geometryField));
        sql.Append(") as _geojson FROM ");
        sql.Append(table);
        sql.Append(" t WHERE t.");
        sql.Append(QuoteIdentifier(primaryKey));
        sql.Append(" = :featureId");
        sql.Append(" LIMIT 1");

        parameters.Add(new SqlParameter { Name = "featureId", Value = featureId });

        return (sql.ToString(), parameters);
    }

    public (string sql, List<SqlParameter> parameters) BuildInsert(FeatureRecord record)
    {
        var table = GetTableExpression();
        var parameters = new List<SqlParameter>();
        var columns = new List<string>();
        var paramNames = new List<string>();
        var paramIndex = 0;

        foreach (var (key, value) in record.Attributes)
        {
            if (string.Equals(key, "_geojson", StringComparison.OrdinalIgnoreCase))
                continue;

            // Validate column name to prevent SQL injection
            SqlIdentifierValidator.ValidateIdentifier(key, nameof(key));

            columns.Add(QuoteIdentifier(key));
            var paramName = $"p{paramIndex}";
            paramNames.Add($":{paramName}");
            parameters.Add(new SqlParameter { Name = paramName, Value = FormatParameterValue(value) });
            paramIndex++;
        }

        var sql = $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)})";
        return (sql, parameters);
    }

    public (string sql, List<SqlParameter> parameters) BuildUpdate(string featureId, FeatureRecord record)
    {
        var table = GetTableExpression();
        var primaryKey = GetPrimaryKeyColumn();
        var parameters = new List<SqlParameter>();
        var setClauses = new List<string>();
        var paramIndex = 0;

        foreach (var (key, value) in record.Attributes)
        {
            if (string.Equals(key, "_geojson", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(key, primaryKey, StringComparison.OrdinalIgnoreCase))
                continue;

            // Validate column name to prevent SQL injection
            SqlIdentifierValidator.ValidateIdentifier(key, nameof(key));

            var paramName = $"p{paramIndex}";
            setClauses.Add($"{QuoteIdentifier(key)} = :{paramName}");
            parameters.Add(new SqlParameter { Name = paramName, Value = FormatParameterValue(value) });
            paramIndex++;
        }

        parameters.Add(new SqlParameter { Name = "featureId", Value = featureId });
        var sql = $"UPDATE {table} SET {string.Join(", ", setClauses)} WHERE {QuoteIdentifier(primaryKey)} = :featureId";
        return (sql, parameters);
    }

    public (string sql, List<SqlParameter> parameters) BuildDelete(string featureId)
    {
        var table = GetTableExpression();
        var primaryKey = GetPrimaryKeyColumn();
        var parameters = new List<SqlParameter>
        {
            new SqlParameter { Name = "featureId", Value = featureId }
        };
        var sql = $"DELETE FROM {table} WHERE {QuoteIdentifier(primaryKey)} = :featureId";
        return (sql, parameters);
    }

    public (string sql, List<SqlParameter> parameters) BuildStatistics(
        FeatureQuery query,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields)
    {
        Guard.NotNull(query);
        Guard.NotNull(statistics);

        var sql = new StringBuilder();
        var parameters = new List<SqlParameter>();
        const string alias = "t";

        sql.Append("SELECT ");
        var selectParts = new List<string>();

        if (groupByFields is { Count: > 0 })
        {
            foreach (var groupField in groupByFields)
            {
                SqlIdentifierValidator.ValidateIdentifier(groupField, nameof(groupByFields));
                selectParts.Add($"{alias}.{QuoteIdentifier(groupField)}");
            }
        }

        foreach (var statistic in statistics)
        {
            var outputName = statistic.OutputName ?? $"{statistic.Type}_{statistic.FieldName}";
            var aggregateExpression = BuildAggregateExpression(statistic, alias);
            selectParts.Add($"{aggregateExpression} AS {QuoteAlias(outputName)}");
        }

        if (selectParts.Count == 0)
        {
            selectParts.Add("1");
        }

        sql.Append(string.Join(", ", selectParts));
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        if (groupByFields is { Count: > 0 })
        {
            sql.Append(" GROUP BY ");
            sql.Append(string.Join(", ", groupByFields.Select(field =>
            {
                SqlIdentifierValidator.ValidateIdentifier(field, nameof(groupByFields));
                return $"{alias}.{QuoteIdentifier(field)}";
            })));
        }

        return (sql.ToString(), parameters);
    }

    public (string sql, List<SqlParameter> parameters) BuildDistinct(
        FeatureQuery query,
        IReadOnlyList<string> fieldNames)
    {
        Guard.NotNull(query);
        Guard.NotNull(fieldNames);

        var sql = new StringBuilder();
        var parameters = new List<SqlParameter>();
        const string alias = "t";

        sql.Append("SELECT DISTINCT ");
        sql.Append(string.Join(", ", fieldNames.Select(field =>
        {
            SqlIdentifierValidator.ValidateIdentifier(field, nameof(fieldNames));
            return $"{alias}.{QuoteIdentifier(field)}";
        })));
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);
        sql.Append(" LIMIT 10000");

        return (sql.ToString(), parameters);
    }

    public (string sql, List<SqlParameter> parameters) BuildExtent(FeatureQuery query)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var parameters = new List<SqlParameter>();
        const string alias = "t";
        var geometryColumn = QuoteIdentifier(_layer.GeometryField);

        sql.Append("SELECT ST_ASGEOJSON(ST_Extent(");
        sql.Append($"{alias}.{geometryColumn}");
        sql.Append(")) AS extent_geojson FROM ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        return (sql.ToString(), parameters);
    }

    internal string GetTableExpression()
    {
        var storage = _layer.Storage;
        if (storage is null)
        {
            throw new InvalidOperationException($"Layer '{_layer.Id}' does not have storage configuration.");
        }

        var table = storage.Table;
        if (table.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Layer '{_layer.Id}' does not have a table name.");
        }

        // Handle schema.table format with proper validation for SQL injection prevention
        if (table.Contains('.', StringComparison.Ordinal))
        {
            var parts = table.Split('.', 2);
            if (parts.Length == 2)
            {
                var schema = parts[0].Trim();
                var tableName = parts[1].Trim();

                // Validate both schema and table name to prevent SQL injection
                SqlIdentifierValidator.ValidateIdentifier(schema, nameof(schema));
                SqlIdentifierValidator.ValidateIdentifier(tableName, nameof(tableName));

                return $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";
            }
        }

        // Single table name without schema
        return QuoteIdentifier(table);
    }

    private string GetPrimaryKeyColumn()
    {
        var storage = _layer.Storage;
        if (storage is null)
        {
            throw new InvalidOperationException($"Layer '{_layer.Id}' does not have storage configuration.");
        }

        var keyColumn = storage.PrimaryKey;
        if (keyColumn.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Layer '{_layer.Id}' does not have a primary key column.");
        }

        return keyColumn;
    }

    private static string BuildAggregateExpression(StatisticDefinition statistic, string alias)
    {
        var fieldReference = statistic.FieldName.IsNullOrWhiteSpace()
            ? null
            : $"{alias}.{QuoteIdentifier(statistic.FieldName)}";

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

    private void AppendWhereClause(
        StringBuilder sql,
        FeatureQuery query,
        List<SqlParameter> parameters,
        string alias)
    {
        if (query.Bbox is null)
        {
            return;
        }

        ValidateBbox(query.Bbox);
        var geometryField = $"{alias}.{QuoteIdentifier(_layer.GeometryField)}";

        sql.Append(" WHERE ST_Intersects(");
        sql.Append(geometryField);
        sql.Append(", ST_MakeEnvelope(:minx, :miny, :maxx, :maxy, 4326))");

        parameters.Add(new SqlParameter { Name = "minx", Value = query.Bbox.MinX.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        parameters.Add(new SqlParameter { Name = "miny", Value = query.Bbox.MinY.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        parameters.Add(new SqlParameter { Name = "maxx", Value = query.Bbox.MaxX.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        parameters.Add(new SqlParameter { Name = "maxy", Value = query.Bbox.MaxY.ToString(System.Globalization.CultureInfo.InvariantCulture) });
    }

    private static string FormatParameterValue(object? value)
    {
        // Use SqlParameterHelper for consistent parameter value formatting
        return SqlParameterHelper.FormatParameterValue(value);
    }

    private static void ValidateBbox(BoundingBox bbox)
    {
        if (!IsValidCoordinate(bbox.MinX) || !IsValidCoordinate(bbox.MinY) ||
            !IsValidCoordinate(bbox.MaxX) || !IsValidCoordinate(bbox.MaxY))
        {
            throw new ArgumentException("Invalid bounding box coordinates. All values must be valid numbers.");
        }

        if (bbox.MinX > bbox.MaxX)
        {
            throw new ArgumentException($"Invalid bounding box: MinX ({bbox.MinX}) cannot be greater than MaxX ({bbox.MaxX}).");
        }

        if (bbox.MinY > bbox.MaxY)
        {
            throw new ArgumentException($"Invalid bounding box: MinY ({bbox.MinY}) cannot be greater than MaxY ({bbox.MaxY}).");
        }
    }

    private static bool IsValidCoordinate(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static int ValidateLimit(int? limit)
    {
        // Use PaginationHelper for consistent limit validation
        return PaginationHelper.ValidateLimit(limit, MaxLimit);
    }

    private static int ValidateOffset(int? offset)
    {
        // Use PaginationHelper for consistent offset validation
        return PaginationHelper.ValidateOffset(offset);
    }

    /// <summary>
    /// Quotes a Redshift identifier using double quotes, with SQL injection protection.
    /// Validates the identifier for length, valid characters, and potential injection attacks before quoting.
    /// Handles qualified names (e.g., schema.table) by quoting each part individually.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote (table, column, or schema name)</param>
    /// <returns>The safely quoted identifier for use in Redshift SQL</returns>
    /// <exception cref="ArgumentException">Thrown if the identifier is invalid or potentially malicious</exception>
    private static string QuoteIdentifier(string identifier)
    {
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
}

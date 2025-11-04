// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Oracle;

/// <summary>
/// Builds SQL queries for Oracle Spatial (SDO_GEOMETRY)
/// </summary>
internal sealed class OracleFeatureQueryBuilder
{
    private readonly LayerDefinition _layer;

    public OracleFeatureQueryBuilder(LayerDefinition layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildSelect(FeatureQuery query)
    {
        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>();
        var geometryField = _layer.GeometryField;
        var table = GetTableExpression();

        // Build explicit column list to avoid SELECT * bandwidth waste
        // Using t.* for now - consider enumerating columns from layer metadata in future
        sql.Append("SELECT t.*, SDO_UTIL.TO_GEOJSON(t.");
        sql.Append(QuoteIdentifier(geometryField));
        sql.Append(") as GEOJSON FROM ");
        sql.Append(table);
        sql.Append(" t");

        // WHERE clause for bounding box
        if (query.Bbox != null)
        {
            var bbox = query.Bbox;
            sql.Append(" WHERE SDO_RELATE(t.");
            sql.Append(QuoteIdentifier(geometryField));
            sql.Append(", SDO_GEOMETRY(2003, 4326, NULL, SDO_ELEM_INFO_ARRAY(1,1003,3), ");
            sql.Append("SDO_ORDINATE_ARRAY(:bbox_minx, :bbox_miny, :bbox_maxx, :bbox_maxy)), 'mask=anyinteract') = 'TRUE'");

            parameters["bbox_minx"] = bbox.MinX;
            parameters["bbox_miny"] = bbox.MinY;
            parameters["bbox_maxx"] = bbox.MaxX;
            parameters["bbox_maxy"] = bbox.MaxY;
        }

        // ORDER BY
        if (query.SortOrders?.Count > 0)
        {
            sql.Append(" ORDER BY ");
            sql.Append(string.Join(", ", query.SortOrders.Select(so =>
                $"{QuoteIdentifier(so.Field)} {(so.Direction == FeatureSortDirection.Ascending ? "ASC" : "DESC")}")));
        }

        // LIMIT and OFFSET (Oracle 12c+ syntax)
        if (query.Offset > 0)
        {
            sql.Append(" OFFSET :offset_value ROWS");
            parameters["offset_value"] = query.Offset;
        }

        if (query.Limit > 0)
        {
            sql.Append(" FETCH NEXT :limit_value ROWS ONLY");
            parameters["limit_value"] = query.Limit;
        }

        return (sql.ToString(), parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildCount(FeatureQuery query)
    {
        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>();
        var table = GetTableExpression();

        sql.Append("SELECT COUNT(*) as COUNT FROM ");
        sql.Append(table);

        if (query.Bbox != null)
        {
            var bbox = query.Bbox;
            var geometryField = _layer.GeometryField;
            sql.Append(" WHERE SDO_RELATE(");
            sql.Append(QuoteIdentifier(geometryField));
            sql.Append(", SDO_GEOMETRY(2003, 4326, NULL, SDO_ELEM_INFO_ARRAY(1,1003,3), ");
            sql.Append("SDO_ORDINATE_ARRAY(:bbox_minx, :bbox_miny, :bbox_maxx, :bbox_maxy)), 'mask=anyinteract') = 'TRUE'");

            parameters["bbox_minx"] = bbox.MinX;
            parameters["bbox_miny"] = bbox.MinY;
            parameters["bbox_maxx"] = bbox.MaxX;
            parameters["bbox_maxy"] = bbox.MaxY;
        }

        return (sql.ToString(), parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildById(string featureId)
    {
        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>();
        var geometryField = _layer.GeometryField;
        var table = GetTableExpression();
        var primaryKey = GetPrimaryKeyColumn();

        // Build explicit column list to avoid SELECT * bandwidth waste
        // Using t.* for now - consider enumerating columns from layer metadata in future
        sql.Append("SELECT t.*, SDO_UTIL.TO_GEOJSON(t.");
        sql.Append(QuoteIdentifier(geometryField));
        sql.Append(") as GEOJSON FROM ");
        sql.Append(table);
        sql.Append(" t WHERE t.");
        sql.Append(QuoteIdentifier(primaryKey));
        sql.Append(" = :feature_id");
        sql.Append(" FETCH NEXT 1 ROWS ONLY");

        parameters["feature_id"] = featureId;

        return (sql.ToString(), parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildInsert(FeatureRecord record)
    {
        var table = GetTableExpression();
        var columns = new List<string>();
        var values = new List<string>();
        var parameters = new Dictionary<string, object?>();
        var parameterIndex = 0;

        foreach (var (key, value) in record.Attributes)
        {
            if (string.Equals(key, "GEOJSON", StringComparison.OrdinalIgnoreCase))
                continue;

            columns.Add(QuoteIdentifier(key));

            var paramName = $"p{parameterIndex++}";
            values.Add($":{paramName}");
            parameters[paramName] = value;
        }

        return ($"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})", parameters);
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
            if (string.Equals(key, "GEOJSON", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(key, primaryKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var paramName = $"p{parameterIndex++}";
            setClauses.Add($"{QuoteIdentifier(key)} = :{paramName}");
            parameters[paramName] = value;
        }

        var idParamName = $"p{parameterIndex}";
        parameters[idParamName] = featureId;

        return ($"UPDATE {table} SET {string.Join(", ", setClauses)} WHERE {QuoteIdentifier(primaryKey)} = :{idParamName}", parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildDelete(string featureId)
    {
        var parameters = new Dictionary<string, object?>();
        var table = GetTableExpression();
        var primaryKey = GetPrimaryKeyColumn();

        parameters["feature_id"] = featureId;

        return ($"DELETE FROM {table} WHERE {QuoteIdentifier(primaryKey)} = :feature_id", parameters);
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
        const string alias = "t";

        sql.Append("SELECT ");
        var selectParts = new List<string>();

        if (groupByFields is { Count: > 0 })
        {
            foreach (var groupField in groupByFields)
            {
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

        AppendBboxWhereClause(sql, query, parameters, alias);

        if (groupByFields is { Count: > 0 })
        {
            sql.Append(" GROUP BY ");
            sql.Append(string.Join(", ", groupByFields.Select(field => $"{alias}.{QuoteIdentifier(field)}")));
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
        const string alias = "t";

        sql.Append("SELECT DISTINCT ");
        sql.Append(string.Join(", ", fieldNames.Select(field => $"{alias}.{QuoteIdentifier(field)}")));
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendBboxWhereClause(sql, query, parameters, alias);
        sql.Append(" FETCH FIRST 10000 ROWS ONLY");

        return (sql.ToString(), parameters);
    }

    public (string Sql, Dictionary<string, object?> Parameters) BuildExtent(FeatureQuery query)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>();
        const string alias = "t";
        var geometryColumn = $"{alias}.{QuoteIdentifier(_layer.GeometryField)}";

        sql.Append("SELECT ");
        sql.Append($"MIN(SDO_GEOM.SDO_MIN_MBR_ORDINATE({geometryColumn}, 1)) AS {QuoteAlias("minx")}, ");
        sql.Append($"MIN(SDO_GEOM.SDO_MIN_MBR_ORDINATE({geometryColumn}, 2)) AS {QuoteAlias("miny")}, ");
        sql.Append($"MAX(SDO_GEOM.SDO_MAX_MBR_ORDINATE({geometryColumn}, 1)) AS {QuoteAlias("maxx")}, ");
        sql.Append($"MAX(SDO_GEOM.SDO_MAX_MBR_ORDINATE({geometryColumn}, 2)) AS {QuoteAlias("maxy")}");
        sql.Append(" FROM ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendBboxWhereClause(sql, query, parameters, alias);

        return (sql.ToString(), parameters);
    }

    private string BuildAggregateExpression(StatisticDefinition statistic, string alias)
    {
        return AggregateExpressionBuilder.Build(statistic, alias, QuoteIdentifier);
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

    private void AppendBboxWhereClause(
        StringBuilder sql,
        FeatureQuery query,
        Dictionary<string, object?> parameters,
        string alias)
    {
        if (query.Bbox is null)
        {
            return;
        }

        var bbox = query.Bbox;
        var geometryField = $"{alias}.{QuoteIdentifier(_layer.GeometryField)}";

        sql.Append(" WHERE SDO_RELATE(");
        sql.Append(geometryField);
        sql.Append(", SDO_GEOMETRY(2003, 4326, NULL, SDO_ELEM_INFO_ARRAY(1,1003,3), ");
        sql.Append("SDO_ORDINATE_ARRAY(:bbox_minx, :bbox_miny, :bbox_maxx, :bbox_maxy)), 'mask=anyinteract') = 'TRUE'");

        parameters["bbox_minx"] = bbox.MinX;
        parameters["bbox_miny"] = bbox.MinY;
        parameters["bbox_maxx"] = bbox.MaxX;
        parameters["bbox_maxy"] = bbox.MaxY;
    }

    /// <summary>
    /// Gets the quoted table expression for the layer.
    /// </summary>
    public string GetTableExpression()
    {
        return LayerMetadataHelper.GetTableExpression(_layer, QuoteIdentifier);
    }

    private string GetPrimaryKeyColumn()
    {
        return LayerMetadataHelper.GetPrimaryKeyColumn(_layer);
    }

    /// <summary>
    /// Quotes an Oracle identifier using double quotes, with SQL injection protection.
    /// Validates the identifier for length, valid characters, and potential injection attacks before quoting.
    /// Handles qualified names (e.g., schema.table) by quoting each part individually.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote (table, column, or schema name)</param>
    /// <returns>The safely quoted identifier for use in Oracle SQL</returns>
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

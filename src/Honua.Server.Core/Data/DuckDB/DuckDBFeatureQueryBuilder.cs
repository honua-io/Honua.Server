// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Security;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.DuckDB;

internal sealed class DuckDBFeatureQueryBuilder
{
    private readonly LayerDefinition _layer;

    public DuckDBFeatureQueryBuilder(ServiceDefinition service, LayerDefinition layer)
    {
        _ = service ?? throw new ArgumentNullException(nameof(service));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }

    public DuckDBQueryDefinition BuildSelect(FeatureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("SELECT ");
        sql.Append(BuildSelectList(query, alias));
        sql.Append(" FROM ");
        sql.Append(QuoteIdentifier(GetTableName()));
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);
        AppendOrderBy(sql, query, alias);
        AppendPagination(sql, query, parameters);

        return new DuckDBQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public DuckDBQueryDefinition BuildCount(FeatureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("SELECT COUNT(*) FROM ");
        sql.Append(QuoteIdentifier(GetTableName()));
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        return new DuckDBQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public DuckDBQueryDefinition BuildById(string featureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureId);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("SELECT ");
        sql.Append(BuildSelectList(new FeatureQuery(), alias));
        sql.Append(" FROM ");
        sql.Append(QuoteIdentifier(GetTableName()));
        sql.Append(' ');
        sql.Append(alias);
        sql.Append(" WHERE ");
        sql.Append(alias);
        sql.Append('.');
        sql.Append(QuoteIdentifier(GetPrimaryKeyColumn()));
        sql.Append(" = $feature_id LIMIT 1");

        parameters["feature_id"] = featureId;

        return new DuckDBQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public DuckDBQueryDefinition BuildStatistics(
        FeatureQuery query,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(statistics);
        if (statistics.Count == 0)
        {
            throw new ArgumentException("At least one statistic must be specified.", nameof(statistics));
        }

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("SELECT ");

        var segments = new List<string>();
        if (groupByFields is { Count: > 0 })
        {
            segments.AddRange(groupByFields.Select(field => $"{alias}.{QuoteIdentifier(field)}"));
        }

        foreach (var statistic in statistics)
        {
            var aggregate = BuildAggregateExpression(statistic, alias);
            var outputName = statistic.OutputName ?? $"{statistic.Type}_{statistic.FieldName}";
            segments.Add($"{aggregate} AS {QuoteAlias(outputName)}");
        }

        sql.Append(string.Join(", ", segments));
        sql.Append(" FROM ");
        sql.Append(QuoteIdentifier(GetTableName()));
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        if (groupByFields is { Count: > 0 })
        {
            sql.Append(" GROUP BY ");
            sql.Append(string.Join(", ", groupByFields.Select(field => $"{alias}.{QuoteIdentifier(field)}")));
        }

        if (query.HavingClause.HasValue())
        {
            sql.Append(" HAVING ");
            sql.Append(query.HavingClause);
        }

        return new DuckDBQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public DuckDBQueryDefinition BuildDistinct(FeatureQuery query, IReadOnlyList<string> fieldNames)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(fieldNames);
        if (fieldNames.Count == 0)
        {
            throw new ArgumentException("At least one field is required for DISTINCT queries.", nameof(fieldNames));
        }

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("SELECT DISTINCT ");
        sql.Append(string.Join(", ", fieldNames.Select(field => $"{alias}.{QuoteIdentifier(field)}")));
        sql.Append(" FROM ");
        sql.Append(QuoteIdentifier(GetTableName()));
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        const int MaxDistinctLimit = 10000;
        var effectiveLimit = query.Limit.HasValue
            ? Math.Min(query.Limit.Value, MaxDistinctLimit)
            : MaxDistinctLimit;
        var paginatedQuery = query with { Limit = effectiveLimit };
        AppendPagination(sql, paginatedQuery, parameters);

        return new DuckDBQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public DuckDBQueryDefinition BuildExtent(FeatureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        var geometryColumn = $"{alias}.{QuoteIdentifier(GetGeometryColumn())}";

        // DuckDB spatial extension supports ST_Extent for calculating bounding boxes
        sql.Append("SELECT ");
        sql.Append($"ST_XMin(ST_Extent({geometryColumn})) AS {QuoteAlias("minx")}, ");
        sql.Append($"ST_YMin(ST_Extent({geometryColumn})) AS {QuoteAlias("miny")}, ");
        sql.Append($"ST_XMax(ST_Extent({geometryColumn})) AS {QuoteAlias("maxx")}, ");
        sql.Append($"ST_YMax(ST_Extent({geometryColumn})) AS {QuoteAlias("maxy")}");
        sql.Append(" FROM ");
        sql.Append(QuoteIdentifier(GetTableName()));
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        return new DuckDBQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    private string BuildSelectList(FeatureQuery query, string alias)
    {
        if (query.PropertyNames is null || query.PropertyNames.Count == 0)
        {
            return $"{alias}.*";
        }

        var columns = ResolveSelectColumns(query);
        var formatted = new List<string>(columns.Count);
        foreach (var column in columns)
        {
            formatted.Add($"{alias}.{QuoteIdentifier(column)}");
        }

        return string.Join(", ", formatted);
    }

    private void AppendWhereClause(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters, string alias)
    {
        var predicates = new List<string>();

        AppendBoundingBoxPredicate(query, predicates, parameters, alias);
        AppendTemporalPredicate(query, predicates, parameters, alias);
        AppendFilterPredicate(query, predicates, parameters, alias);

        if (predicates.Count == 0)
        {
            return;
        }

        sql.Append(" WHERE ");
        sql.Append(string.Join(" AND ", predicates));
    }

    private void AppendBoundingBoxPredicate(FeatureQuery query, ICollection<string> predicates, IDictionary<string, object?> parameters, string alias)
    {
        if (query.Bbox is null)
        {
            return;
        }

        var bbox = query.Bbox;
        var geometryColumn = $"{alias}.{QuoteIdentifier(GetGeometryColumn())}";

        // DuckDB spatial extension supports ST_Intersects with bounding boxes
        var bboxWkt = $"POLYGON(({bbox.MinX} {bbox.MinY}, {bbox.MaxX} {bbox.MinY}, {bbox.MaxX} {bbox.MaxY}, {bbox.MinX} {bbox.MaxY}, {bbox.MinX} {bbox.MinY}))";
        predicates.Add($"ST_Intersects({geometryColumn}, ST_GeomFromText('{bboxWkt}'))");
    }

    private void AppendTemporalPredicate(FeatureQuery query, ICollection<string> predicates, IDictionary<string, object?> parameters, string alias)
    {
        if (query.Temporal is null)
        {
            return;
        }

        var temporalColumn = _layer.Storage?.TemporalColumn;
        if (string.IsNullOrWhiteSpace(temporalColumn))
        {
            return;
        }

        var column = $"{alias}.{QuoteIdentifier(temporalColumn)}";
        if (query.Temporal.Start is not null)
        {
            predicates.Add($"{column} >= $datetime_start");
            parameters["datetime_start"] = query.Temporal.Start.Value.UtcDateTime;
        }

        if (query.Temporal.End is not null)
        {
            predicates.Add($"{column} <= $datetime_end");
            parameters["datetime_end"] = query.Temporal.End.Value.UtcDateTime;
        }
    }

    private void AppendFilterPredicate(FeatureQuery query, ICollection<string> predicates, IDictionary<string, object?> parameters, string alias)
    {
        if (query.Filter?.Expression is null)
        {
            return;
        }

        var entityDefinition = query.EntityDefinition ?? throw new InvalidOperationException("Query entity definition is required to translate filters.");
        var translator = new SqlFilterTranslator(entityDefinition, parameters, QuoteIdentifier);
        var predicate = translator.Translate(query.Filter, alias);

        if (!string.IsNullOrWhiteSpace(predicate))
        {
            predicates.Add(predicate);
        }
    }

    private void AppendOrderBy(StringBuilder sql, FeatureQuery query, string alias)
    {
        var defaultSort = string.IsNullOrWhiteSpace(_layer.IdField) ? "rowid" : _layer.IdField;
        PaginationHelper.BuildOrderByClause(sql, query.SortOrders, alias, QuoteIdentifier, defaultSort);
    }

    private void AppendPagination(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters)
    {
        PaginationHelper.BuildOffsetLimitClause(
            sql,
            query.Offset,
            query.Limit,
            parameters,
            PaginationHelper.DatabaseVendor.PostgreSQL, // DuckDB uses PostgreSQL-style parameters
            "$");
    }

    private string GetTableName() => LayerMetadataHelper.GetTableName(_layer);

    private string GetPrimaryKeyColumn() => LayerMetadataHelper.GetPrimaryKeyColumn(_layer);

    private string GetGeometryColumn() => LayerMetadataHelper.GetGeometryColumn(_layer);

    private string BuildAggregateExpression(StatisticDefinition statistic, string alias)
    {
        return AggregateExpressionBuilder.Build(statistic, alias, QuoteIdentifier);
    }

    private IReadOnlyList<string> ResolveSelectColumns(FeatureQuery query)
    {
        if (query.PropertyNames is null || query.PropertyNames.Count == 0)
        {
            return Array.Empty<string>();
        }

        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? column)
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                return;
            }

            if (seen.Add(column))
            {
                columns.Add(column);
            }
        }

        Add(_layer.GeometryField);
        Add(_layer.IdField);
        Add(_layer.Storage?.PrimaryKey);
        if (query.SortOrders is { Count: > 0 })
        {
            foreach (var sort in query.SortOrders)
            {
                Add(sort.Field);
            }
        }

        foreach (var property in query.PropertyNames)
        {
            Add(property);
        }

        return columns;
    }

    internal static string QuoteIdentifier(string identifier)
    {
        // DuckDB uses double quotes for identifiers like PostgreSQL
        return SqlIdentifierValidator.ValidateAndQuotePostgreSQL(identifier);
    }

    private static string QuoteAlias(string alias) => SqlIdentifierValidator.ValidateAndQuotePostgreSQL(alias);
}

internal sealed record DuckDBQueryDefinition(string Sql, IReadOnlyDictionary<string, object?> Parameters);

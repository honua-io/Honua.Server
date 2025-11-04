// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Security;
using NetTopologySuite.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.MySql;

internal sealed class MySqlFeatureQueryBuilder
{
    internal const string GeoJsonColumnAlias = "__geom_geojson";

    private readonly ServiceDefinition _service;
    private readonly LayerDefinition _layer;
    private readonly int _storageSrid;
    private readonly int _targetSrid;

    public MySqlFeatureQueryBuilder(ServiceDefinition service, LayerDefinition layer, int storageSrid, int targetSrid)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _storageSrid = storageSrid;
        _targetSrid = targetSrid;
    }

    public MySqlQueryDefinition BuildSelect(FeatureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("select ");
        sql.Append(BuildSelectList(query, alias));
        sql.Append(" from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);
        AppendOrderBy(sql, query, alias);
        AppendPagination(sql, query, parameters);

        return new MySqlQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public MySqlQueryDefinition BuildCount(FeatureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("select count(*) from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        return new MySqlQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public MySqlQueryDefinition BuildById(string featureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureId);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("select ");
        sql.Append(BuildSelectList(new FeatureQuery(), alias));
        sql.Append(" from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);
        sql.Append(" where ");
        sql.Append(alias);
        sql.Append('.');
        sql.Append(QuoteIdentifier(GetPrimaryKeyColumn()));
        sql.Append(" = @feature_id limit 1");

        parameters["feature_id"] = NormalizeKeyValue(featureId);

        return new MySqlQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public MySqlQueryDefinition BuildStatistics(
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

        sql.Append("select ");

        var segments = new List<string>();
        if (groupByFields is { Count: > 0 })
        {
            segments.AddRange(groupByFields.Select(field => $"{alias}.{QuoteIdentifier(field)}"));
        }

        foreach (var statistic in statistics)
        {
            var aggregate = BuildAggregateExpression(statistic, alias);
            var outputName = statistic.OutputName ?? $"{statistic.Type}_{statistic.FieldName}";
            segments.Add($"{aggregate} as {QuoteAlias(outputName)}");
        }

        sql.Append(string.Join(", ", segments));
        sql.Append(" from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        if (groupByFields is { Count: > 0 })
        {
            sql.Append(" group by ");
            sql.Append(string.Join(", ", groupByFields.Select(field => $"{alias}.{QuoteIdentifier(field)}")));
        }

        if (query.HavingClause.HasValue())
        {
            sql.Append(" having ");
            sql.Append(query.HavingClause);
        }

        return new MySqlQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public MySqlQueryDefinition BuildDistinct(FeatureQuery query, IReadOnlyList<string> fieldNames)
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

        sql.Append("select distinct ");
        sql.Append(string.Join(", ", fieldNames.Select(field => $"{alias}.{QuoteIdentifier(field)}")));
        sql.Append(" from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        const int MaxDistinctLimit = 10000;
        var effectiveLimit = query.Limit.HasValue
            ? Math.Min(query.Limit.Value, MaxDistinctLimit)
            : MaxDistinctLimit;
        var paginatedQuery = query with { Limit = effectiveLimit };
        AppendPagination(sql, paginatedQuery, parameters);

        return new MySqlQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public MySqlQueryDefinition BuildExtent(FeatureQuery query, int targetSrid)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        var geometryColumn = $"{alias}.{QuoteIdentifier(GetGeometryColumn())}";
        var geometryReference = targetSrid != _storageSrid
            ? $"ST_Transform({geometryColumn}, {targetSrid})"
            : geometryColumn;

        sql.Append("select ");
        sql.Append($"min(ST_XMin({geometryReference})) as {QuoteAlias("minx")}, ");
        sql.Append($"min(ST_YMin({geometryReference})) as {QuoteAlias("miny")}, ");
        sql.Append($"max(ST_XMax({geometryReference})) as {QuoteAlias("maxx")}, ");
        sql.Append($"max(ST_YMax({geometryReference})) as {QuoteAlias("maxy")}");
        sql.Append(" from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        return new MySqlQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    private string BuildSelectList(FeatureQuery query, string alias)
    {
        var segments = new List<string>();

        if (query.PropertyNames is null || query.PropertyNames.Count == 0)
        {
            segments.Add($"{alias}.*");
        }
        else
        {
            foreach (var column in ResolveSelectColumns(query))
            {
                if (string.Equals(column, GetGeometryColumn(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                segments.Add($"{alias}.{QuoteIdentifier(column)}");
            }
        }

        segments.Add(BuildGeometryProjection(alias));
        return string.Join(", ", segments);
    }

    private string BuildGeometryProjection(string alias)
    {
        var geometryIdentifier = QuoteIdentifier(GetGeometryColumn());
        var sourceExpression = $"{alias}.{geometryIdentifier}";
        var projected = _targetSrid != _storageSrid
            ? $"ST_Transform({sourceExpression}, {_targetSrid})"
            : sourceExpression;

        return $"ST_AsGeoJSON({projected}) AS {GeoJsonColumnAlias}";
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

        sql.Append(" where ");
        sql.Append(string.Join(" and ", predicates));
    }

    private void AppendBoundingBoxPredicate(FeatureQuery query, ICollection<string> predicates, IDictionary<string, object?> parameters, string alias)
    {
        if (query.Bbox is null)
        {
            return;
        }

        var bbox = query.Bbox;
        var bboxSrid = CrsHelper.ParseCrs(bbox.Crs ?? query.Crs ?? DetermineDefaultCrs());
        var geomColumn = $"{alias}.{QuoteIdentifier(GetGeometryColumn())}";
        var envelope = $"ST_MakeEnvelope(@bbox_minx, @bbox_miny, @bbox_maxx, @bbox_maxy, {bboxSrid})";
        var filterEnvelope = _storageSrid == bboxSrid
            ? envelope
            : $"ST_Transform({envelope}, {_storageSrid})";

        predicates.Add($"MBRIntersects({geomColumn}, {filterEnvelope})");
        predicates.Add($"ST_Intersects({geomColumn}, {filterEnvelope})");

        parameters["bbox_minx"] = bbox.MinX;
        parameters["bbox_miny"] = bbox.MinY;
        parameters["bbox_maxx"] = bbox.MaxX;
        parameters["bbox_maxy"] = bbox.MaxY;
    }

    private void AppendTemporalPredicate(FeatureQuery query, ICollection<string> predicates, IDictionary<string, object?> parameters, string alias)
    {
        if (query.Temporal is null)
        {
            return;
        }

        var temporalColumn = _layer.Storage?.TemporalColumn;
        if (temporalColumn.IsNullOrWhiteSpace())
        {
            return;
        }

        var column = $"{alias}.{QuoteIdentifier(temporalColumn)}";
        if (query.Temporal.Start is not null)
        {
            predicates.Add($"{column} >= @datetime_start");
            parameters["datetime_start"] = query.Temporal.Start.Value.UtcDateTime;
        }

        if (query.Temporal.End is not null)
        {
            predicates.Add($"{column} <= @datetime_end");
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
        var translator = new SqlFilterTranslator(
            entityDefinition,
            parameters,
            QuoteIdentifier,
            parameterPrefix: "filter",
            functionTranslator: (function, contextAlias) => TranslateFunction(function, contextAlias, query, parameters));
        var predicate = translator.Translate(query.Filter, alias);

        if (predicate.HasValue())
        {
            predicates.Add(predicate);
        }
    }

    private string? TranslateFunction(QueryFunctionExpression expression, string alias, FeatureQuery query, IDictionary<string, object?> parameters)
    {
        var functionName = expression.Name.ToLowerInvariant();

        // Handle measurement functions (geo.distance, geo.length) - return numeric values
        if (functionName == "geo.distance")
        {
            return TranslateGeoDistance(expression, alias, query, parameters);
        }

        if (functionName == "geo.length")
        {
            return TranslateGeoLength(expression, alias, parameters);
        }

        // Handle boolean predicate functions (geo.intersects)
        if (!string.Equals(expression.Name, "geo.intersects", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (expression.Arguments.Count != 2)
        {
            throw new NotSupportedException("geo.intersects requires exactly two arguments.");
        }

        if (!TryExtractFieldAndGeometry(expression.Arguments[0], expression.Arguments[1], out var fieldName, out var geometry) &&
            !TryExtractFieldAndGeometry(expression.Arguments[1], expression.Arguments[0], out fieldName, out geometry))
        {
            throw new NotSupportedException("geo.intersects requires a field reference and a geometry literal.");
        }

        var geomColumn = $"{alias}.{QuoteIdentifier(fieldName)}";
        var srid = geometry.Srid ?? ResolveQuerySrid(query);
        var geometryParam = AddSpatialParameter(parameters, geometry.WellKnownText);
        var geometrySql = $"ST_GeomFromText({geometryParam}, {srid})";
        var projectedGeometry = srid == _storageSrid ? geometrySql : $"ST_Transform({geometrySql}, {_storageSrid})";

        var envelopeSql = BuildEnvelopeSql(geometry.WellKnownText, srid, parameters);
        if (envelopeSql is not null)
        {
            return $"(MBRIntersects({geomColumn}, {envelopeSql}) AND ST_Intersects({geomColumn}, {projectedGeometry}))";
        }

        return $"ST_Intersects({geomColumn}, {projectedGeometry})";
    }

    private string TranslateGeoDistance(QueryFunctionExpression expression, string alias, FeatureQuery query, IDictionary<string, object?> parameters)
    {
        if (expression.Arguments.Count != 2)
        {
            throw new NotSupportedException("geo.distance requires exactly two arguments.");
        }

        if (!TryExtractFieldAndGeometry(expression.Arguments[0], expression.Arguments[1], out var fieldName, out var geometry) &&
            !TryExtractFieldAndGeometry(expression.Arguments[1], expression.Arguments[0], out fieldName, out geometry))
        {
            throw new NotSupportedException("geo.distance requires a field reference and a geometry literal.");
        }

        var geomColumn = $"{alias}.{QuoteIdentifier(fieldName)}";
        var srid = geometry.Srid ?? ResolveQuerySrid(query);
        var geometryParam = AddSpatialParameter(parameters, geometry.WellKnownText);
        var geometrySql = $"ST_GeomFromText({geometryParam}, {srid})";
        var projectedGeometry = srid == _storageSrid ? geometrySql : $"ST_Transform({geometrySql}, {_storageSrid})";

        // MySQL ST_Distance returns distance in the units of the spatial reference system
        // For geographic SRS (like 4326), use ST_Distance_Sphere for meters
        if (srid == 4326 || _storageSrid == 4326)
        {
            return $"ST_Distance_Sphere({geomColumn}, {projectedGeometry})";
        }

        return $"ST_Distance({geomColumn}, {projectedGeometry})";
    }

    private string TranslateGeoLength(QueryFunctionExpression expression, string alias, IDictionary<string, object?> parameters)
    {
        if (expression.Arguments.Count != 1)
        {
            throw new NotSupportedException("geo.length requires exactly one argument.");
        }

        if (expression.Arguments[0] is not QueryFieldReference field)
        {
            throw new NotSupportedException("geo.length requires a field reference argument.");
        }

        var geomColumn = $"{alias}.{QuoteIdentifier(field.Name)}";

        // MySQL ST_Length returns length in the units of the spatial reference system
        // For geographic coordinates (SRID 4326), the result is in degrees
        // Note: MySQL doesn't have ST_Length_Sphere, so geographic length is limited
        return $"ST_Length({geomColumn})";
    }

    private static bool TryExtractFieldAndGeometry(QueryExpression candidateField, QueryExpression candidateGeometry, out string fieldName, out QueryGeometryValue geometry)
    {
        fieldName = string.Empty;
        geometry = null!;

        if (candidateField is QueryFieldReference field && candidateGeometry is QueryConstant { Value: QueryGeometryValue geometryValue })
        {
            fieldName = field.Name;
            geometry = geometryValue;
            return true;
        }

        return false;
    }

    private int ResolveQuerySrid(FeatureQuery query)
    {
        if (query.Crs.HasValue())
        {
            return CrsHelper.ParseCrs(query.Crs);
        }

        if (_layer.Storage?.Srid is int storageSrid)
        {
            return storageSrid;
        }

        return _storageSrid;
    }

    private string? BuildEnvelopeSql(string wkt, int srid, IDictionary<string, object?> parameters)
    {
        try
        {
            var reader = new WKTReader();
            var geometry = reader.Read(wkt);
            var envelope = geometry.EnvelopeInternal;
            if (envelope is null || envelope.IsNull)
            {
                return null;
            }

            var minX = AddSpatialParameter(parameters, envelope.MinX);
            var minY = AddSpatialParameter(parameters, envelope.MinY);
            var maxX = AddSpatialParameter(parameters, envelope.MaxX);
            var maxY = AddSpatialParameter(parameters, envelope.MaxY);

            var envelopeSql = $"ST_MakeEnvelope({minX}, {minY}, {maxX}, {maxY}, {srid})";
            return srid == _storageSrid ? envelopeSql : $"ST_Transform({envelopeSql}, {_storageSrid})";
        }
        catch
        {
            return null;
        }
    }

    private static string AddSpatialParameter(IDictionary<string, object?> parameters, object? value)
    {
        var key = $"filter_spatial_{parameters.Count}";
        parameters[key] = value;
        return $"@{key}";
    }

    private void AppendOrderBy(StringBuilder sql, FeatureQuery query, string alias)
    {
        var defaultSort = PaginationHelper.GetDefaultOrderByColumn(_layer);
        PaginationHelper.BuildOrderByClause(sql, query.SortOrders, alias, QuoteIdentifier, defaultSort);
    }

    private void AppendPagination(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters)
    {
        PaginationHelper.BuildOffsetLimitClause(
            sql,
            query.Offset,
            query.Limit,
            parameters,
            PaginationHelper.DatabaseVendor.MySQL,
            "@");
    }

    private string GetTableExpression()
    {
        return LayerMetadataHelper.GetTableExpression(_layer, QuoteIdentifier);
    }

    private string GetPrimaryKeyColumn() => LayerMetadataHelper.GetPrimaryKeyColumn(_layer);

    private string GetGeometryColumn() => LayerMetadataHelper.GetGeometryColumn(_layer);

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
            if (column.IsNullOrWhiteSpace())
            {
                return;
            }

            if (seen.Add(column))
            {
                columns.Add(column);
            }
        }

        Add(GetGeometryColumn());
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

    private object NormalizeKeyValue(string featureId)
    {
        return LayerMetadataHelper.NormalizeKeyValue(featureId, _layer);
    }

    private string BuildAggregateExpression(StatisticDefinition statistic, string alias)
    {
        return AggregateExpressionBuilder.Build(statistic, alias, QuoteIdentifier);
    }

    internal static string QuoteIdentifier(string identifier)
    {
        // Validate identifier for SQL injection protection before quoting
        return SqlIdentifierValidator.ValidateAndQuoteMySql(identifier);
    }

    private static string QuoteAlias(string alias) => SqlIdentifierValidator.ValidateAndQuoteMySql(alias);

    private string DetermineDefaultCrs()
    {
        if (_service.Ogc.DefaultCrs.HasValue())
        {
            return _service.Ogc.DefaultCrs!;
        }

        if (_layer.Crs.Count > 0)
        {
            return _layer.Crs[0];
        }

        return "EPSG:4326";
    }
}

internal sealed record MySqlQueryDefinition(string Sql, IReadOnlyDictionary<string, object?> Parameters);

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
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

public sealed class PostgresFeatureQueryBuilder
{
    internal const string GeoJsonColumnAlias = "__geom_geojson";

    private readonly ServiceDefinition _service;
    private readonly LayerDefinition _layer;
    private readonly int _storageSrid;
    private readonly int _targetSrid;

    public PostgresFeatureQueryBuilder(ServiceDefinition service, LayerDefinition layer, int storageSrid, int targetSrid)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _storageSrid = storageSrid;
        _targetSrid = targetSrid;
    }

    internal PostgresQueryDefinition BuildSelect(FeatureQuery query)
    {
        Guard.NotNull(query);

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

        return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    internal PostgresQueryDefinition BuildCount(FeatureQuery query)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("select count(*) from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    internal PostgresQueryDefinition BuildById(string featureId)
    {
        Guard.NotNullOrWhiteSpace(featureId);

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

        return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    private object NormalizeKeyValue(string featureId)
    {
        return LayerMetadataHelper.NormalizeKeyValue(featureId, _layer);
    }

    // Note: TranslateFunction handles spatial functions for OData and CQL2
    private string? TranslateFunction(QueryFunctionExpression expression, string alias, FeatureQuery query, IDictionary<string, object?> parameters)
    {
        var functionName = expression.Name.ToLowerInvariant();
        if (!functionName.StartsWith("geo.", StringComparison.Ordinal))
        {
            return null;
        }

        // Handle geo.distance and geo.length (measurement functions returning numbers)
        if (functionName == "geo.distance")
        {
            return TranslateGeoDistance(expression, alias, query, parameters);
        }

        if (functionName == "geo.length")
        {
            return TranslateGeoLength(expression, alias, parameters);
        }

        // Handle boolean predicate functions (geo.intersects, etc.)
        if (expression.Arguments.Count != 2)
        {
            throw new NotSupportedException($"{expression.Name} requires exactly two arguments.");
        }

        if (!TryExtractFieldAndGeometry(expression.Arguments[0], expression.Arguments[1], out var fieldName, out var geometry) &&
            !TryExtractFieldAndGeometry(expression.Arguments[1], expression.Arguments[0], out fieldName, out geometry))
        {
            throw new NotSupportedException($"{expression.Name} requires a field reference and a geometry literal.");
        }

        var geomColumn = $"{alias}.{QuoteIdentifier(fieldName)}";
        var srid = geometry.Srid ?? ResolveQuerySrid(query);
        var geometryParam = AddSpatialParameter(parameters, geometry.WellKnownText);
        var geometrySql = $"ST_GeomFromText({geometryParam}, {srid})";
        var projectedGeometry = srid == _storageSrid ? geometrySql : $"ST_Transform({geometrySql}, {_storageSrid})";

        var spatialOperator = functionName switch
        {
            "geo.intersects" => "ST_Intersects",
            "geo.contains" => "ST_Contains",
            "geo.within" => "ST_Within",
            "geo.crosses" => "ST_Crosses",
            "geo.overlaps" => "ST_Overlaps",
            "geo.touches" => "ST_Touches",
            "geo.disjoint" => "ST_Disjoint",
            "geo.equals" => "ST_Equals",
            _ => throw new NotSupportedException($"Spatial function '{expression.Name}' is not supported.")
        };

        // Use bounding box optimization for intersects (most common)
        if (functionName == "geo.intersects")
        {
            var envelopeSql = BuildEnvelopeSql(geometry.WellKnownText, srid, parameters);
            if (envelopeSql is not null)
            {
                return $"({geomColumn} && {envelopeSql}) AND {spatialOperator}({geomColumn}, {projectedGeometry})";
            }
        }

        return $"{spatialOperator}({geomColumn}, {projectedGeometry})";
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

        // ST_Distance returns distance in meters for geography, units of the SRS for geometry
        // For geographic coordinates (SRID 4326), use geography cast for meters
        if (srid == 4326 || _storageSrid == 4326)
        {
            return $"ST_Distance({geomColumn}::geography, {projectedGeometry}::geography)";
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

        // ST_Length returns length in meters for geography, units of the SRS for geometry
        // For geographic coordinates (SRID 4326), use geography cast for meters
        if (_storageSrid == 4326)
        {
            return $"ST_Length({geomColumn}::geography)";
        }

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

        predicates.Add($"{geomColumn} && {filterEnvelope}");
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
        var querySrid = ResolveQuerySrid(query);
        var spatialTranslator = new PostgresSpatialFilterTranslator(_storageSrid, querySrid, QuoteIdentifier, parameters, alias);

        var translator = new SqlFilterTranslator(
            entityDefinition,
            parameters,
            QuoteIdentifier,
            "filter",
            (func, funcAlias) => TranslateFunction(func, funcAlias, query, parameters),
            (spatial, spatialAlias) => spatialTranslator.Translate(spatial));

        var predicate = translator.Translate(query.Filter, alias);

        if (predicate.HasValue())
        {
            predicates.Add(predicate);
        }
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
            PaginationHelper.DatabaseVendor.PostgreSQL,
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

    /// <summary>
    /// Builds SQL for statistical aggregations using GROUP BY.
    /// CRITICAL PERFORMANCE OPTIMIZATION - uses SQL aggregation instead of loading records into memory.
    /// </summary>
    internal PostgresQueryDefinition BuildStatistics(
        FeatureQuery query,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields)
    {
        Guard.NotNull(query);
        Guard.NotNull(statistics);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("select ");

        // Add GROUP BY fields first
        if (groupByFields is { Count: > 0 })
        {
            sql.Append(string.Join(", ", groupByFields.Select(f => $"{alias}.{QuoteIdentifier(f)}")));
            sql.Append(", ");
        }

        // Add aggregate functions
        var aggregates = new List<string>();
        foreach (var stat in statistics)
        {
            var aggregate = AggregateExpressionBuilder.Build(stat, alias, QuoteIdentifier);
            aggregates.Add(aggregate);
        }

        sql.Append(string.Join(", ", aggregates));
        sql.Append(" from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        // Add GROUP BY clause
        if (groupByFields is { Count: > 0 })
        {
            sql.Append(" group by ");
            sql.Append(string.Join(", ", groupByFields.Select(f => $"{alias}.{QuoteIdentifier(f)}")));
        }

        // Add HAVING clause if specified
        if (query.HavingClause.HasValue())
        {
            sql.Append(" having ");
            sql.Append(query.HavingClause);
        }

        return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    /// <summary>
    /// Builds SQL for distinct values using SELECT DISTINCT.
    /// CRITICAL PERFORMANCE OPTIMIZATION - uses SQL DISTINCT instead of loading records into memory.
    /// </summary>
    internal PostgresQueryDefinition BuildDistinct(
        FeatureQuery query,
        IReadOnlyList<string> fieldNames)
    {
        Guard.NotNull(query);
        Guard.NotNull(fieldNames);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("select distinct ");
        sql.Append(string.Join(", ", fieldNames.Select(f => $"{alias}.{QuoteIdentifier(f)}")));
        sql.Append(" from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        // Apply limit and offset from query, with safety cap of 10000 rows
        const int MaxDistinctLimit = 10000;
        var effectiveLimit = query.Limit.HasValue
            ? Math.Min(query.Limit.Value, MaxDistinctLimit)
            : MaxDistinctLimit;

        var paginatedQuery = query with { Limit = effectiveLimit };
        AppendPagination(sql, paginatedQuery, parameters);

        return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    /// <summary>
    /// Builds SQL for spatial extent calculation using PostGIS ST_Extent.
    /// CRITICAL PERFORMANCE OPTIMIZATION - uses SQL aggregation instead of loading geometries into memory.
    /// </summary>
    internal PostgresQueryDefinition BuildExtent(FeatureQuery query, int targetSrid)
    {
        Guard.NotNull(query);

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        var geometryColumn = GetGeometryColumn();
        var geomRef = $"{alias}.{QuoteIdentifier(geometryColumn)}";

        // Transform to target SRID if needed
        if (targetSrid != _storageSrid)
        {
            geomRef = $"ST_Transform({geomRef}, {targetSrid})";
        }

        sql.Append($"select ST_Extent({geomRef})::text from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        return new PostgresQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    private static string QuoteIdentifier(string identifier)
    {
        // Validate identifier for SQL injection protection before quoting
        return SqlIdentifierValidator.ValidateAndQuotePostgres(identifier);
    }

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

public sealed record PostgresQueryDefinition(string Sql, IReadOnlyDictionary<string, object?> Parameters);




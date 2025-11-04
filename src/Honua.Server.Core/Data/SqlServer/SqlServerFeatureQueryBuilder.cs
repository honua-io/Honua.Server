// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Security;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.SqlServer;

internal sealed class SqlServerFeatureQueryBuilder
{
    internal const string GeometryWktAlias = "__geom_wkt";
    internal const string GeometrySridAlias = "__geom_srid";

    private readonly ServiceDefinition _service;
    private readonly LayerDefinition _layer;
    private readonly int _storageSrid;
    private readonly bool _isGeography;

    private int _parameterIndex;

    public SqlServerFeatureQueryBuilder(ServiceDefinition service, LayerDefinition layer, int storageSrid, int targetSrid, bool isGeography)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _storageSrid = storageSrid;
        _isGeography = isGeography;

        // targetSrid parameter accepted for API compatibility but not currently used
        _ = targetSrid;
    }

    public SqlServerQueryDefinition BuildSelect(FeatureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _parameterIndex = 0;

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

        return new SqlServerQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public SqlServerQueryDefinition BuildCount(FeatureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _parameterIndex = 0;

        var sql = new StringBuilder();
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        const string alias = "t";

        sql.Append("select count(*) from ");
        sql.Append(GetTableExpression());
        sql.Append(' ');
        sql.Append(alias);

        AppendWhereClause(sql, query, parameters, alias);

        return new SqlServerQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
    }

    public SqlServerQueryDefinition BuildById(string featureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureId);
        _parameterIndex = 0;

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
        sql.Append(" = @feature_id");

        parameters["@feature_id"] = NormalizeKeyValue(featureId);

        return new SqlServerQueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(parameters));
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

        AddGeometryProjections(segments, alias);

        return string.Join(", ", segments);
    }

    private void AddGeometryProjections(ICollection<string> segments, string alias)
    {
        var column = $"{alias}.{QuoteIdentifier(GetGeometryColumn())}";
        segments.Add($"CASE WHEN {column} IS NULL THEN NULL ELSE {column}.STAsText() END AS [{GeometryWktAlias}]");
        segments.Add($"CASE WHEN {column} IS NULL THEN NULL ELSE {column}.STSrid END AS [{GeometrySridAlias}]");
    }

    private void AppendWhereClause(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters, string alias)
    {
        var predicates = new List<string>();
        AppendBoundingBoxPredicate(query, predicates, parameters, alias);
        AppendTemporalPredicate(query, predicates, parameters, alias);
        AppendFilterPredicate(query, predicates, parameters, alias);
        AppendKeysetPredicate(query, predicates, parameters, alias);

        if (predicates.Count == 0)
        {
            return;
        }

        sql.Append(" where ");
        sql.Append(string.Join(" and ", predicates));
    }

    private void AppendKeysetPredicate(FeatureQuery query, ICollection<string> predicates, IDictionary<string, object?> parameters, string alias)
    {
        if (string.IsNullOrEmpty(query.Cursor))
        {
            return;
        }

        var keysetClause = Query.KeysetPaginationHelper.BuildKeysetWhereClause(
            query,
            parameters,
            alias,
            identifierQuoter: QuoteIdentifier,
            parameterAdder: (name, value) => AddParameter(parameters, name, value),
            defaultPrimaryKey: GetPrimaryKeyColumn());

        if (!string.IsNullOrEmpty(keysetClause))
        {
            predicates.Add(keysetClause);
        }
    }

    private void AppendBoundingBoxPredicate(FeatureQuery query, ICollection<string> predicates, IDictionary<string, object?> parameters, string alias)
    {
        if (query.Bbox is null)
        {
            return;
        }

        var bbox = query.Bbox;
        var bboxCrs = bbox.Crs ?? query.Crs ?? DetermineDefaultCrs();
        var bboxSrid = CrsHelper.ParseCrs(bboxCrs);

        double minX = bbox.MinX;
        double minY = bbox.MinY;
        double maxX = bbox.MaxX;
        double maxY = bbox.MaxY;

        if (_storageSrid != 0 && bboxSrid != _storageSrid)
        {
            var transformed = CrsTransform.TransformEnvelope(minX, minY, maxX, maxY, bboxSrid, _storageSrid);
            minX = transformed.MinX;
            minY = transformed.MinY;
            maxX = transformed.MaxX;
            maxY = transformed.MaxY;
            bboxSrid = _storageSrid;
        }

        var wkt = BuildEnvelopeWkt(minX, minY, maxX, maxY);
        var wktParam = AddParameter(parameters, "bbox_wkt", wkt);
        var sridParam = AddParameter(parameters, "bbox_srid", bboxSrid);

        var factory = _isGeography ? "geography::STGeomFromText" : "geometry::STGeomFromText";
        var geomColumn = $"{alias}.{QuoteIdentifier(GetGeometryColumn())}";
        predicates.Add($"{geomColumn}.STIntersects({factory}({wktParam}, {sridParam})) = 1");
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
            var startParam = AddParameter(parameters, "datetime_start", query.Temporal.Start.Value.UtcDateTime);
            predicates.Add($"{column} >= {startParam}");
        }

        if (query.Temporal.End is not null)
        {
            var endParam = AddParameter(parameters, "datetime_end", query.Temporal.End.Value.UtcDateTime);
            predicates.Add($"{column} <= {endParam}");
        }
    }

    private void AppendFilterPredicate(FeatureQuery query, ICollection<string> predicates, IDictionary<string, object?> parameters, string alias)
    {
        if (query.Filter?.Expression is null)
        {
            return;
        }

        var entityDefinition = query.EntityDefinition;
        if (entityDefinition is null)
        {
            return;
        }

        var translator = new SqlFilterTranslator(
            entityDefinition,
            parameters,
            QuoteIdentifier,
            "filter",
            (function, currentAlias) => TranslateFunction(function, currentAlias, query, parameters));

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
        var index = ++_parameterIndex;
        var geometryParam = AddParameter(parameters, $"geom_wkt_{index}", geometry.WellKnownText);
        var sridParam = AddParameter(parameters, $"geom_srid_{index}", srid);
        var factory = _isGeography ? "geography::STGeomFromText" : "geometry::STGeomFromText";
        return $"{geomColumn}.STIntersects({factory}({geometryParam}, {sridParam})) = 1";
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
        var index = ++_parameterIndex;
        var geometryParam = AddParameter(parameters, $"geom_wkt_{index}", geometry.WellKnownText);
        var sridParam = AddParameter(parameters, $"geom_srid_{index}", srid);
        var factory = _isGeography ? "geography::STGeomFromText" : "geometry::STGeomFromText";

        // For geography type (SRID 4326), STDistance returns meters
        // For geometry type with projected CRS, STDistance returns units of the CRS
        return $"{geomColumn}.STDistance({factory}({geometryParam}, {sridParam}))";
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

        // For geography type (SRID 4326), STLength returns meters
        // For geometry type with projected CRS, STLength returns units of the CRS
        return $"{geomColumn}.STLength()";
    }

    private static bool TryExtractFieldAndGeometry(
        QueryExpression candidateField,
        QueryExpression candidateGeometry,
        out string fieldName,
        out QueryGeometryValue geometry)
    {
        fieldName = string.Empty;
        geometry = null!;

        if (candidateField is QueryFieldReference field &&
            candidateGeometry is QueryConstant { Value: QueryGeometryValue geometryValue })
        {
            fieldName = field.Name;
            geometry = geometryValue;
            return true;
        }

        return false;
    }

    private void AppendOrderBy(StringBuilder sql, FeatureQuery query, string alias)
    {
        var defaultSort = PaginationHelper.GetDefaultOrderByColumn(_layer);
        PaginationHelper.BuildOrderByClause(sql, query.SortOrders, alias, QuoteIdentifier, defaultSort);
    }

    private void AppendPagination(StringBuilder sql, FeatureQuery query, IDictionary<string, object?> parameters)
    {
        var hasCursor = !string.IsNullOrEmpty(query.Cursor);

        // Prefer keyset (cursor-based) pagination for O(1) performance
        // Fall back to OFFSET only for backward compatibility
        if (hasCursor)
        {
            // Keyset pagination: Build WHERE clause from cursor values
            // This provides constant-time O(1) performance vs O(N) for OFFSET
            // The cursor contains the last seen values of sort columns

            // Note: Keyset WHERE clause should be added in WHERE section, not here
            // This method only adds LIMIT clause
            if (query.Limit.HasValue)
            {
                var limitParam = AddParameter(parameters, "limit", query.Limit!.Value);
                sql.Append(" offset 0 rows fetch next ");
                sql.Append(limitParam);
                sql.Append(" rows only");
            }
            return;
        }

        // Legacy OFFSET pagination (deprecated) - Use PaginationHelper for standard syntax
        PaginationHelper.BuildOffsetLimitClause(
            sql,
            query.Offset,
            query.Limit,
            parameters,
            PaginationHelper.DatabaseVendor.SqlServer,
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

        return query.PropertyNames;
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

    private static string BuildEnvelopeWkt(double minX, double minY, double maxX, double maxY)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
            minX,
            minY,
            maxX,
            maxY);
    }

    internal static string QuoteIdentifier(string identifier)
    {
        // Validate identifier for SQL injection protection before quoting
        return SqlIdentifierValidator.ValidateAndQuoteSqlServer(identifier);
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

    private string AddParameter(IDictionary<string, object?> parameters, string baseName, object? value)
    {
        var name = $"@{baseName}";
        if (parameters.ContainsKey(name))
        {
            do
            {
                name = $"@{baseName}_{++_parameterIndex}";
            }
            while (parameters.ContainsKey(name));
        }

        parameters[name] = value ?? DBNull.Value;
        return name;
    }

    private object NormalizeKeyValue(string featureId)
    {
        return LayerMetadataHelper.NormalizeKeyValue(featureId, _layer);
    }
}

internal sealed record SqlServerQueryDefinition(string Sql, IReadOnlyDictionary<string, object?> Parameters);



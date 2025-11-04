// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;

namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Translates spatial filter expressions to PostgreSQL/PostGIS SQL.
/// </summary>
internal sealed class PostgresSpatialFilterTranslator
{
    private readonly int _storageSrid;
    private readonly int _querySrid;
    private readonly Func<string, string> _quoteIdentifier;
    private readonly IDictionary<string, object?> _parameters;
    private readonly string _alias;

    public PostgresSpatialFilterTranslator(
        int storageSrid,
        int querySrid,
        Func<string, string> quoteIdentifier,
        IDictionary<string, object?> parameters,
        string alias)
    {
        _storageSrid = storageSrid;
        _querySrid = querySrid;
        _quoteIdentifier = quoteIdentifier ?? throw new ArgumentNullException(nameof(quoteIdentifier));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _alias = alias ?? throw new ArgumentNullException(nameof(alias));
    }

    public string Translate(QuerySpatialExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Extract field name
        if (expression.GeometryProperty is not QueryFieldReference field)
        {
            throw new NotSupportedException("Spatial expressions require a field reference for the geometry property.");
        }

        // Extract geometry value
        if (expression.TestGeometry is not QueryConstant { Value: QueryGeometryValue geometryValue })
        {
            throw new NotSupportedException("Spatial expressions require a geometry constant for the test geometry.");
        }

        var geomColumn = $"{_alias}.{_quoteIdentifier(field.Name)}";
        var srid = geometryValue.Srid ?? _querySrid;
        var wkt = SpatialFilterTranslator.NormalizeGeometryLiteral(geometryValue.WellKnownText);
        var geometryParam = SpatialFilterTranslator.AddParameter(_parameters, "filter_spatial", wkt);
        var geometrySql = $"ST_GeomFromText({geometryParam}, {srid})";
        var projectedGeometry = srid == _storageSrid
            ? geometrySql
            : $"ST_Transform({geometrySql}, {_storageSrid})";

        // Handle DWithin specially
        if (expression.Predicate == SpatialPredicate.DWithin)
        {
            if (!expression.Distance.HasValue)
            {
                throw new InvalidOperationException("DWithin predicate requires a distance value.");
            }

            var distanceParam = SpatialFilterTranslator.AddParameter(_parameters, "filter_param", expression.Distance.Value);

            // For geographic coordinates (SRID 4326), use geography cast for accurate distance in meters
            if (srid == 4326 || _storageSrid == 4326)
            {
                return $"ST_DWithin({geomColumn}::geography, {projectedGeometry}::geography, {distanceParam})";
            }

            return $"ST_DWithin({geomColumn}, {projectedGeometry}, {distanceParam})";
        }

        // Map spatial predicate to PostGIS function using base utility
        var spatialOperator = SpatialFilterTranslator.GetSpatialPredicateName(expression.Predicate, "ST_");

        // Use bounding box optimization for Intersects (most common operation)
        if (expression.Predicate == SpatialPredicate.Intersects)
        {
            var envelopeSql = BuildEnvelopeSql(wkt, srid);
            if (envelopeSql is not null)
            {
                // The && operator uses spatial index for fast bounding box intersection
                // followed by actual ST_Intersects for precise result
                return $"({geomColumn} && {envelopeSql}) AND {spatialOperator}({geomColumn}, {projectedGeometry})";
            }
        }

        return $"{spatialOperator}({geomColumn}, {projectedGeometry})";
    }

    private string? BuildEnvelopeSql(string wkt, int srid)
    {
        var envelope = SpatialFilterTranslator.ExtractEnvelope(wkt);
        if (envelope is null)
        {
            return null;
        }

        var minX = SpatialFilterTranslator.AddParameter(_parameters, "filter_param", envelope.MinX);
        var minY = SpatialFilterTranslator.AddParameter(_parameters, "filter_param", envelope.MinY);
        var maxX = SpatialFilterTranslator.AddParameter(_parameters, "filter_param", envelope.MaxX);
        var maxY = SpatialFilterTranslator.AddParameter(_parameters, "filter_param", envelope.MaxY);

        var envelopeSql = $"ST_MakeEnvelope({minX}, {minY}, {maxX}, {maxY}, {srid})";
        return srid == _storageSrid
            ? envelopeSql
            : $"ST_Transform({envelopeSql}, {_storageSrid})";
    }
}

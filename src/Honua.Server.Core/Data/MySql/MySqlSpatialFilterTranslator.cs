// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;

namespace Honua.Server.Core.Data.MySql;

/// <summary>
/// Translates spatial filter expressions to MySQL spatial SQL.
/// </summary>
internal sealed class MySqlSpatialFilterTranslator
{
    private readonly int _storageSrid;
    private readonly Func<string, string> _quoteIdentifier;
    private readonly IDictionary<string, object?> _parameters;
    private readonly string _alias;

    public MySqlSpatialFilterTranslator(
        int storageSrid,
        Func<string, string> quoteIdentifier,
        IDictionary<string, object?> parameters,
        string alias)
    {
        _storageSrid = storageSrid;
        _quoteIdentifier = quoteIdentifier ?? throw new ArgumentNullException(nameof(quoteIdentifier));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _alias = alias ?? throw new ArgumentNullException(nameof(alias));
    }

    public string Translate(QuerySpatialExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (expression.GeometryProperty is not QueryFieldReference field)
        {
            throw new NotSupportedException("Spatial expressions require a field reference for the geometry property.");
        }

        if (expression.TestGeometry is not QueryConstant { Value: QueryGeometryValue geometryValue })
        {
            throw new NotSupportedException("Spatial expressions require a geometry constant for the test geometry.");
        }

        var geomColumn = $"{_alias}.{_quoteIdentifier(field.Name)}";
        var wkt = SpatialFilterTranslator.NormalizeGeometryLiteral(geometryValue.WellKnownText);
        var geometryParam = SpatialFilterTranslator.AddParameter(_parameters, "filter_spatial", wkt);
        var srid = geometryValue.Srid ?? _storageSrid;
        var geometrySql = $"ST_GeomFromText({geometryParam}, {srid})";

        // Handle DWithin specially
        if (expression.Predicate == SpatialPredicate.DWithin)
        {
            if (!expression.Distance.HasValue)
            {
                throw new InvalidOperationException("DWithin predicate requires a distance value.");
            }

            var distanceParam = SpatialFilterTranslator.AddParameter(_parameters, "filter_param", expression.Distance.Value);
            // MySQL ST_Distance_Sphere for geographic distance in meters
            if (srid == 4326 || _storageSrid == 4326)
            {
                return $"ST_Distance_Sphere({geomColumn}, {geometrySql}) <= {distanceParam}";
            }

            return $"ST_Distance({geomColumn}, {geometrySql}) <= {distanceParam}";
        }

        // Map spatial predicate to MySQL function using base utility
        var spatialFunction = SpatialFilterTranslator.GetSpatialPredicateName(expression.Predicate, "ST_");

        return $"{spatialFunction}({geomColumn}, {geometrySql})";
    }
}

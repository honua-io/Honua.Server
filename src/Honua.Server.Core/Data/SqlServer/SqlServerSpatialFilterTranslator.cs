// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;

namespace Honua.Server.Core.Data.SqlServer;

/// <summary>
/// Translates spatial filter expressions to SQL Server spatial SQL.
/// </summary>
internal sealed class SqlServerSpatialFilterTranslator
{
    private readonly int _storageSrid;
    private readonly bool _isGeography;
    private readonly Func<string, string> _quoteIdentifier;
    private readonly IDictionary<string, object?> _parameters;
    private readonly string _alias;

    public SqlServerSpatialFilterTranslator(
        int storageSrid,
        bool isGeography,
        Func<string, string> quoteIdentifier,
        IDictionary<string, object?> parameters,
        string alias)
    {
        _storageSrid = storageSrid;
        _isGeography = isGeography;
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

        // SQL Server spatial methods
        var geometryType = _isGeography ? "geography" : "geometry";
        var geometrySql = $"{geometryType}::STGeomFromText({geometryParam}, {srid})";

        // Handle DWithin specially
        if (expression.Predicate == SpatialPredicate.DWithin)
        {
            if (!expression.Distance.HasValue)
            {
                throw new InvalidOperationException("DWithin predicate requires a distance value.");
            }

            var distanceParam = SpatialFilterTranslator.AddParameter(_parameters, "filter_param", expression.Distance.Value);
            // STDistance returns distance, we check if it's within threshold
            return $"{geomColumn}.STDistance({geometrySql}) <= {distanceParam}";
        }

        // Map spatial predicate to SQL Server method using base utility (no prefix, Pascal case)
        var spatialMethod = SpatialFilterTranslator.GetSpatialPredicateName(expression.Predicate, "ST");

        // SQL Server spatial methods return 1 for true, 0 for false
        return $"{geomColumn}.{spatialMethod}({geometrySql}) = 1";
    }
}

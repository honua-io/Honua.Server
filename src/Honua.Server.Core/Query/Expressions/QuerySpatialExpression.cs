// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query.Expressions;

/// <summary>
/// Represents a spatial predicate expression for WFS filters.
/// </summary>
public sealed class QuerySpatialExpression : QueryExpression
{
    public QuerySpatialExpression(SpatialPredicate predicate, QueryExpression geometryProperty, QueryExpression testGeometry, double? distance = null)
    {
        Predicate = predicate;
        GeometryProperty = geometryProperty ?? throw new ArgumentNullException(nameof(geometryProperty));
        TestGeometry = testGeometry ?? throw new ArgumentNullException(nameof(testGeometry));
        Distance = distance;

        if (predicate == SpatialPredicate.DWithin && distance is null)
        {
            throw new ArgumentException("Distance is required for DWithin predicate", nameof(distance));
        }
    }

    public SpatialPredicate Predicate { get; }
    public QueryExpression GeometryProperty { get; }
    public QueryExpression TestGeometry { get; }
    public double? Distance { get; }
}

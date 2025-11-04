// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using NetTopologySuite.Geometries;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Geoservices.GeometryService;

public interface IGeometryOperationExecutor
{
    IReadOnlyList<Geometry> Project(GeometryProjectOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> Buffer(GeometryBufferOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> Simplify(GeometrySimplifyOperation operation, CancellationToken cancellationToken = default);
    Geometry? Union(GeometrySetOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> Intersect(GeometryPairwiseOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> Difference(GeometryPairwiseOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> ConvexHull(GeometrySetOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<double> Distance(GeometryDistanceOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<double> Areas(GeometryMeasurementOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<double> Lengths(GeometryMeasurementOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> LabelPoints(GeometryLabelPointsOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> Cut(GeometryCutOperation operation, CancellationToken cancellationToken = default);
    Geometry Reshape(GeometryReshapeOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> Offset(GeometryOffsetOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> TrimExtend(GeometryTrimExtendOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> Densify(GeometryDensifyOperation operation, CancellationToken cancellationToken = default);
    IReadOnlyList<Geometry> Generalize(GeometryGeneralizeOperation operation, CancellationToken cancellationToken = default);
}

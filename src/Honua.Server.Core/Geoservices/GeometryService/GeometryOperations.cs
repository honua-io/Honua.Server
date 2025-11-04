// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using NetTopologySuite.Geometries;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Geoservices.GeometryService;

public sealed record GeometryBufferOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Geometries,
    double Distance,
    string Unit,
    bool UnionResults);

public sealed record GeometrySimplifyOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Geometries);

public sealed record GeometrySetOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Geometries);

public sealed record GeometryPairwiseOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Geometries1,
    IReadOnlyList<Geometry> Geometries2);

public sealed record GeometryDistanceOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Geometries1,
    IReadOnlyList<Geometry> Geometries2,
    string? DistanceUnit,
    bool Geodesic);

public sealed record GeometryMeasurementOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Polygon> Polygons,
    string? AreaUnit,
    string? LengthUnit);

public sealed record GeometryLabelPointsOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Geometries);

public sealed record GeometryCutOperation(
    string GeometryType,
    int SpatialReference,
    Geometry Target,
    Geometry Cutter);

public sealed record GeometryReshapeOperation(
    string GeometryType,
    int SpatialReference,
    Geometry Target,
    Geometry Reshaper);

public sealed record GeometryOffsetOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Geometries,
    double OffsetDistance,
    string OffsetHow,
    double BevelRatio);

public sealed record GeometryTrimExtendOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Polylines,
    Geometry TrimExtendTo,
    int ExtendHow);

public sealed record GeometryDensifyOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Geometries,
    double MaxSegmentLength);

public sealed record GeometryGeneralizeOperation(
    string GeometryType,
    int SpatialReference,
    IReadOnlyList<Geometry> Geometries,
    double MaxDeviation);

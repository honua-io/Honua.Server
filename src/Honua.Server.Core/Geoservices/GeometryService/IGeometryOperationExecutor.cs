// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Threading;
using NetTopologySuite.Geometries;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Geoservices.GeometryService;

/// <summary>
/// Executes spatial geometry operations for the ArcGIS Geoservices REST API.
/// Provides GIS analysis capabilities including projection, buffering, topological operations, and measurements.
/// </summary>
/// <remarks>
/// This interface implements the geometry service operations defined in the ArcGIS REST API specification.
/// All operations work with NetTopologySuite geometry objects and support batch processing for efficiency.
///
/// Common operation types:
/// - **Projection**: Transform geometries between coordinate systems
/// - **Buffer**: Create areas around geometries at specified distances
/// - **Topological**: Union, intersect, difference, convex hull operations
/// - **Measurement**: Calculate areas, lengths, and distances
/// - **Editing**: Cut, reshape, offset, trim/extend operations
/// - **Simplification**: Generalize and densify geometry vertices
/// </remarks>
public interface IGeometryOperationExecutor
{
    /// <summary>
    /// Projects geometries from one spatial reference to another.
    /// </summary>
    /// <param name="operation">The projection operation parameters including source/target spatial references.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of geometries projected to the target spatial reference.</returns>
    IReadOnlyList<Geometry> Project(GeometryProjectOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates buffer polygons around input geometries at specified distances.
    /// </summary>
    /// <param name="operation">The buffer operation parameters including distance and units.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of buffer polygons.</returns>
    IReadOnlyList<Geometry> Buffer(GeometryBufferOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simplifies geometries by reducing vertex count while preserving shape.
    /// </summary>
    /// <param name="operation">The simplification operation parameters including tolerance.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of simplified geometries.</returns>
    IReadOnlyList<Geometry> Simplify(GeometrySimplifyOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the geometric union of input geometries into a single geometry.
    /// </summary>
    /// <param name="operation">The union operation parameters.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The union geometry, or null if the input is empty.</returns>
    Geometry? Union(GeometrySetOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the geometric intersection of geometry pairs.
    /// </summary>
    /// <param name="operation">The intersection operation parameters with geometry pairs.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of intersection geometries.</returns>
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

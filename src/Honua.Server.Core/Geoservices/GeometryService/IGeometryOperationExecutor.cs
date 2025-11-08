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

    /// <summary>
    /// Computes the geometric difference (subtraction) of geometry pairs.
    /// Returns the part of the first geometry that does not intersect with the second geometry.
    /// </summary>
    /// <param name="operation">The difference operation parameters with geometry pairs.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of difference geometries (first minus second).</returns>
    IReadOnlyList<Geometry> Difference(GeometryPairwiseOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the convex hull of each geometry in the input set.
    /// The convex hull is the smallest convex polygon that contains the geometry.
    /// </summary>
    /// <param name="operation">The convex hull operation parameters with geometries.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of convex hull geometries (always polygons or points).</returns>
    IReadOnlyList<Geometry> ConvexHull(GeometrySetOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the minimum distance between geometry pairs.
    /// Distance is measured in the units of the spatial reference system.
    /// </summary>
    /// <param name="operation">The distance operation parameters with geometry pairs.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of distances (0 if geometries intersect, positive otherwise).</returns>
    IReadOnlyList<double> Distance(GeometryDistanceOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the planar area of each polygon geometry.
    /// Area is measured in square units of the spatial reference system.
    /// Returns 0 for non-polygon geometries (points, lines).
    /// </summary>
    /// <param name="operation">The area measurement operation parameters with geometries.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of areas in square units (always non-negative).</returns>
    IReadOnlyList<double> Areas(GeometryMeasurementOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the planar length of each line or polygon boundary.
    /// Length is measured in linear units of the spatial reference system.
    /// For polygons, returns the perimeter. Returns 0 for points.
    /// </summary>
    /// <param name="operation">The length measurement operation parameters with geometries.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of lengths in linear units (always non-negative).</returns>
    IReadOnlyList<double> Lengths(GeometryMeasurementOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes representative label points for each polygon geometry.
    /// The label point is guaranteed to be inside the polygon and suitable for placing text labels.
    /// </summary>
    /// <param name="operation">The label points operation parameters with polygon geometries.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of point geometries representing optimal label positions.</returns>
    IReadOnlyList<Geometry> LabelPoints(GeometryLabelPointsOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuts each polyline or polygon geometry at the locations where it intersects a cutter polyline.
    /// Returns the resulting split geometries as separate features.
    /// </summary>
    /// <param name="operation">The cut operation parameters with geometries and cutter polyline.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of geometries resulting from the cut operation.</returns>
    IReadOnlyList<Geometry> Cut(GeometryCutOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reshapes a polyline or polygon by replacing part of it with a reshape polyline.
    /// The reshape line must intersect the geometry at exactly two locations to define the replacement segment.
    /// </summary>
    /// <param name="operation">The reshape operation parameters with geometry and reshape polyline.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The reshaped geometry, or original geometry if reshape is invalid.</returns>
    Geometry Reshape(GeometryReshapeOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates offset geometries at a specified distance from the input geometries.
    /// Positive offset expands polygons and offsets lines to the right; negative offsets to the left.
    /// </summary>
    /// <param name="operation">The offset operation parameters with geometries and offset distance.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of offset geometries parallel to the input geometries.</returns>
    IReadOnlyList<Geometry> Offset(GeometryOffsetOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trims or extends polyline geometries to the first intersection with an extend-to polyline.
    /// Used for editing operations to snap lines to other features.
    /// </summary>
    /// <param name="operation">The trim/extend operation parameters with polylines and extend-to line.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of trimmed or extended polyline geometries.</returns>
    IReadOnlyList<Geometry> TrimExtend(GeometryTrimExtendOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Densifies geometries by adding vertices along line segments to create smoother curves.
    /// Vertices are added at regular intervals not exceeding the specified maximum segment length.
    /// </summary>
    /// <param name="operation">The densify operation parameters with geometries and max segment length.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of densified geometries with additional vertices.</returns>
    IReadOnlyList<Geometry> Densify(GeometryDensifyOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generalizes (simplifies) geometries by removing vertices while preserving overall shape.
    /// Uses Douglas-Peucker algorithm with the specified maximum deviation tolerance.
    /// </summary>
    /// <param name="operation">The generalize operation parameters with geometries and deviation tolerance.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of generalized geometries with fewer vertices.</returns>
    IReadOnlyList<Geometry> Generalize(GeometryGeneralizeOperation operation, CancellationToken cancellationToken = default);
}

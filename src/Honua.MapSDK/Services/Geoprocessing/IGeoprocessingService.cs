// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Honua.MapSDK.Models.Geoprocessing;

namespace Honua.MapSDK.Services.Geoprocessing;

/// <summary>
/// Interface for client-side geoprocessing operations using Turf.js
/// Provides spatial analysis and geometric operations directly in the browser
/// </summary>
public interface IGeoprocessingService
{
    // ========== Geometric Operations ==========

    /// <summary>
    /// Creates a buffer around input features at a specified distance
    /// </summary>
    /// <param name="input">Input GeoJSON feature or geometry</param>
    /// <param name="distance">Buffer distance</param>
    /// <param name="units">Distance units (meters, kilometers, miles, etc.)</param>
    /// <returns>Buffered geometry as GeoJSON</returns>
    Task<object> BufferAsync(object input, double distance, string units = "meters");

    /// <summary>
    /// Finds the geometric intersection of two layers
    /// </summary>
    /// <param name="layer1">First GeoJSON layer</param>
    /// <param name="layer2">Second GeoJSON layer</param>
    /// <returns>Intersection result as GeoJSON</returns>
    Task<object> IntersectAsync(object layer1, object layer2);

    /// <summary>
    /// Combines multiple features into a single geometry
    /// </summary>
    /// <param name="layers">Array of GeoJSON features to union</param>
    /// <returns>Unioned geometry as GeoJSON</returns>
    Task<object> UnionAsync(object[] layers);

    /// <summary>
    /// Removes areas of layer2 from layer1
    /// </summary>
    /// <param name="layer1">Base layer (GeoJSON)</param>
    /// <param name="layer2">Layer to subtract (GeoJSON)</param>
    /// <returns>Difference result as GeoJSON</returns>
    Task<object> DifferenceAsync(object layer1, object layer2);

    /// <summary>
    /// Clips features to a clipping boundary
    /// </summary>
    /// <param name="clip">Clipping boundary (GeoJSON polygon)</param>
    /// <param name="subject">Features to clip (GeoJSON)</param>
    /// <returns>Clipped features as GeoJSON</returns>
    Task<object> ClipAsync(object clip, object subject);

    /// <summary>
    /// Simplifies a geometry by reducing the number of vertices
    /// </summary>
    /// <param name="geometry">Input geometry (GeoJSON)</param>
    /// <param name="tolerance">Simplification tolerance</param>
    /// <param name="highQuality">Use higher quality but slower algorithm</param>
    /// <returns>Simplified geometry as GeoJSON</returns>
    Task<object> SimplifyAsync(object geometry, double tolerance = 0.01, bool highQuality = false);

    // ========== Measurements ==========

    /// <summary>
    /// Calculates the area of a polygon
    /// </summary>
    /// <param name="polygon">Input polygon (GeoJSON)</param>
    /// <param name="units">Area units (meters, kilometers, miles, hectares, etc.)</param>
    /// <returns>Area in specified units</returns>
    Task<double> AreaAsync(object polygon, string units = "meters");

    /// <summary>
    /// Calculates the length of a line
    /// </summary>
    /// <param name="line">Input line (GeoJSON LineString)</param>
    /// <param name="units">Length units (meters, kilometers, miles, etc.)</param>
    /// <returns>Length in specified units</returns>
    Task<double> LengthAsync(object line, string units = "meters");

    /// <summary>
    /// Calculates the distance between two points
    /// </summary>
    /// <param name="point1">First point</param>
    /// <param name="point2">Second point</param>
    /// <param name="units">Distance units (meters, kilometers, miles, etc.)</param>
    /// <returns>Distance in specified units</returns>
    Task<double> DistanceAsync(Coordinate point1, Coordinate point2, string units = "meters");

    /// <summary>
    /// Calculates the perimeter of a polygon
    /// </summary>
    /// <param name="polygon">Input polygon (GeoJSON)</param>
    /// <param name="units">Length units (meters, kilometers, miles, etc.)</param>
    /// <returns>Perimeter in specified units</returns>
    Task<double> PerimeterAsync(object polygon, string units = "meters");

    // ========== Spatial Relationships ==========

    /// <summary>
    /// Tests if container geometry contains the contained geometry
    /// </summary>
    /// <param name="container">Container geometry (GeoJSON)</param>
    /// <param name="contained">Geometry to test (GeoJSON)</param>
    /// <returns>True if container contains the geometry</returns>
    Task<bool> ContainsAsync(object container, object contained);

    /// <summary>
    /// Tests if two geometries intersect
    /// </summary>
    /// <param name="geometry1">First geometry (GeoJSON)</param>
    /// <param name="geometry2">Second geometry (GeoJSON)</param>
    /// <returns>True if geometries intersect</returns>
    Task<bool> IntersectsAsync(object geometry1, object geometry2);

    /// <summary>
    /// Tests if inner geometry is within outer geometry
    /// </summary>
    /// <param name="inner">Inner geometry (GeoJSON)</param>
    /// <param name="outer">Outer geometry (GeoJSON)</param>
    /// <returns>True if inner is within outer</returns>
    Task<bool> WithinAsync(object inner, object outer);

    /// <summary>
    /// Tests if two geometries overlap
    /// </summary>
    /// <param name="geometry1">First geometry (GeoJSON)</param>
    /// <param name="geometry2">Second geometry (GeoJSON)</param>
    /// <returns>True if geometries overlap</returns>
    Task<bool> OverlapsAsync(object geometry1, object geometry2);

    // ========== Geometric Calculations ==========

    /// <summary>
    /// Calculates the centroid of a feature
    /// </summary>
    /// <param name="geometry">Input geometry (GeoJSON)</param>
    /// <returns>Centroid as GeoJSON Point</returns>
    Task<object> CentroidAsync(object geometry);

    /// <summary>
    /// Calculates the convex hull of features
    /// </summary>
    /// <param name="points">Input points or geometries (GeoJSON)</param>
    /// <returns>Convex hull as GeoJSON Polygon</returns>
    Task<object> ConvexHullAsync(object points);

    /// <summary>
    /// Calculates the bounding box of a feature
    /// </summary>
    /// <param name="geometry">Input geometry (GeoJSON)</param>
    /// <returns>Bounding box as [minLng, minLat, maxLng, maxLat]</returns>
    Task<double[]> BboxAsync(object geometry);

    /// <summary>
    /// Calculates the envelope (bounding box polygon) of a feature
    /// </summary>
    /// <param name="geometry">Input geometry (GeoJSON)</param>
    /// <returns>Envelope as GeoJSON Polygon</returns>
    Task<object> EnvelopeAsync(object geometry);

    // ========== Advanced Operations ==========

    /// <summary>
    /// Creates Voronoi polygons from a set of points
    /// </summary>
    /// <param name="points">Input points (GeoJSON FeatureCollection)</param>
    /// <param name="bbox">Optional bounding box for Voronoi diagram</param>
    /// <returns>Voronoi polygons as GeoJSON FeatureCollection</returns>
    Task<object> VoronoiAsync(object points, double[]? bbox = null);

    /// <summary>
    /// Dissolves features based on a property
    /// </summary>
    /// <param name="features">Input features (GeoJSON FeatureCollection)</param>
    /// <param name="propertyName">Property name to dissolve by</param>
    /// <returns>Dissolved features as GeoJSON FeatureCollection</returns>
    Task<object> DissolveAsync(object features, string? propertyName = null);

    /// <summary>
    /// Transforms coordinates of a feature
    /// </summary>
    /// <param name="geometry">Input geometry (GeoJSON)</param>
    /// <param name="transformFunction">JavaScript transform function as string</param>
    /// <returns>Transformed geometry as GeoJSON</returns>
    Task<object> TransformAsync(object geometry, string transformFunction);

    // ========== Batch Operations ==========

    /// <summary>
    /// Executes a geoprocessing operation and returns detailed results
    /// </summary>
    /// <param name="operationType">Type of operation to perform</param>
    /// <param name="parameters">Operation parameters</param>
    /// <returns>Detailed geoprocessing result</returns>
    Task<GeoprocessingResult> ExecuteOperationAsync(
        GeoprocessingOperationType operationType,
        GeoprocessingParameters parameters);
}

/// <summary>
/// Parameters for geoprocessing operations
/// </summary>
public class GeoprocessingParameters
{
    /// <summary>
    /// Primary input geometry or feature
    /// </summary>
    public object? Input { get; set; }

    /// <summary>
    /// Secondary input (for operations like intersect, difference)
    /// </summary>
    public object? SecondaryInput { get; set; }

    /// <summary>
    /// Array of inputs (for operations like union)
    /// </summary>
    public object[]? MultipleInputs { get; set; }

    /// <summary>
    /// Distance parameter (for buffer)
    /// </summary>
    public double? Distance { get; set; }

    /// <summary>
    /// Units for measurements
    /// </summary>
    public string Units { get; set; } = "meters";

    /// <summary>
    /// First coordinate (for distance calculations)
    /// </summary>
    public Coordinate? Point1 { get; set; }

    /// <summary>
    /// Second coordinate (for distance calculations)
    /// </summary>
    public Coordinate? Point2 { get; set; }

    /// <summary>
    /// Tolerance parameter (for simplification)
    /// </summary>
    public double? Tolerance { get; set; }

    /// <summary>
    /// High quality flag (for simplification)
    /// </summary>
    public bool HighQuality { get; set; } = false;

    /// <summary>
    /// Property name (for dissolve)
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// Bounding box (for voronoi)
    /// </summary>
    public double[]? BoundingBox { get; set; }

    /// <summary>
    /// Transform function (for coordinate transformations)
    /// </summary>
    public string? TransformFunction { get; set; }

    /// <summary>
    /// Additional options
    /// </summary>
    public GeoprocessingOptions Options { get; set; } = new();
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.MapSDK.Models.Geoprocessing;

/// <summary>
/// Represents the result of a geoprocessing operation
/// </summary>
public class GeoprocessingResult
{
    /// <summary>
    /// Unique identifier for the operation
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Type of geoprocessing operation performed
    /// </summary>
    public GeoprocessingOperationType OperationType { get; set; }

    /// <summary>
    /// Input parameters used for the operation
    /// </summary>
    public Dictionary<string, object> InputParameters { get; set; } = new();

    /// <summary>
    /// Result geometry as GeoJSON
    /// </summary>
    public object? ResultGeometry { get; set; }

    /// <summary>
    /// Numeric result (for measurements like area, length, distance)
    /// </summary>
    public double? NumericResult { get; set; }

    /// <summary>
    /// Boolean result (for spatial relationship queries)
    /// </summary>
    public bool? BooleanResult { get; set; }

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code for programmatic error handling
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// When the operation was executed
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata about the operation
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Number of features processed
    /// </summary>
    public int? FeatureCount { get; set; }

    /// <summary>
    /// Units used for measurements (meters, kilometers, miles, etc.)
    /// </summary>
    public string? Units { get; set; }
}

/// <summary>
/// Types of geoprocessing operations
/// </summary>
public enum GeoprocessingOperationType
{
    /// <summary>
    /// Buffer operation - creates a polygon at a fixed distance around features
    /// </summary>
    Buffer,

    /// <summary>
    /// Intersection - finds the geometric intersection of two layers
    /// </summary>
    Intersect,

    /// <summary>
    /// Union - combines multiple features into one
    /// </summary>
    Union,

    /// <summary>
    /// Difference - removes areas of one layer from another
    /// </summary>
    Difference,

    /// <summary>
    /// Clip - cuts features to a clipping boundary
    /// </summary>
    Clip,

    /// <summary>
    /// Dissolve - merges adjacent features with common attributes
    /// </summary>
    Dissolve,

    /// <summary>
    /// Area measurement - calculates polygon area
    /// </summary>
    Area,

    /// <summary>
    /// Length measurement - calculates line length
    /// </summary>
    Length,

    /// <summary>
    /// Distance measurement - calculates distance between two points
    /// </summary>
    Distance,

    /// <summary>
    /// Centroid - finds the center point of a feature
    /// </summary>
    Centroid,

    /// <summary>
    /// Convex hull - creates the smallest convex polygon containing all points
    /// </summary>
    ConvexHull,

    /// <summary>
    /// Contains - tests if one feature contains another
    /// </summary>
    Contains,

    /// <summary>
    /// Intersects - tests if two features intersect
    /// </summary>
    Intersects,

    /// <summary>
    /// Within - tests if one feature is within another
    /// </summary>
    Within,

    /// <summary>
    /// Voronoi - creates voronoi polygons from points
    /// </summary>
    Voronoi,

    /// <summary>
    /// Simplify - reduces the complexity of a geometry
    /// </summary>
    Simplify,

    /// <summary>
    /// Transform - applies coordinate transformations
    /// </summary>
    Transform
}

/// <summary>
/// Options for geoprocessing operations
/// </summary>
public class GeoprocessingOptions
{
    /// <summary>
    /// Units for distance/area measurements
    /// </summary>
    public string Units { get; set; } = "meters";

    /// <summary>
    /// Number of decimal places for numeric results
    /// </summary>
    public int Precision { get; set; } = 2;

    /// <summary>
    /// Whether to simplify output geometries
    /// </summary>
    public bool SimplifyOutput { get; set; } = false;

    /// <summary>
    /// Tolerance for simplification
    /// </summary>
    public double SimplifyTolerance { get; set; } = 0.01;

    /// <summary>
    /// Whether to validate geometries before processing
    /// </summary>
    public bool ValidateGeometry { get; set; } = true;

    /// <summary>
    /// Maximum number of features to process in a single operation
    /// </summary>
    public int MaxFeatures { get; set; } = 10000;

    /// <summary>
    /// Timeout in seconds for long-running operations
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Coordinate pair for point operations
/// </summary>
public class Coordinate
{
    /// <summary>
    /// Longitude
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Latitude
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Creates a coordinate array [longitude, latitude]
    /// </summary>
    public double[] ToArray() => new[] { Longitude, Latitude };

    /// <summary>
    /// Creates a coordinate from an array
    /// </summary>
    public static Coordinate FromArray(double[] coords)
    {
        if (coords == null || coords.Length < 2)
        {
            throw new ArgumentException("Coordinate array must have at least 2 elements");
        }

        return new Coordinate
        {
            Longitude = coords[0],
            Latitude = coords[1]
        };
    }
}

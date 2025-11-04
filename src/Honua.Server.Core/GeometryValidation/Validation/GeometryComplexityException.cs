// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.GeometryValidation;

/// <summary>
/// Exception thrown when a geometry exceeds configured complexity limits.
/// Used to prevent DoS attacks via resource exhaustion.
/// </summary>
public sealed class GeometryComplexityException : ArgumentException
{
    /// <summary>
    /// Gets the error code identifying which limit was exceeded.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the actual value that exceeded the limit.
    /// </summary>
    public long ActualValue { get; }

    /// <summary>
    /// Gets the maximum allowed value.
    /// </summary>
    public long MaxValue { get; }

    /// <summary>
    /// Gets a suggested simplification technique to reduce complexity.
    /// </summary>
    public string? Suggestion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeometryComplexityException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code identifying which limit was exceeded.</param>
    /// <param name="actualValue">The actual value that exceeded the limit.</param>
    /// <param name="maxValue">The maximum allowed value.</param>
    /// <param name="suggestion">Optional suggestion for simplification.</param>
    public GeometryComplexityException(
        string message,
        string errorCode,
        long actualValue,
        long maxValue,
        string? suggestion = null)
        : base(message)
    {
        ErrorCode = errorCode;
        ActualValue = actualValue;
        MaxValue = maxValue;
        Suggestion = suggestion;
    }

    /// <summary>
    /// Creates an exception for vertex count limit exceeded.
    /// </summary>
    public static GeometryComplexityException VertexCountExceeded(int actualCount, int maxCount)
    {
        return new GeometryComplexityException(
            $"Geometry has {actualCount:N0} vertices, exceeding maximum of {maxCount:N0}",
            "VERTEX_COUNT_EXCEEDED",
            actualCount,
            maxCount,
            "Consider using ST_Simplify() to reduce the number of vertices, or split the geometry into smaller parts");
    }

    /// <summary>
    /// Creates an exception for ring count limit exceeded.
    /// </summary>
    public static GeometryComplexityException RingCountExceeded(int actualCount, int maxCount)
    {
        return new GeometryComplexityException(
            $"Polygon has {actualCount:N0} rings, exceeding maximum of {maxCount:N0}",
            "RING_COUNT_EXCEEDED",
            actualCount,
            maxCount,
            "Consider splitting the polygon into multiple geometries, or removing unnecessary holes");
    }

    /// <summary>
    /// Creates an exception for nesting depth limit exceeded.
    /// </summary>
    public static GeometryComplexityException NestingDepthExceeded(int actualDepth, int maxDepth)
    {
        return new GeometryComplexityException(
            $"Geometry collection has nesting depth of {actualDepth}, exceeding maximum of {maxDepth}",
            "NESTING_DEPTH_EXCEEDED",
            actualDepth,
            maxDepth,
            "Flatten the geometry collection structure to reduce nesting");
    }

    /// <summary>
    /// Creates an exception for coordinate precision limit exceeded.
    /// </summary>
    public static GeometryComplexityException CoordinatePrecisionExceeded(int actualPrecision, int maxPrecision)
    {
        return new GeometryComplexityException(
            $"Coordinate has precision of {actualPrecision} decimal places, exceeding maximum of {maxPrecision}",
            "COORDINATE_PRECISION_EXCEEDED",
            actualPrecision,
            maxPrecision,
            $"Round coordinates to {maxPrecision} decimal places or fewer");
    }

    /// <summary>
    /// Creates an exception for geometry size limit exceeded.
    /// </summary>
    public static GeometryComplexityException GeometrySizeExceeded(int actualSize, int maxSize)
    {
        return new GeometryComplexityException(
            $"Geometry size is {actualSize:N0} bytes, exceeding maximum of {maxSize:N0} bytes",
            "GEOMETRY_SIZE_EXCEEDED",
            actualSize,
            maxSize,
            "Simplify the geometry or reduce coordinate precision to decrease size");
    }

    /// <summary>
    /// Creates an exception for coordinate count limit exceeded.
    /// </summary>
    public static GeometryComplexityException CoordinateCountExceeded(int actualCount, int maxCount)
    {
        return new GeometryComplexityException(
            $"Geometry has {actualCount:N0} coordinates, exceeding maximum of {maxCount:N0}",
            "COORDINATE_COUNT_EXCEEDED",
            actualCount,
            maxCount,
            "Simplify the geometry to reduce the number of coordinates");
    }
}

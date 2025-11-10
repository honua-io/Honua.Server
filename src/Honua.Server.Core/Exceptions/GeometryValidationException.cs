// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for geometry validation errors.
/// </summary>
public class GeometryValidationException : HonuaException
{
    public GeometryValidationException(string message) : base(message, "GEOMETRY_VALIDATION_FAILED")
    {
    }

    public GeometryValidationException(string message, Exception innerException) : base(message, "GEOMETRY_VALIDATION_FAILED", innerException)
    {
    }

    public GeometryValidationException(string message, string? errorCode) : base(message, errorCode)
    {
    }

    public GeometryValidationException(string message, string? errorCode, Exception innerException) : base(message, errorCode, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a geometry is invalid according to OGC standards.
/// This is NOT a transient error.
/// </summary>
public sealed class InvalidGeometryException : GeometryValidationException
{
    public string? GeometryType { get; }
    public string? ValidationError { get; }

    public InvalidGeometryException(string message)
        : base(message, "INVALID_GEOMETRY")
    {
    }

    public InvalidGeometryException(string geometryType, string validationError)
        : base($"Invalid {geometryType}: {validationError}", "INVALID_GEOMETRY")
    {
        GeometryType = geometryType;
        ValidationError = validationError;
    }

    public InvalidGeometryException(string message, Exception innerException)
        : base(message, "INVALID_GEOMETRY", innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a geometry has invalid or unsupported spatial reference.
/// This is NOT a transient error.
/// </summary>
public sealed class InvalidSpatialReferenceException : GeometryValidationException
{
    public int? Srid { get; }

    public InvalidSpatialReferenceException(string message)
        : base(message, "INVALID_SPATIAL_REFERENCE")
    {
    }

    public InvalidSpatialReferenceException(int srid, string message)
        : base(message, "INVALID_SPATIAL_REFERENCE")
    {
        Srid = srid;
    }
}

/// <summary>
/// Exception thrown when geometry coordinates are out of valid bounds.
/// This is NOT a transient error.
/// </summary>
public sealed class GeometryOutOfBoundsException : GeometryValidationException
{
    public double? Latitude { get; }
    public double? Longitude { get; }

    public GeometryOutOfBoundsException(string message)
        : base(message, "GEOMETRY_OUT_OF_BOUNDS")
    {
    }

    public GeometryOutOfBoundsException(double latitude, double longitude)
        : base($"Coordinates ({latitude}, {longitude}) are out of valid bounds", "GEOMETRY_OUT_OF_BOUNDS")
    {
        Latitude = latitude;
        Longitude = longitude;
    }
}

/// <summary>
/// Exception thrown when a geometry type is not supported for an operation.
/// This is NOT a transient error.
/// </summary>
public sealed class UnsupportedGeometryTypeException : GeometryValidationException
{
    public string? GeometryType { get; }
    public string? Operation { get; }

    public UnsupportedGeometryTypeException(string geometryType, string operation)
        : base($"Geometry type '{geometryType}' is not supported for operation '{operation}'", "UNSUPPORTED_GEOMETRY_TYPE")
    {
        GeometryType = geometryType;
        Operation = operation;
    }
}

/// <summary>
/// Exception thrown when geometry transformation fails.
/// This may be a transient error if caused by external service issues.
/// </summary>
public sealed class GeometryTransformationException : GeometryValidationException
{
    public int? SourceSrid { get; }
    public int? TargetSrid { get; }

    public GeometryTransformationException(string message)
        : base(message, "GEOMETRY_TRANSFORMATION_FAILED")
    {
    }

    public GeometryTransformationException(int sourceSrid, int targetSrid, string message, Exception innerException)
        : base($"Failed to transform geometry from SRID {sourceSrid} to {targetSrid}: {message}", "GEOMETRY_TRANSFORMATION_FAILED", innerException)
    {
        SourceSrid = sourceSrid;
        TargetSrid = targetSrid;
    }
}

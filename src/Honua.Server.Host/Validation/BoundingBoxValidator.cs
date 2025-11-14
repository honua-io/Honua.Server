// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Validation;

/// <summary>
/// Validates bounding box coordinates for OGC and STAC API endpoints.
/// SECURITY: Prevents invalid coordinate ranges that could cause processing errors or exploits.
/// </summary>
/// <remarks>
/// This validator ensures that:
/// - minX is less than maxX
/// - minY is less than maxY
/// - Values are within valid range for the coordinate reference system
/// - Valid coordinate count (4 for 2D, 6 for 3D with altitude/depth)
/// - No NaN or Infinity values
/// - Coordinates are within reasonable Earth bounds
/// </remarks>
public static class BoundingBoxValidator
{
    /// <summary>
    /// Validates a 2D bounding box (4 coordinates: minX, minY, maxX, maxY).
    /// </summary>
    /// <param name="bbox">Array of bounding box coordinates.</param>
    /// <param name="srid">Optional SRID to validate coordinate ranges. Default is 4326 (WGS84).</param>
    /// <returns>True if valid; otherwise, false.</returns>
    public static bool IsValid2D(double[] bbox, int srid = 4326)
    {
        if (bbox == null || bbox.Length != 4)
        {
            return false;
        }

        return IsValidCore(bbox[0], bbox[1], bbox[2], bbox[3], srid);
    }

    /// <summary>
    /// Validates a 3D bounding box (6 coordinates: minX, minY, minZ, maxX, maxY, maxZ).
    /// </summary>
    /// <param name="bbox">Array of bounding box coordinates.</param>
    /// <param name="srid">Optional SRID to validate coordinate ranges. Default is 4326 (WGS84).</param>
    /// <returns>True if valid; otherwise, false.</returns>
    public static bool IsValid3D(double[] bbox, int srid = 4326)
    {
        if (bbox == null || bbox.Length != 6)
        {
            return false;
        }

        // Validate 2D portion first
        if (!IsValidCore(bbox[0], bbox[1], bbox[3], bbox[4], srid))
        {
            return false;
        }

        // Validate Z/altitude dimension
        var minZ = bbox[2];
        var maxZ = bbox[5];

        if (double.IsNaN(minZ) || double.IsNaN(maxZ) || double.IsInfinity(minZ) || double.IsInfinity(maxZ))
        {
            return false;
        }

        if (minZ > maxZ)
        {
            return false;
        }

        // Reasonable altitude bounds: -11,000m (Mariana Trench) to +9,000m (Mount Everest + atmosphere)
        if (minZ < ApiLimitsAndConstants.MinAltitudeMeters || maxZ > ApiLimitsAndConstants.MaxAltitudeMeters)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a bounding box with either 4 or 6 coordinates.
    /// </summary>
    /// <param name="bbox">Array of bounding box coordinates (4 for 2D, 6 for 3D).</param>
    /// <param name="srid">Optional SRID to validate coordinate ranges. Default is 4326 (WGS84).</param>
    /// <returns>True if valid; otherwise, false.</returns>
    public static bool IsValid(double[] bbox, int srid = 4326)
    {
        if (bbox == null)
        {
            return false;
        }

        return bbox.Length switch
        {
            4 => IsValid2D(bbox, srid),
            6 => IsValid3D(bbox, srid),
            _ => false
        };
    }

    /// <summary>
    /// Core validation logic for 2D bounding box.
    /// </summary>
    private static bool IsValidCore(double minX, double minY, double maxX, double maxY, int srid)
    {
        // Check for NaN or Infinity
        if (double.IsNaN(minX) || double.IsNaN(minY) || double.IsNaN(maxX) || double.IsNaN(maxY))
        {
            return false;
        }

        if (double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(maxX) || double.IsInfinity(maxY))
        {
            return false;
        }

        // Check min < max
        if (minX >= maxX || minY >= maxY)
        {
            return false;
        }

        // Validate against SRID if specified
        if (srid != 0)
        {
            // For geographic CRS (lat/lon)
            if (SridValidator.IsGeographic(srid))
            {
                // Longitude: -180 to 180
                if (minX < ApiLimitsAndConstants.MinLongitude || maxX > ApiLimitsAndConstants.MaxLongitude)
                {
                    return false;
                }

                // Latitude: -90 to 90
                if (minY < ApiLimitsAndConstants.MinLatitude || maxY > ApiLimitsAndConstants.MaxLatitude)
                {
                    return false;
                }
            }
            // For projected CRS
            else if (SridValidator.IsProjected(srid))
            {
                // Very generous bounds - Earth's circumference is ~40,000 km
                if (Math.Abs(minX) > ApiLimitsAndConstants.MaxProjectedExtentMeters || Math.Abs(maxX) > ApiLimitsAndConstants.MaxProjectedExtentMeters)
                {
                    return false;
                }

                if (Math.Abs(minY) > ApiLimitsAndConstants.MaxProjectedExtentMeters || Math.Abs(maxY) > ApiLimitsAndConstants.MaxProjectedExtentMeters)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a bounding box and throws an exception if invalid.
    /// </summary>
    /// <param name="bbox">Array of bounding box coordinates.</param>
    /// <param name="srid">Optional SRID to validate coordinate ranges. Default is 4326 (WGS84).</param>
    /// <param name="parameterName">The parameter name for error messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when bbox is null.</exception>
    /// <exception cref="ArgumentException">Thrown when bbox is invalid.</exception>
    public static void Validate(double[] bbox, int srid = 4326, string parameterName = "bbox")
    {
        if (bbox == null)
        {
            throw new ArgumentNullException(parameterName, "Bounding box cannot be null.");
        }

        if (bbox.Length != 4 && bbox.Length != 6)
        {
            throw new ArgumentException(
                $"Bounding box must contain 4 coordinates (2D) or 6 coordinates (3D), but got {bbox.Length}.",
                parameterName);
        }

        // Get coordinate indices based on dimension
        int minXIndex = 0, minYIndex = 1, maxXIndex, maxYIndex;
        double? minZ = null, maxZ = null;

        if (bbox.Length == 4)
        {
            maxXIndex = 2;
            maxYIndex = 3;
        }
        else // 6 coordinates
        {
            minZ = bbox[2];
            maxXIndex = 3;
            maxYIndex = 4;
            maxZ = bbox[5];
        }

        var minX = bbox[minXIndex];
        var minY = bbox[minYIndex];
        var maxX = bbox[maxXIndex];
        var maxY = bbox[maxYIndex];

        // Check for NaN or Infinity
        if (double.IsNaN(minX) || double.IsNaN(minY) || double.IsNaN(maxX) || double.IsNaN(maxY))
        {
            throw new ArgumentException("Bounding box coordinates cannot be NaN.", parameterName);
        }

        if (double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(maxX) || double.IsInfinity(maxY))
        {
            throw new ArgumentException("Bounding box coordinates cannot be infinity.", parameterName);
        }

        // Check min < max
        if (minX >= maxX)
        {
            throw new ArgumentException(
                $"Bounding box minX ({minX}) must be less than maxX ({maxX}).",
                parameterName);
        }

        if (minY >= maxY)
        {
            throw new ArgumentException(
                $"Bounding box minY ({minY}) must be less than maxY ({maxY}).",
                parameterName);
        }

        // Validate Z coordinates if present
        if (minZ.HasValue && maxZ.HasValue)
        {
            if (double.IsNaN(minZ.Value) || double.IsNaN(maxZ.Value))
            {
                throw new ArgumentException("Bounding box Z coordinates cannot be NaN.", parameterName);
            }

            if (double.IsInfinity(minZ.Value) || double.IsInfinity(maxZ.Value))
            {
                throw new ArgumentException("Bounding box Z coordinates cannot be infinity.", parameterName);
            }

            if (minZ.Value > maxZ.Value)
            {
                throw new ArgumentException(
                    $"Bounding box minZ ({minZ.Value}) must be less than or equal to maxZ ({maxZ.Value}).",
                    parameterName);
            }

            if (minZ.Value < ApiLimitsAndConstants.MinAltitudeMeters || maxZ.Value > ApiLimitsAndConstants.MaxAltitudeMeters)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Bounding box Z coordinates must be between {ApiLimitsAndConstants.MinAltitudeMeters}m and {ApiLimitsAndConstants.MaxAltitudeMeters}m.");
            }
        }

        // Validate against SRID
        if (srid != 0)
        {
            if (SridValidator.IsGeographic(srid))
            {
                if (minX < ApiLimitsAndConstants.MinLongitude || maxX > ApiLimitsAndConstants.MaxLongitude)
                {
                    throw new ArgumentOutOfRangeException(
                        parameterName,
                        $"For geographic CRS (SRID {srid}), longitude must be between {ApiLimitsAndConstants.MinLongitude} and {ApiLimitsAndConstants.MaxLongitude} degrees. Got minX={minX}, maxX={maxX}.");
                }

                if (minY < ApiLimitsAndConstants.MinLatitude || maxY > ApiLimitsAndConstants.MaxLatitude)
                {
                    throw new ArgumentOutOfRangeException(
                        parameterName,
                        $"For geographic CRS (SRID {srid}), latitude must be between {ApiLimitsAndConstants.MinLatitude} and {ApiLimitsAndConstants.MaxLatitude} degrees. Got minY={minY}, maxY={maxY}.");
                }
            }
            else if (SridValidator.IsProjected(srid))
            {
                if (Math.Abs(minX) > ApiLimitsAndConstants.MaxProjectedExtentMeters || Math.Abs(maxX) > ApiLimitsAndConstants.MaxProjectedExtentMeters ||
                    Math.Abs(minY) > ApiLimitsAndConstants.MaxProjectedExtentMeters || Math.Abs(maxY) > ApiLimitsAndConstants.MaxProjectedExtentMeters)
                {
                    throw new ArgumentOutOfRangeException(
                        parameterName,
                        $"For projected CRS (SRID {srid}), coordinates must be within ±{ApiLimitsAndConstants.MaxProjectedExtentMeters:N0} meters.");
                }
            }
        }
    }

    /// <summary>
    /// Tries to validate a bounding box.
    /// </summary>
    /// <param name="bbox">Array of bounding box coordinates.</param>
    /// <param name="srid">Optional SRID to validate coordinate ranges. Default is 4326 (WGS84).</param>
    /// <param name="errorMessage">The error message if validation fails.</param>
    /// <returns>True if valid; otherwise, false with an error message.</returns>
    public static bool TryValidate(double[] bbox, int srid, out string? errorMessage)
    {
        try
        {
            Validate(bbox, srid);
            errorMessage = null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentNullException or ArgumentOutOfRangeException)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Gets the area of a 2D bounding box.
    /// </summary>
    /// <param name="bbox">Array of bounding box coordinates (must be 4 or 6 elements).</param>
    /// <returns>The area in square units of the coordinate system.</returns>
    /// <remarks>
    /// For geographic coordinates (degrees), this returns area in square degrees.
    /// For projected coordinates (meters), this returns area in square meters.
    /// </remarks>
    public static double GetArea(double[] bbox)
    {
        Guard.NotNull(bbox);

        if (bbox.Length != 4 && bbox.Length != 6)
        {
            throw new ArgumentException("Bounding box must have 4 or 6 coordinates.", nameof(bbox));
        }

        var minX = bbox[0];
        var minY = bbox[1];
        var maxX = bbox.Length == 4 ? bbox[2] : bbox[3];
        var maxY = bbox.Length == 4 ? bbox[3] : bbox[4];

        var width = maxX - minX;
        var height = maxY - minY;

        return width * height;
    }

    /// <summary>
    /// Checks if a bounding box is too large for efficient processing.
    /// </summary>
    /// <param name="bbox">Array of bounding box coordinates.</param>
    /// <param name="srid">The SRID of the bounding box.</param>
    /// <param name="maxAreaSquareDegrees">Maximum allowed area in square degrees (for geographic CRS).</param>
    /// <returns>True if the bounding box exceeds reasonable size limits; otherwise, false.</returns>
    /// <remarks>
    /// Large bounding boxes can cause performance issues in spatial queries.
    /// This helps prevent denial-of-service through oversized queries.
    /// </remarks>
    public static bool IsTooLarge(double[] bbox, int srid = 4326, double maxAreaSquareDegrees = ApiLimitsAndConstants.MaxBoundingBoxAreaSquareDegrees)
    {
        if (!IsValid(bbox, srid))
        {
            return true; // Invalid bbox is considered "too large"
        }

        if (SridValidator.IsGeographic(srid))
        {
            var area = GetArea(bbox);
            return area > maxAreaSquareDegrees;
        }

        // For projected CRS, check if it spans more than reasonable distance
        var minX = bbox[0];
        var minY = bbox[1];
        var maxX = bbox.Length == 4 ? bbox[2] : bbox[3];
        var maxY = bbox.Length == 4 ? bbox[3] : bbox[4];

        var width = maxX - minX;
        var height = maxY - minY;

        // For projected coordinates, limit to reasonable Earth surface areas
        // Half of Earth's circumference: ~20,000 km
        return width > ApiLimitsAndConstants.MaxProjectedExtentMeters || height > ApiLimitsAndConstants.MaxProjectedExtentMeters;
    }
}

/// <summary>
/// Validation attribute for bounding box arrays.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidBoundingBoxAttribute : ValidationAttribute
{
    private readonly int srid;
    private readonly bool allow3D;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidBoundingBoxAttribute"/> class.
    /// </summary>
    /// <param name="srid">The SRID to use for validation (default is 4326 - WGS84).</param>
    /// <param name="allow3D">Whether to allow 3D bounding boxes with altitude. Default is true.</param>
    public ValidBoundingBoxAttribute(int srid = 4326, bool allow3D = true)
    {
        this.srid = srid;
        this.allow3D = allow3D;
    }

    protected override System.ComponentModel.DataAnnotations.ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return System.ComponentModel.DataAnnotations.ValidationResult.Success;
        }

        if (value is not double[] bbox)
        {
            return new System.ComponentModel.DataAnnotations.ValidationResult("Bounding box must be an array of doubles.");
        }

        if (!this.allow3D && bbox.Length == 6)
        {
            return new System.ComponentModel.DataAnnotations.ValidationResult("3D bounding boxes are not allowed for this parameter.");
        }

        if (!BoundingBoxValidator.TryValidate(bbox, this.srid, out var errorMessage))
        {
            return new System.ComponentModel.DataAnnotations.ValidationResult(errorMessage ?? "Invalid bounding box.");
        }

        return System.ComponentModel.DataAnnotations.ValidationResult.Success;
    }
}

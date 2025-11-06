// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Operation.Valid;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Core.Validation;

/// <summary>
/// Validates geometry constraints for data integrity and compatibility.
/// Ensures topological validity, coordinate validity, CRS compliance, and proper structure.
/// </summary>
public static class GeometryValidator
{
    /// <summary>
    /// Validation result containing success status, errors, and warnings.
    /// </summary>
    public sealed class ValidationResult
    {
        private ValidationResult(bool isValid, List<GeometryValidationError> errors, List<GeometryValidationWarning> warnings)
        {
            IsValid = isValid;
            Errors = errors;
            Warnings = warnings;
        }

        public bool IsValid { get; }
        public IReadOnlyList<GeometryValidationError> Errors { get; }
        public IReadOnlyList<GeometryValidationWarning> Warnings { get; }
        public string? ErrorMessage => Errors.Count > 0 ? string.Join("; ", Errors.Select(e => e.Message)) : null;

        public static ValidationResult Valid() => new(true, new List<GeometryValidationError>(), new List<GeometryValidationWarning>());
        public static ValidationResult ValidWithWarnings(List<GeometryValidationWarning> warnings) => new(true, new List<GeometryValidationError>(), warnings);
        public static ValidationResult Error(string message, string? errorCode = null, string? location = null)
        {
            var error = new GeometryValidationError
            {
                ErrorCode = errorCode ?? "VALIDATION_ERROR",
                Message = message,
                Location = location
            };
            return new(false, new List<GeometryValidationError> { error }, new List<GeometryValidationWarning>());
        }
        public static ValidationResult WithErrors(List<GeometryValidationError> errors, List<GeometryValidationWarning>? warnings = null)
            => new(false, errors, warnings ?? new List<GeometryValidationWarning>());
    }

    /// <summary>
    /// Represents a geometry validation error.
    /// </summary>
    public sealed class GeometryValidationError
    {
        public required string ErrorCode { get; init; }
        public required string Message { get; init; }
        public string? Location { get; init; }
    }

    /// <summary>
    /// Represents a geometry validation warning.
    /// </summary>
    public sealed class GeometryValidationWarning
    {
        public required string WarningCode { get; init; }
        public required string Message { get; init; }
        public string? Location { get; init; }
    }

    /// <summary>
    /// Options for geometry validation.
    /// </summary>
    public sealed class GeometryValidationOptions
    {
        public bool AllowEmpty { get; set; } = false;
        public bool AllowInvalid { get; set; } = false;
        public bool AutoRepair { get; set; } = true;
        public bool ValidateCoordinates { get; set; } = true;
        public bool CheckSelfIntersection { get; set; } = true;
        public int? TargetSrid { get; set; }
        public double MinX { get; set; } = -180.0;
        public double MaxX { get; set; } = 180.0;
        public double MinY { get; set; } = -90.0;
        public double MaxY { get; set; } = 90.0;
        public double? MinZ { get; set; }
        public double? MaxZ { get; set; }
        public int MaxCoordinates { get; set; } = 1_000_000;
    }

    /// <summary>
    /// Validates any geometry type with comprehensive checks.
    /// </summary>
    public static ValidationResult ValidateGeometry(Geometry? geometry, GeometryValidationOptions? options = null)
    {
        options ??= new GeometryValidationOptions();
        var errors = new List<GeometryValidationError>();
        var warnings = new List<GeometryValidationWarning>();

        // Null check
        if (geometry is null)
        {
            errors.Add(new GeometryValidationError
            {
                ErrorCode = "NULL_GEOMETRY",
                Message = "Geometry cannot be null"
            });
            return ValidationResult.WithErrors(errors);
        }

        // Empty geometry check
        if (geometry.IsEmpty)
        {
            if (!options.AllowEmpty)
            {
                errors.Add(new GeometryValidationError
                {
                    ErrorCode = "EMPTY_GEOMETRY",
                    Message = "Geometry is empty"
                });
                return ValidationResult.WithErrors(errors);
            }
            else
            {
                warnings.Add(new GeometryValidationWarning
                {
                    WarningCode = "EMPTY_GEOMETRY",
                    Message = "Geometry is empty but allowed by configuration"
                });
                return ValidationResult.ValidWithWarnings(warnings);
            }
        }

        // Coordinate validation
        if (options.ValidateCoordinates)
        {
            ValidateCoordinates(geometry, options, errors, warnings);
            if (errors.Count > 0)
            {
                return ValidationResult.WithErrors(errors, warnings);
            }
        }

        // Geometry count validation
        var coordCount = geometry.NumPoints;
        if (coordCount > options.MaxCoordinates)
        {
            errors.Add(new GeometryValidationError
            {
                ErrorCode = "TOO_MANY_COORDINATES",
                Message = $"Geometry has {coordCount} coordinates, exceeding maximum of {options.MaxCoordinates}"
            });
            return ValidationResult.WithErrors(errors, warnings);
        }

        // Type-specific validation
        var typeValidation = geometry switch
        {
            Point point => ValidatePoint(point),
            LinearRing linearRing => ValidateLinearRing(linearRing),
            LineString lineString => ValidateLineString(lineString),
            Polygon polygon => ValidatePolygon(polygon),
            MultiPoint multiPoint => ValidateMultiPoint(multiPoint),
            MultiLineString multiLineString => ValidateMultiLineString(multiLineString),
            MultiPolygon multiPolygon => ValidateMultiPolygon(multiPolygon),
            GeometryCollection geometryCollection => ValidateGeometryCollection(geometryCollection),
            _ => ValidationResult.Error($"Unsupported geometry type: {geometry.GeometryType}", "UNSUPPORTED_GEOMETRY_TYPE")
        };

        if (!typeValidation.IsValid)
        {
            errors.AddRange(typeValidation.Errors);
        }
        warnings.AddRange(typeValidation.Warnings);

        // Topological validation (if enabled and not already invalid)
        if (options.CheckSelfIntersection && errors.Count == 0 && !geometry.IsValid)
        {
            var validationError = GetValidationError(geometry);
            errors.Add(new GeometryValidationError
            {
                ErrorCode = "TOPOLOGY_ERROR",
                Message = $"Geometry has topological errors: {validationError}"
            });
        }

        return errors.Count > 0
            ? ValidationResult.WithErrors(errors, warnings)
            : warnings.Count > 0
                ? ValidationResult.ValidWithWarnings(warnings)
                : ValidationResult.Valid();
    }

    /// <summary>
    /// Validates coordinates for NaN, Infinity, and range issues.
    /// </summary>
    private static void ValidateCoordinates(
        Geometry geometry,
        GeometryValidationOptions options,
        List<GeometryValidationError> errors,
        List<GeometryValidationWarning> warnings)
    {
        var coordinates = geometry.Coordinates;
        for (int i = 0; i < coordinates.Length; i++)
        {
            var coord = coordinates[i];
            var location = $"Coordinate {i}";

            // Check for NaN
            if (double.IsNaN(coord.X) || double.IsNaN(coord.Y))
            {
                errors.Add(new GeometryValidationError
                {
                    ErrorCode = "NAN_COORDINATE",
                    Message = $"Coordinate contains NaN value: ({coord.X}, {coord.Y})",
                    Location = location
                });
                continue; // Skip further checks for this coordinate
            }

            // Check for Infinity
            if (double.IsInfinity(coord.X) || double.IsInfinity(coord.Y))
            {
                errors.Add(new GeometryValidationError
                {
                    ErrorCode = "INFINITE_COORDINATE",
                    Message = $"Coordinate contains infinite value: ({coord.X}, {coord.Y})",
                    Location = location
                });
                continue;
            }

            // Check X range
            if (coord.X < options.MinX || coord.X > options.MaxX)
            {
                errors.Add(new GeometryValidationError
                {
                    ErrorCode = "X_OUT_OF_RANGE",
                    Message = $"X coordinate {coord.X} is out of valid range [{options.MinX}, {options.MaxX}]",
                    Location = location
                });
            }

            // Check Y range
            if (coord.Y < options.MinY || coord.Y > options.MaxY)
            {
                errors.Add(new GeometryValidationError
                {
                    ErrorCode = "Y_OUT_OF_RANGE",
                    Message = $"Y coordinate {coord.Y} is out of valid range [{options.MinY}, {options.MaxY}]",
                    Location = location
                });
            }

            // Check Z if present
            if (!double.IsNaN(coord.Z))
            {
                if (double.IsInfinity(coord.Z))
                {
                    errors.Add(new GeometryValidationError
                    {
                        ErrorCode = "INFINITE_Z_COORDINATE",
                        Message = $"Z coordinate contains infinite value: {coord.Z}",
                        Location = location
                    });
                }
                else if (options.MinZ.HasValue && coord.Z < options.MinZ.Value)
                {
                    errors.Add(new GeometryValidationError
                    {
                        ErrorCode = "Z_OUT_OF_RANGE",
                        Message = $"Z coordinate {coord.Z} is below minimum {options.MinZ.Value}",
                        Location = location
                    });
                }
                else if (options.MaxZ.HasValue && coord.Z > options.MaxZ.Value)
                {
                    errors.Add(new GeometryValidationError
                    {
                        ErrorCode = "Z_OUT_OF_RANGE",
                        Message = $"Z coordinate {coord.Z} exceeds maximum {options.MaxZ.Value}",
                        Location = location
                    });
                }
            }
        }

        // Check for duplicate consecutive points (warning only)
        if (coordinates.Length > 1)
        {
            int duplicateCount = 0;
            for (int i = 1; i < coordinates.Length; i++)
            {
                if (coordinates[i].Equals2D(coordinates[i - 1]))
                {
                    duplicateCount++;
                }
            }
            if (duplicateCount > 0)
            {
                warnings.Add(new GeometryValidationWarning
                {
                    WarningCode = "DUPLICATE_CONSECUTIVE_POINTS",
                    Message = $"Found {duplicateCount} duplicate consecutive point(s)"
                });
            }
        }
    }

    /// <summary>
    /// Validates a Point geometry.
    /// </summary>
    public static ValidationResult ValidatePoint(Point? point)
    {
        if (point is null)
        {
            return ValidationResult.Error("Point cannot be null", "NULL_GEOMETRY");
        }

        if (point.IsEmpty)
        {
            return ValidationResult.Error("Point is empty", "EMPTY_GEOMETRY");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Validates a MultiPoint geometry.
    /// </summary>
    public static ValidationResult ValidateMultiPoint(MultiPoint? multiPoint)
    {
        if (multiPoint is null)
        {
            return ValidationResult.Error("MultiPoint cannot be null", "NULL_GEOMETRY");
        }

        if (multiPoint.NumGeometries == 0)
        {
            return ValidationResult.Error("MultiPoint must contain at least one point", "EMPTY_GEOMETRY");
        }

        for (int i = 0; i < multiPoint.NumGeometries; i++)
        {
            var point = (Point)multiPoint.GetGeometryN(i);
            var result = ValidatePoint(point);
            if (!result.IsValid)
            {
                return ValidationResult.Error($"Point {i} in MultiPoint is invalid: {result.ErrorMessage}", "INVALID_COMPONENT", $"Point {i}");
            }
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Validates a MultiLineString geometry.
    /// </summary>
    public static ValidationResult ValidateMultiLineString(MultiLineString? multiLineString)
    {
        if (multiLineString is null)
        {
            return ValidationResult.Error("MultiLineString cannot be null", "NULL_GEOMETRY");
        }

        if (multiLineString.NumGeometries == 0)
        {
            return ValidationResult.Error("MultiLineString must contain at least one line string", "EMPTY_GEOMETRY");
        }

        for (int i = 0; i < multiLineString.NumGeometries; i++)
        {
            var lineString = (LineString)multiLineString.GetGeometryN(i);
            var result = ValidateLineString(lineString);
            if (!result.IsValid)
            {
                return ValidationResult.Error($"LineString {i} in MultiLineString is invalid: {result.ErrorMessage}", "INVALID_COMPONENT", $"LineString {i}");
            }
        }

        if (!multiLineString.IsValid)
        {
            return ValidationResult.Error($"MultiLineString has topological errors: {GetValidationError(multiLineString)}", "TOPOLOGY_ERROR");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Validates a GeometryCollection.
    /// </summary>
    public static ValidationResult ValidateGeometryCollection(GeometryCollection? collection)
    {
        if (collection is null)
        {
            return ValidationResult.Error("GeometryCollection cannot be null", "NULL_GEOMETRY");
        }

        if (collection.NumGeometries == 0)
        {
            return ValidationResult.Error("GeometryCollection must contain at least one geometry", "EMPTY_GEOMETRY");
        }

        var errors = new List<GeometryValidationError>();
        var warnings = new List<GeometryValidationWarning>();

        for (int i = 0; i < collection.NumGeometries; i++)
        {
            var geom = collection.GetGeometryN(i);
            var options = new GeometryValidationOptions();
            var result = ValidateGeometry(geom, options);

            if (!result.IsValid)
            {
                errors.AddRange(result.Errors.Select(e => new GeometryValidationError
                {
                    ErrorCode = e.ErrorCode,
                    Message = e.Message,
                    Location = $"Geometry {i}" + (e.Location != null ? $" > {e.Location}" : "")
                }));
            }
            warnings.AddRange(result.Warnings);
        }

        return errors.Count > 0
            ? ValidationResult.WithErrors(errors, warnings)
            : warnings.Count > 0
                ? ValidationResult.ValidWithWarnings(warnings)
                : ValidationResult.Valid();
    }

    /// <summary>
    /// Repairs invalid geometry using NetTopologySuite operations.
    /// </summary>
    public static Geometry? RepairGeometry(Geometry? geometry)
    {
        if (geometry is null || geometry.IsValid)
        {
            return geometry;
        }

        try
        {
            // Strategy 1: Buffer by 0 (most common fix for self-intersections)
            var repaired = geometry.Buffer(0);
            if (repaired != null && repaired.IsValid && !repaired.IsEmpty)
            {
                repaired.SRID = geometry.SRID;
                return repaired;
            }

            // Strategy 2: Snap to grid to fix precision issues
            var snapped = geometry.Copy();
            snapped.Apply(new SnapToGridOperation(1e-9));
            if (snapped != null && snapped.IsValid && !snapped.IsEmpty)
            {
                snapped.SRID = geometry.SRID;
                return snapped;
            }

            // Strategy 3: For polygons, try fixing orientation
            if (geometry is Polygon polygon)
            {
                var oriented = EnsureCorrectOrientation(polygon);
                if (oriented.IsValid)
                {
                    return oriented;
                }
            }
            else if (geometry is MultiPolygon multiPolygon)
            {
                var polygons = new Polygon[multiPolygon.NumGeometries];
                for (int i = 0; i < multiPolygon.NumGeometries; i++)
                {
                    polygons[i] = EnsureCorrectOrientation((Polygon)multiPolygon.GetGeometryN(i));
                }
                var oriented = geometry.Factory.CreateMultiPolygon(polygons);
                oriented.SRID = geometry.SRID;
                if (oriented.IsValid)
                {
                    return oriented;
                }
            }

            // Unable to repair
            return null;
        }
        catch
        {
            // Repair failed
            return null;
        }
    }

    /// <summary>
    /// Operation to snap coordinates to a grid for precision fixes.
    /// </summary>
    private sealed class SnapToGridOperation : ICoordinateSequenceFilter
    {
        private readonly double _gridSize;

        public SnapToGridOperation(double gridSize)
        {
            _gridSize = gridSize;
        }

        public void Filter(CoordinateSequence coordSeq, int i)
        {
            var coord = coordSeq.GetCoordinate(i);
            coord.X = Math.Round(coord.X / _gridSize) * _gridSize;
            coord.Y = Math.Round(coord.Y / _gridSize) * _gridSize;
            if (!double.IsNaN(coord.Z))
            {
                coord.Z = Math.Round(coord.Z / _gridSize) * _gridSize;
            }
            coordSeq.SetOrdinate(i, 0, coord.X);
            coordSeq.SetOrdinate(i, 1, coord.Y);
            if (coordSeq.Dimension > 2)
            {
                coordSeq.SetOrdinate(i, 2, coord.Z);
            }
        }

        public bool Done => false;
        public bool GeometryChanged => true;
    }

    /// <summary>
    /// Gets validation options based on SRID.
    /// </summary>
    public static GeometryValidationOptions GetOptionsForSrid(int srid)
    {
        return srid switch
        {
            4326 => new GeometryValidationOptions // WGS84
            {
                MinX = -180.0,
                MaxX = 180.0,
                MinY = -90.0,
                MaxY = 90.0,
                TargetSrid = 4326
            },
            3857 => new GeometryValidationOptions // Web Mercator
            {
                MinX = -20037508.34,
                MaxX = 20037508.34,
                MinY = -20048966.10,
                MaxY = 20048966.10,
                TargetSrid = 3857
            },
            _ => new GeometryValidationOptions // Generic
            {
                MinX = double.MinValue,
                MaxX = double.MaxValue,
                MinY = double.MinValue,
                MaxY = double.MaxValue,
                TargetSrid = srid
            }
        };
    }

    /// <summary>
    /// Validates a polygon geometry according to Esri GeoServices requirements:
    /// - Exterior ring must be closed (first coordinate == last coordinate)
    /// - Exterior ring must have at least 4 points (including closing point)
    /// - Exterior ring must be counter-clockwise (for exterior rings)
    /// - Holes must be closed and clockwise
    /// - Geometry must be topologically valid (no self-intersections)
    /// </summary>
    public static ValidationResult ValidatePolygon(Polygon polygon)
    {
        if (polygon is null)
        {
            return ValidationResult.Error("Polygon cannot be null");
        }

        // Validate exterior ring
        var exteriorRing = polygon.ExteriorRing;
        if (exteriorRing is null)
        {
            return ValidationResult.Error("Polygon must have an exterior ring");
        }

        // Check if ring is closed
        if (!IsRingClosed(exteriorRing))
        {
            return ValidationResult.Error("Exterior ring is not closed (first and last coordinates must match)");
        }

        // Check minimum vertices (4 points including closing point = triangle minimum)
        if (exteriorRing.NumPoints < 4)
        {
            return ValidationResult.Error($"Exterior ring must have at least 4 points (including closing point), found {exteriorRing.NumPoints}");
        }

        // Validate ring orientation using NetTopologySuite's robust algorithm
        // Note: Esri uses clockwise for exterior rings, but NTS/OGC uses counter-clockwise
        // We validate based on OGC standard (CCW exterior, CW holes)
        if (!IsCounterClockwise(exteriorRing))
        {
            return ValidationResult.Error("Exterior ring must be counter-clockwise (OGC standard). Use NTS Reverse() to fix orientation.");
        }

        // Check all interior rings (holes)
        for (int i = 0; i < polygon.NumInteriorRings; i++)
        {
            var hole = polygon.GetInteriorRingN(i);

            if (!IsRingClosed(hole))
            {
                return ValidationResult.Error($"Interior ring {i} is not closed (first and last coordinates must match)");
            }

            if (hole.NumPoints < 4)
            {
                return ValidationResult.Error($"Interior ring {i} must have at least 4 points (including closing point), found {hole.NumPoints}");
            }

            // Holes must be clockwise (opposite of exterior)
            if (IsCounterClockwise(hole))
            {
                return ValidationResult.Error($"Interior ring {i} must be clockwise (OGC standard). Use NTS Reverse() to fix orientation.");
            }
        }

        // Check for self-intersections and other topological issues using NTS built-in validation
        if (!polygon.IsValid)
        {
            return ValidationResult.Error($"Polygon has topological errors: {GetValidationError(polygon)}");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Validates a linear ring (closed line string used in polygons).
    /// </summary>
    public static ValidationResult ValidateLinearRing(LinearRing ring)
    {
        if (ring is null)
        {
            return ValidationResult.Error("Linear ring cannot be null");
        }

        if (!IsRingClosed(ring))
        {
            return ValidationResult.Error("Linear ring is not closed (first and last coordinates must match)");
        }

        if (ring.NumPoints < 4)
        {
            return ValidationResult.Error($"Linear ring must have at least 4 points (including closing point), found {ring.NumPoints}");
        }

        // Check for self-intersection
        if (!ring.IsValid)
        {
            return ValidationResult.Error($"Linear ring has topological errors: {GetValidationError(ring)}");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Validates a line string geometry.
    /// </summary>
    public static ValidationResult ValidateLineString(LineString lineString)
    {
        if (lineString is null)
        {
            return ValidationResult.Error("LineString cannot be null");
        }

        if (lineString.NumPoints < 2)
        {
            return ValidationResult.Error($"LineString must have at least 2 points, found {lineString.NumPoints}");
        }

        if (!lineString.IsValid)
        {
            return ValidationResult.Error($"LineString has topological errors: {GetValidationError(lineString)}");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Validates a multi-polygon geometry.
    /// </summary>
    public static ValidationResult ValidateMultiPolygon(MultiPolygon multiPolygon)
    {
        if (multiPolygon is null)
        {
            return ValidationResult.Error("MultiPolygon cannot be null");
        }

        if (multiPolygon.NumGeometries == 0)
        {
            return ValidationResult.Error("MultiPolygon must contain at least one polygon");
        }

        for (int i = 0; i < multiPolygon.NumGeometries; i++)
        {
            var polygon = (Polygon)multiPolygon.GetGeometryN(i);
            var result = ValidatePolygon(polygon);
            if (!result.IsValid)
            {
                return ValidationResult.Error($"Polygon {i} in MultiPolygon is invalid: {result.ErrorMessage}");
            }
        }

        // Check overall validity
        if (!multiPolygon.IsValid)
        {
            return ValidationResult.Error($"MultiPolygon has topological errors: {GetValidationError(multiPolygon)}");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Checks if a linear ring is closed (first coordinate equals last coordinate).
    /// Supports 2D and 3D coordinate comparison.
    /// </summary>
    private static bool IsRingClosed(LineString ring)
    {
        if (ring.NumPoints < 2)
        {
            return false;
        }

        var coords = ring.Coordinates;
        var first = coords[0];
        var last = coords[^1];

        // Check X and Y coordinates (2D)
        if (!first.Equals2D(last))
        {
            return false;
        }

        // Check Z coordinate if present (3D)
        if (!double.IsNaN(first.Z) && !double.IsNaN(last.Z))
        {
            if (Math.Abs(first.Z - last.Z) > 1e-9)
            {
                return false;
            }
        }
        else if (double.IsNaN(first.Z) != double.IsNaN(last.Z))
        {
            // One has Z, the other doesn't - not a match
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines if a linear ring is counter-clockwise using NetTopologySuite's robust algorithm.
    /// Uses the signed area method (shoelace formula): CCW rings have positive signed area
    /// in standard coordinate systems where Y increases upward (OGC standard).
    /// </summary>
    /// <remarks>
    /// IMPORTANT: Esri uses clockwise for exterior rings, but OGC/NTS uses counter-clockwise.
    /// This method validates against OGC standard. When serializing to Esri formats,
    /// ring orientation may need to be reversed.
    /// </remarks>
    private static bool IsCounterClockwise(LineString ring)
    {
        // Use NetTopologySuite's robust and well-tested implementation
        // rather than a custom shoelace implementation which can be error-prone
        return Orientation.IsCCW(ring.CoordinateSequence);
    }

    /// <summary>
    /// Ensures polygon rings have correct orientation (CCW exterior, CW holes) per OGC standard.
    /// Returns a new polygon with corrected orientation if needed, or the original if already correct.
    /// </summary>
    public static Polygon EnsureCorrectOrientation(Polygon polygon)
    {
        var factory = polygon.Factory;
        var exteriorRing = (LinearRing)polygon.ExteriorRing;
        var needsReversal = false;

        // Ensure exterior ring is CCW
        LinearRing correctedExterior;
        if (IsCounterClockwise(exteriorRing))
        {
            correctedExterior = exteriorRing;
        }
        else
        {
            correctedExterior = (LinearRing)exteriorRing.Reverse();
            needsReversal = true;
        }

        // Ensure holes are CW (opposite of exterior)
        var correctedHoles = new LinearRing[polygon.NumInteriorRings];
        for (int i = 0; i < polygon.NumInteriorRings; i++)
        {
            var hole = (LinearRing)polygon.GetInteriorRingN(i);
            if (IsCounterClockwise(hole))
            {
                // Hole is CCW, needs to be reversed to CW
                correctedHoles[i] = (LinearRing)hole.Reverse();
                needsReversal = true;
            }
            else
            {
                correctedHoles[i] = hole;
            }
        }

        // Only create new polygon if orientation needed correction
        if (needsReversal)
        {
            var corrected = factory.CreatePolygon(correctedExterior, correctedHoles);
            corrected.SRID = polygon.SRID;
            return corrected;
        }

        return polygon;
    }

    /// <summary>
    /// Ensures polygon rings use GeoServices ring orientation (CW exterior, CCW holes) - opposite of OGC.
    /// Returns a new polygon with GeoServices ring orientation if needed.
    /// </summary>
    public static Polygon EnsureEsriOrientation(Polygon polygon)
    {
        var factory = polygon.Factory;
        var exteriorRing = (LinearRing)polygon.ExteriorRing;
        var needsReversal = false;

        // Ensure exterior ring is CW (GeoServices specification, opposite of OGC)
        LinearRing correctedExterior;
        if (!IsCounterClockwise(exteriorRing))
        {
            correctedExterior = exteriorRing;
        }
        else
        {
            correctedExterior = (LinearRing)exteriorRing.Reverse();
            needsReversal = true;
        }

        // Ensure holes are CCW (GeoServices specification, opposite of OGC)
        var correctedHoles = new LinearRing[polygon.NumInteriorRings];
        for (int i = 0; i < polygon.NumInteriorRings; i++)
        {
            var hole = (LinearRing)polygon.GetInteriorRingN(i);
            if (!IsCounterClockwise(hole))
            {
                // Hole is CW, needs to be reversed to CCW for Esri
                correctedHoles[i] = (LinearRing)hole.Reverse();
                needsReversal = true;
            }
            else
            {
                correctedHoles[i] = hole;
            }
        }

        // Only create new polygon if orientation needed correction
        if (needsReversal)
        {
            var corrected = factory.CreatePolygon(correctedExterior, correctedHoles);
            corrected.SRID = polygon.SRID;
            return corrected;
        }

        return polygon;
    }

    /// <summary>
    /// Gets a validation error message for a geometry using NTS's IsValidOp.
    /// </summary>
    private static string GetValidationError(NetTopologySuite.Geometries.Geometry geometry)
    {
        try
        {
            var op = new NetTopologySuite.Operation.Valid.IsValidOp(geometry);
            var error = op.ValidationError;
            if (error != null)
            {
                return error.ToString();
            }
            return "Unknown topological error";
        }
        catch
        {
            return "Unknown topological error";
        }
    }
}

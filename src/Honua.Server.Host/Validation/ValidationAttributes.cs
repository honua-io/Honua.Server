// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using NetTopologySuite.IO;
using Newtonsoft.Json;

namespace Honua.Server.Host.Validation;

/// <summary>
/// Validates that a string contains a valid collection name (alphanumeric, underscore, hyphen only).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CollectionNameAttribute : ValidationAttribute
{
    private static readonly Regex CollectionNameRegex = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null or "")
        {
            return ValidationResult.Success;
        }

        if (value is not string stringValue)
        {
            return new ValidationResult("Collection name must be a string.");
        }

        if (!CollectionNameRegex.IsMatch(stringValue))
        {
            return new ValidationResult(
                "Collection name must contain only alphanumeric characters, underscores, and hyphens.");
        }

        if (stringValue.Length > 255)
        {
            return new ValidationResult("Collection name cannot exceed 255 characters.");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a latitude value is between -90 and 90 degrees.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LatitudeAttribute : RangeAttribute
{
    public LatitudeAttribute() : base(-90.0, 90.0)
    {
        ErrorMessage = "Latitude must be between -90 and 90 degrees.";
    }
}

/// <summary>
/// Validates that a longitude value is between -180 and 180 degrees.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LongitudeAttribute : RangeAttribute
{
    public LongitudeAttribute() : base(-180.0, 180.0)
    {
        ErrorMessage = "Longitude must be between -180 and 180 degrees.";
    }
}

/// <summary>
/// Validates that a string contains valid GeoJSON.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class GeoJsonAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null or "")
        {
            return ValidationResult.Success;
        }

        if (value is not string geoJsonString)
        {
            return new ValidationResult("GeoJSON must be a string.");
        }

        try
        {
            var serializer = GeoJsonSerializer.Create();
            using var stringReader = new StringReader(geoJsonString);
            using var jsonReader = new JsonTextReader(stringReader);
            var geometry = serializer.Deserialize<NetTopologySuite.Geometries.Geometry>(jsonReader);

            if (geometry == null)
            {
                return new ValidationResult("GeoJSON could not be parsed.");
            }

            if (!geometry.IsValid)
            {
                return new ValidationResult("GeoJSON geometry is invalid.");
            }

            return ValidationResult.Success;
        }
        catch (JsonException ex)
        {
            return new ValidationResult($"Invalid GeoJSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new ValidationResult($"GeoJSON validation error: {ex.Message}");
        }
    }
}

/// <summary>
/// Validates that a zoom level is within the valid range (0-30).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ZoomLevelAttribute : RangeAttribute
{
    public ZoomLevelAttribute() : base(0, 30)
    {
        ErrorMessage = "Zoom level must be between 0 and 30.";
    }
}

/// <summary>
/// Validates that a tile size is within reasonable limits.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TileSizeAttribute : ValidationAttribute
{
    private readonly int minSize;
    private readonly int maxSize;

    public TileSizeAttribute(int minSize = 64, int maxSize = 4096)
    {
        this.minSize = minSize;
        this.maxSize = maxSize;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not int intValue)
        {
            return new ValidationResult("Tile size must be an integer.");
        }

        if (intValue < _minSize || intValue > _maxSize)
        {
            return new ValidationResult($"Tile size must be between {_minSize} and {_maxSize} pixels.");
        }

        // Ensure it's a power of 2 or common size
        var validSizes = new[] { 64, 128, 256, 512, 1024, 2048, 4096 };
        if (!validSizes.Contains(intValue))
        {
            return new ValidationResult($"Tile size should be one of: {string.Join(", ", validSizes)}.");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a file size is within limits.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class FileSizeAttribute : ValidationAttribute
{
    private readonly long maxBytes;

    public FileSizeAttribute(long maxBytes)
    {
        this.maxBytes = maxBytes;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not long longValue)
        {
            return new ValidationResult("File size must be a long integer.");
        }

        if (longValue < 0)
        {
            return new ValidationResult("File size cannot be negative.");
        }

        if (longValue > _maxBytes)
        {
            var maxMb = _maxBytes / (1024.0 * 1024.0);
            return new ValidationResult($"File size cannot exceed {maxMb:F2} MB.");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a string represents a valid ISO 8601 datetime or interval.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class Iso8601DateTimeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null or "")
        {
            return ValidationResult.Success;
        }

        if (value is not string stringValue)
        {
            return new ValidationResult("ISO 8601 datetime must be a string.");
        }

        // Try to parse as DateTime
        if (DateTimeOffset.TryParse(stringValue, out _))
        {
            return ValidationResult.Success;
        }

        // Try to parse as interval (e.g., "2024-01-01T00:00:00Z/2024-12-31T23:59:59Z")
        if (stringValue.Contains('/'))
        {
            var parts = stringValue.Split('/');
            if (parts.Length == 2 &&
                DateTimeOffset.TryParse(parts[0], out _) &&
                DateTimeOffset.TryParse(parts[1], out _))
            {
                return ValidationResult.Success;
            }
        }

        return new ValidationResult("Value must be a valid ISO 8601 datetime or interval.");
    }
}

/// <summary>
/// Validates that a MIME type is one of the allowed types.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class AllowedMimeTypesAttribute : ValidationAttribute
{
    private readonly string[] allowedTypes;

    public AllowedMimeTypesAttribute(params string[] allowedTypes)
    {
        this.allowedTypes = allowedTypes;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null or "")
        {
            return ValidationResult.Success;
        }

        if (value is not string stringValue)
        {
            return new ValidationResult("MIME type must be a string.");
        }

        if (this.allowedTypes.Contains(stringValue, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(
            $"MIME type must be one of: {string.Join(", ", _allowedTypes)}");
    }
}

/// <summary>
/// Validates that a string does not contain path traversal sequences.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NoPathTraversalAttribute : ValidationAttribute
{
    private static readonly string[] DangerousPatterns = ["../", "..\\", "%2e%2e/", "%2e%2e\\"];

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null or "")
        {
            return ValidationResult.Success;
        }

        if (value is not string stringValue)
        {
            return new ValidationResult("Value must be a string.");
        }

        var lowerValue = stringValue.ToLowerInvariant();
        foreach (var pattern in DangerousPatterns)
        {
            if (lowerValue.Contains(pattern))
            {
                return new ValidationResult("Value contains invalid path traversal sequences.");
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a string is safe (no control characters, reasonable length).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SafeStringAttribute : ValidationAttribute
{
    private readonly int maxLength;

    public SafeStringAttribute(int maxLength = 1000)
    {
        this.maxLength = maxLength;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null or "")
        {
            return ValidationResult.Success;
        }

        if (value is not string stringValue)
        {
            return new ValidationResult("Value must be a string.");
        }

        if (stringValue.Length > _maxLength)
        {
            return new ValidationResult($"String length cannot exceed {_maxLength} characters.");
        }

        // Check for control characters (except common whitespace)
        foreach (var ch in stringValue)
        {
            if (char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t')
            {
                return new ValidationResult("String contains invalid control characters.");
            }
        }

        return ValidationResult.Success;
    }
}

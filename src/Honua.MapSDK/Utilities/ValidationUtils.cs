// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Honua.MapSDK.Utilities;

/// <summary>
/// Utility methods for input validation.
/// </summary>
public static class ValidationUtils
{
    /// <summary>
    /// Validates if a string is a valid URL.
    /// </summary>
    /// <param name="url">URL to validate.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Validates if coordinates are within valid WGS84 bounds.
    /// </summary>
    /// <param name="longitude">Longitude value.</param>
    /// <param name="latitude">Latitude value.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    public static bool IsValidCoordinate(double longitude, double latitude)
    {
        return longitude >= -180 && longitude <= 180 && latitude >= -90 && latitude <= 90;
    }

    /// <summary>
    /// Validates a GeoJSON string.
    /// </summary>
    /// <param name="geoJson">GeoJSON string to validate.</param>
    /// <returns>Validation result with error message if invalid.</returns>
    public static ValidationResult ValidateGeoJson(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
            return ValidationResult.Failure("GeoJSON cannot be empty");

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(geoJson);
            var root = document.RootElement;

            // Must have a type property
            if (!root.TryGetProperty("type", out var typeProperty))
                return ValidationResult.Failure("GeoJSON must have a 'type' property");

            var type = typeProperty.GetString();
            if (string.IsNullOrEmpty(type))
                return ValidationResult.Failure("GeoJSON 'type' cannot be empty");

            // Validate type
            var validTypes = new[] { "Feature", "FeatureCollection", "Point", "LineString", "Polygon", "MultiPoint", "MultiLineString", "MultiPolygon", "GeometryCollection" };
            if (!validTypes.Contains(type))
                return ValidationResult.Failure($"Invalid GeoJSON type: {type}");

            // If FeatureCollection, must have features array
            if (type == "FeatureCollection")
            {
                if (!root.TryGetProperty("features", out var features))
                    return ValidationResult.Failure("FeatureCollection must have a 'features' property");

                if (features.ValueKind != System.Text.Json.JsonValueKind.Array)
                    return ValidationResult.Failure("'features' must be an array");
            }

            // If Feature, must have geometry
            if (type == "Feature")
            {
                if (!root.TryGetProperty("geometry", out _))
                    return ValidationResult.Failure("Feature must have a 'geometry' property");
            }

            return ValidationResult.Success();
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a date range.
    /// </summary>
    /// <param name="start">Start date.</param>
    /// <param name="end">End date.</param>
    /// <returns>Validation result.</returns>
    public static ValidationResult ValidateDateRange(DateTime start, DateTime end)
    {
        if (start > end)
            return ValidationResult.Failure("Start date must be before end date");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a numerical range.
    /// </summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="min">Minimum value.</param>
    /// <param name="max">Maximum value.</param>
    /// <returns>Validation result.</returns>
    public static ValidationResult ValidateRange(double value, double min, double max)
    {
        if (value < min || value > max)
            return ValidationResult.Failure($"Value must be between {min} and {max}");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a color hex string.
    /// </summary>
    /// <param name="hex">Hex color string.</param>
    /// <returns>Validation result.</returns>
    public static ValidationResult ValidateHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return ValidationResult.Failure("Color cannot be empty");

        var pattern = @"^#?([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$";
        if (!Regex.IsMatch(hex, pattern))
            return ValidationResult.Failure("Invalid hex color format. Expected format: #RGB or #RRGGBB");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Sanitizes HTML input to prevent XSS attacks.
    /// </summary>
    /// <param name="input">Input string.</param>
    /// <returns>Sanitized string.</returns>
    public static string SanitizeHtml(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#x27;")
            .Replace("/", "&#x2F;");
    }

    /// <summary>
    /// Validates and sanitizes a file path to prevent path traversal attacks.
    /// </summary>
    /// <param name="path">File path to validate.</param>
    /// <returns>Validation result.</returns>
    public static ValidationResult ValidateFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Failure("Path cannot be empty");

        // Check for path traversal attempts
        if (path.Contains("..") || path.Contains("~"))
            return ValidationResult.Failure("Path contains invalid sequences");

        // Check for absolute paths (which could be dangerous)
        if (Path.IsPathRooted(path))
            return ValidationResult.Failure("Absolute paths are not allowed");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a property name for filtering/sorting.
    /// </summary>
    /// <param name="propertyName">Property name to validate.</param>
    /// <returns>Validation result.</returns>
    public static ValidationResult ValidatePropertyName(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return ValidationResult.Failure("Property name cannot be empty");

        // Only allow alphanumeric characters, underscores, and dots (for nested properties)
        var pattern = @"^[a-zA-Z_][a-zA-Z0-9_.]*$";
        if (!Regex.IsMatch(propertyName, pattern))
            return ValidationResult.Failure("Invalid property name format");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates an email address.
    /// </summary>
    /// <param name="email">Email address to validate.</param>
    /// <returns>Validation result.</returns>
    public static ValidationResult ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return ValidationResult.Failure("Email cannot be empty");

        var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (!Regex.IsMatch(email, pattern))
            return ValidationResult.Failure("Invalid email format");

        return ValidationResult.Success();
    }
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation succeeded.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with an error message.
    /// </summary>
    /// <param name="errorMessage">Error message.</param>
    public static ValidationResult Failure(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage
    };
}

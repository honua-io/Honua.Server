// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Globalization;

namespace Honua.Server.Core.Utilities;

/// <summary>
/// Helper methods for culture-invariant formatting and parsing.
/// Essential for geospatial APIs to ensure consistent coordinate, date, and number formatting
/// regardless of the server's culture settings or client's Accept-Language header.
/// </summary>
/// <remarks>
/// All geospatial data interchange formats (GeoJSON, WKT, WMS, WFS, OGC API) require
/// culture-invariant formatting:
/// - Coordinates must use period (.) as decimal separator (not comma)
/// - Dates must use ISO 8601 format
/// - Numbers in URLs/responses must be culture-invariant
///
/// This prevents bugs like:
/// - German locale: 52,5 instead of 52.5 (breaks GeoJSON)
/// - Turkish locale: "I".ToLower() = "ı" instead of "i" (breaks case-insensitive comparisons)
/// </remarks>
public static class CultureInvariantHelpers
{
    /// <summary>
    /// Formats a coordinate value (latitude, longitude, or other numeric coordinate) using invariant culture.
    /// Always uses period (.) as decimal separator and 6 decimal places for precision.
    /// </summary>
    /// <param name="value">The coordinate value to format.</param>
    /// <returns>Culture-invariant string representation with 6 decimal places.</returns>
    /// <example>
    /// FormatCoordinate(52.520008) returns "52.520008"
    /// FormatCoordinate(13.404954) returns "13.404954"
    /// </example>
    public static string FormatCoordinate(double value)
    {
        return value.ToString("F6", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a coordinate value with custom precision using invariant culture.
    /// </summary>
    /// <param name="value">The coordinate value to format.</param>
    /// <param name="decimalPlaces">Number of decimal places (0-15).</param>
    /// <returns>Culture-invariant string representation.</returns>
    public static string FormatCoordinate(double value, int decimalPlaces)
    {
        return value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a number using invariant culture without forcing decimal places.
    /// Uses "G" (general) format which automatically chooses fixed-point or scientific notation.
    /// </summary>
    /// <param name="value">The number to format.</param>
    /// <returns>Culture-invariant string representation.</returns>
    public static string FormatNumber(double value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a decimal number using invariant culture.
    /// </summary>
    /// <param name="value">The decimal value to format.</param>
    /// <returns>Culture-invariant string representation.</returns>
    public static string FormatNumber(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a date/time value to ISO 8601 format with UTC timezone (RFC 3339).
    /// Format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
    /// </summary>
    /// <param name="date">The date/time value to format.</param>
    /// <returns>ISO 8601 formatted string with 'Z' suffix indicating UTC.</returns>
    /// <example>
    /// FormatDateTime(DateTimeOffset.Parse("2025-10-31T14:30:00Z"))
    /// returns "2025-10-31T14:30:00Z"
    /// </example>
    public static string FormatDateTime(DateTimeOffset date)
    {
        return date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a date/time value to ISO 8601 format with milliseconds.
    /// Format: "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"
    /// </summary>
    /// <param name="date">The date/time value to format.</param>
    /// <returns>ISO 8601 formatted string with milliseconds and 'Z' suffix.</returns>
    public static string FormatDateTimeWithMilliseconds(DateTimeOffset date)
    {
        return date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a date value (without time) to ISO 8601 date format.
    /// Format: "yyyy-MM-dd"
    /// </summary>
    /// <param name="date">The date value to format.</param>
    /// <returns>ISO 8601 date string.</returns>
    public static string FormatDate(DateTimeOffset date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a coordinate string using invariant culture.
    /// Accepts both comma and period as decimal separator for flexibility,
    /// but always outputs using period.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The parsed double value.</returns>
    /// <exception cref="FormatException">Thrown when the string is not a valid number.</exception>
    public static double ParseCoordinate(string value)
    {
        return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tries to parse a coordinate string using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed double value if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseCoordinate(string value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Parses a number string using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The parsed double value.</returns>
    /// <exception cref="FormatException">Thrown when the string is not a valid number.</exception>
    public static double ParseNumber(string value)
    {
        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tries to parse a number string using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed double value if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseNumber(string value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Parses a decimal number string using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The parsed decimal value.</returns>
    /// <exception cref="FormatException">Thrown when the string is not a valid number.</exception>
    public static decimal ParseDecimal(string value)
    {
        return decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tries to parse a decimal number string using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed decimal value if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseDecimal(string value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Parses an integer using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The parsed integer value.</returns>
    /// <exception cref="FormatException">Thrown when the string is not a valid integer.</exception>
    public static int ParseInt(string value)
    {
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Tries to parse an integer using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed integer value if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseInt(string value, out int result)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Parses a date/time string using ISO 8601 format and invariant culture.
    /// Accepts formats: "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd"
    /// </summary>
    /// <param name="value">The ISO 8601 date/time string to parse.</param>
    /// <returns>The parsed DateTimeOffset value.</returns>
    /// <exception cref="FormatException">Thrown when the string is not a valid date/time.</exception>
    public static DateTimeOffset ParseDateTime(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    /// <summary>
    /// Tries to parse a date/time string using ISO 8601 format and invariant culture.
    /// </summary>
    /// <param name="value">The ISO 8601 date/time string to parse.</param>
    /// <param name="result">The parsed DateTimeOffset value if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParseDateTime(string value, out DateTimeOffset result)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
    }

    /// <summary>
    /// Performs culture-invariant case-insensitive string comparison.
    /// Safe for technical identifiers, layer names, format names, etc.
    /// </summary>
    /// <param name="a">First string to compare.</param>
    /// <param name="b">Second string to compare.</param>
    /// <returns>True if strings are equal ignoring case (ordinal comparison).</returns>
    /// <remarks>
    /// Uses StringComparison.OrdinalIgnoreCase to avoid the "Turkish I problem":
    /// - In Turkish: "i".ToUpper() = "İ" (not "I")
    /// - In Turkish: "I".ToLower() = "ı" (not "i")
    /// Ordinal comparison treats all ASCII characters consistently.
    /// </remarks>
    public static bool EqualsIgnoreCase(string? a, string? b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Performs culture-invariant case-insensitive string contains check.
    /// </summary>
    /// <param name="source">The string to search in.</param>
    /// <param name="value">The value to search for.</param>
    /// <returns>True if source contains value (case-insensitive ordinal).</returns>
    public static bool ContainsIgnoreCase(this string? source, string value)
    {
        return source?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// Performs culture-invariant case-insensitive string starts with check.
    /// </summary>
    /// <param name="source">The string to check.</param>
    /// <param name="value">The prefix to check for.</param>
    /// <returns>True if source starts with value (case-insensitive ordinal).</returns>
    public static bool StartsWithIgnoreCase(this string? source, string value)
    {
        return source?.StartsWith(value, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// Performs culture-invariant case-insensitive string ends with check.
    /// </summary>
    /// <param name="source">The string to check.</param>
    /// <param name="value">The suffix to check for.</param>
    /// <returns>True if source ends with value (case-insensitive ordinal).</returns>
    public static bool EndsWithIgnoreCase(this string? source, string value)
    {
        return source?.EndsWith(value, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}

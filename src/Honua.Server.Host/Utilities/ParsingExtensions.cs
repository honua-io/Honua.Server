// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;

#nullable enable

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Extension methods for parsing strings to numeric types using InvariantCulture.
/// Consolidates duplicate parsing logic throughout the GeoservicesREST module.
/// </summary>
public static class ParsingExtensions
{
    /// <summary>
    /// Tries to parse a string to a double using InvariantCulture.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="result">The parsed double value, or 0 if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseDouble(this string? value, out double result)
    {
        if (value == null)
        {
            result = 0;
            return false;
        }
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Tries to parse a string to a double using InvariantCulture with only Float style (no thousands separator).
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="result">The parsed double value, or 0 if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseDoubleStrict(this string? value, out double result)
    {
        if (value == null)
        {
            result = 0;
            return false;
        }
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Tries to parse a ReadOnlySpan&lt;char&gt; to a double using InvariantCulture.
    /// </summary>
    /// <param name="value">The span value to parse.</param>
    /// <param name="result">The parsed double value, or 0 if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseDouble(this ReadOnlySpan<char> value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Tries to parse a string to a float using InvariantCulture.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="result">The parsed float value, or 0 if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseFloat(this string? value, out float result)
    {
        if (value == null)
        {
            result = 0;
            return false;
        }
        return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Tries to parse a string to an int using InvariantCulture.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="result">The parsed int value, or 0 if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseInt(this string? value, out int result)
    {
        if (value == null)
        {
            result = 0;
            return false;
        }
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Tries to parse a string to a decimal using InvariantCulture.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="result">The parsed decimal value, or 0 if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseDecimal(this string? value, out decimal result)
    {
        if (value == null)
        {
            result = 0;
            return false;
        }
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }
}

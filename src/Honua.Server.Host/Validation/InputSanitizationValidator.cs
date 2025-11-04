// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Host.Validation;

/// <summary>
/// Provides input sanitization and validation for user-supplied data.
/// DATA INTEGRITY: Prevents injection attacks and ensures data quality.
/// </summary>
public static class InputSanitizationValidator
{
    private const int MaxStringLength = 10000;
    private const int MaxIdentifierLength = 255;
    private const int MaxArrayLength = 10000;

    private static readonly Regex SqlInjectionPattern = new(
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|DECLARE|SCRIPT|JAVASCRIPT|ONERROR|ONCLICK)\b|--|;|/\*|\*/|xp_|sp_)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PathTraversalPattern = new(
        @"(\.\.[\\/]|\.\.%2[fF]|%2e%2e[\\/]|%2e%2e%2[fF])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex XssPattern = new(
        @"(<script|<iframe|javascript:|onerror=|onclick=|onload=|<object|<embed|eval\(|expression\()",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ValidIdentifierPattern = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.Compiled);

    private static readonly Regex ValidNumericPattern = new(
        @"^-?\d+(\.\d+)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates and sanitizes a string input.
    /// </summary>
    /// <param name="value">The string to validate.</param>
    /// <param name="parameterName">The parameter name for error messages.</param>
    /// <param name="maxLength">Maximum allowed length (default: 10000).</param>
    /// <param name="allowNull">Whether null values are allowed (default: false).</param>
    /// <param name="checkSqlInjection">Whether to check for SQL injection patterns (default: true).</param>
    /// <param name="checkXss">Whether to check for XSS patterns (default: true).</param>
    /// <param name="checkPathTraversal">Whether to check for path traversal patterns (default: false).</param>
    /// <returns>The validated string.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static string? ValidateString(
        string? value,
        string parameterName,
        int maxLength = MaxStringLength,
        bool allowNull = false,
        bool checkSqlInjection = true,
        bool checkXss = true,
        bool checkPathTraversal = false)
    {
        if (value is null)
        {
            if (!allowNull)
            {
                throw new ArgumentException($"Parameter '{parameterName}' cannot be null.", parameterName);
            }
            return null;
        }

        // Length validation
        if (value.Length > maxLength)
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' exceeds maximum length of {maxLength} characters (actual: {value.Length}).",
                parameterName);
        }

        // SQL injection check
        if (checkSqlInjection && SqlInjectionPattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' contains potentially unsafe SQL patterns.",
                parameterName);
        }

        // XSS check
        if (checkXss && XssPattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' contains potentially unsafe script patterns.",
                parameterName);
        }

        // Path traversal check
        if (checkPathTraversal && PathTraversalPattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' contains path traversal patterns.",
                parameterName);
        }

        return value;
    }

    /// <summary>
    /// Validates an identifier (table name, column name, etc.).
    /// </summary>
    /// <param name="value">The identifier to validate.</param>
    /// <param name="parameterName">The parameter name for error messages.</param>
    /// <param name="allowNull">Whether null values are allowed.</param>
    /// <returns>The validated identifier.</returns>
    public static string? ValidateIdentifier(
        string? value,
        string parameterName,
        bool allowNull = false)
    {
        if (value is null)
        {
            if (!allowNull)
            {
                throw new ArgumentException($"Identifier '{parameterName}' cannot be null.", parameterName);
            }
            return null;
        }

        if (value.Length > MaxIdentifierLength)
        {
            throw new ArgumentException(
                $"Identifier '{parameterName}' exceeds maximum length of {MaxIdentifierLength} characters.",
                parameterName);
        }

        // Validate identifier format
        if (!ValidIdentifierPattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"Identifier '{parameterName}' has invalid format. " +
                $"Identifiers must start with a letter or underscore and contain only letters, digits, and underscores.",
                parameterName);
        }

        // Check for SQL keywords (case-insensitive)
        var sqlKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
            "TABLE", "INDEX", "VIEW", "FUNCTION", "PROCEDURE", "TRIGGER",
            "FROM", "WHERE", "ORDER", "GROUP", "HAVING", "JOIN", "UNION"
        };

        if (sqlKeywords.Contains(value))
        {
            throw new ArgumentException(
                $"Identifier '{parameterName}' cannot be a SQL keyword: '{value}'.",
                parameterName);
        }

        return value;
    }

    /// <summary>
    /// Validates a numeric string.
    /// </summary>
    /// <param name="value">The numeric string to validate.</param>
    /// <param name="parameterName">The parameter name for error messages.</param>
    /// <param name="allowNull">Whether null values are allowed.</param>
    /// <returns>The validated numeric string.</returns>
    public static string? ValidateNumericString(
        string? value,
        string parameterName,
        bool allowNull = false)
    {
        if (value is null)
        {
            if (!allowNull)
            {
                throw new ArgumentException($"Numeric parameter '{parameterName}' cannot be null.", parameterName);
            }
            return null;
        }

        if (value.IsNullOrWhiteSpace())
        {
            throw new ArgumentException($"Numeric parameter '{parameterName}' cannot be empty.", parameterName);
        }

        if (!ValidNumericPattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' must be a valid numeric value, got: '{value}'.",
                parameterName);
        }

        return value;
    }

    /// <summary>
    /// Validates an integer within a specified range.
    /// </summary>
    /// <param name="value">The integer to validate.</param>
    /// <param name="parameterName">The parameter name for error messages.</param>
    /// <param name="minValue">Minimum allowed value (inclusive).</param>
    /// <param name="maxValue">Maximum allowed value (inclusive).</param>
    /// <returns>The validated integer.</returns>
    public static int ValidateInteger(
        int value,
        string parameterName,
        int minValue = int.MinValue,
        int maxValue = int.MaxValue)
    {
        if (value < minValue || value > maxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Parameter '{parameterName}' must be between {minValue} and {maxValue}.");
        }

        return value;
    }

    /// <summary>
    /// Validates a long integer within a specified range.
    /// </summary>
    public static long ValidateLong(
        long value,
        string parameterName,
        long minValue = long.MinValue,
        long maxValue = long.MaxValue)
    {
        if (value < minValue || value > maxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Parameter '{parameterName}' must be between {minValue} and {maxValue}.");
        }

        return value;
    }

    /// <summary>
    /// Validates a double within a specified range.
    /// </summary>
    public static double ValidateDouble(
        double value,
        string parameterName,
        double minValue = double.MinValue,
        double maxValue = double.MaxValue)
    {
        if (double.IsNaN(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be NaN.", parameterName);
        }

        if (double.IsInfinity(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be infinity.", parameterName);
        }

        if (value < minValue || value > maxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Parameter '{parameterName}' must be between {minValue} and {maxValue}.");
        }

        return value;
    }

    /// <summary>
    /// Validates an array length.
    /// </summary>
    /// <param name="array">The array to validate.</param>
    /// <param name="parameterName">The parameter name for error messages.</param>
    /// <param name="maxLength">Maximum allowed length (default: 10000).</param>
    /// <param name="allowNull">Whether null values are allowed.</param>
    /// <param name="allowEmpty">Whether empty arrays are allowed.</param>
    /// <returns>The validated array.</returns>
    public static T[]? ValidateArray<T>(
        T[]? array,
        string parameterName,
        int maxLength = MaxArrayLength,
        bool allowNull = false,
        bool allowEmpty = true)
    {
        if (array is null)
        {
            if (!allowNull)
            {
                throw new ArgumentException($"Array parameter '{parameterName}' cannot be null.", parameterName);
            }
            return null;
        }

        if (array.Length == 0 && !allowEmpty)
        {
            throw new ArgumentException($"Array parameter '{parameterName}' cannot be empty.", parameterName);
        }

        if (array.Length > maxLength)
        {
            throw new ArgumentException(
                $"Array parameter '{parameterName}' exceeds maximum length of {maxLength} (actual: {array.Length}).",
                parameterName);
        }

        return array;
    }

    /// <summary>
    /// Validates a GUID string.
    /// </summary>
    public static string? ValidateGuid(
        string? value,
        string parameterName,
        bool allowNull = false)
    {
        if (value is null)
        {
            if (!allowNull)
            {
                throw new ArgumentException($"GUID parameter '{parameterName}' cannot be null.", parameterName);
            }
            return null;
        }

        if (!Guid.TryParse(value, out _))
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' must be a valid GUID, got: '{value}'.",
                parameterName);
        }

        return value;
    }

    /// <summary>
    /// Validates a URL string.
    /// </summary>
    public static string? ValidateUrl(
        string? value,
        string parameterName,
        bool allowNull = false,
        bool requireHttps = false)
    {
        if (value is null)
        {
            if (!allowNull)
            {
                throw new ArgumentException($"URL parameter '{parameterName}' cannot be null.", parameterName);
            }
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' must be a valid absolute URL, got: '{value}'.",
                parameterName);
        }

        if (requireHttps && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' must use HTTPS scheme, got: '{uri.Scheme}'.",
                parameterName);
        }

        return value;
    }

    /// <summary>
    /// Validates an email address.
    /// PERFORMANCE: Uses cached compiled pattern to avoid repeated compilation.
    /// </summary>
    public static string? ValidateEmail(
        string? value,
        string parameterName,
        bool allowNull = false)
    {
        if (value is null)
        {
            if (!allowNull)
            {
                throw new ArgumentException($"Email parameter '{parameterName}' cannot be null.", parameterName);
            }
            return null;
        }

        // Basic email validation (not RFC 5322 compliant, but good enough for most cases)
        var emailPattern = RegexCache.GetOrAdd(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        if (!emailPattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"Parameter '{parameterName}' must be a valid email address, got: '{value}'.",
                parameterName);
        }

        return value;
    }

    /// <summary>
    /// Sanitizes HTML by removing dangerous tags and attributes.
    /// PERFORMANCE: Uses cached compiled patterns to avoid repeated compilation.
    /// </summary>
    public static string SanitizeHtml(string html)
    {
        if (html.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        // Remove script tags
        var scriptPattern = RegexCache.GetOrAdd(@"<script[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        html = scriptPattern.Replace(html, string.Empty);

        // Remove event handlers
        var eventPattern = RegexCache.GetOrAdd(@"\s*on\w+\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        html = eventPattern.Replace(html, string.Empty);

        // Remove javascript: links
        var jsLinkPattern = RegexCache.GetOrAdd(@"javascript:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        html = jsLinkPattern.Replace(html, string.Empty);

        return html;
    }
}

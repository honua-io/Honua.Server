// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Text.RegularExpressions;

namespace Honua.Server.Core.Security;

/// <summary>
/// Provides input validation and sanitization helpers to prevent injection attacks and XSS.
/// </summary>
public static class InputValidationHelpers
{
    // Regex patterns for validation
    private static readonly Regex AlphanumericRegex = new Regex(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex UuidRegex = new Regex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);

    /// <summary>
    /// Validates that a string contains only alphanumeric characters, underscores, and hyphens.
    /// </summary>
    public static bool IsAlphanumeric(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return AlphanumericRegex.IsMatch(input);
    }

    /// <summary>
    /// Validates that a string is a valid email address format.
    /// </summary>
    public static bool IsValidEmail(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return EmailRegex.IsMatch(input);
    }

    /// <summary>
    /// Validates that a string is a valid UUID/GUID format.
    /// </summary>
    public static bool IsValidUuid(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return UuidRegex.IsMatch(input) || Guid.TryParse(input, out _);
    }

    /// <summary>
    /// Validates that a string length is within acceptable bounds.
    /// </summary>
    public static bool IsValidLength(string? input, int minLength = 1, int maxLength = 1000)
    {
        if (input == null)
        {
            return false;
        }

        return input.Length >= minLength && input.Length <= maxLength;
    }

    /// <summary>
    /// Sanitizes a string by removing potentially dangerous characters for use in SQL LIKE clauses.
    /// </summary>
    public static string SanitizeLikePattern(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Escape special SQL LIKE characters: % _ [ ]
        return input
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
    }

    /// <summary>
    /// Validates that a string does not contain SQL injection patterns.
    /// NOTE: This is defense-in-depth. Always use parameterized queries as the primary defense.
    /// </summary>
    public static bool ContainsSqlInjectionPatterns(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Check for common SQL injection patterns (case-insensitive)
        var lowercaseInput = input.ToLowerInvariant();

        // Dangerous SQL keywords in suspicious contexts
        string[] suspiciousPatterns = new[]
        {
            "union select",
            "'; drop",
            "\"; drop",
            "' or '1'='1",
            "' or 1=1",
            "\" or \"1\"=\"1",
            "\" or 1=1",
            "'; exec",
            "\"; exec",
            "xp_cmdshell",
            "sp_executesql"
        };

        foreach (var pattern in suspiciousPatterns)
        {
            if (lowercaseInput.Contains(pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that a string does not contain XSS patterns.
    /// NOTE: This is defense-in-depth. Always use proper output encoding as the primary defense.
    /// </summary>
    public static bool ContainsXssPatterns(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var lowercaseInput = input.ToLowerInvariant();

        // Check for common XSS patterns
        string[] xssPatterns = new[]
        {
            "<script",
            "javascript:",
            "onerror=",
            "onload=",
            "onclick=",
            "onmouseover=",
            "<iframe",
            "<object",
            "<embed",
            "eval(",
            "expression("
        };

        foreach (var pattern in xssPatterns)
        {
            if (lowercaseInput.Contains(pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that a string is safe for use (no SQL injection or XSS patterns).
    /// </summary>
    public static bool IsSafeInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return true; // Null/empty is safe
        }

        return !ContainsSqlInjectionPatterns(input) && !ContainsXssPatterns(input);
    }

    /// <summary>
    /// Validates a resource ID format (alphanumeric, UUID, or numeric).
    /// </summary>
    public static bool IsValidResourceId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Accept UUIDs, integers, or alphanumeric identifiers
        return IsValidUuid(input)
            || int.TryParse(input, out _)
            || long.TryParse(input, out _)
            || IsAlphanumeric(input);
    }

    /// <summary>
    /// Validates a page size parameter for pagination (prevents DoS via large page sizes).
    /// </summary>
    public static bool IsValidPageSize(int pageSize, int maxPageSize = 100)
    {
        return pageSize > 0 && pageSize <= maxPageSize;
    }

    /// <summary>
    /// Validates a page number parameter for pagination.
    /// </summary>
    public static bool IsValidPageNumber(int pageNumber)
    {
        return pageNumber >= 0;
    }

    /// <summary>
    /// Throws an ArgumentException if the input contains SQL injection patterns.
    /// </summary>
    public static void ThrowIfSqlInjection(string? input, string paramName)
    {
        if (ContainsSqlInjectionPatterns(input))
        {
            throw new ArgumentException($"Input contains potentially dangerous SQL patterns: {paramName}", paramName);
        }
    }

    /// <summary>
    /// Throws an ArgumentException if the input contains XSS patterns.
    /// </summary>
    public static void ThrowIfXss(string? input, string paramName)
    {
        if (ContainsXssPatterns(input))
        {
            throw new ArgumentException($"Input contains potentially dangerous XSS patterns: {paramName}", paramName);
        }
    }

    /// <summary>
    /// Throws an ArgumentException if the input is not safe.
    /// </summary>
    public static void ThrowIfUnsafeInput(string? input, string paramName)
    {
        if (!IsSafeInput(input))
        {
            throw new ArgumentException($"Input contains potentially dangerous patterns: {paramName}", paramName);
        }
    }
}

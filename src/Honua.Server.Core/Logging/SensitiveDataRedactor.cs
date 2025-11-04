// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Logging;

/// <summary>
/// Utility class to redact sensitive data from log messages and connection strings.
/// </summary>
public static class SensitiveDataRedactor
{
    private static readonly Regex PasswordPattern = new(@"(password|pwd|passwd)\s*=\s*[^;]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ConnectionStringPattern = new(@"(User\s*Id|UID)\s*=\s*[^;]+;.*?(Password|PWD)\s*=\s*[^;]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ApiKeyPattern = new(@"(api[_-]?key|apikey|access[_-]?token|secret[_-]?key)\s*[:=]\s*[^\s&;]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AuthHeaderPattern = new(@"(Authorization|Bearer)\s*:\s*[^\s,]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AwsKeyPattern = new(@"(AKIA|A3T|AGPA|AIDA|AROA|AIPA|ANPA|ANVA|ASIA)[A-Z0-9]{16}", RegexOptions.Compiled);

    /// <summary>
    /// Redacts sensitive information from a string, typically used for logging.
    /// </summary>
    /// <param name="input">The input string that may contain sensitive data.</param>
    /// <returns>The input string with sensitive data redacted.</returns>
    public static string Redact(string? input)
    {
        if (input.IsNullOrEmpty())
        {
            return input ?? string.Empty;
        }

        var result = input;

        // Redact passwords in connection strings
        result = PasswordPattern.Replace(result, "$1=***REDACTED***");

        // Redact complete connection string credentials
        result = ConnectionStringPattern.Replace(result, m =>
        {
            var groups = m.Groups;
            return $"{groups[1].Value}=***REDACTED***;{groups[2].Value}=***REDACTED***";
        });

        // Redact API keys and tokens
        result = ApiKeyPattern.Replace(result, "$1=***REDACTED***");

        // Redact authorization headers
        result = AuthHeaderPattern.Replace(result, "$1: ***REDACTED***");

        // Redact AWS access keys
        result = AwsKeyPattern.Replace(result, "***REDACTED_AWS_KEY***");

        return result;
    }

    /// <summary>
    /// Redacts password from a connection string while preserving other information.
    /// </summary>
    /// <param name="connectionString">The connection string to redact.</param>
    /// <returns>Connection string with password redacted.</returns>
    public static string RedactConnectionString(string? connectionString)
    {
        if (connectionString.IsNullOrEmpty())
        {
            return connectionString ?? string.Empty;
        }

        return PasswordPattern.Replace(connectionString, "$1=***REDACTED***");
    }

    /// <summary>
    /// Extracts safe database name from connection string for logging.
    /// PERFORMANCE: Uses cached compiled patterns to avoid repeated compilation.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>Database name or "unknown" if not found.</returns>
    public static string GetSafeDatabaseName(string? connectionString)
    {
        if (connectionString.IsNullOrEmpty())
        {
            return "unknown";
        }

        var patterns = new[]
        {
            RegexCache.GetOrAdd(@"Database\s*=\s*([^;]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            RegexCache.GetOrAdd(@"Initial\s*Catalog\s*=\s*([^;]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            RegexCache.GetOrAdd(@"Data\s*Source\s*=\s*([^;]+\.db)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        foreach (var pattern in patterns)
        {
            var match = pattern.Match(connectionString);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return "unknown";
    }
}

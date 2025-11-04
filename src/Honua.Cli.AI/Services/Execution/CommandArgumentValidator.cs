// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Execution;

/// <summary>
/// Provides validation for command arguments to prevent command injection attacks
/// </summary>
public static class CommandArgumentValidator
{
    // Shell metacharacters that could be used for command injection
    private static readonly char[] ShellMetacharacters =
    {
        ';', '&', '|', '`', '$', '(', ')', '<', '>', '\n', '\r', '\\', '"', '\'', '*', '?', '[', ']', '{', '}', '~', '#'
    };

    // Pattern for safe identifiers (alphanumeric, dash, underscore, dot)
    private static readonly Regex SafeIdentifierPattern = new Regex(@"^[a-zA-Z0-9_\.\-]+$", RegexOptions.Compiled);

    // Pattern for safe paths (alphanumeric, dash, underscore, dot, slash)
    private static readonly Regex SafePathPattern = new Regex(@"^[a-zA-Z0-9_\.\-/]+$", RegexOptions.Compiled);

    // Pattern for safe database names (alphanumeric, underscore)
    private static readonly Regex SafeDatabaseNamePattern = new Regex(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

    /// <summary>
    /// Validates that a string is a safe identifier (no shell metacharacters)
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="parameterName">Parameter name for error messages</param>
    /// <exception cref="ArgumentException">Thrown if validation fails</exception>
    public static void ValidateIdentifier(string value, string parameterName)
    {
        if (value.IsNullOrWhiteSpace())
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);

        // Check for shell metacharacters first (most critical security check)
        if (ContainsShellMetacharacters(value))
            throw new ArgumentException(
                $"{parameterName} contains shell metacharacters which are not allowed for security reasons.",
                parameterName);

        // Then validate the pattern
        if (!SafeIdentifierPattern.IsMatch(value))
            throw new ArgumentException(
                $"{parameterName} contains invalid characters. Only alphanumeric, dash, underscore, and dot are allowed.",
                parameterName);
    }

    /// <summary>
    /// Validates that a string is a safe path (no shell metacharacters)
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="parameterName">Parameter name for error messages</param>
    /// <exception cref="ArgumentException">Thrown if validation fails</exception>
    public static void ValidatePath(string value, string parameterName)
    {
        if (value.IsNullOrWhiteSpace())
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);

        // Check for path traversal attempts first
        if (value.Contains(".."))
            throw new ArgumentException(
                $"{parameterName} contains path traversal sequence '..' which is not allowed.",
                parameterName);

        // Check for shell metacharacters (most critical security check)
        if (ContainsShellMetacharacters(value))
            throw new ArgumentException(
                $"{parameterName} contains shell metacharacters which are not allowed for security reasons.",
                parameterName);

        // Then validate the pattern
        if (!SafePathPattern.IsMatch(value))
            throw new ArgumentException(
                $"{parameterName} contains invalid characters. Only alphanumeric, dash, underscore, dot, and forward slash are allowed.",
                parameterName);
    }

    /// <summary>
    /// Validates that a string is a safe database name
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="parameterName">Parameter name for error messages</param>
    /// <exception cref="ArgumentException">Thrown if validation fails</exception>
    public static void ValidateDatabaseName(string value, string parameterName)
    {
        if (value.IsNullOrWhiteSpace())
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);

        if (!SafeDatabaseNamePattern.IsMatch(value))
            throw new ArgumentException(
                $"{parameterName} contains invalid characters. Only alphanumeric and underscore are allowed for database names.",
                parameterName);

        if (value.Length > 63) // PostgreSQL limit
            throw new ArgumentException(
                $"{parameterName} exceeds maximum length of 63 characters.",
                parameterName);
    }

    /// <summary>
    /// Checks if a string contains shell metacharacters
    /// </summary>
    private static bool ContainsShellMetacharacters(string value)
    {
        if (value.IsNullOrEmpty())
            return false;

        return value.IndexOfAny(ShellMetacharacters) >= 0;
    }

    /// <summary>
    /// Validates SQL to prevent basic injection attempts
    /// Note: This should be used alongside proper parameterized queries or database libraries
    /// </summary>
    /// <param name="sql">SQL to validate</param>
    /// <param name="parameterName">Parameter name for error messages</param>
    public static void ValidateSQL(string sql, string parameterName)
    {
        if (sql.IsNullOrWhiteSpace())
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);

        // Check for shell metacharacters that could be used for command injection
        // Even with ArgumentList, these characters should not appear in SQL for security
        var dangerousShellChars = new[] { '`', '$', '|', '&', '\n', '\r', '<', '>', ';' };
        foreach (var c in dangerousShellChars)
        {
            if (sql.Contains(c))
                throw new ArgumentException(
                    $"{parameterName} contains shell metacharacter '{c}' which could be used for command injection",
                    parameterName);
        }

        // Check for common SQL injection patterns
        var dangerousPatterns = new[]
        {
            @";\s*DROP\s+",
            @";\s*DELETE\s+FROM\s+",
            @";\s*UPDATE\s+.*\s+SET\s+",
            @";\s*INSERT\s+INTO\s+",
            @"\bEXEC\s*\(",
            @"\bEXECUTE\s*\(",
            @"--.*",  // SQL comments
            @"/\*.*\*/",  // Multi-line comments
            @"\bxp_cmdshell\b",
            @"\bsp_executesql\b"
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase))
                throw new ArgumentException(
                    $"{parameterName} contains potentially dangerous SQL pattern: {pattern}",
                    parameterName);
        }
    }

    /// <summary>
    /// Validates that SQL is a safe DDL statement (CREATE, ALTER, DROP for specific objects)
    /// Uses allowlist approach for maximum security
    /// </summary>
    /// <param name="sql">SQL to validate</param>
    /// <param name="parameterName">Parameter name for error messages</param>
    /// <exception cref="ArgumentException">Thrown if SQL is not a valid DDL statement</exception>
    public static void ValidateDDLStatement(string sql, string parameterName)
    {
        if (sql.IsNullOrWhiteSpace())
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);

        // First run the general SQL validation
        ValidateSQL(sql, parameterName);

        // Normalize for pattern matching
        var normalized = sql.Trim().ToUpperInvariant();

        // Remove extra whitespace for pattern matching
        normalized = Regex.Replace(normalized, @"\s+", " ");

        // Allowlist of safe DDL patterns
        var allowedPatterns = new[]
        {
            @"^CREATE DATABASE\s+[a-zA-Z0-9_]+$",
            @"^CREATE EXTENSION\s+(IF\s+NOT\s+EXISTS\s+)?[a-zA-Z0-9_]+$",
            @"^CREATE TABLE\s+[a-zA-Z0-9_\.]+\s*\(",
            @"^CREATE INDEX\s+[a-zA-Z0-9_]+\s+ON\s+[a-zA-Z0-9_\.]+",
            @"^ALTER TABLE\s+[a-zA-Z0-9_\.]+\s+(ADD|DROP|ALTER|RENAME)",
            @"^DROP TABLE\s+(IF\s+EXISTS\s+)?[a-zA-Z0-9_\.]+$",
            @"^DROP INDEX\s+(IF\s+EXISTS\s+)?[a-zA-Z0-9_\.]+$",
            @"^SELECT\s+", // Allow SELECT for queries like PostGIS_version()
            @"^\\C\s+[a-zA-Z0-9_]+$" // psql meta-command to connect to database
        };

        foreach (var pattern in allowedPatterns)
        {
            if (Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase))
                return; // Valid DDL statement
        }

        throw new ArgumentException(
            $"{parameterName} is not a recognized safe DDL statement. Only CREATE DATABASE, CREATE EXTENSION, CREATE TABLE, CREATE INDEX, ALTER TABLE, DROP TABLE, DROP INDEX, and SELECT queries are allowed.",
            parameterName);
    }

    /// <summary>
    /// Validates a connection string format
    /// </summary>
    public static void ValidateConnectionString(string connectionString, string parameterName)
    {
        if (connectionString.IsNullOrWhiteSpace())
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);

        // Check if it's a URL-style connection string (postgres://, mysql://, etc.)
        var isUrlStyle = connectionString.Contains("://");

        // Block shell metacharacters that could be used for injection
        // For URL-style, also block semicolons as they're not valid in URLs
        // For key-value style (e.g., "Host=localhost;Database=test"), semicolons are valid separators
        var dangerousChars = isUrlStyle
            ? new[] { ';', '`', '$', '|', '&', '<', '>', '\n', '\r' }
            : new[] { '`', '$', '|', '&', '<', '>', '\n', '\r' };

        foreach (var c in dangerousChars)
        {
            if (connectionString.Contains(c))
                throw new ArgumentException(
                    $"{parameterName} contains potentially dangerous character: '{c}'",
                    parameterName);
        }
    }

    /// <summary>
    /// Validates a Docker container name or ID
    /// </summary>
    public static void ValidateContainerName(string containerName, string parameterName)
    {
        if (containerName.IsNullOrWhiteSpace())
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);

        // Container names can contain alphanumeric characters, underscores, periods, and hyphens
        // Container IDs are hexadecimal strings
        var validPattern = new Regex(@"^[a-zA-Z0-9_\.\-]+$", RegexOptions.Compiled);

        if (!validPattern.IsMatch(containerName))
            throw new ArgumentException(
                $"{parameterName} contains invalid characters. Container names can only contain alphanumeric characters, underscores, periods, and hyphens.",
                parameterName);

        // Check for shell metacharacters as additional security layer
        if (ContainsShellMetacharacters(containerName))
            throw new ArgumentException(
                $"{parameterName} contains shell metacharacters which are not allowed for security reasons.",
                parameterName);
    }
}

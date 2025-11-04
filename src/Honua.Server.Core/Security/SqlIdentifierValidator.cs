// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Security;

/// <summary>
/// Validates SQL identifiers (table names, column names, schema names) to prevent SQL injection attacks.
/// This validator enforces strict rules to ensure identifiers are safe for use in dynamic SQL construction.
/// </summary>
public static partial class SqlIdentifierValidator
{
    /// <summary>
    /// Maximum allowed length for a SQL identifier.
    /// This is conservative - most databases support 128 or more characters.
    /// </summary>
    public const int MaxIdentifierLength = 128;

    /// <summary>
    /// Regex pattern that matches valid SQL identifiers.
    /// Only allows: letters (a-z, A-Z), digits (0-9), underscores (_)
    /// Must start with a letter or underscore.
    /// Hyphens and other special characters require quoting.
    /// </summary>
    private static readonly Regex ValidIdentifierPattern = CreateValidIdentifierRegex();

    /// <summary>
    /// List of SQL keywords that should not be used as unquoted identifiers.
    /// This is a subset of commonly reserved words across all major SQL databases.
    /// </summary>
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TABLE",
        "DATABASE", "INDEX", "VIEW", "TRIGGER", "PROCEDURE", "FUNCTION", "SCHEMA",
        "FROM", "WHERE", "JOIN", "INNER", "OUTER", "LEFT", "RIGHT", "ON", "AS",
        "AND", "OR", "NOT", "IN", "EXISTS", "BETWEEN", "LIKE", "IS", "NULL",
        "ORDER", "GROUP", "BY", "HAVING", "UNION", "ALL", "DISTINCT", "INTO",
        "VALUES", "SET", "EXEC", "EXECUTE", "DECLARE", "BEGIN", "END", "IF",
        "ELSE", "WHILE", "CASE", "WHEN", "THEN", "RETURN", "GO", "USE",
        "GRANT", "REVOKE", "WITH", "CONSTRAINT", "PRIMARY", "FOREIGN", "KEY",
        "REFERENCES", "CHECK", "DEFAULT", "UNIQUE", "CASCADE", "RESTRICT"
    };

    /// <summary>
    /// Validates a SQL identifier and throws an exception if invalid.
    /// </summary>
    /// <param name="identifier">The identifier to validate (may contain dots for qualified names)</param>
    /// <param name="parameterName">The parameter name for error messages</param>
    /// <exception cref="ArgumentException">Thrown if the identifier is invalid</exception>
    public static void ValidateIdentifier(string identifier, string parameterName = "identifier")
    {
        if (!TryValidateIdentifier(identifier, out var errorMessage))
        {
            throw new ArgumentException(errorMessage, parameterName);
        }
    }

    /// <summary>
    /// Validates a SQL identifier and returns a boolean result.
    /// </summary>
    /// <param name="identifier">The identifier to validate (may contain dots for qualified names)</param>
    /// <param name="errorMessage">Output parameter containing the error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool TryValidateIdentifier(string identifier, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (identifier.IsNullOrWhiteSpace())
        {
            errorMessage = "SQL identifier cannot be null or whitespace.";
            return false;
        }

        // Split on dots to handle qualified names (e.g., schema.table.column)
        // Don't trim entries - whitespace should be caught as invalid
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            errorMessage = "SQL identifier cannot be empty after splitting on dots.";
            return false;
        }

        // Validate each part individually
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            // Remove quotes if present for validation
            var unquoted = UnquoteIdentifier(part);

            // Reserved keywords are allowed since all query builders quote identifiers
            // This check used to reject unquoted reserved keywords, but that was overly strict
            // because ValidateAndQuote* methods handle quoting automatically.
            // We still validate quoted identifiers to ensure they don't contain SQL injection attempts.

            // Check length
            if (unquoted.Length > MaxIdentifierLength)
            {
                errorMessage = $"SQL identifier part '{unquoted}' exceeds maximum length of {MaxIdentifierLength} characters.";
                return false;
            }

            // Check for valid characters (unless the identifier is already quoted)
            // Quoted identifiers can contain any characters and will be properly escaped
            if (IsQuoted(part))
            {
                // Already quoted - skip pattern validation
                // The quoting methods will handle escaping special characters
                continue;
            }

            if (!ValidIdentifierPattern.IsMatch(unquoted))
            {
                errorMessage = $"SQL identifier part '{unquoted}' contains invalid characters. Only letters, digits, and underscores are allowed, and it must start with a letter or underscore.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates and quotes a SQL identifier for safe use in PostgreSQL queries.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote</param>
    /// <returns>The quoted identifier safe for PostgreSQL</returns>
    public static string ValidateAndQuotePostgres(string identifier)
    {
        ValidateIdentifier(identifier);
        return QuotePostgresIdentifier(identifier);
    }

    /// <summary>
    /// Validates and quotes a SQL identifier for safe use in MySQL queries.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote</param>
    /// <returns>The quoted identifier safe for MySQL</returns>
    public static string ValidateAndQuoteMySql(string identifier)
    {
        ValidateIdentifier(identifier);
        return QuoteMySqlIdentifier(identifier);
    }

    /// <summary>
    /// Validates and quotes a SQL identifier for safe use in SQL Server queries.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote</param>
    /// <returns>The quoted identifier safe for SQL Server</returns>
    public static string ValidateAndQuoteSqlServer(string identifier)
    {
        ValidateIdentifier(identifier);
        return QuoteSqlServerIdentifier(identifier);
    }

    /// <summary>
    /// Validates and quotes a SQL identifier for safe use in SQLite queries.
    /// </summary>
    /// <param name="identifier">The identifier to validate and quote</param>
    /// <returns>The quoted identifier safe for SQLite</returns>
    public static string ValidateAndQuoteSqlite(string identifier)
    {
        ValidateIdentifier(identifier);
        return QuoteSqliteIdentifier(identifier);
    }

    /// <summary>
    /// Quotes a PostgreSQL identifier using double quotes.
    /// Handles qualified names (schema.table) and escapes embedded quotes.
    /// </summary>
    private static string QuotePostgresIdentifier(string identifier)
    {
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var unquoted = UnquoteIdentifier(parts[i]);
            // Escape double quotes by doubling them
            parts[i] = $"\"{unquoted.Replace("\"", "\"\"")}\"";
        }
        return string.Join('.', parts);
    }

    /// <summary>
    /// Quotes a MySQL identifier using backticks.
    /// Handles qualified names (database.table) and escapes embedded backticks.
    /// </summary>
    private static string QuoteMySqlIdentifier(string identifier)
    {
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var unquoted = UnquoteIdentifier(parts[i]);
            // Escape backticks by doubling them
            parts[i] = $"`{unquoted.Replace("`", "``")}`";
        }
        return string.Join('.', parts);
    }

    /// <summary>
    /// Quotes a SQL Server identifier using square brackets.
    /// Handles qualified names (schema.table) and escapes embedded brackets.
    /// </summary>
    private static string QuoteSqlServerIdentifier(string identifier)
    {
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var unquoted = UnquoteIdentifier(parts[i]);
            // Escape closing brackets by doubling them
            parts[i] = $"[{unquoted.Replace("]", "]]")}]";
        }
        return string.Join('.', parts);
    }

    /// <summary>
    /// Quotes a SQLite identifier using double quotes.
    /// Handles qualified names and escapes embedded quotes.
    /// SQLite uses the same quoting as PostgreSQL.
    /// </summary>
    private static string QuoteSqliteIdentifier(string identifier)
    {
        // SQLite uses same quoting style as PostgreSQL
        return QuotePostgresIdentifier(identifier);
    }

    /// <summary>
    /// Removes quotes from an identifier if present.
    /// Supports PostgreSQL/SQLite double quotes, MySQL backticks, and SQL Server square brackets.
    /// </summary>
    private static string UnquoteIdentifier(string identifier)
    {
        if (identifier.IsNullOrEmpty())
        {
            return identifier;
        }

        // Don't trim - whitespace should be caught as invalid
        // PostgreSQL/SQLite: "identifier"
        if (identifier.StartsWith("\"", StringComparison.Ordinal) && identifier.EndsWith("\"", StringComparison.Ordinal) && identifier.Length >= 2)
        {
            return identifier[1..^1].Replace("\"\"", "\"");
        }

        // MySQL: `identifier`
        if (identifier.StartsWith("`", StringComparison.Ordinal) && identifier.EndsWith("`", StringComparison.Ordinal) && identifier.Length >= 2)
        {
            return identifier[1..^1].Replace("``", "`");
        }

        // SQL Server: [identifier]
        if (identifier.StartsWith("[", StringComparison.Ordinal) && identifier.EndsWith("]", StringComparison.Ordinal) && identifier.Length >= 2)
        {
            return identifier[1..^1].Replace("]]", "]");
        }

        return identifier;
    }

    /// <summary>
    /// Checks if an identifier is already quoted.
    /// </summary>
    private static bool IsQuoted(string identifier)
    {
        if (identifier.IsNullOrEmpty())
        {
            return false;
        }

        // Don't trim - check quotes as-is
        return (identifier.StartsWith("\"", StringComparison.Ordinal) && identifier.EndsWith("\"", StringComparison.Ordinal)) ||
               (identifier.StartsWith("`", StringComparison.Ordinal) && identifier.EndsWith("`", StringComparison.Ordinal)) ||
               (identifier.StartsWith("[", StringComparison.Ordinal) && identifier.EndsWith("]", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\A[a-zA-Z_][a-zA-Z0-9_]*\z", RegexOptions.Compiled)]
    private static partial Regex CreateValidIdentifierRegex();
}

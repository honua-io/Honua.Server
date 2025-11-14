// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates SQL queries for security concerns, primarily SQL injection prevention.
/// </summary>
internal static class SqlSecurityValidator
{
    /// <summary>
    /// Validates a SQL query for security issues.
    /// </summary>
    /// <param name="layerId">The layer ID (for error messages).</param>
    /// <param name="sql">The SQL query to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when SQL validation fails.</exception>
    public static void ValidateSql(string layerId, string sql)
    {
        if (sql.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layerId}' SQL view must have a non-empty SQL query.");
        }

        var sqlTrimmed = sql.Trim();
        var sqlLower = sqlTrimmed.ToLowerInvariant();

        // Must be a SELECT statement
        if (!sqlLower.StartsWith("select"))
        {
            throw new InvalidDataException($"Layer '{layerId}' SQL view must start with SELECT. Only SELECT queries are allowed.");
        }

        // Check for dangerous SQL keywords that could modify data or structure
        var dangerousKeywords = new[]
        {
            "drop ", "truncate ", "alter ", "create ", "insert ", "update ", "delete ",
            "exec ", "execute ", "xp_", "sp_", "grant ", "revoke ", "commit ", "rollback ",
            "begin ", "end;", "declare ", "set ", "use ", "shutdown ", "backup ", "restore "
        };

        foreach (var keyword in dangerousKeywords)
        {
            if (sqlLower.Contains(keyword))
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view contains potentially dangerous keyword '{keyword.Trim()}'. Only SELECT queries are allowed.");
            }
        }

        // Check for SQL comment patterns that could be used for SQL injection
        if (sqlLower.Contains("--") || sqlLower.Contains("/*"))
        {
            throw new InvalidDataException($"Layer '{layerId}' SQL view contains SQL comments which are not allowed for security reasons.");
        }
    }
}

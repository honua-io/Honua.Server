// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Data.Query;

/// <summary>
/// Provides common functionality for building SQL aggregate expressions across all database providers.
/// Extracted from duplicate implementations in PostgreSQL, MySQL, SQL Server, SQLite, and Oracle query builders.
/// </summary>
/// <remarks>
/// This utility consolidates 100% identical aggregate expression building logic that was duplicated
/// across all 5+ query builder implementations, reducing code duplication and maintenance burden.
/// </remarks>
public static class AggregateExpressionBuilder
{
    /// <summary>
    /// Builds a SQL aggregate expression (COUNT, SUM, AVG, MIN, MAX) from a statistic definition.
    /// </summary>
    /// <param name="statistic">The statistic definition specifying the type and field</param>
    /// <param name="alias">The table alias to use in the field reference</param>
    /// <param name="quoteIdentifier">Function to quote identifiers for the specific database provider</param>
    /// <returns>A SQL aggregate expression string</returns>
    /// <exception cref="ArgumentNullException">Thrown when statistic or quoteIdentifier is null</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the statistic type is not supported or when a field name is required but not provided
    /// </exception>
    public static string Build(
        StatisticDefinition statistic,
        string alias,
        Func<string, string> quoteIdentifier)
    {
        if (statistic is null)
        {
            throw new ArgumentNullException(nameof(statistic));
        }

        if (quoteIdentifier is null)
        {
            throw new ArgumentNullException(nameof(quoteIdentifier));
        }

        var fieldReference = statistic.FieldName.IsNullOrWhiteSpace()
            ? null
            : $"{alias}.{quoteIdentifier(statistic.FieldName)}";

        return statistic.Type switch
        {
            StatisticType.Count => "COUNT(*)",
            StatisticType.Sum => EnsureAggregateField("SUM", fieldReference, statistic.Type),
            StatisticType.Avg => EnsureAggregateField("AVG", fieldReference, statistic.Type),
            StatisticType.Min => EnsureAggregateField("MIN", fieldReference, statistic.Type),
            StatisticType.Max => EnsureAggregateField("MAX", fieldReference, statistic.Type),
            _ => throw new NotSupportedException($"Statistic type '{statistic.Type}' is not supported.")
        };
    }

    /// <summary>
    /// Ensures that a field reference is provided for aggregate functions that require one.
    /// </summary>
    /// <param name="functionName">The SQL aggregate function name (SUM, AVG, MIN, MAX)</param>
    /// <param name="fieldReference">The field reference to aggregate</param>
    /// <param name="type">The statistic type for error reporting</param>
    /// <returns>A SQL aggregate function call</returns>
    /// <exception cref="NotSupportedException">Thrown when field reference is null or whitespace</exception>
    private static string EnsureAggregateField(string functionName, string? fieldReference, StatisticType type)
    {
        if (fieldReference.IsNullOrWhiteSpace())
        {
            throw new NotSupportedException($"Statistic type '{type}' requires a field name.");
        }

        return $"{functionName}({fieldReference})";
    }
}

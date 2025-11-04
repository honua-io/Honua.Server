// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Honua.Server.Core.Security;

namespace Honua.Server.Host.Carto;

internal sealed class CartoSqlQueryParser
{
    private static readonly Regex SelectRegex = new(
        @"^\s*SELECT\s+(?<select>.+?)\s+FROM\s+(?<from>\S+)(?<tail>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TailRegex = new(
        @"^\s*
            (?:WHERE\s+(?<where>.*?)(?=(GROUP\s+BY|ORDER\s+BY|LIMIT|OFFSET|$)))?\s*
            (?:GROUP\s+BY\s+(?<group>.*?)(?=(ORDER\s+BY|LIMIT|OFFSET|$)))?\s*
            (?:ORDER\s+BY\s+(?<order>.*?)(?=(LIMIT|OFFSET|$)))?\s*
            (?:LIMIT\s+(?<limit>\d+))?\s*
            (?:OFFSET\s+(?<offset>\d+))?\s*
            $",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

    public bool TryParse(string sql, out CartoSqlQueryDefinition query, out string? error)
    {
        query = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(sql))
        {
            error = "SQL query must be provided.";
            return false;
        }

        var normalized = NormalizeSql(sql);
        var match = SelectRegex.Match(normalized);
        if (!match.Success)
        {
            error = "Only simple SELECT statements are supported.";
            return false;
        }

        var datasetToken = match.Groups["from"].Value.Trim();
        var datasetIdentifierToken = TrimIdentifier(datasetToken);
        if (!CartoDatasetResolver.TryParseDatasetId(datasetIdentifierToken.Value, out var serviceId, out var layerId))
        {
            error = "FROM clause must reference a dataset using the pattern serviceId.layerId.";
            return false;
        }

        var selectClause = match.Groups["select"].Value.Trim();
        var tail = match.Groups["tail"].Value;

        var (
            isCount,
            isDistinct,
            projections,
            aggregates,
            countAlias,
            selectError) = ParseSelectClause(selectClause);
        if (selectError is not null)
        {
            error = selectError;
            return false;
        }

        var (whereClause, groupBy, orderings, limit, offset, tailError) = ParseTail(tail);
        if (tailError is not null)
        {
            error = tailError;
            return false;
        }

        if (groupBy.Count > 0)
        {
            if (aggregates.Count == 0)
            {
                error = "GROUP BY clause requires at least one aggregate expression.";
                return false;
            }

            foreach (var projection in projections)
            {
                if (!groupBy.Contains(projection.Source, StringComparer.OrdinalIgnoreCase))
                {
                    error = $"Column '{projection.Source}' must appear in the GROUP BY clause.";
                    return false;
                }
            }
        }
        else if (aggregates.Count > 0 && projections.Count > 0 && !isCount)
        {
            error = "Aggregate queries that project non-aggregated columns must include a GROUP BY clause.";
            return false;
        }

        query = new CartoSqlQueryDefinition(
            CartoDatasetResolver.BuildDatasetId(serviceId, layerId),
            isCount,
            isDistinct,
            projections,
            aggregates,
            groupBy,
            limit,
            offset,
            countAlias,
            whereClause,
            orderings);
        return true;
    }

    private static string NormalizeSql(string sql)
    {
        var normalized = sql.Trim();
        if (normalized.EndsWith(';'))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private static (bool IsCountOnly, bool IsDistinct, IReadOnlyList<CartoSqlProjection> Projections, IReadOnlyList<CartoSqlAggregateDefinition> Aggregates, string? CountAlias, string? Error) ParseSelectClause(string selectClause)
    {
        if (string.IsNullOrWhiteSpace(selectClause))
        {
            return (false, false, Array.Empty<CartoSqlProjection>(), Array.Empty<CartoSqlAggregateDefinition>(), null, "SELECT clause cannot be empty.");
        }

        var text = selectClause.Trim();
        var isDistinct = false;
        if (text.StartsWith("distinct", StringComparison.OrdinalIgnoreCase))
        {
            var remainder = text[8..];
            if (remainder.Length == 0 || char.IsWhiteSpace(remainder[0]))
            {
                isDistinct = true;
                text = remainder.TrimStart();
            }
        }

        if (string.Equals(text, "*", StringComparison.Ordinal))
        {
            return (false, isDistinct, Array.Empty<CartoSqlProjection>(), Array.Empty<CartoSqlAggregateDefinition>(), null, null);
        }

        var segments = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return (false, isDistinct, Array.Empty<CartoSqlProjection>(), Array.Empty<CartoSqlAggregateDefinition>(), null, "SELECT clause cannot be empty.");
        }

        var projections = new List<CartoSqlProjection>(segments.Length);
        var aggregates = new List<CartoSqlAggregateDefinition>();
        string? countAlias = null;

        foreach (var rawSegment in segments)
        {
            var segment = rawSegment.Trim();
            if (segment.Length == 0)
            {
                return (false, isDistinct, Array.Empty<CartoSqlProjection>(), Array.Empty<CartoSqlAggregateDefinition>(), null, "Column selection cannot be empty.");
            }

            if (TryParseAggregate(segment, out var aggregate, out var aggregateError))
            {
                if (aggregateError is not null)
                {
                    return (false, isDistinct, Array.Empty<CartoSqlProjection>(), Array.Empty<CartoSqlAggregateDefinition>(), null, aggregateError);
                }

                aggregates.Add(aggregate!);
                if (aggregate!.Function == CartoSqlAggregateFunction.Count && aggregate.TargetField is null)
                {
                    countAlias = aggregate.OutputName;
                }

                continue;
            }

            if (!TryParseProjection(segment, out var projection, out var projectionError))
            {
                return (false, isDistinct, Array.Empty<CartoSqlProjection>(), Array.Empty<CartoSqlAggregateDefinition>(), null, projectionError);
            }

            projections.Add(projection);
        }

        var isCountOnly = aggregates.Count == 1 && projections.Count == 0 && aggregates[0].Function == CartoSqlAggregateFunction.Count && aggregates[0].TargetField is null;
        return (isCountOnly, isDistinct, projections, aggregates, countAlias, null);
    }

    private static bool TryParseProjection(string segment, out CartoSqlProjection projection, out string? error)
    {
        projection = null!;
        error = null;

        var trimmed = segment.Trim();
        var columnPart = trimmed;
        string? aliasPart = null;

        var asIndex = IndexOfAsKeyword(trimmed);
        if (asIndex >= 0)
        {
            columnPart = trimmed[..asIndex].Trim();
            aliasPart = trimmed[(asIndex + 3)..].Trim();
        }
        else
        {
            var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 1)
            {
                columnPart = tokens[0];
                aliasPart = tokens[^1];
            }
        }

        var columnIdentifier = ExtractIdentifier(columnPart);
        if (!columnIdentifier.IsQuoted && !IsValidIdentifier(columnIdentifier.Value))
        {
            error = $"Column '{columnIdentifier.Value}' is not supported.";
            return false;
        }

        // Validate identifier to prevent SQL injection
        if (!SqlIdentifierValidator.TryValidateIdentifier(columnIdentifier.Value, out var validationError))
        {
            error = $"Invalid column identifier: {validationError}";
            return false;
        }

        var aliasIdentifier = aliasPart is null ? columnIdentifier : ExtractIdentifier(aliasPart);
        if (aliasIdentifier.Value.Length == 0)
        {
            aliasIdentifier = columnIdentifier;
        }

        projection = new CartoSqlProjection(columnIdentifier.Value, aliasIdentifier.Value, aliasIdentifier.IsQuoted);
        return true;
    }

    private static bool TryParseAggregate(string segment, out CartoSqlAggregateDefinition? aggregate, out string? error)
    {
        aggregate = null;
        error = null;

        var match = Regex.Match(
            segment,
            @"^(?<func>[A-Za-z]+)\s*\(\s*(?<target>\*|[^\)]+)\s*\)\s*(?:as\s+(?<alias>.+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return false;
        }

        var functionName = match.Groups["func"].Value.Trim();
        if (functionName.Length == 0)
        {
            error = "Aggregate function name is required.";
            return true;
        }

        var targetToken = match.Groups["target"].Value.Trim();
        var aliasToken = match.Groups["alias"].Success ? match.Groups["alias"].Value : null;

        var function = ParseAggregateFunction(functionName, out var functionError);
        if (functionError is not null)
        {
            error = functionError;
            return true;
        }

        string? targetField = null;
        switch (function)
        {
            case CartoSqlAggregateFunction.Count:
                if (!string.Equals(targetToken, "*", StringComparison.Ordinal))
                {
                    var identifier = ExtractIdentifier(targetToken);
                    if (!identifier.IsQuoted && !IsValidIdentifier(identifier.Value))
                    {
                        error = $"COUNT target '{identifier.Value}' is not supported.";
                        return true;
                    }

                    // Validate identifier to prevent SQL injection
                    if (!SqlIdentifierValidator.TryValidateIdentifier(identifier.Value, out var identifierError))
                    {
                        error = $"Invalid COUNT target identifier: {identifierError}";
                        return true;
                    }

                    targetField = identifier.Value;
                }
                break;
            case CartoSqlAggregateFunction.Sum:
            case CartoSqlAggregateFunction.Avg:
            case CartoSqlAggregateFunction.Min:
            case CartoSqlAggregateFunction.Max:
                if (string.Equals(targetToken, "*", StringComparison.Ordinal))
                {
                    error = $"{function.ToString().ToUpperInvariant()} requires a column reference.";
                    return true;
                }

                var targetIdentifier = ExtractIdentifier(targetToken);
                if (!targetIdentifier.IsQuoted && !IsValidIdentifier(targetIdentifier.Value))
                {
                    error = $"Aggregate column '{targetIdentifier.Value}' is not supported.";
                    return true;
                }

                // Validate identifier to prevent SQL injection
                if (!SqlIdentifierValidator.TryValidateIdentifier(targetIdentifier.Value, out var targetError))
                {
                    error = $"Invalid aggregate column identifier: {targetError}";
                    return true;
                }

                targetField = targetIdentifier.Value;
                break;
        }

        string outputName;
        if (!string.IsNullOrWhiteSpace(aliasToken))
        {
            var aliasIdentifier = ExtractIdentifier(aliasToken!);
            if (aliasIdentifier.Value.Length == 0)
            {
                error = "Aggregate alias cannot be empty.";
                return true;
            }

            outputName = aliasIdentifier.Value;
        }
        else
        {
            outputName = function switch
            {
                CartoSqlAggregateFunction.Count => targetField is null ? "count" : $"count_{targetField}",
                CartoSqlAggregateFunction.Sum => $"sum_{targetField}",
                CartoSqlAggregateFunction.Avg => $"avg_{targetField}",
                CartoSqlAggregateFunction.Min => $"min_{targetField}",
                CartoSqlAggregateFunction.Max => $"max_{targetField}",
                _ => "aggregate"
            };
        }

        aggregate = new CartoSqlAggregateDefinition(function, outputName, targetField);
        return true;
    }

    private static CartoSqlAggregateFunction ParseAggregateFunction(string name, out string? error)
    {
        error = null;

        if (string.Equals(name, "count", StringComparison.OrdinalIgnoreCase))
        {
            return CartoSqlAggregateFunction.Count;
        }

        if (string.Equals(name, "sum", StringComparison.OrdinalIgnoreCase))
        {
            return CartoSqlAggregateFunction.Sum;
        }

        if (string.Equals(name, "avg", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "average", StringComparison.OrdinalIgnoreCase))
        {
            return CartoSqlAggregateFunction.Avg;
        }

        if (string.Equals(name, "min", StringComparison.OrdinalIgnoreCase))
        {
            return CartoSqlAggregateFunction.Min;
        }

        if (string.Equals(name, "max", StringComparison.OrdinalIgnoreCase))
        {
            return CartoSqlAggregateFunction.Max;
        }

        error = $"Aggregate function '{name}' is not supported.";
        return CartoSqlAggregateFunction.Count;
    }

    private readonly struct IdentifierToken
    {
        public IdentifierToken(string value, bool isQuoted)
        {
            Value = value;
            IsQuoted = isQuoted;
        }

        public string Value { get; }
        public bool IsQuoted { get; }
    }

    private static IdentifierToken ExtractIdentifier(string token)
    {
        var trimmed = token.Trim();
        return TrimIdentifier(trimmed);
    }

    private static IdentifierToken TrimIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return new IdentifierToken(string.Empty, false);
        }

        var trimmed = identifier.Trim();
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
        {
            return new IdentifierToken(trimmed[1..^1], true);
        }

        return new IdentifierToken(trimmed, false);
    }

    private static bool IsValidIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        foreach (var ch in identifier)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '.')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static int IndexOfAsKeyword(string text)
    {
        var span = text.AsSpan();
        for (var i = 0; i < span.Length - 2; i++)
        {
            if ((span[i] == ' ' || span[i] == '\t') &&
                (span[i + 1] == 'A' || span[i + 1] == 'a') &&
                (span[i + 2] == 'S' || span[i + 2] == 's'))
            {
                var before = i;
                while (before > 0 && char.IsWhiteSpace(span[before - 1]))
                {
                    before--;
                }

                var after = i + 3;
                while (after < span.Length && char.IsWhiteSpace(span[after]))
                {
                    after++;
                }

                if (before > 0 && after < span.Length)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static (string? Where, IReadOnlyList<string> GroupBy, IReadOnlyList<CartoSqlSortDefinition> SortOrders, int? Limit, int? Offset, string? Error) ParseTail(string tail)
    {
        if (string.IsNullOrWhiteSpace(tail))
        {
            return (null, Array.Empty<string>(), Array.Empty<CartoSqlSortDefinition>(), null, null, null);
        }

        var text = tail.Trim();
        if (text.Length == 0)
        {
            return (null, Array.Empty<string>(), Array.Empty<CartoSqlSortDefinition>(), null, null, null);
        }

        var match = TailRegex.Match(text);
        if (!match.Success)
        {
            return (null, Array.Empty<string>(), Array.Empty<CartoSqlSortDefinition>(), null, null, "Unsupported SQL syntax. Only WHERE, GROUP BY, ORDER BY, LIMIT, and OFFSET clauses are supported after the FROM clause.");
        }

        string? whereClause = null;
        if (match.Groups["where"].Success)
        {
            whereClause = match.Groups["where"].Value.Trim();
        }

        IReadOnlyList<string> groupBy = Array.Empty<string>();
        if (match.Groups["group"].Success)
        {
            var groupClause = match.Groups["group"].Value.Trim();
            var (groups, groupError) = ParseGroupByClause(groupClause);
            if (groupError is not null)
            {
                return (null, Array.Empty<string>(), Array.Empty<CartoSqlSortDefinition>(), null, null, groupError);
            }

            groupBy = groups;
        }

        IReadOnlyList<CartoSqlSortDefinition> sortOrders = Array.Empty<CartoSqlSortDefinition>();
        if (match.Groups["order"].Success)
        {
            var orderClause = match.Groups["order"].Value.Trim();
            var (orders, sortError) = ParseOrderClause(orderClause);
            if (sortError is not null)
            {
                return (null, Array.Empty<string>(), Array.Empty<CartoSqlSortDefinition>(), null, null, sortError);
            }

            sortOrders = orders;
        }

        int? limit = null;
        if (match.Groups["limit"].Success)
        {
            limit = int.Parse(match.Groups["limit"].Value, CultureInfo.InvariantCulture);
        }

        int? offset = null;
        if (match.Groups["offset"].Success)
        {
            offset = int.Parse(match.Groups["offset"].Value, CultureInfo.InvariantCulture);
        }

        return (whereClause, groupBy, sortOrders, limit, offset, null);
    }

    private static (IReadOnlyList<string> Columns, string? Error) ParseGroupByClause(string clause)
    {
        var segments = clause.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return (Array.Empty<string>(), "GROUP BY clause must include at least one column.");
        }

        var columns = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            var identifier = ExtractIdentifier(segment);
            if (!identifier.IsQuoted && !IsValidIdentifier(identifier.Value))
            {
                return (Array.Empty<string>(), $"GROUP BY column '{identifier.Value}' is not supported.");
            }

            // Validate identifier to prevent SQL injection
            if (!SqlIdentifierValidator.TryValidateIdentifier(identifier.Value, out var groupError))
            {
                return (Array.Empty<string>(), $"Invalid GROUP BY column identifier: {groupError}");
            }

            columns.Add(identifier.Value);
        }

        return (columns, null);
    }

    private static (IReadOnlyList<CartoSqlSortDefinition> SortOrders, string? Error) ParseOrderClause(string clause)
    {
        var segments = clause.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return (Array.Empty<CartoSqlSortDefinition>(), "ORDER BY clause must include at least one column.");
        }

        var orders = new List<CartoSqlSortDefinition>(segments.Length);
        foreach (var segment in segments)
        {
            var descending = false;
            var identifierPart = segment.TrimEnd();

            var lastSpace = identifierPart.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                var directionCandidate = identifierPart[(lastSpace + 1)..];
                if (directionCandidate.Equals("DESC", StringComparison.OrdinalIgnoreCase))
                {
                    descending = true;
                    identifierPart = identifierPart[..lastSpace].TrimEnd();
                }
                else if (directionCandidate.Equals("ASC", StringComparison.OrdinalIgnoreCase))
                {
                    identifierPart = identifierPart[..lastSpace].TrimEnd();
                }
            }

            var identifier = ExtractIdentifier(identifierPart);
            if (!identifier.IsQuoted && !IsValidIdentifier(identifier.Value))
            {
                return (Array.Empty<CartoSqlSortDefinition>(), $"ORDER BY column '{identifier.Value}' is not supported.");
            }

            // Validate identifier to prevent SQL injection
            if (!SqlIdentifierValidator.TryValidateIdentifier(identifier.Value, out var orderError))
            {
                return (Array.Empty<CartoSqlSortDefinition>(), $"Invalid ORDER BY column identifier: {orderError}");
            }

            orders.Add(new CartoSqlSortDefinition(identifier.Value, descending));
        }

        return (orders, null);
    }
}

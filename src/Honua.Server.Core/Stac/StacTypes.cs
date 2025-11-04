// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Honua.Server.Core.Stac;

public sealed record StacLink
{
    public required string Rel { get; init; }
    public required string Href { get; init; }
    public string? Type { get; init; }
    public string? Title { get; init; }
    public string? Hreflang { get; init; }
    public JsonObject? Properties { get; init; }
}

public sealed record StacAsset
{
    public required string Href { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Type { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public JsonObject? Properties { get; init; }
}

public sealed record StacSearchParameters
{
    public IReadOnlyList<string>? Collections { get; init; }
    public IReadOnlyList<string>? Ids { get; init; }
    public double[]? Bbox { get; init; }
    public ParsedGeometry? Intersects { get; init; }
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public int Limit { get; init; } = 10;
    public string? Token { get; init; }
    public IReadOnlyList<StacSortField>? SortBy { get; init; }
    public string? Filter { get; init; }
    public string? FilterLang { get; init; }

    /// <summary>
    /// Backwards-compatible accessor for ISO 8601 interval strings.
    /// Supports formats like "start/end", "start/..", "../end", or single instants.
    /// </summary>
    public string? Datetime
    {
        get
        {
            if (Start is null && End is null)
            {
                return null;
            }

            var startPart = Start.HasValue ? Start.Value.ToString("O") : "..";
            var endPart = End.HasValue ? End.Value.ToString("O") : "..";

            if (Start.HasValue && End.HasValue && Start.Value == End.Value)
            {
                return Start.Value.ToString("O");
            }

            return $"{startPart}/{endPart}";
        }
        init
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Start = null;
                End = null;
                return;
            }

            if (!value.Contains('/'))
            {
                if (TryParseInstant(value, out var instant))
                {
                    Start = instant;
                    End = instant;
                }
                else
                {
                    Start = null;
                    End = null;
                }

                return;
            }

            var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
            Start = ParseEndpoint(parts[0]);
            End = ParseEndpoint(parts[1]);
        }
    }

    private static DateTimeOffset? ParseEndpoint(string token)
    {
        if (string.Equals(token, "..", StringComparison.Ordinal))
        {
            return null;
        }

        return TryParseInstant(token, out var value) ? value : null;
    }

    private static bool TryParseInstant(string token, out DateTimeOffset? value)
    {
        value = null;
        if (DateTimeOffset.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }
}

public sealed record StacSearchResult
{
    public required IReadOnlyList<StacItemRecord> Items { get; init; }
    public int Matched { get; init; }
    public string? NextToken { get; init; }

    public int NumberMatched => Matched;
}

public sealed record StacCollectionListResult
{
    public required IReadOnlyList<StacCollectionRecord> Collections { get; init; }
    public int TotalCount { get; init; }
    public string? NextToken { get; init; }
}

public sealed record StacExtent
{
    public static StacExtent Empty { get; } = new();

    public IReadOnlyList<double[]> Spatial { get; init; } = Array.Empty<double[]>();
    public IReadOnlyList<StacTemporalInterval> Temporal { get; init; } = Array.Empty<StacTemporalInterval>();
    public JsonObject? AdditionalProperties { get; init; }

    public bool IsEmpty => Spatial.Count == 0 && Temporal.Count == 0 && AdditionalProperties is null;
}

public sealed record StacTemporalInterval
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public JsonObject? Properties { get; init; }

    public bool IsEmpty => Start is null && End is null && Properties is null;
}

/// <summary>
/// Represents the sort direction for a STAC search field.
/// </summary>
public enum StacSortDirection
{
    /// <summary>
    /// Ascending sort order (A-Z, 0-9, oldest to newest).
    /// </summary>
    Ascending,

    /// <summary>
    /// Descending sort order (Z-A, 9-0, newest to oldest).
    /// </summary>
    Descending
}

/// <summary>
/// Represents a field and direction for sorting STAC search results.
/// </summary>
public sealed record StacSortField
{
    /// <summary>
    /// The field name to sort by (e.g., "datetime", "id", "properties.cloud_cover").
    /// </summary>
    public required string Field { get; init; }

    /// <summary>
    /// The sort direction (ascending or descending).
    /// </summary>
    public StacSortDirection Direction { get; init; } = StacSortDirection.Ascending;
}

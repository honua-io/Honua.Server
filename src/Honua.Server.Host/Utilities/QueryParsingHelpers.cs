// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Query;
using Honua.Server.Host.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Utilities;

public static class QueryParsingHelpers
{
    public readonly record struct QueryTemporalRange(DateTimeOffset? Start, DateTimeOffset? End);

    public readonly record struct PaginationParameters(int Limit, int Offset);

    public readonly record struct BoundingBoxWithCrs(double[] Coordinates, string? Crs);

    /// <summary>
    /// Gets the last value from a query collection for the specified key.
    /// </summary>
    /// <param name="query">The query collection to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The last value for the key, or null if not found or empty.</returns>
    public static string? GetQueryValue(IQueryCollection query, string key)
    {
        Guard.NotNull(query);

        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return null;
        }
        return values[^1];
    }

    /// <summary>
    /// Gets the last value from a query collection for the specified key, or a default value.
    /// </summary>
    /// <param name="query">The query collection to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The last value for the key, or the default value.</returns>
    public static string? GetQueryValue(IQueryCollection query, string key, string? defaultValue)
    {
        return GetQueryValue(query, key) ?? defaultValue;
    }

    public static (double[]? Value, IResult? Error) ParseBoundingBox(IQueryCollection query, string parameterName = "bbox", bool allowAltitude = false, string? crs = null)
    {
        Guard.NotNull(query);

        if (!query.TryGetValue(parameterName, out var values) || values.Count == 0)
        {
            return (null, null);
        }

        return ParseBoundingBox(values[^1], parameterName, allowAltitude, crs);
    }

    public static (double[]? Value, IResult? Error) ParseBoundingBox(string? raw, string parameterName = "bbox", bool allowAltitude = false, string? crs = null)
    {
        if (raw is null)
        {
            return (null, null);
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return (null, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid {parameterName} parameter",
                detail: $"The {parameterName} parameter cannot be empty. Expected format: comma-separated coordinates like '-180,-90,180,90'"));
        }

        var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var expectedLengths = allowAltitude ? new[] { 4, 6 } : new[] { 4 };
        if (Array.IndexOf(expectedLengths, parts.Length) < 0)
        {
            var detail = allowAltitude
                ? $"Invalid {parameterName}: expected 4 coordinates [minX, minY, maxX, maxY] or 6 coordinates [minX, minY, minZ, maxX, maxY, maxZ], but received {parts.Length} values. Example: '-180,-90,180,90' or '-180,-90,0,180,90,1000'"
                : $"Invalid {parameterName}: expected 4 coordinates [minX, minY, maxX, maxY], but received {parts.Length} values. Example: '-180,-90,180,90'";
            return (null, Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Invalid {parameterName} parameter", detail: detail));
        }

        var numbers = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!parts[i].TryParseDouble(out numbers[i]))
            {
                return (null, Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid {parameterName} parameter",
                    detail: $"Failed to parse coordinate at position {i + 1}: '{parts[i]}' is not a valid number. All {parameterName} values must be numeric (e.g., -122.5, 45.0)."));
            }
        }

        // BUG FIX #26: Skip coordinate range validation for non-CRS84 coordinates
        // Projected coordinate systems (e.g., EPSG:3857 Web Mercator) use meters, not degrees
        var isCrs84 = crs is null || crs.Equals("CRS84", StringComparison.OrdinalIgnoreCase) ||
                      crs.Equals("http://www.opengis.net/def/crs/OGC/1.3/CRS84", StringComparison.OrdinalIgnoreCase) ||
                      crs.Equals("EPSG:4326", StringComparison.OrdinalIgnoreCase);

        // Only validate coordinate ranges for CRS84/WGS84 (geographic coordinates)
        if (isCrs84)
        {
            // Validate coordinate ranges
            if (parts.Length == 4)
            {
                // 2D bbox validation
                if (numbers[0] < ApiLimitsAndConstants.MinLongitude || numbers[0] > ApiLimitsAndConstants.MaxLongitude)
                {
                    return (null, Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: $"Invalid {parameterName} parameter",
                        detail: $"Minimum longitude (minX) is out of valid range: {numbers[0]}. Must be between {ApiLimitsAndConstants.MinLongitude} and {ApiLimitsAndConstants.MaxLongitude}."));
                }
                if (numbers[1] < ApiLimitsAndConstants.MinLatitude || numbers[1] > ApiLimitsAndConstants.MaxLatitude)
                {
                    return (null, Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: $"Invalid {parameterName} parameter",
                        detail: $"Minimum latitude (minY) is out of valid range: {numbers[1]}. Must be between {ApiLimitsAndConstants.MinLatitude} and {ApiLimitsAndConstants.MaxLatitude}."));
                }
                if (numbers[2] < ApiLimitsAndConstants.MinLongitude || numbers[2] > ApiLimitsAndConstants.MaxLongitude)
                {
                    return (null, Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: $"Invalid {parameterName} parameter",
                        detail: $"Maximum longitude (maxX) is out of valid range: {numbers[2]}. Must be between {ApiLimitsAndConstants.MinLongitude} and {ApiLimitsAndConstants.MaxLongitude}."));
                }
                if (numbers[3] < ApiLimitsAndConstants.MinLatitude || numbers[3] > ApiLimitsAndConstants.MaxLatitude)
                {
                    return (null, Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: $"Invalid {parameterName} parameter",
                        detail: $"Maximum latitude (maxY) is out of valid range: {numbers[3]}. Must be between {ApiLimitsAndConstants.MinLatitude} and {ApiLimitsAndConstants.MaxLatitude}."));
                }
            }
            else if (parts.Length == 6)
            {
                // 3D bbox validation
                if (numbers[0] < ApiLimitsAndConstants.MinLongitude || numbers[0] > ApiLimitsAndConstants.MaxLongitude)
                {
                    return (null, Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: $"Invalid {parameterName} parameter",
                        detail: $"Minimum longitude (minX) is out of valid range: {numbers[0]}. Must be between {ApiLimitsAndConstants.MinLongitude} and {ApiLimitsAndConstants.MaxLongitude}."));
                }
                if (numbers[1] < ApiLimitsAndConstants.MinLatitude || numbers[1] > ApiLimitsAndConstants.MaxLatitude)
                {
                    return (null, Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: $"Invalid {parameterName} parameter",
                        detail: $"Minimum latitude (minY) is out of valid range: {numbers[1]}. Must be between {ApiLimitsAndConstants.MinLatitude} and {ApiLimitsAndConstants.MaxLatitude}."));
                }
                if (numbers[3] < ApiLimitsAndConstants.MinLongitude || numbers[3] > ApiLimitsAndConstants.MaxLongitude)
                {
                    return (null, Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: $"Invalid {parameterName} parameter",
                        detail: $"Maximum longitude (maxX) is out of valid range: {numbers[3]}. Must be between {ApiLimitsAndConstants.MinLongitude} and {ApiLimitsAndConstants.MaxLongitude}."));
                }
                if (numbers[4] < ApiLimitsAndConstants.MinLatitude || numbers[4] > ApiLimitsAndConstants.MaxLatitude)
                {
                    return (null, Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: $"Invalid {parameterName} parameter",
                        detail: $"Maximum latitude (maxY) is out of valid range: {numbers[4]}. Must be between {ApiLimitsAndConstants.MinLatitude} and {ApiLimitsAndConstants.MaxLatitude}."));
                }
            }
        }

        // BUG FIX #27: Allow dateline-wrapping bounding boxes (minX > maxX for antimeridian crossing)
        // Only enforce minX <= maxX for non-dateline-wrapping cases
        // Example: bbox=170,-10,-170,10 represents a small region crossing the antimeridian (dateline)
        // Downstream tile logic will split this into two ranges: [170,180] and [-180,-170]

        // Validate min <= max for latitude (always enforced)
        if (numbers[1] > numbers[parts.Length == 4 ? 3 : 4])
        {
            return (null, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid {parameterName} parameter",
                detail: $"Minimum latitude must be less than or equal to maximum latitude. Got minY={numbers[1]}, maxY={numbers[parts.Length == 4 ? 3 : 4]}"));
        }

        if (parts.Length == 6 && numbers[2] > numbers[5])
        {
            return (null, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid {parameterName} parameter",
                detail: $"Minimum altitude must be less than or equal to maximum altitude. Got minZ={numbers[2]}, maxZ={numbers[5]}"));
        }

        return (numbers, null);
    }

    public static (BoundingBoxWithCrs? Value, IResult? Error) ParseBoundingBoxWithCrs(string? raw, string parameterName = "bbox", bool allowAltitude = false)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        var tokens = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (tokens.Count == 0)
        {
            return (null, Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Invalid {parameterName} parameter", detail: $"{parameterName} cannot be empty."));
        }

        string? crs = null;
        if (tokens.Count > 0 && !tokens[^1].TryParseDouble(out _))
        {
            crs = tokens[^1];
            tokens.RemoveAt(tokens.Count - 1);
        }

        var numericPortion = string.Join(',', tokens);
        var (bbox, error) = ParseBoundingBox(numericPortion, parameterName, allowAltitude, crs);
        if (bbox is null)
        {
            return (null, error);
        }

        return (new BoundingBoxWithCrs(bbox, crs), null);
    }

    public static (int? Value, IResult? Error) ParsePositiveInt(
        IQueryCollection query,
        string key,
        bool required = false,
        int? defaultValue = null,
        bool allowZero = false,
        string? errorDetail = null)
    {
        Guard.NotNull(query);

        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            if (defaultValue.HasValue)
            {
                return (defaultValue.Value, null);
            }

            if (!required)
            {
                return (null, null);
            }

            return (null, BuildIntegerProblem(key, allowZero, errorDetail));
        }

        var raw = values[^1];
        if (raw.IsNullOrWhiteSpace())
        {
            if (defaultValue.HasValue)
            {
                return (defaultValue.Value, null);
            }

            if (!required)
            {
                return (null, null);
            }

            return (null, BuildIntegerProblem(key, allowZero, errorDetail));
        }

        if (!raw.Trim().TryParseInt(out var parsed) ||
            parsed < 0 || (!allowZero && parsed == 0))
        {
            return (null, BuildIntegerProblem(key, allowZero, errorDetail));
        }

        return (parsed, null);
    }

    public static (int? Value, IResult? Error) ParsePositiveInt(
        string? raw,
        string key,
        bool required = false,
        int? defaultValue = null,
        bool allowZero = false,
        string? errorDetail = null)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            if (defaultValue.HasValue)
            {
                return (defaultValue.Value, null);
            }

            if (!required)
            {
                return (null, null);
            }

            return (null, BuildIntegerProblem(key, allowZero, errorDetail));
        }

        if (!raw.Trim().TryParseInt(out var parsed) ||
            parsed < 0 || (!allowZero && parsed == 0))
        {
            return (null, BuildIntegerProblem(key, allowZero, errorDetail));
        }

        return (parsed, null);
    }

    public static (string? Value, IResult? Error) ResolveCrs(
        string? raw,
        IReadOnlyCollection<string> supported,
        string parameterName,
        string? defaultValue = null,
        bool required = false)
    {
        Guard.NotNull(supported);

        string? candidate = raw;
        if (candidate.IsNullOrWhiteSpace())
        {
            if (defaultValue.HasValue())
            {
                candidate = defaultValue;
            }
            else if (!required)
            {
                return (null, null);
            }
            else
            {
                return (null, Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid {parameterName} parameter",
                    detail: $"Parameter '{parameterName}' is required."));
            }
        }

        var normalized = CrsHelper.NormalizeIdentifier(candidate!);
        if (supported.Count > 0 && !supported.Any(crs => string.Equals(crs, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            var supportedList = string.Join(", ", supported);
            return (null, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid {parameterName} parameter",
                detail: $"Requested {parameterName} '{candidate}' is not supported. Supported CRS values: {supportedList}."));
        }

        return (normalized, null);
    }

    public static bool ParseBoolean(IQueryCollection query, string key, bool defaultValue)
    {
        Guard.NotNull(query);

        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return defaultValue;
        }

        return ParseBoolean(values[^1], defaultValue);
    }

    public static bool ParseBoolean(string? raw, bool defaultValue)
    {
        // Delegate to QueryParameterHelper for consistent boolean parsing
        var (result, _) = QueryParameterHelper.ParseBoolean(raw, defaultValue);
        return result;
    }

    public static string ExtractProblemMessage(IResult? error, string defaultMessage)
    {
        if (error is IValueHttpResult { Value: ProblemDetails problem })
        {
            return problem.Detail ?? problem.Title ?? defaultMessage;
        }

        return defaultMessage;
    }

    public static (QueryTemporalRange? Value, IResult? Error) ParseTemporalRange(string? raw, string parameterName = "datetime")
    {
        // Delegate to QueryParameterHelper for core parsing logic
        var (interval, error) = QueryParameterHelper.ParseTemporalRange(raw);

        if (error is not null)
        {
            return (null, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid {parameterName} parameter",
                detail: $"The {parameterName} parameter {error}. Examples: '2023-01-01T00:00:00Z' or '2023-01-01T00:00:00Z/2023-12-31T23:59:59Z' or '../2023-12-31T23:59:59Z' (open-ended range)."));
        }

        if (interval is null)
        {
            return (null, null);
        }

        return (new QueryTemporalRange(interval.Start, interval.End), null);
    }

    public static IReadOnlyList<string> ParseCsv(string? raw)
    {
        // Delegate to QueryParameterHelper for consistent CSV parsing
        return QueryParameterHelper.ParseCommaSeparatedList(raw);
    }

    public static PaginationParameters ParsePagination(
        IQueryCollection query,
        int defaultLimit,
        int defaultOffset,
        int minLimit,
        int maxLimit,
        int minOffset = 0,
        string limitKey = "limit",
        string offsetKey = "offset")
    {
        Guard.NotNull(query);

        var limit = ParseInt(query, limitKey, defaultLimit, minLimit, maxLimit, allowZero: false);
        var offset = ParseInt(query, offsetKey, defaultOffset, minOffset, int.MaxValue, allowZero: true);
        return new PaginationParameters(limit, offset);
    }


    private static int ParseInt(IQueryCollection query, string key, int defaultValue, int minimum, int maximum, bool allowZero)
    {
        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return defaultValue;
        }

        if (!values[^1].TryParseInt(out var parsed))
        {
            return defaultValue;
        }

        if ((!allowZero && parsed == 0) || parsed < minimum)
        {
            return defaultValue;
        }

        if (parsed > maximum)
        {
            return maximum;
        }

        return parsed;
    }

    private static IResult BuildIntegerProblem(string key, bool allowZero, string? detailOverride)
    {
        var detail = detailOverride ?? $"Parameter '{key}' must be a {(allowZero ? "non-negative" : "positive")} integer.";
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: $"Invalid {key} parameter",
            detail: detail);
    }
}

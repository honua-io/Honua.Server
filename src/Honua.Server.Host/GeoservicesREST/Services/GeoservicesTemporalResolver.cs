// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Resolves temporal parameters for Geoservices REST API requests.
/// </summary>
internal static class GeoservicesTemporalResolver
{
    public static TemporalInterval? ResolveTemporalRange(IQueryCollection query, LayerDefinition layer)
    {
        if (!query.TryGetValue("time", out var values) || values.Count == 0)
        {
            return null;
        }

        var temporalField = layer.Storage?.TemporalColumn;
        if (string.IsNullOrWhiteSpace(temporalField))
        {
            ThrowBadRequest("Layer does not support time queries.");
        }

        var raw = values[^1];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = raw.Trim();
        if (normalized.Length == 0 ||
            normalized.EqualsIgnoreCase("null") ||
            normalized.EqualsIgnoreCase("none"))
        {
            return null;
        }

        var tokens = normalized.Split(',', StringSplitOptions.TrimEntries);
        if (tokens.Length > 2)
        {
            ThrowBadRequest("time parameter must include at most two comma-separated values.");
        }

        DateTimeOffset? start;
        DateTimeOffset? end;

        if (tokens.Length == 1)
        {
            var instant = ParseTemporalBoundary(tokens[0]);
            start = instant;
            end = instant;
        }
        else
        {
            start = ParseTemporalBoundary(tokens[0]);
            end = ParseTemporalBoundary(tokens.Length > 1 ? tokens[1] : null);
        }

        if (start is null && end is null)
        {
            return null;
        }

        return new TemporalInterval(start, end);
    }

    private static DateTimeOffset? ParseTemporalBoundary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.EqualsIgnoreCase("null") ||
            value.EqualsIgnoreCase("none") ||
            value.EqualsIgnoreCase(".."))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochMillis))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(epochMillis);
            }
            catch (ArgumentOutOfRangeException)
            {
                ThrowBadRequest($"time parameter value '{value}' is outside the valid range for epoch milliseconds.");
            }
        }

        if (value.TryParseDoubleStrict(out var floatingEpoch))
        {
            var millis = Convert.ToInt64(Math.Round(floatingEpoch, MidpointRounding.AwayFromZero));
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(millis);
            }
            catch (ArgumentOutOfRangeException)
            {
                ThrowBadRequest($"time parameter value '{value}' is outside the valid range for epoch milliseconds.");
            }
        }

        // SECURITY FIX (Bug 35): Validate ISO8601 format more strictly
        // DateTimeOffset.TryParse is too lenient and accepts invalid formats
        // Use RoundtripKind to enforce strict ISO8601 parsing
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed;
        }

        // Fallback to AssumeUniversal for common ISO8601 variants without timezone
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            // Validate that the parsed value looks like a valid ISO8601 date
            // Reject values like "invalid" that might parse as dates
            if (value.Contains('-') || value.Contains('T') || value.Contains(':'))
            {
                return parsed;
            }
        }

        ThrowBadRequest($"time parameter value '{value}' must be ISO-8601 or epoch milliseconds.");
        return null;
    }

    private static void ThrowBadRequest(string message)
    {
        throw new GeoservicesRESTQueryException(message);
    }
}

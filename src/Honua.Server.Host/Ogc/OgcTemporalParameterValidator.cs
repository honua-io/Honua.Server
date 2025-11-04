// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Validation;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Validates temporal parameters for OGC API requests according to RFC 3339 and ISO 8601 standards.
/// </summary>
/// <remarks>
/// This validator implements comprehensive temporal validation for OGC API Tiles and Features, including:
/// - ISO 8601 / RFC 3339 datetime format validation
/// - Temporal interval parsing (start/end with ".." separator)
/// - Open-ended intervals (../2024 or 2024/..)
/// - Present time notation ("now" or empty end)
/// - Temporal bounds checking against collection temporal extent
/// - Duration notation (P1Y, P1M, P1D, etc.)
/// - Timezone handling and UTC normalization
///
/// Reference: OGC API - Features - Part 1: Core (datetime parameter)
/// https://docs.ogc.org/is/17-069r4/17-069r4.html#_parameter_datetime
/// </remarks>
internal static class OgcTemporalParameterValidator
{
    // ISO 8601 duration pattern: P[n]Y[n]M[n]DT[n]H[n]M[n]S
    private static readonly Regex DurationPattern = new(
        @"^P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates a datetime parameter value according to OGC API specifications.
    /// </summary>
    /// <param name="datetimeValue">The datetime parameter value from the query string.</param>
    /// <param name="temporal">The layer's temporal definition with constraints.</param>
    /// <param name="allowFuture">Whether to allow future dates (default: true for raster forecasts).</param>
    /// <returns>The validated datetime string, potentially normalized.</returns>
    /// <exception cref="OgcTemporalValidationException">Thrown when validation fails.</exception>
    public static string? Validate(string? datetimeValue, LayerTemporalDefinition temporal, bool allowFuture = true)
    {
        // Empty or null datetime - use layer default if specified
        if (datetimeValue.IsNullOrWhiteSpace())
        {
            if (!temporal.Enabled)
            {
                return null;
            }
            return temporal.DefaultValue;
        }

        // Check if this is an interval (contains "..")
        if (datetimeValue.Contains(".."))
        {
            return ValidateInterval(datetimeValue, temporal, allowFuture);
        }

        // Single datetime value
        return ValidateSingleDatetime(datetimeValue, temporal, allowFuture);
    }

    /// <summary>
    /// Validates a single datetime value.
    /// </summary>
    private static string ValidateSingleDatetime(string datetimeValue, LayerTemporalDefinition temporal, bool allowFuture)
    {
        // If layer has fixed values, validate against them
        if (temporal.FixedValues is { Count: > 0 })
        {
            if (!temporal.FixedValues.Contains(datetimeValue, StringComparer.OrdinalIgnoreCase))
            {
                throw new OgcTemporalValidationException(
                    $"datetime value '{datetimeValue}' is not in the allowed set. " +
                    $"Valid values: {string.Join(", ", temporal.FixedValues)}");
            }
            return datetimeValue;
        }

        // Parse and validate the datetime
        var parsed = ParseDatetime(datetimeValue);

        // Validate against layer temporal bounds
        ValidateAgainstLayerBounds(parsed, temporal);

        // Validate against reasonable bounds
        if (!TemporalRangeValidator.IsDateValid(parsed, allowFuture))
        {
            if (!allowFuture && parsed > DateTimeOffset.UtcNow)
            {
                throw new OgcTemporalValidationException(
                    $"datetime value '{datetimeValue}' is in the future, which is not allowed for this layer.");
            }
            throw new OgcTemporalValidationException(
                $"datetime value '{datetimeValue}' is outside the valid range " +
                $"({TemporalRangeValidator.MinimumDate:yyyy-MM-dd} to {TemporalRangeValidator.MaximumDate:yyyy-MM-dd}).");
        }

        return datetimeValue;
    }

    /// <summary>
    /// Validates a temporal interval (start/end or start/duration).
    /// </summary>
    private static string ValidateInterval(string intervalValue, LayerTemporalDefinition temporal, bool allowFuture)
    {
        var parts = intervalValue.Split(new[] { ".." }, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            throw new OgcTemporalValidationException(
                $"Invalid interval format '{intervalValue}'. Expected 'start..end' or 'start..' or '..end'.");
        }

        var startStr = parts[0].Trim();
        var endStr = parts[1].Trim();

        // Handle open-ended intervals
        DateTimeOffset? start = null;
        DateTimeOffset? end = null;

        // Parse start (may be empty for open start: "..end")
        if (!string.IsNullOrEmpty(startStr))
        {
            start = ParseDatetime(startStr);
            ValidateAgainstLayerBounds(start.Value, temporal);
        }

        // Parse end (may be empty for open end: "start.." or special value "now")
        if (!string.IsNullOrEmpty(endStr))
        {
            // Handle "now" keyword for current time
            if (endStr.Equals("now", StringComparison.OrdinalIgnoreCase))
            {
                end = DateTimeOffset.UtcNow;
            }
            // Handle duration notation (e.g., "2024-01-01..P1M" means 1 month from start)
            else if (endStr.StartsWith("P", StringComparison.OrdinalIgnoreCase))
            {
                if (start == null)
                {
                    throw new OgcTemporalValidationException(
                        "Duration notation requires a start datetime (e.g., '2024-01-01..P1M').");
                }
                var duration = ParseDuration(endStr);
                end = start.Value + duration;
            }
            else
            {
                end = ParseDatetime(endStr);
                ValidateAgainstLayerBounds(end.Value, temporal);
            }
        }

        // Validate the interval
        if (!TemporalRangeValidator.IsRangeValid(start, end, allowFuture))
        {
            if (start.HasValue && end.HasValue && start.Value > end.Value)
            {
                throw new OgcTemporalValidationException(
                    $"Invalid temporal interval: start ({start.Value:yyyy-MM-dd'T'HH:mm:ssK}) " +
                    $"must be before or equal to end ({end.Value:yyyy-MM-dd'T'HH:mm:ssK}).");
            }

            if (!allowFuture)
            {
                if (start.HasValue && start.Value > DateTimeOffset.UtcNow)
                {
                    throw new OgcTemporalValidationException(
                        "Interval start date is in the future, which is not allowed for this layer.");
                }
                if (end.HasValue && end.Value > DateTimeOffset.UtcNow)
                {
                    throw new OgcTemporalValidationException(
                        "Interval end date is in the future, which is not allowed for this layer.");
                }
            }

            throw new OgcTemporalValidationException(
                $"Temporal interval '{intervalValue}' is invalid or exceeds maximum allowed span.");
        }

        // Return the original interval string (validation passed)
        return intervalValue;
    }

    /// <summary>
    /// Parses a datetime string according to ISO 8601 / RFC 3339.
    /// </summary>
    private static DateTimeOffset ParseDatetime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new OgcTemporalValidationException("datetime value cannot be empty.");
        }

        // Handle special keywords
        if (value.Equals("now", StringComparison.OrdinalIgnoreCase))
        {
            return DateTimeOffset.UtcNow;
        }

        // Try RFC 3339 / ISO 8601 parsing with strict validation
        // Use RoundtripKind to enforce proper format
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed;
        }

        // Fallback: Try with AssumeUniversal for dates without explicit timezone
        // (e.g., "2024-01-01" or "2024-01-01T12:00:00")
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            // Validate it looks like ISO 8601 format (has date separators)
            if (value.Contains('-') || value.Contains('T') || value.Contains(':'))
            {
                return parsed;
            }
        }

        throw new OgcTemporalValidationException(
            $"datetime value '{value}' is not a valid ISO 8601 / RFC 3339 datetime. " +
            "Expected format: YYYY-MM-DD, YYYY-MM-DDTHH:MM:SSZ, or YYYY-MM-DDTHH:MM:SS+00:00");
    }

    /// <summary>
    /// Parses an ISO 8601 duration string (e.g., P1Y2M3DT4H5M6S).
    /// </summary>
    private static TimeSpan ParseDuration(string duration)
    {
        var match = DurationPattern.Match(duration);
        if (!match.Success)
        {
            throw new OgcTemporalValidationException(
                $"Invalid ISO 8601 duration '{duration}'. " +
                "Expected format: P[n]Y[n]M[n]DT[n]H[n]M[n]S (e.g., P1Y, P1M, P1D, PT1H)");
        }

        // Parse duration components
        var years = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        var months = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        var days = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        var hours = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
        var minutes = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0;
        var seconds = match.Groups[6].Success ? double.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture) : 0;

        // Convert to TimeSpan (approximate for years and months)
        // Note: This is approximate because months/years vary in length
        var totalDays = (years * 365.25) + (months * 30.44) + days;
        return TimeSpan.FromDays(totalDays) + TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Validates a datetime against the layer's temporal bounds.
    /// </summary>
    private static void ValidateAgainstLayerBounds(DateTimeOffset value, LayerTemporalDefinition temporal)
    {
        if (!temporal.Enabled)
        {
            return;
        }

        // Check against layer temporal extent
        if (!temporal.MinValue.IsNullOrWhiteSpace() && !temporal.MaxValue.IsNullOrWhiteSpace())
        {
            if (!DateTimeOffset.TryParse(temporal.MinValue, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AdjustToUniversal, out var minValue))
            {
                // Try fallback parsing
                DateTimeOffset.TryParse(temporal.MinValue, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out minValue);
            }

            if (!DateTimeOffset.TryParse(temporal.MaxValue, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AdjustToUniversal, out var maxValue))
            {
                // Try fallback parsing
                DateTimeOffset.TryParse(temporal.MaxValue, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out maxValue);
            }

            if (value < minValue || value > maxValue)
            {
                throw new OgcTemporalValidationException(
                    $"datetime value '{value:yyyy-MM-dd'T'HH:mm:ssK}' is outside the layer's temporal extent. " +
                    $"Valid range: {temporal.MinValue} to {temporal.MaxValue}");
            }
        }
    }

    /// <summary>
    /// Tries to validate a datetime parameter, returning false if validation fails.
    /// </summary>
    public static bool TryValidate(string? datetimeValue, LayerTemporalDefinition temporal, bool allowFuture, out string? validatedValue, out string? errorMessage)
    {
        try
        {
            validatedValue = Validate(datetimeValue, temporal, allowFuture);
            errorMessage = null;
            return true;
        }
        catch (OgcTemporalValidationException ex)
        {
            validatedValue = null;
            errorMessage = ex.Message;
            return false;
        }
    }
}

/// <summary>
/// Exception thrown when OGC temporal parameter validation fails.
/// </summary>
public sealed class OgcTemporalValidationException : Exception
{
    public OgcTemporalValidationException(string message) : base(message)
    {
    }

    public OgcTemporalValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

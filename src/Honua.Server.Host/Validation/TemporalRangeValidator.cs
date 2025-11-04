// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.Validation;

/// <summary>
/// Validates temporal (date/time) ranges to ensure logical consistency and reasonable bounds.
/// SECURITY: Prevents injection of invalid date ranges that could cause query performance issues or exploits.
/// </summary>
/// <remarks>
/// This validator ensures that:
/// - Start dates come before end dates
/// - Dates are within reasonable bounds (not year 1 or year 9999)
/// - Future date validation where appropriate
/// - No extremely large time spans that could cause performance issues
/// </remarks>
public static class TemporalRangeValidator
{
    /// <summary>
    /// Minimum allowed date for temporal queries (January 1, 1900).
    /// </summary>
    /// <remarks>
    /// Dates before 1900 are generally not used in modern geospatial data.
    /// This prevents potential exploits using dates near DateTime.MinValue.
    /// </remarks>
    public static readonly DateTimeOffset MinimumDate = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Maximum allowed date for temporal queries (December 31, 2100).
    /// </summary>
    /// <remarks>
    /// Dates beyond 2100 are unlikely to be useful in most geospatial applications.
    /// This prevents potential exploits using dates near DateTime.MaxValue.
    /// </remarks>
    public static readonly DateTimeOffset MaximumDate = new(2100, 12, 31, 23, 59, 59, TimeSpan.Zero);

    /// <summary>
    /// Maximum allowed time span for a temporal range query (100 years).
    /// </summary>
    /// <remarks>
    /// Extremely large time spans can cause performance issues in temporal queries.
    /// 100 years should be sufficient for most legitimate use cases.
    /// </remarks>
    public static readonly TimeSpan MaximumTimeSpan = TimeSpan.FromDays(365.25 * 100); // 100 years

    /// <summary>
    /// Validates that a date is within reasonable bounds.
    /// </summary>
    /// <param name="date">The date to validate.</param>
    /// <returns>True if the date is within bounds; otherwise, false.</returns>
    public static bool IsDateInBounds(DateTimeOffset date)
    {
        return date >= MinimumDate && date <= MaximumDate;
    }

    /// <summary>
    /// Validates that a date is within reasonable bounds and not in the future.
    /// </summary>
    /// <param name="date">The date to validate.</param>
    /// <param name="allowFuture">Whether to allow future dates. Default is false.</param>
    /// <returns>True if the date is valid; otherwise, false.</returns>
    public static bool IsDateValid(DateTimeOffset date, bool allowFuture = false)
    {
        if (!IsDateInBounds(date))
        {
            return false;
        }

        if (!allowFuture && date > DateTimeOffset.UtcNow)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a temporal range is logically consistent.
    /// </summary>
    /// <param name="start">The start of the range (may be null for open-ended ranges).</param>
    /// <param name="end">The end of the range (may be null for open-ended ranges).</param>
    /// <param name="allowFuture">Whether to allow future dates in the range. Default is false.</param>
    /// <returns>True if the range is valid; otherwise, false.</returns>
    /// <remarks>
    /// A range is valid if:
    /// - Both dates are within reasonable bounds (if provided)
    /// - Start date is before or equal to end date
    /// - The time span doesn't exceed maximum allowed
    /// - Future dates are only allowed if explicitly permitted
    /// </remarks>
    public static bool IsRangeValid(DateTimeOffset? start, DateTimeOffset? end, bool allowFuture = false)
    {
        // Null values are allowed for open-ended ranges
        if (start.HasValue && !IsDateValid(start.Value, allowFuture))
        {
            return false;
        }

        if (end.HasValue && !IsDateValid(end.Value, allowFuture))
        {
            return false;
        }

        // If both are specified, start must be before or equal to end
        if (start.HasValue && end.HasValue)
        {
            if (start.Value > end.Value)
            {
                return false;
            }

            // Check time span doesn't exceed maximum
            var span = end.Value - start.Value;
            if (span > MaximumTimeSpan)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a temporal range and throws an exception if invalid.
    /// </summary>
    /// <param name="start">The start of the range (may be null for open-ended ranges).</param>
    /// <param name="end">The end of the range (may be null for open-ended ranges).</param>
    /// <param name="allowFuture">Whether to allow future dates in the range. Default is false.</param>
    /// <param name="startParameterName">The parameter name for the start date (used in error messages).</param>
    /// <param name="endParameterName">The parameter name for the end date (used in error messages).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when dates are outside valid bounds.</exception>
    /// <exception cref="ArgumentException">Thrown when the range is logically invalid.</exception>
    public static void Validate(
        DateTimeOffset? start,
        DateTimeOffset? end,
        bool allowFuture = false,
        string startParameterName = "startDate",
        string endParameterName = "endDate")
    {
        if (start.HasValue)
        {
            if (!IsDateInBounds(start.Value))
            {
                throw new ArgumentOutOfRangeException(
                    startParameterName,
                    start.Value,
                    $"Start date must be between {MinimumDate:yyyy-MM-dd} and {MaximumDate:yyyy-MM-dd}.");
            }

            if (!allowFuture && start.Value > DateTimeOffset.UtcNow)
            {
                throw new ArgumentOutOfRangeException(
                    startParameterName,
                    start.Value,
                    "Start date cannot be in the future.");
            }
        }

        if (end.HasValue)
        {
            if (!IsDateInBounds(end.Value))
            {
                throw new ArgumentOutOfRangeException(
                    endParameterName,
                    end.Value,
                    $"End date must be between {MinimumDate:yyyy-MM-dd} and {MaximumDate:yyyy-MM-dd}.");
            }

            if (!allowFuture && end.Value > DateTimeOffset.UtcNow)
            {
                throw new ArgumentOutOfRangeException(
                    endParameterName,
                    end.Value,
                    "End date cannot be in the future.");
            }
        }

        if (start.HasValue && end.HasValue)
        {
            if (start.Value > end.Value)
            {
                throw new ArgumentException(
                    $"Start date ({start.Value:yyyy-MM-dd}) must be before or equal to end date ({end.Value:yyyy-MM-dd}).",
                    startParameterName);
            }

            var span = end.Value - start.Value;
            if (span > MaximumTimeSpan)
            {
                throw new ArgumentException(
                    $"Temporal range ({span.TotalDays:F0} days) exceeds maximum allowed span ({MaximumTimeSpan.TotalDays:F0} days).",
                    startParameterName);
            }
        }
    }

    /// <summary>
    /// Tries to validate a temporal range.
    /// </summary>
    /// <param name="start">The start of the range (may be null for open-ended ranges).</param>
    /// <param name="end">The end of the range (may be null for open-ended ranges).</param>
    /// <param name="allowFuture">Whether to allow future dates in the range. Default is false.</param>
    /// <param name="errorMessage">The error message if validation fails.</param>
    /// <returns>True if valid; otherwise, false with an error message.</returns>
    public static bool TryValidate(
        DateTimeOffset? start,
        DateTimeOffset? end,
        bool allowFuture,
        out string? errorMessage)
    {
        if (start.HasValue && !IsDateInBounds(start.Value))
        {
            errorMessage = $"Start date must be between {MinimumDate:yyyy-MM-dd} and {MaximumDate:yyyy-MM-dd}.";
            return false;
        }

        if (start.HasValue && !allowFuture && start.Value > DateTimeOffset.UtcNow)
        {
            errorMessage = "Start date cannot be in the future.";
            return false;
        }

        if (end.HasValue && !IsDateInBounds(end.Value))
        {
            errorMessage = $"End date must be between {MinimumDate:yyyy-MM-dd} and {MaximumDate:yyyy-MM-dd}.";
            return false;
        }

        if (end.HasValue && !allowFuture && end.Value > DateTimeOffset.UtcNow)
        {
            errorMessage = "End date cannot be in the future.";
            return false;
        }

        if (start.HasValue && end.HasValue)
        {
            if (start.Value > end.Value)
            {
                errorMessage = "Start date must be before or equal to end date.";
                return false;
            }

            var span = end.Value - start.Value;
            if (span > MaximumTimeSpan)
            {
                errorMessage = $"Temporal range exceeds maximum allowed span of {MaximumTimeSpan.TotalDays:F0} days.";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }
}

/// <summary>
/// Validation attribute for temporal range parameters.
/// </summary>
/// <remarks>
/// Use this attribute on date/time properties to ensure they fall within acceptable bounds.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidTemporalDateAttribute : ValidationAttribute
{
    private readonly bool _allowFuture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidTemporalDateAttribute"/> class.
    /// </summary>
    /// <param name="allowFuture">Whether to allow future dates. Default is false.</param>
    public ValidTemporalDateAttribute(bool allowFuture = false)
    {
        _allowFuture = allowFuture;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        DateTimeOffset date;
        switch (value)
        {
            case DateTimeOffset dto:
                date = dto;
                break;
            case DateTime dt:
                date = new DateTimeOffset(dt.ToUniversalTime());
                break;
            case string str when DateTimeOffset.TryParse(str, out var parsed):
                date = parsed;
                break;
            default:
                return new ValidationResult("Value must be a valid date/time.");
        }

        if (!TemporalRangeValidator.IsDateValid(date, _allowFuture))
        {
            var minDate = TemporalRangeValidator.MinimumDate;
            var maxDate = TemporalRangeValidator.MaximumDate;
            var futureRestriction = _allowFuture ? "" : " Future dates are not allowed.";
            return new ValidationResult(
                $"Date must be between {minDate:yyyy-MM-dd} and {maxDate:yyyy-MM-dd}.{futureRestriction}");
        }

        return ValidationResult.Success;
    }
}

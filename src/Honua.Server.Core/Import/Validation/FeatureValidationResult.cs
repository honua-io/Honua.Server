// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Core.Import.Validation;

/// <summary>
/// Result of validating a feature against a schema.
/// </summary>
public sealed class FeatureValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public IReadOnlyList<ValidationError> Errors { get; init; } = Array.Empty<ValidationError>();

    public static FeatureValidationResult Success() => new();

    public static FeatureValidationResult Failure(params ValidationError[] errors) => new() { Errors = errors };

    public static FeatureValidationResult Failure(IEnumerable<ValidationError> errors) => new() { Errors = errors.ToArray() };
}

/// <summary>
/// Represents a schema validation error for a feature field.
/// </summary>
public sealed class ValidationError
{
    public required string FieldName { get; init; }
    public required string ErrorCode { get; init; }
    public required string Message { get; init; }
    public object? ActualValue { get; init; }
    public object? ExpectedValue { get; init; }
    public int? FeatureIndex { get; init; }

    public override string ToString()
    {
        var prefix = FeatureIndex.HasValue ? $"Feature {FeatureIndex}: " : string.Empty;
        return $"{prefix}Field '{FieldName}': {Message}";
    }
}

/// <summary>
/// Standard validation error codes.
/// </summary>
public static class ValidationErrorCodes
{
    public const string RequiredFieldMissing = "REQUIRED_FIELD_MISSING";
    public const string InvalidType = "INVALID_TYPE";
    public const string StringTooLong = "STRING_TOO_LONG";
    public const string NumericOutOfRange = "NUMERIC_OUT_OF_RANGE";
    public const string InvalidEnumValue = "INVALID_ENUM_VALUE";
    public const string InvalidPattern = "INVALID_PATTERN";
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string TypeCoercionFailed = "TYPE_COERCION_FAILED";
    public const string InvalidGeometry = "INVALID_GEOMETRY";
    public const string PrecisionExceeded = "PRECISION_EXCEEDED";
    public const string ScaleExceeded = "SCALE_EXCEEDED";
}

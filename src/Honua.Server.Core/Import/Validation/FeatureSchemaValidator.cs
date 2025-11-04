// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Import.Validation;

/// <summary>
/// Validates feature properties against layer schema definitions.
/// Supports type validation, required fields, string length constraints, numeric ranges, and custom validators.
/// </summary>
public sealed class FeatureSchemaValidator : IFeatureSchemaValidator
{
    private readonly ILogger<FeatureSchemaValidator> _logger;

    public FeatureSchemaValidator(ILogger<FeatureSchemaValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public FeatureValidationResult ValidateFeature(
        IDictionary<string, object?> properties,
        LayerDefinition layer,
        SchemaValidationOptions? options = null)
    {
        if (properties is null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        options ??= SchemaValidationOptions.Default;

        if (!options.ValidateSchema)
        {
            return FeatureValidationResult.Success();
        }

        var errors = new List<ValidationError>();

        // Build field index for fast lookup
        var fieldMap = layer.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        // Validate each field defined in the schema
        foreach (var field in layer.Fields)
        {
            if (!properties.TryGetValue(field.Name, out var value))
            {
                // Field not present in properties
                if (!field.Nullable)
                {
                    errors.Add(new ValidationError
                    {
                        FieldName = field.Name,
                        ErrorCode = ValidationErrorCodes.RequiredFieldMissing,
                        Message = $"Required field '{field.Name}' is missing",
                        ExpectedValue = field.DataType ?? field.StorageType
                    });
                }
                continue;
            }

            // Null value handling
            if (value is null)
            {
                if (!field.Nullable)
                {
                    errors.Add(new ValidationError
                    {
                        FieldName = field.Name,
                        ErrorCode = ValidationErrorCodes.RequiredFieldMissing,
                        Message = $"Required field '{field.Name}' cannot be null",
                        ActualValue = null,
                        ExpectedValue = "non-null value"
                    });
                }
                continue;
            }

            // Validate field value
            var fieldErrors = ValidateFieldValue(field, value, options);
            errors.AddRange(fieldErrors);

            if (errors.Count >= options.MaxValidationErrors)
            {
                _logger.LogWarning("Validation stopped after reaching maximum error count ({MaxErrors})", options.MaxValidationErrors);
                break;
            }
        }

        return errors.Count > 0
            ? FeatureValidationResult.Failure(errors)
            : FeatureValidationResult.Success();
    }

    public IReadOnlyList<FeatureValidationResult> ValidateFeatures(
        IEnumerable<IDictionary<string, object?>> features,
        LayerDefinition layer,
        SchemaValidationOptions? options = null)
    {
        if (features is null)
        {
            throw new ArgumentNullException(nameof(features));
        }

        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        options ??= SchemaValidationOptions.Default;

        var results = new List<FeatureValidationResult>();
        var featureIndex = 0;

        foreach (var feature in features)
        {
            var result = ValidateFeature(feature, layer, options);

            // Add feature index to errors
            if (!result.IsValid)
            {
                var errorsWithIndex = result.Errors.Select(e => new ValidationError
                {
                    FieldName = e.FieldName,
                    ErrorCode = e.ErrorCode,
                    Message = e.Message,
                    ActualValue = e.ActualValue,
                    ExpectedValue = e.ExpectedValue,
                    FeatureIndex = featureIndex
                }).ToArray();

                results.Add(FeatureValidationResult.Failure(errorsWithIndex));
            }
            else
            {
                results.Add(result);
            }

            featureIndex++;
        }

        return results;
    }

    private List<ValidationError> ValidateFieldValue(
        FieldDefinition field,
        object value,
        SchemaValidationOptions options)
    {
        var errors = new List<ValidationError>();
        var storageType = (field.StorageType ?? field.DataType ?? "text").Trim().ToLowerInvariant();

        // Attempt type coercion if enabled
        if (options.CoerceTypes)
        {
            var coercionResult = TypeCoercion.TryCoerce(value, storageType);
            if (coercionResult.Success)
            {
                value = coercionResult.Value!;
            }
            else if (options.ValidationMode == SchemaValidationMode.Strict)
            {
                errors.Add(new ValidationError
                {
                    FieldName = field.Name,
                    ErrorCode = ValidationErrorCodes.TypeCoercionFailed,
                    Message = coercionResult.ErrorMessage ?? $"Failed to coerce value to type '{storageType}'",
                    ActualValue = value,
                    ExpectedValue = storageType
                });
                return errors; // Don't continue validation if coercion failed
            }
        }

        // Type-specific validation
        switch (storageType)
        {
            case "integer" or "bigint":
                errors.AddRange(ValidateInteger(field, value));
                break;

            case "smallint":
                errors.AddRange(ValidateSmallInt(field, value));
                break;

            case "float":
                errors.AddRange(ValidateFloat(field, value));
                break;

            case "double":
                errors.AddRange(ValidateDouble(field, value));
                break;

            case "datetime" or "timestamp":
                errors.AddRange(ValidateDateTime(field, value));
                break;

            case "uuid" or "uniqueidentifier":
                errors.AddRange(ValidateGuid(field, value));
                break;

            case "boolean" or "bool":
                errors.AddRange(ValidateBoolean(field, value));
                break;

            case "text" or "string" or "varchar" or "nvarchar" or "longtext":
                errors.AddRange(ValidateString(field, value, options));
                break;

            case "geometry":
                errors.AddRange(ValidateGeometry(field, value));
                break;

            case "blob" or "binary" or "bytea" or "varbinary":
                errors.AddRange(ValidateBinary(field, value));
                break;

            default:
                // Unknown type - treat as string
                errors.AddRange(ValidateString(field, value, options));
                break;
        }

        return errors;
    }

    private List<ValidationError> ValidateInteger(FieldDefinition field, object value)
    {
        var errors = new List<ValidationError>();

        if (value is not long and not int and not short and not byte)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects an integer value",
                ActualValue = value,
                ExpectedValue = "integer"
            });
            return errors;
        }

        var longValue = Convert.ToInt64(value);

        // Validate range for int64
        if (longValue < long.MinValue || longValue > long.MaxValue)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.NumericOutOfRange,
                Message = $"Field '{field.Name}' value {longValue} is out of range for bigint",
                ActualValue = longValue,
                ExpectedValue = $"{long.MinValue} to {long.MaxValue}"
            });
        }

        return errors;
    }

    private List<ValidationError> ValidateSmallInt(FieldDefinition field, object value)
    {
        var errors = new List<ValidationError>();

        if (value is not short and not byte and not int and not long)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects a small integer value",
                ActualValue = value,
                ExpectedValue = "smallint"
            });
            return errors;
        }

        var intValue = Convert.ToInt32(value);

        if (intValue < short.MinValue || intValue > short.MaxValue)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.NumericOutOfRange,
                Message = $"Field '{field.Name}' value {intValue} is out of range for smallint",
                ActualValue = intValue,
                ExpectedValue = $"{short.MinValue} to {short.MaxValue}"
            });
        }

        return errors;
    }

    private List<ValidationError> ValidateFloat(FieldDefinition field, object value)
    {
        var errors = new List<ValidationError>();

        if (value is not float and not double and not int and not long and not decimal)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects a numeric value",
                ActualValue = value,
                ExpectedValue = "float"
            });
            return errors;
        }

        var floatValue = Convert.ToSingle(value);

        if (float.IsInfinity(floatValue) || float.IsNaN(floatValue))
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.NumericOutOfRange,
                Message = $"Field '{field.Name}' value is not a valid float",
                ActualValue = value,
                ExpectedValue = "valid float"
            });
        }

        return errors;
    }

    private List<ValidationError> ValidateDouble(FieldDefinition field, object value)
    {
        var errors = new List<ValidationError>();

        if (value is not double and not float and not int and not long and not decimal)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects a numeric value",
                ActualValue = value,
                ExpectedValue = "double"
            });
            return errors;
        }

        var doubleValue = Convert.ToDouble(value);

        if (double.IsInfinity(doubleValue) || double.IsNaN(doubleValue))
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.NumericOutOfRange,
                Message = $"Field '{field.Name}' value is not a valid double",
                ActualValue = value,
                ExpectedValue = "valid double"
            });
        }

        // Validate precision and scale if specified
        if (field.Precision.HasValue || field.Scale.HasValue)
        {
            var decimalValue = Convert.ToDecimal(value);
            errors.AddRange(ValidateDecimalPrecisionAndScale(field, decimalValue));
        }

        return errors;
    }

    private List<ValidationError> ValidateDecimalPrecisionAndScale(FieldDefinition field, decimal value)
    {
        var errors = new List<ValidationError>();

        if (field.Precision.HasValue)
        {
            var valueString = value.ToString("G29"); // Max precision for decimal
            var parts = valueString.Split('.');
            var integerPart = parts[0].TrimStart('-');
            var fractionalPart = parts.Length > 1 ? parts[1] : string.Empty;
            var totalDigits = integerPart.Length + fractionalPart.Length;

            if (totalDigits > field.Precision.Value)
            {
                errors.Add(new ValidationError
                {
                    FieldName = field.Name,
                    ErrorCode = ValidationErrorCodes.PrecisionExceeded,
                    Message = $"Field '{field.Name}' has {totalDigits} total digits, exceeds precision of {field.Precision.Value}",
                    ActualValue = value,
                    ExpectedValue = $"precision {field.Precision.Value}"
                });
            }
        }

        if (field.Scale.HasValue)
        {
            var valueString = value.ToString("G29");
            var parts = valueString.Split('.');
            var fractionalPart = parts.Length > 1 ? parts[1] : string.Empty;

            if (fractionalPart.Length > field.Scale.Value)
            {
                errors.Add(new ValidationError
                {
                    FieldName = field.Name,
                    ErrorCode = ValidationErrorCodes.ScaleExceeded,
                    Message = $"Field '{field.Name}' has {fractionalPart.Length} decimal places, exceeds scale of {field.Scale.Value}",
                    ActualValue = value,
                    ExpectedValue = $"scale {field.Scale.Value}"
                });
            }
        }

        return errors;
    }

    private List<ValidationError> ValidateDateTime(FieldDefinition field, object value)
    {
        var errors = new List<ValidationError>();

        if (value is not DateTime and not DateTimeOffset)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects a datetime value",
                ActualValue = value,
                ExpectedValue = "datetime"
            });
        }

        return errors;
    }

    private List<ValidationError> ValidateGuid(FieldDefinition field, object value)
    {
        var errors = new List<ValidationError>();

        if (value is not Guid)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects a UUID/GUID value",
                ActualValue = value,
                ExpectedValue = "UUID"
            });
        }

        return errors;
    }

    private List<ValidationError> ValidateBoolean(FieldDefinition field, object value)
    {
        var errors = new List<ValidationError>();

        if (value is not bool)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects a boolean value",
                ActualValue = value,
                ExpectedValue = "boolean"
            });
        }

        return errors;
    }

    private List<ValidationError> ValidateString(FieldDefinition field, object value, SchemaValidationOptions options)
    {
        var errors = new List<ValidationError>();

        if (value is not string stringValue)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects a string value",
                ActualValue = value,
                ExpectedValue = "string"
            });
            return errors;
        }

        // Validate string length
        if (field.MaxLength.HasValue && stringValue.Length > field.MaxLength.Value)
        {
            if (options.TruncateLongStrings)
            {
                _logger.LogWarning("Truncating field '{FieldName}' from {ActualLength} to {MaxLength} characters",
                    field.Name, stringValue.Length, field.MaxLength.Value);
                // Note: Actual truncation would be applied by the caller
            }
            else
            {
                errors.Add(new ValidationError
                {
                    FieldName = field.Name,
                    ErrorCode = ValidationErrorCodes.StringTooLong,
                    Message = $"Field '{field.Name}' exceeds maximum length of {field.MaxLength.Value} characters",
                    ActualValue = stringValue.Length,
                    ExpectedValue = field.MaxLength.Value
                });
            }
        }

        // Validate custom formats if enabled
        if (options.ValidateCustomFormats)
        {
            errors.AddRange(ValidateStringFormat(field, stringValue));
        }

        return errors;
    }

    private List<ValidationError> ValidateStringFormat(FieldDefinition field, string value)
    {
        var errors = new List<ValidationError>();
        var fieldNameLower = field.Name.ToLowerInvariant();

        // Detect common field types by name convention
        if (fieldNameLower.Contains("email") && !CustomFieldValidators.IsValidEmail(value))
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidFormat,
                Message = $"Field '{field.Name}' contains invalid email address",
                ActualValue = value,
                ExpectedValue = "valid email address"
            });
        }
        else if ((fieldNameLower.Contains("url") || fieldNameLower.Contains("link") || fieldNameLower.Contains("href")) &&
                 !value.IsNullOrWhiteSpace() &&
                 !CustomFieldValidators.IsValidUrl(value))
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidFormat,
                Message = $"Field '{field.Name}' contains invalid URL",
                ActualValue = value,
                ExpectedValue = "valid URL"
            });
        }
        else if (fieldNameLower.Contains("phone") && !value.IsNullOrWhiteSpace() && !CustomFieldValidators.IsValidPhone(value))
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidFormat,
                Message = $"Field '{field.Name}' contains invalid phone number",
                ActualValue = value,
                ExpectedValue = "valid phone number"
            });
        }
        else if ((fieldNameLower.Contains("zip") || fieldNameLower.Contains("postal")) &&
                 !value.IsNullOrWhiteSpace() &&
                 !CustomFieldValidators.IsValidUsPostalCode(value))
        {
            // Only validate if it looks like a US postal code
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidFormat,
                Message = $"Field '{field.Name}' contains invalid US postal code",
                ActualValue = value,
                ExpectedValue = "valid US postal code (e.g., 12345 or 12345-6789)"
            });
        }

        return errors;
    }

    private List<ValidationError> ValidateGeometry(FieldDefinition field, object value)
    {
        var errors = new List<ValidationError>();

        // Geometry is typically stored as GeoJSON string
        if (value is not string geometryJson)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects a geometry value (GeoJSON string)",
                ActualValue = value,
                ExpectedValue = "GeoJSON string"
            });
            return errors;
        }

        // Basic validation - ensure it's not empty
        if (geometryJson.IsNullOrWhiteSpace())
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidGeometry,
                Message = $"Field '{field.Name}' contains empty geometry",
                ActualValue = geometryJson,
                ExpectedValue = "valid GeoJSON"
            });
        }

        // TODO: Could add more detailed GeoJSON validation here
        // For now, assume the GeoJSON is valid if it's a non-empty string

        return errors;
    }

    private List<ValidationError> ValidateBinary(FieldDefinition field, object value)
    {
        var errors = new List<ValidationError>();

        if (value is not byte[] and not string)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.InvalidType,
                Message = $"Field '{field.Name}' expects a binary value (byte array or base64 string)",
                ActualValue = value,
                ExpectedValue = "byte array or base64 string"
            });
        }

        // Validate size constraints if MaxLength is specified
        if (field.MaxLength.HasValue && value is byte[] bytes && bytes.Length > field.MaxLength.Value)
        {
            errors.Add(new ValidationError
            {
                FieldName = field.Name,
                ErrorCode = ValidationErrorCodes.StringTooLong,
                Message = $"Field '{field.Name}' binary data exceeds maximum size of {field.MaxLength.Value} bytes",
                ActualValue = bytes.Length,
                ExpectedValue = field.MaxLength.Value
            });
        }

        return errors;
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Defines a validation rule for feature attributes
/// </summary>
public class ValidationRule
{
    /// <summary>
    /// Field name this rule applies to
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// Display name for the field
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Type of validation
    /// </summary>
    public required ValidationType Type { get; set; }

    /// <summary>
    /// Whether this field is required
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Minimum value (for numeric fields)
    /// </summary>
    public double? MinValue { get; set; }

    /// <summary>
    /// Maximum value (for numeric fields)
    /// </summary>
    public double? MaxValue { get; set; }

    /// <summary>
    /// Minimum length (for string fields)
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Maximum length (for string fields)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Regular expression pattern (for string fields)
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Allowed values (for domain/dropdown fields)
    /// </summary>
    public List<ValidationDomainValue> DomainValues { get; set; } = new();

    /// <summary>
    /// Custom error message
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Custom validation function (JavaScript expression)
    /// </summary>
    public string? CustomValidation { get; set; }

    /// <summary>
    /// Whether this rule is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Validate a value against this rule
    /// </summary>
    public ValidationResult Validate(object? value)
    {
        var result = new ValidationResult { IsValid = true };

        // Check required
        if (IsRequired && (value == null || string.IsNullOrWhiteSpace(value.ToString())))
        {
            result.IsValid = false;
            result.ErrorMessage = ErrorMessage ?? $"{DisplayName ?? FieldName} is required";
            return result;
        }

        if (value == null)
        {
            return result; // null is ok if not required
        }

        // Type-specific validation
        switch (Type)
        {
            case ValidationType.String:
                return ValidateString(value.ToString()!);

            case ValidationType.Number:
                return ValidateNumber(value);

            case ValidationType.Integer:
                return ValidateInteger(value);

            case ValidationType.Date:
                return ValidateDate(value);

            case ValidationType.Email:
                return ValidateEmail(value.ToString()!);

            case ValidationType.Url:
                return ValidateUrl(value.ToString()!);

            case ValidationType.Domain:
                return ValidateDomain(value);

            case ValidationType.Boolean:
                return ValidateBoolean(value);

            default:
                return result;
        }
    }

    private ValidationResult ValidateString(string value)
    {
        var result = new ValidationResult { IsValid = true };

        if (MinLength.HasValue && value.Length < MinLength.Value)
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be at least {MinLength.Value} characters";
        }
        else if (MaxLength.HasValue && value.Length > MaxLength.Value)
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must not exceed {MaxLength.Value} characters";
        }
        else if (!string.IsNullOrEmpty(Pattern))
        {
            try
            {
                var regex = new System.Text.RegularExpressions.Regex(Pattern);
                if (!regex.IsMatch(value))
                {
                    result.IsValid = false;
                    result.ErrorMessage = ErrorMessage ?? $"{DisplayName ?? FieldName} format is invalid";
                }
            }
            catch
            {
                result.IsValid = false;
                result.ErrorMessage = "Invalid validation pattern";
            }
        }

        return result;
    }

    private ValidationResult ValidateNumber(object value)
    {
        var result = new ValidationResult { IsValid = true };

        if (!double.TryParse(value.ToString(), out var numValue))
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be a valid number";
            return result;
        }

        if (MinValue.HasValue && numValue < MinValue.Value)
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be at least {MinValue.Value}";
        }
        else if (MaxValue.HasValue && numValue > MaxValue.Value)
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must not exceed {MaxValue.Value}";
        }

        return result;
    }

    private ValidationResult ValidateInteger(object value)
    {
        var result = new ValidationResult { IsValid = true };

        if (!int.TryParse(value.ToString(), out var intValue))
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be a valid integer";
            return result;
        }

        if (MinValue.HasValue && intValue < MinValue.Value)
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be at least {MinValue.Value}";
        }
        else if (MaxValue.HasValue && intValue > MaxValue.Value)
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must not exceed {MaxValue.Value}";
        }

        return result;
    }

    private ValidationResult ValidateDate(object value)
    {
        var result = new ValidationResult { IsValid = true };

        if (value is not DateTime && !DateTime.TryParse(value.ToString(), out _))
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be a valid date";
        }

        return result;
    }

    private ValidationResult ValidateEmail(string value)
    {
        var result = new ValidationResult { IsValid = true };

        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        var regex = new System.Text.RegularExpressions.Regex(emailPattern);

        if (!regex.IsMatch(value))
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be a valid email address";
        }

        return result;
    }

    private ValidationResult ValidateUrl(string value)
    {
        var result = new ValidationResult { IsValid = true };

        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be a valid URL";
        }

        return result;
    }

    private ValidationResult ValidateDomain(object value)
    {
        var result = new ValidationResult { IsValid = true };

        if (DomainValues.Count == 0)
        {
            return result;
        }

        var valueStr = value.ToString();
        if (!DomainValues.Any(dv => dv.Code?.ToString() == valueStr))
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be one of the allowed values";
        }

        return result;
    }

    private ValidationResult ValidateBoolean(object value)
    {
        var result = new ValidationResult { IsValid = true };

        if (value is not bool && !bool.TryParse(value.ToString(), out _))
        {
            result.IsValid = false;
            result.ErrorMessage = $"{DisplayName ?? FieldName} must be true or false";
        }

        return result;
    }
}

/// <summary>
/// Types of validation
/// </summary>
public enum ValidationType
{
    /// <summary>
    /// String/text field
    /// </summary>
    String,

    /// <summary>
    /// Numeric field (decimal)
    /// </summary>
    Number,

    /// <summary>
    /// Integer field
    /// </summary>
    Integer,

    /// <summary>
    /// Date/time field
    /// </summary>
    Date,

    /// <summary>
    /// Email address
    /// </summary>
    Email,

    /// <summary>
    /// URL/web address
    /// </summary>
    Url,

    /// <summary>
    /// Boolean field
    /// </summary>
    Boolean,

    /// <summary>
    /// Domain/coded value (dropdown)
    /// </summary>
    Domain,

    /// <summary>
    /// Custom validation
    /// </summary>
    Custom
}

/// <summary>
/// Domain value for dropdown fields
/// </summary>
public class ValidationDomainValue
{
    /// <summary>
    /// Code/value stored in database
    /// </summary>
    public required object Code { get; set; }

    /// <summary>
    /// Display name shown to user
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of this value
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Display order
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this value is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Result of a validation check
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional validation details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}

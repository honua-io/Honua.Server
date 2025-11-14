// <copyright file="AlertInputValidator.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text;
using System.Text.RegularExpressions;
using Honua.Server.AlertReceiver.Configuration;
using Microsoft.Extensions.Options;

namespace Honua.Server.AlertReceiver.Validation;

/// <summary>
/// Provides comprehensive input validation for alert label keys and values to protect against
/// SQL injection, XSS, JSON injection, and other malicious payloads.
/// </summary>
/// <remarks>
/// Security considerations:
/// - Label keys must follow a strict naming convention to prevent injection attacks
/// - Values are sanitized to remove control characters and null bytes
/// - HTML encoding should be applied when displaying values in web UI
/// - Validation is applied consistently across all alert ingestion endpoints
/// - Known safe label keys are configurable via appsettings.json
///
/// Configuration:
/// Add the following to your appsettings.json to customize the known safe labels:
/// <code>
/// "AlertValidation": {
///   "Labels": {
///     "KnownSafeLabels": [
///       "severity",
///       "priority",
///       "environment",
///       "custom_label_1",
///       "custom_label_2"
///     ]
///   }
/// }
/// </code>
/// </remarks>
public class AlertInputValidator
{
    // Regex for label keys: alphanumeric, underscore, hyphen, and dot only
    // This prevents SQL injection, XSS, and other injection attacks
    private static readonly Regex LabelKeyPattern = new(
        @"^[a-zA-Z0-9_.-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    // Known safe label keys loaded from configuration
    private readonly HashSet<string> knownSafeLabelKeys;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlertInputValidator"/> class.
    /// </summary>
    /// <param name="labelConfiguration">The alert label configuration.</param>
    public AlertInputValidator(IOptions<AlertLabelConfiguration> labelConfiguration)
    {
        ArgumentNullException.ThrowIfNull(labelConfiguration);

        var config = labelConfiguration.Value;
        this.knownSafeLabelKeys = new HashSet<string>(
            config.KnownSafeLabels ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates a label key for security and format compliance.
    /// </summary>
    /// <param name="key">The label key to validate.</param>
    /// <param name="errorMessage">The validation error message if validation fails.</param>
    /// <returns>True if the key is valid; otherwise, false.</returns>
    public bool ValidateLabelKey(string key, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            errorMessage = "Label key cannot be null or empty";
            return false;
        }

        if (key.Length > 256)
        {
            errorMessage = $"Label key '{TruncateForDisplay(key, 50)}' exceeds 256 character limit";
            return false;
        }

        // Check for control characters and null bytes
        if (ContainsControlCharacters(key))
        {
            errorMessage = $"Label key '{TruncateForDisplay(key, 50)}' contains control characters";
            return false;
        }

        // Validate against pattern to prevent injection attacks
        if (!LabelKeyPattern.IsMatch(key))
        {
            errorMessage = $"Label key '{TruncateForDisplay(key, 50)}' contains invalid characters. Only alphanumeric, underscore, hyphen, and dot are allowed";
            return false;
        }

        // Additional validation: keys should not start or end with dots or hyphens
        if (key.StartsWith('.') || key.StartsWith('-') || key.EndsWith('.') || key.EndsWith('-'))
        {
            errorMessage = $"Label key '{TruncateForDisplay(key, 50)}' cannot start or end with dot or hyphen";
            return false;
        }

        // Warn if key is not in the known safe list (informational, not blocking)
        // This can be logged for monitoring purposes
        if (!this.knownSafeLabelKeys.Contains(key))
        {
            // Allow custom keys but log for monitoring
            // errorMessage is not set as this is just informational
        }

        return true;
    }

    /// <summary>
    /// Validates and sanitizes a label value, removing control characters and null bytes.
    /// </summary>
    /// <param name="key">The label key (for error messages).</param>
    /// <param name="value">The label value to validate.</param>
    /// <param name="sanitizedValue">The sanitized value with control characters removed.</param>
    /// <param name="errorMessage">The validation error message if validation fails.</param>
    /// <returns>True if the value is valid; otherwise, false.</returns>
    public bool ValidateAndSanitizeLabelValue(
        string key,
        string value,
        out string sanitizedValue,
        out string? errorMessage)
    {
        errorMessage = null;
        sanitizedValue = value;

        if (value == null)
        {
            errorMessage = $"Label value for key '{key}' cannot be null";
            return false;
        }

        if (value.Length > 1000)
        {
            errorMessage = $"Label value for key '{key}' exceeds 1000 character limit";
            return false;
        }

        // Sanitize value: remove control characters and null bytes
        // This protects against various injection attacks
        sanitizedValue = SanitizeValue(value);

        // Check if sanitization removed too much content
        if (string.IsNullOrWhiteSpace(sanitizedValue) && !string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"Label value for key '{key}' contains only control characters";
            return false;
        }

        // Warn if value was modified by sanitization (for logging/monitoring)
        if (sanitizedValue != value)
        {
            // Value was sanitized - this should be logged for security monitoring
            // but is not a validation failure
        }

        return true;
    }

    /// <summary>
    /// Validates a context dictionary key for security and format compliance.
    /// Context keys have similar validation rules to label keys.
    /// </summary>
    public bool ValidateContextKey(string key, out string? errorMessage)
    {
        // Context keys follow the same rules as label keys
        return ValidateLabelKey(key, out errorMessage);
    }

    /// <summary>
    /// Validates and sanitizes a context value.
    /// Context values can be of various types (string, number, boolean, etc.).
    /// </summary>
    public bool ValidateAndSanitizeContextValue(
        string key,
        object? value,
        out object? sanitizedValue,
        out string? errorMessage)
    {
        errorMessage = null;
        sanitizedValue = value;

        if (value == null)
        {
            // Null values are allowed in context
            return true;
        }

        // For string values, apply the same sanitization as label values
        if (value is string stringValue)
        {
            if (stringValue.Length > 4000)
            {
                errorMessage = $"Context value for key '{key}' exceeds 4000 character limit";
                return false;
            }

            var sanitized = SanitizeValue(stringValue);
            sanitizedValue = sanitized;

            // Check if sanitization removed too much content
            if (string.IsNullOrWhiteSpace(sanitized) && !string.IsNullOrWhiteSpace(stringValue))
            {
                errorMessage = $"Context value for key '{key}' contains only control characters";
                return false;
            }

            return true;
        }

        // For numeric types, validate range
        if (value is int or long or float or double or decimal)
        {
            // Numeric values are generally safe, but check for special values
            if (value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
            {
                errorMessage = $"Context value for key '{key}' contains invalid numeric value (NaN or Infinity)";
                return false;
            }

            if (value is float f && (float.IsNaN(f) || float.IsInfinity(f)))
            {
                errorMessage = $"Context value for key '{key}' contains invalid numeric value (NaN or Infinity)";
                return false;
            }

            return true;
        }

        // Boolean values are safe
        if (value is bool)
        {
            return true;
        }

        // For other types, convert to string and validate
        var strValue = value.ToString();
        if (strValue != null && strValue.Length > 4000)
        {
            errorMessage = $"Context value for key '{key}' exceeds 4000 character limit when serialized";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates all labels in a dictionary, sanitizing values as needed.
    /// </summary>
    public bool ValidateLabels(
        Dictionary<string, string>? labels,
        out Dictionary<string, string>? sanitizedLabels,
        out List<string> errors)
    {
        sanitizedLabels = null;
        errors = new List<string>();

        if (labels == null || labels.Count == 0)
        {
            return true;
        }

        if (labels.Count > 50)
        {
            errors.Add("Maximum 50 labels allowed");
            return false;
        }

        sanitizedLabels = new Dictionary<string, string>(labels.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in labels)
        {
            if (!ValidateLabelKey(key, out var keyError))
            {
                errors.Add(keyError!);
                continue;
            }

            if (!ValidateAndSanitizeLabelValue(key, value, out var sanitizedValue, out var valueError))
            {
                errors.Add(valueError!);
                continue;
            }

            sanitizedLabels[key] = sanitizedValue;
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Validates all context entries in a dictionary, sanitizing values as needed.
    /// </summary>
    public bool ValidateContext(
        Dictionary<string, object>? context,
        out Dictionary<string, object>? sanitizedContext,
        out List<string> errors)
    {
        sanitizedContext = null;
        errors = new List<string>();

        if (context == null || context.Count == 0)
        {
            return true;
        }

        if (context.Count > 100)
        {
            errors.Add("Maximum 100 context entries allowed");
            return false;
        }

        sanitizedContext = new Dictionary<string, object>(context.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in context)
        {
            if (!ValidateContextKey(key, out var keyError))
            {
                errors.Add(keyError!);
                continue;
            }

            if (!ValidateAndSanitizeContextValue(key, value, out var sanitizedValue, out var valueError))
            {
                errors.Add(valueError!);
                continue;
            }

            if (sanitizedValue != null)
            {
                sanitizedContext[key] = sanitizedValue;
            }
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Sanitizes a string value by removing control characters and null bytes.
    /// Preserves printable characters, whitespace (space, tab, newline), and common punctuation.
    /// </summary>
    private static string SanitizeValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            // Allow printable ASCII and extended characters, common whitespace
            // Block control characters (0x00-0x1F except tab, newline, carriage return)
            // Block DEL (0x7F) and C1 control characters (0x80-0x9F)
            if (c == '\t' || c == '\n' || c == '\r')
            {
                // Allow tab, newline, carriage return
                sb.Append(c);
            }
            else if (c < 0x20 || c == 0x7F || (c >= 0x80 && c <= 0x9F))
            {
                // Skip control characters and null bytes
                continue;
            }
            else
            {
                // Allow all other characters
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Checks if a string contains control characters or null bytes.
    /// </summary>
    private static bool ContainsControlCharacters(string value)
    {
        foreach (var c in value)
        {
            if (c < 0x20 || c == 0x7F || (c >= 0x80 && c <= 0x9F))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Truncates a string for display in error messages.
    /// </summary>
    private static string TruncateForDisplay(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Checks if a label key is in the known safe list.
    /// This can be used for logging/monitoring purposes.
    /// </summary>
    public bool IsKnownSafeLabelKey(string key)
    {
        return this.knownSafeLabelKeys.Contains(key);
    }
}

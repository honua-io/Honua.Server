// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Honua.Server.Core.Configuration.V2.Validation;

/// <summary>
/// Result of configuration validation.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<ValidationError> _errors = new();
    private readonly List<ValidationWarning> _warnings = new();

    /// <summary>
    /// Whether validation passed (no errors).
    /// Warnings are allowed.
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// Validation errors (blocking issues).
    /// </summary>
    public IReadOnlyList<ValidationError> Errors => _errors;

    /// <summary>
    /// Validation warnings (non-blocking issues).
    /// </summary>
    public IReadOnlyList<ValidationWarning> Warnings => _warnings;

    /// <summary>
    /// Add a validation error.
    /// </summary>
    public void AddError(string message, string? location = null, string? suggestion = null)
    {
        _errors.Add(new ValidationError(message, location, suggestion));
    }

    /// <summary>
    /// Add a validation warning.
    /// </summary>
    public void AddWarning(string message, string? location = null, string? suggestion = null)
    {
        _warnings.Add(new ValidationWarning(message, location, suggestion));
    }

    /// <summary>
    /// Merge another validation result into this one.
    /// </summary>
    public void Merge(ValidationResult other)
    {
        _errors.AddRange(other.Errors);
        _warnings.AddRange(other.Warnings);
    }

    /// <summary>
    /// Get a formatted summary of validation results.
    /// </summary>
    public string GetSummary()
    {
        var sb = new StringBuilder();

        if (IsValid && _warnings.Count == 0)
        {
            sb.AppendLine("✓ Configuration is valid");
            return sb.ToString();
        }

        if (_errors.Count > 0)
        {
            sb.AppendLine($"✗ Validation failed with {_errors.Count} error(s)");
            sb.AppendLine();

            foreach (var error in _errors)
            {
                sb.AppendLine(error.ToString());
                sb.AppendLine();
            }
        }

        if (_warnings.Count > 0)
        {
            sb.AppendLine($"⚠ {_warnings.Count} warning(s)");
            sb.AppendLine();

            foreach (var warning in _warnings)
            {
                sb.AppendLine(warning.ToString());
                sb.AppendLine();
            }
        }

        if (IsValid)
        {
            sb.AppendLine("✓ Configuration is valid (with warnings)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Create a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new();

    /// <summary>
    /// Create a failed validation result with an error.
    /// </summary>
    public static ValidationResult Error(string message, string? location = null, string? suggestion = null)
    {
        var result = new ValidationResult();
        result.AddError(message, location, suggestion);
        return result;
    }
}

/// <summary>
/// Validation error (blocking issue).
/// </summary>
public sealed record ValidationError(string Message, string? Location = null, string? Suggestion = null)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("ERROR: ");

        if (!string.IsNullOrWhiteSpace(Location))
        {
            sb.Append($"[{Location}] ");
        }

        sb.AppendLine(Message);

        if (!string.IsNullOrWhiteSpace(Suggestion))
        {
            sb.AppendLine($"  → Suggestion: {Suggestion}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Validation warning (non-blocking issue).
/// </summary>
public sealed record ValidationWarning(string Message, string? Location = null, string? Suggestion = null)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("WARNING: ");

        if (!string.IsNullOrWhiteSpace(Location))
        {
            sb.Append($"[{Location}] ");
        }

        sb.AppendLine(Message);

        if (!string.IsNullOrWhiteSpace(Suggestion))
        {
            sb.AppendLine($"  → Suggestion: {Suggestion}");
        }

        return sb.ToString();
    }
}

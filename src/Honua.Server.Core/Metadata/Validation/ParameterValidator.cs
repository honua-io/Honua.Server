// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates SQL parameter definitions including type checking and validation rules.
/// </summary>
internal static class ParameterValidator
{
    /// <summary>
    /// Validates a SQL view parameter definition.
    /// </summary>
    /// <param name="layerId">The layer ID (for error messages).</param>
    /// <param name="parameter">The parameter to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when parameter validation fails.</exception>
    public static void ValidateParameter(string layerId, SqlViewParameterDefinition parameter)
    {
        if (parameter.Name.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layerId}' SQL view contains a parameter without a name.");
        }

        if (parameter.Type.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' must have a type.");
        }

        // Validate type is a known type
        var validTypes = new[] { "string", "integer", "long", "double", "decimal", "boolean", "date", "datetime" };
        if (!validTypes.Contains(parameter.Type.ToLowerInvariant()))
        {
            throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has invalid type '{parameter.Type}'. Valid types: {string.Join(", ", validTypes)}");
        }

        // Validate parameter validation rules
        if (parameter.Validation is not null)
        {
            ValidateParameterValidation(layerId, parameter);
        }
    }

    /// <summary>
    /// Validates the validation rules for a parameter (e.g., min/max, length, patterns).
    /// </summary>
    /// <param name="layerId">The layer ID (for error messages).</param>
    /// <param name="parameter">The parameter to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when parameter validation rules are invalid.</exception>
    private static void ValidateParameterValidation(string layerId, SqlViewParameterDefinition parameter)
    {
        var validation = parameter.Validation!;

        // Numeric validations
        if (validation.Min.HasValue || validation.Max.HasValue)
        {
            var numericTypes = new[] { "integer", "long", "double", "decimal" };
            if (!numericTypes.Contains(parameter.Type.ToLowerInvariant()))
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has Min/Max validation but is not a numeric type.");
            }

            if (validation.Min.HasValue && validation.Max.HasValue && validation.Min.Value > validation.Max.Value)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has Min greater than Max.");
            }
        }

        // String validations
        if (validation.MinLength.HasValue || validation.MaxLength.HasValue || validation.Pattern.HasValue())
        {
            if (!parameter.Type.Equals("string", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has string validation but is not a string type.");
            }

            if (validation.MinLength.HasValue && validation.MinLength.Value < 0)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' MinLength cannot be negative.");
            }

            if (validation.MaxLength.HasValue && validation.MaxLength.Value < 1)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' MaxLength must be at least 1.");
            }

            if (validation.MinLength.HasValue && validation.MaxLength.HasValue && validation.MinLength.Value > validation.MaxLength.Value)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' MinLength is greater than MaxLength.");
            }

            // Validate regex pattern if present
            if (validation.Pattern.HasValue())
            {
                try
                {
                    _ = new System.Text.RegularExpressions.Regex(validation.Pattern!);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has invalid regex pattern: {ex.Message}");
                }
            }
        }

        // Allowed values validation
        if (validation.AllowedValues is { Count: > 0 })
        {
            if (validation.AllowedValues.Count > 1000)
            {
                throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has too many allowed values (max 1000).");
            }

            foreach (var value in validation.AllowedValues)
            {
                if (value.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Layer '{layerId}' SQL view parameter '{parameter.Name}' has an empty allowed value.");
                }
            }
        }
    }
}

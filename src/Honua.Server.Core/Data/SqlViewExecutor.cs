// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Data;

/// <summary>
/// Executes SQL views with secure parameter substitution.
/// This class ensures that all parameters are validated and substituted using parameterized queries
/// to prevent SQL injection attacks.
/// </summary>
public sealed class SqlViewExecutor
{
    private readonly ILogger<SqlViewExecutor> _logger;

    public SqlViewExecutor(ILogger<SqlViewExecutor> logger)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SqlViewExecutor>.Instance;
    }

    /// <summary>
    /// Processes a SQL view query by substituting parameters safely.
    /// Returns the processed SQL and a dictionary of parameter values for use with parameterized queries.
    /// </summary>
    /// <param name="sqlView">The SQL view definition.</param>
    /// <param name="requestParameters">Parameters from the HTTP request.</param>
    /// <param name="layerId">Layer ID for error messages.</param>
    /// <returns>A tuple containing the processed SQL and parameter values.</returns>
    public (string Sql, IReadOnlyDictionary<string, object?> Parameters) ProcessSqlView(
        SqlViewDefinition sqlView,
        IReadOnlyDictionary<string, string> requestParameters,
        string layerId)
    {
        Guard.NotNull(sqlView);
        Guard.NotNull(requestParameters);
        Guard.NotNullOrWhiteSpace(layerId);

        var sql = sqlView.Sql;
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

        // Process each defined parameter
        foreach (var paramDef in sqlView.Parameters)
        {
            // Get the raw value from request or use default
            var rawValue = GetParameterValue(paramDef, requestParameters);

            // Validate and convert the value
            var typedValue = ValidateAndConvertParameter(paramDef, rawValue, layerId);

            // Store the parameter value (will be used in parameterized query)
            var paramKey = $"sqlview_{paramDef.Name}";
            parameters[paramKey] = typedValue;

            // Replace the parameter placeholder in SQL with the actual parameter name
            // e.g., `:min_population` becomes `@sqlview_min_population` for Postgres
            // The actual substitution character (@, :, ?) depends on the database provider
            sql = ReplaceParameterPlaceholder(sql, paramDef.Name, paramKey);
        }

        // Apply security filter if present
        if (sqlView.SecurityFilter.HasValue())
        {
            sql = ApplySecurityFilter(sql, sqlView.SecurityFilter!);
        }

        return (sql, parameters);
    }

    /// <summary>
    /// Gets the parameter value from the request or returns the default value.
    /// </summary>
    private string? GetParameterValue(
        SqlViewParameterDefinition paramDef,
        IReadOnlyDictionary<string, string> requestParameters)
    {
        // Try to get from request parameters
        if (requestParameters.TryGetValue(paramDef.Name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // Use default value if provided
        if (paramDef.DefaultValue.HasValue())
        {
            return paramDef.DefaultValue;
        }

        // If required and no value, throw exception
        if (paramDef.Required)
        {
            throw new SqlViewParameterValidationException(
                paramDef.Name,
                "Parameter is required but not provided and has no default value");
        }

        return null;
    }

    /// <summary>
    /// Validates and converts a parameter value to its typed representation.
    /// </summary>
    private object? ValidateAndConvertParameter(
        SqlViewParameterDefinition paramDef,
        string? rawValue,
        string layerId)
    {
        // If value is null and parameter is not required, return null
        if (rawValue is null)
        {
            if (!paramDef.Required)
            {
                return null;
            }

            throw new SqlViewParameterValidationException(
                paramDef.Name,
                "Parameter is required but no value was provided");
        }

        // Apply validation rules if present
        if (paramDef.Validation is not null)
        {
            ValidateParameterValue(paramDef, rawValue);
        }

        // Convert to the appropriate type
        return ConvertParameterValue(paramDef, rawValue);
    }

    /// <summary>
    /// Validates a parameter value against its validation rules.
    /// </summary>
    private void ValidateParameterValue(SqlViewParameterDefinition paramDef, string value)
    {
        var validation = paramDef.Validation!;
        var errorMessage = validation.ErrorMessage ?? "Validation failed";

        // Check allowed values (enum-like validation)
        if (validation.AllowedValues is { Count: > 0 })
        {
            if (!validation.AllowedValues.Contains(value, StringComparer.Ordinal))
            {
                throw new SqlViewParameterValidationException(
                    paramDef.Name,
                    $"{errorMessage}. Value must be one of: {string.Join(", ", validation.AllowedValues)}");
            }
        }

        // String-specific validations
        if (paramDef.Type.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            if (validation.MinLength.HasValue && value.Length < validation.MinLength.Value)
            {
                throw new SqlViewParameterValidationException(
                    paramDef.Name,
                    $"{errorMessage}. Minimum length is {validation.MinLength.Value}");
            }

            if (validation.MaxLength.HasValue && value.Length > validation.MaxLength.Value)
            {
                throw new SqlViewParameterValidationException(
                    paramDef.Name,
                    $"{errorMessage}. Maximum length is {validation.MaxLength.Value}");
            }

            if (validation.Pattern.HasValue())
            {
                var regex = new Regex(validation.Pattern!, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                if (!regex.IsMatch(value))
                {
                    throw new SqlViewParameterValidationException(
                        paramDef.Name,
                        $"{errorMessage}. Value must match pattern: {validation.Pattern}");
                }
            }
        }

        // Numeric validations
        var numericTypes = new[] { "integer", "long", "double", "decimal" };
        if (numericTypes.Contains(paramDef.Type, StringComparer.OrdinalIgnoreCase))
        {
            if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
            {
                throw new SqlViewParameterValidationException(
                    paramDef.Name,
                    $"{errorMessage}. Value must be a valid number");
            }

            if (validation.Min.HasValue && numericValue < validation.Min.Value)
            {
                throw new SqlViewParameterValidationException(
                    paramDef.Name,
                    $"{errorMessage}. Value must be at least {validation.Min.Value}");
            }

            if (validation.Max.HasValue && numericValue > validation.Max.Value)
            {
                throw new SqlViewParameterValidationException(
                    paramDef.Name,
                    $"{errorMessage}. Value must be at most {validation.Max.Value}");
            }
        }
    }

    /// <summary>
    /// Converts a parameter value from string to its typed representation.
    /// </summary>
    private object ConvertParameterValue(SqlViewParameterDefinition paramDef, string value)
    {
        try
        {
            return paramDef.Type.ToLowerInvariant() switch
            {
                "string" => value,
                "integer" => int.Parse(value, CultureInfo.InvariantCulture),
                "long" => long.Parse(value, CultureInfo.InvariantCulture),
                "double" => double.Parse(value, CultureInfo.InvariantCulture),
                "decimal" => decimal.Parse(value, CultureInfo.InvariantCulture),
                "boolean" => bool.Parse(value),
                "date" => DateOnly.Parse(value, CultureInfo.InvariantCulture),
                "datetime" => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                _ => throw new SqlViewParameterValidationException(
                    paramDef.Name,
                    $"Unsupported parameter type: {paramDef.Type}")
            };
        }
        catch (Exception ex) when (ex is not SqlViewParameterValidationException)
        {
            throw new SqlViewParameterValidationException(
                paramDef.Name,
                $"Failed to convert value '{value}' to type {paramDef.Type}: {ex.Message}");
        }
    }

    /// <summary>
    /// Replaces parameter placeholders in the SQL with database-specific parameter names.
    /// This converts `:paramName` to `@paramKey` for use in parameterized queries.
    /// </summary>
    private string ReplaceParameterPlaceholder(string sql, string parameterName, string parameterKey)
    {
        // Replace :paramName with @paramKey
        // We use word boundaries to ensure we only replace the exact parameter
        var pattern = $@":{Regex.Escape(parameterName)}\b";
        var replacement = $"@{parameterKey}";

        try
        {
            var result = Regex.Replace(
                sql,
                pattern,
                replacement,
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(100));

            return result;
        }
        catch (RegexMatchTimeoutException)
        {
            throw new SqlViewValidationException(
                $"Timeout while processing parameter '{parameterName}' in SQL query");
        }
    }

    /// <summary>
    /// Applies a security filter to the SQL query by wrapping it in a subquery.
    /// This ensures the security filter is always applied.
    /// </summary>
    private string ApplySecurityFilter(string sql, string securityFilter)
    {
        // Wrap the original SQL in a subquery and apply the security filter
        var sb = new StringBuilder();
        sb.Append("SELECT * FROM (");
        sb.Append(sql);
        sb.Append(") AS __sqlview_secure WHERE ");
        sb.Append(securityFilter);

        return sb.ToString();
    }

    /// <summary>
    /// Extracts parameter names from a SQL view query.
    /// This is used for validation and documentation purposes.
    /// </summary>
    public static IReadOnlyList<string> ExtractParameterNames(string sql)
    {
        Guard.NotNullOrWhiteSpace(sql);

        var parameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Match :paramName patterns
        var pattern = @":([a-zA-Z_][a-zA-Z0-9_]*)";
        var matches = Regex.Matches(sql, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                parameters.Add(match.Groups[1].Value);
            }
        }

        return parameters.ToList();
    }

    /// <summary>
    /// Validates that all parameters referenced in the SQL are defined.
    /// This is called during metadata validation.
    /// </summary>
    public static void ValidateParameterReferences(SqlViewDefinition sqlView, string layerId)
    {
        Guard.NotNull(sqlView);
        Guard.NotNullOrWhiteSpace(layerId);

        var referencedParams = ExtractParameterNames(sqlView.Sql);
        var definedParams = new HashSet<string>(
            sqlView.Parameters.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        // Check for referenced but not defined parameters
        foreach (var referencedParam in referencedParams)
        {
            if (!definedParams.Contains(referencedParam))
            {
                throw new InvalidOperationException(
                    $"SQL view for layer '{layerId}' references parameter ':{referencedParam}' but it is not defined in the parameters list");
            }
        }

        // Check for defined but not referenced parameters (warning, not error)
        foreach (var param in sqlView.Parameters)
        {
            if (!referencedParams.Contains(param.Name))
            {
                _logger.LogWarning("SQL view for layer {LayerId} defines parameter {ParameterName} but it is not used in the SQL query", layerId, param.Name);
            }
        }
    }

    /// <summary>
    /// Validates that all parameters referenced in the SQL are defined (static version).
    /// This is called during metadata validation.
    /// </summary>
    public static void ValidateParameterReferencesStatic(SqlViewDefinition sqlView, string layerId)
    {
        Guard.NotNull(sqlView);
        Guard.NotNullOrWhiteSpace(layerId);

        var referencedParams = ExtractParameterNames(sqlView.Sql);
        var definedParams = new HashSet<string>(
            sqlView.Parameters.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        // Check for referenced but not defined parameters
        foreach (var referencedParam in referencedParams)
        {
            if (!definedParams.Contains(referencedParam))
            {
                throw new InvalidOperationException(
                    $"SQL view for layer '{layerId}' references parameter ':{referencedParam}' but it is not defined in the parameters list");
            }
        }

        // Check for defined but not referenced parameters - no logging in static version
    }
}

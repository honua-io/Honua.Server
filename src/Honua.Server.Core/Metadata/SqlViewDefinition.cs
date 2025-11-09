// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Defines a virtual layer backed by a SQL query instead of a physical table.
/// This enables dynamic layers from complex queries, joins, and computed fields.
/// </summary>
public sealed record SqlViewDefinition
{
    /// <summary>
    /// The SQL query that defines this view. Must be a SELECT statement.
    /// Parameters are referenced using colon notation (:paramName) for consistency across database providers.
    /// Example: "SELECT * FROM cities WHERE population > :min_population AND region = :region"
    /// </summary>
    public required string Sql { get; init; }

    /// <summary>
    /// Optional description of what this SQL view does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Parameters that can be passed to the SQL query.
    /// These are substituted using parameterized queries to prevent SQL injection.
    /// </summary>
    public IReadOnlyList<SqlViewParameterDefinition> Parameters { get; init; } = Array.Empty<SqlViewParameterDefinition>();

    /// <summary>
    /// Whether to validate geometry in the query result.
    /// If true, ensures all returned geometries are valid.
    /// </summary>
    public bool ValidateGeometry { get; init; }

    /// <summary>
    /// Optional SQL hints for query optimization.
    /// Example: "USE INDEX (idx_geometry)" for MySQL.
    /// </summary>
    public string? Hints { get; init; }

    /// <summary>
    /// Maximum query execution timeout in seconds.
    /// If not specified, uses the default timeout from the data source.
    /// </summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Whether this SQL view is read-only.
    /// If true, editing operations (INSERT, UPDATE, DELETE) are not allowed.
    /// Default is true for safety.
    /// </summary>
    public bool ReadOnly { get; init; } = true;

    /// <summary>
    /// Optional SQL WHERE clause that is always appended to the query.
    /// This provides an additional security layer to restrict data access.
    /// Example: "deleted_at IS NULL"
    /// </summary>
    public string? SecurityFilter { get; init; }

    /// <summary>
    /// Database provider-specific settings.
    /// Key is the provider name (e.g., "postgres", "sqlserver", "mysql").
    /// Value contains provider-specific SQL or settings.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProviderSettings { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Defines a parameter for a SQL view query.
/// Parameters are validated and substituted using parameterized queries.
/// </summary>
public sealed record SqlViewParameterDefinition
{
    /// <summary>
    /// Parameter name as referenced in the SQL query (without the colon prefix).
    /// Example: "min_population" for parameter :min_population
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable title for the parameter.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Description of what this parameter does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Data type of the parameter.
    /// Supported types: "string", "integer", "long", "double", "decimal", "boolean", "date", "datetime"
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Default value if the parameter is not provided in the request.
    /// Must be compatible with the specified Type.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Whether this parameter is required.
    /// If true and no default value is provided, requests without this parameter will fail.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Validation rules for this parameter.
    /// </summary>
    public SqlViewParameterValidation? Validation { get; init; }
}

/// <summary>
/// Validation rules for SQL view parameters.
/// All validation is applied before parameter substitution to prevent SQL injection.
/// </summary>
public sealed record SqlViewParameterValidation
{
    /// <summary>
    /// Minimum value for numeric parameters.
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// Maximum value for numeric parameters.
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// Minimum length for string parameters.
    /// </summary>
    public int? MinLength { get; init; }

    /// <summary>
    /// Maximum length for string parameters.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Regular expression pattern for string parameters.
    /// Must match for the parameter to be valid.
    /// Example: "^[A-Za-z0-9_]+$" for alphanumeric strings.
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Allowed values for enum-like parameters.
    /// If specified, the parameter value must be one of these values.
    /// Example: ["north", "south", "east", "west"]
    /// </summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }

    /// <summary>
    /// Custom error message when validation fails.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Exception thrown when SQL view validation fails.
/// </summary>
public class SqlViewValidationException : Exception
{
    public SqlViewValidationException(string message) : base(message)
    {
    }

    public SqlViewValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when SQL view parameter validation fails.
/// </summary>
public class SqlViewParameterValidationException : SqlViewValidationException
{
    public string ParameterName { get; }

    public SqlViewParameterValidationException(string parameterName, string message)
        : base($"Parameter '{parameterName}' validation failed: {message}")
    {
        ParameterName = parameterName;
    }
}

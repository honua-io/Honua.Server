// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Validation;

/// <summary>
/// Validates that database schema matches metadata layer definitions.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Validates that all fields defined in a layer exist in the database with compatible types.
    /// </summary>
    /// <param name="layer">The layer definition to validate.</param>
    /// <param name="dataSource">The data source for the layer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any schema mismatches.</returns>
    Task<SchemaValidationResult> ValidateLayerAsync(
        LayerDefinition layer,
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of database schema validation.
/// </summary>
public sealed class SchemaValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<SchemaValidationError> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public static SchemaValidationResult Success() => new();

    public static SchemaValidationResult Failure(params SchemaValidationError[] errors)
    {
        var result = new SchemaValidationResult();
        result.Errors.AddRange(errors);
        return result;
    }

    public void AddError(SchemaValidationErrorType type, string message, string? fieldName = null)
    {
        Errors.Add(new SchemaValidationError(type, message, fieldName));
    }

    public void AddWarning(string message)
    {
        Warnings.Add(message);
    }
}

/// <summary>
/// Represents a schema validation error.
/// </summary>
public sealed class SchemaValidationError
{
    public SchemaValidationErrorType Type { get; }
    public string Message { get; }
    public string? FieldName { get; }

    public SchemaValidationError(SchemaValidationErrorType type, string message, string? fieldName = null)
    {
        Type = type;
        Message = message;
        FieldName = fieldName;
    }
}

/// <summary>
/// Types of schema validation errors.
/// </summary>
public enum SchemaValidationErrorType
{
    /// <summary>
    /// Table or view does not exist in database.
    /// </summary>
    TableNotFound,

    /// <summary>
    /// Column does not exist in database table.
    /// </summary>
    ColumnNotFound,

    /// <summary>
    /// Column exists but data type is incompatible with metadata definition.
    /// </summary>
    TypeMismatch,

    /// <summary>
    /// Primary key column not found.
    /// </summary>
    PrimaryKeyNotFound,

    /// <summary>
    /// Geometry column not found.
    /// </summary>
    GeometryColumnNotFound,

    /// <summary>
    /// Database connection failed.
    /// </summary>
    ConnectionError,

    /// <summary>
    /// Generic validation error.
    /// </summary>
    ValidationError
}

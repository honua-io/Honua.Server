// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Import.Validation;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for data ingestion operations.
/// </summary>
public sealed class DataIngestionOptions
{
    /// <summary>
    /// Number of features to insert in each batch operation.
    /// Default is 1000 features per batch.
    /// Larger batches improve throughput but use more memory.
    /// Smaller batches provide more granular progress updates.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Whether to use bulk insert operations for data ingestion.
    /// When true (default), uses optimized batch INSERT operations.
    /// When false, falls back to individual INSERT per feature (for debugging).
    /// </summary>
    public bool UseBulkInsert { get; set; } = true;

    /// <summary>
    /// How often to report progress during ingestion (in features).
    /// Default is every 100 features.
    /// Lower values provide more frequent updates but may impact performance.
    /// </summary>
    public int ProgressReportInterval { get; set; } = 100;

    /// <summary>
    /// Maximum number of retries for failed batch operations.
    /// Default is 3 retries.
    /// After exhausting retries, the entire ingestion fails.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout for each batch operation.
    /// Default is 5 minutes per batch.
    /// Increase for very large batches or slow networks.
    /// </summary>
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to wrap the entire ingestion in a single transaction.
    /// When true, all batches are committed together (all-or-nothing).
    /// When false (default), each batch is committed independently.
    /// Setting to true provides stronger consistency but holds locks longer.
    /// CRITICAL: This setting is now REQUIRED for data integrity.
    /// The default has been changed to true to prevent partial imports.
    /// </summary>
    public bool UseTransactionalIngestion { get; set; } = true;

    /// <summary>
    /// Timeout for the entire ingestion transaction.
    /// Default is 30 minutes for large dataset imports.
    /// If a single transaction exceeds this timeout, it will be rolled back.
    /// Should be set higher than BatchTimeout * (expected number of batches).
    /// </summary>
    public TimeSpan TransactionTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Transaction isolation level for data ingestion.
    /// Default is RepeatableRead for data consistency.
    /// Options: ReadCommitted, RepeatableRead, Serializable.
    /// Higher isolation levels provide stronger guarantees but may impact concurrency.
    /// </summary>
    public System.Data.IsolationLevel TransactionIsolationLevel { get; set; } = System.Data.IsolationLevel.RepeatableRead;

    /// <summary>
    /// Gets or sets whether to validate geometry during ingestion.
    /// When enabled, checks for invalid geometries, coordinate issues, and topological errors.
    /// Default: true
    /// </summary>
    public bool ValidateGeometry { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically repair invalid geometries.
    /// When true, attempts to fix invalid geometries using Buffer(0) and other NTS operations.
    /// Default: true
    /// </summary>
    public bool AutoRepairGeometries { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to allow empty geometries.
    /// Default: false
    /// </summary>
    public bool AllowEmptyGeometries { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to reject features with invalid geometries.
    /// When true, features with invalid geometries that cannot be repaired are rejected.
    /// When false, features with invalid geometries are logged but still imported.
    /// Default: true
    /// </summary>
    public bool RejectInvalidGeometries { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate coordinate ranges against CRS bounds.
    /// Default: true
    /// </summary>
    public bool ValidateCoordinateRanges { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of coordinates allowed in a single geometry.
    /// Prevents extremely large geometries from causing performance issues.
    /// Default: 1,000,000
    /// </summary>
    public int MaxGeometryCoordinates { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets whether to check for self-intersections and other topological errors.
    /// Default: true
    /// </summary>
    public bool CheckSelfIntersection { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log geometry validation warnings.
    /// Default: true
    /// </summary>
    public bool LogValidationWarnings { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log detailed geometry validation errors.
    /// Default: true
    /// </summary>
    public bool LogValidationErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate feature properties against layer schema.
    /// When enabled, validates required fields, data types, string lengths, and constraints.
    /// Default: true
    /// </summary>
    public bool ValidateSchema { get; set; } = true;

    /// <summary>
    /// Gets or sets the schema validation mode.
    /// Determines how validation failures are handled (Strict, Lenient, LogOnly).
    /// Default: Strict
    /// </summary>
    public SchemaValidationMode SchemaValidationMode { get; set; } = SchemaValidationMode.Strict;

    /// <summary>
    /// Gets or sets whether to automatically truncate strings that exceed MaxLength.
    /// When false, long strings cause validation errors.
    /// Default: false
    /// </summary>
    public bool TruncateLongStrings { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to attempt automatic type coercion for compatible types.
    /// Examples: string "123" -> int 123, string "true" -> bool true
    /// Default: true
    /// </summary>
    public bool CoerceTypes { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of validation errors to collect per feature.
    /// Prevents unbounded memory growth with very invalid data.
    /// Default: 100
    /// </summary>
    public int MaxValidationErrors { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to validate field patterns/regex.
    /// Default: true
    /// </summary>
    public bool ValidatePatterns { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate custom formats (email, URL, phone).
    /// Default: true
    /// </summary>
    public bool ValidateCustomFormats { get; set; } = true;

    /// <summary>
    /// Converts these options to a SchemaValidationOptions instance.
    /// </summary>
    public SchemaValidationOptions ToSchemaValidationOptions()
    {
        return new SchemaValidationOptions
        {
            ValidateSchema = ValidateSchema,
            ValidationMode = SchemaValidationMode,
            TruncateLongStrings = TruncateLongStrings,
            CoerceTypes = CoerceTypes,
            MaxValidationErrors = MaxValidationErrors,
            ValidatePatterns = ValidatePatterns,
            ValidateCustomFormats = ValidateCustomFormats
        };
    }
}

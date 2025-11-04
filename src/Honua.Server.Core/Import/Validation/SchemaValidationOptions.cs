// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Import.Validation;

/// <summary>
/// Configuration options for schema validation during data ingestion.
/// </summary>
public sealed class SchemaValidationOptions
{
    /// <summary>
    /// Whether to perform schema validation. Default: true.
    /// </summary>
    public bool ValidateSchema { get; set; } = true;

    /// <summary>
    /// Validation mode to use. Default: Strict.
    /// </summary>
    public SchemaValidationMode ValidationMode { get; set; } = SchemaValidationMode.Strict;

    /// <summary>
    /// Whether to automatically truncate strings that exceed MaxLength. Default: false.
    /// If false, long strings will cause validation errors.
    /// </summary>
    public bool TruncateLongStrings { get; set; } = false;

    /// <summary>
    /// Whether to attempt automatic type coercion for compatible types. Default: true.
    /// </summary>
    public bool CoerceTypes { get; set; } = true;

    /// <summary>
    /// Maximum number of validation errors to collect before stopping. Default: 100.
    /// Prevents unbounded memory growth with very large invalid datasets.
    /// </summary>
    public int MaxValidationErrors { get; set; } = 100;

    /// <summary>
    /// Whether to validate field patterns/regex. Default: true.
    /// </summary>
    public bool ValidatePatterns { get; set; } = true;

    /// <summary>
    /// Whether to validate custom formats (email, URL, phone). Default: true.
    /// </summary>
    public bool ValidateCustomFormats { get; set; } = true;

    /// <summary>
    /// Default options with strict validation enabled.
    /// </summary>
    public static SchemaValidationOptions Default => new();

    /// <summary>
    /// Lenient options that warn but don't reject invalid features.
    /// </summary>
    public static SchemaValidationOptions Lenient => new()
    {
        ValidationMode = SchemaValidationMode.Lenient,
        CoerceTypes = true,
        TruncateLongStrings = true
    };

    /// <summary>
    /// Disabled validation - no checks performed.
    /// </summary>
    public static SchemaValidationOptions Disabled => new()
    {
        ValidateSchema = false
    };
}

/// <summary>
/// Determines how validation failures are handled.
/// </summary>
public enum SchemaValidationMode
{
    /// <summary>
    /// Reject entire batch on any validation error.
    /// Most strict mode - ensures data integrity.
    /// </summary>
    Strict,

    /// <summary>
    /// Skip invalid features but import valid ones.
    /// Logs validation errors for skipped features.
    /// </summary>
    Lenient,

    /// <summary>
    /// Log validation errors but import all features anyway.
    /// Useful for migration scenarios where data quality is uncertain.
    /// </summary>
    LogOnly
}

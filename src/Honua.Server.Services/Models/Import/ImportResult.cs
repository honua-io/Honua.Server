// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Services.Models.Import;

/// <summary>
/// Result of an import operation
/// </summary>
public class ImportResult
{
    /// <summary>
    /// Whether the import was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of features successfully imported
    /// </summary>
    public int FeaturesImported { get; set; }

    /// <summary>
    /// Number of features that failed validation
    /// </summary>
    public int FeaturesFailed { get; set; }

    /// <summary>
    /// Total number of features in source
    /// </summary>
    public int TotalFeatures { get; set; }

    /// <summary>
    /// Imported layer ID
    /// </summary>
    public string? LayerId { get; set; }

    /// <summary>
    /// Imported layer name
    /// </summary>
    public string? LayerName { get; set; }

    /// <summary>
    /// Validation errors encountered
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Warnings encountered
    /// </summary>
    public List<ValidationError> Warnings { get; set; } = new();

    /// <summary>
    /// Informational messages
    /// </summary>
    public List<string> InfoMessages { get; set; } = new();

    /// <summary>
    /// Bounding box of imported data [west, south, east, north]
    /// </summary>
    public double[]? BoundingBox { get; set; }

    /// <summary>
    /// Time taken for import
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Source format
    /// </summary>
    public string? SourceFormat { get; set; }

    /// <summary>
    /// Source file name
    /// </summary>
    public string? SourceFileName { get; set; }

    /// <summary>
    /// Metadata about the import
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Validation error or warning
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Row number (0-based)
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Field name
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Severity level
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    /// <summary>
    /// Original value that caused the error
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Suggested fix
    /// </summary>
    public string? Suggestion { get; set; }
}

/// <summary>
/// Validation severity levels
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

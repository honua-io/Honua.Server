// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Request for exporting metadata configurations.
/// </summary>
public sealed class ExportRequest
{
    /// <summary>
    /// Export format (json or yaml).
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "json";

    /// <summary>
    /// Export scope (all, services, layers, folders, styles).
    /// </summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "all";

    /// <summary>
    /// Specific item IDs to export (if scope is not 'all').
    /// </summary>
    [JsonPropertyName("itemIds")]
    public List<string> ItemIds { get; set; } = new();

    /// <summary>
    /// Include related items (e.g., export service with its layers).
    /// </summary>
    [JsonPropertyName("includeRelated")]
    public bool IncludeRelated { get; set; } = true;

    /// <summary>
    /// Include metadata (created/updated dates, created by).
    /// </summary>
    [JsonPropertyName("includeMetadata")]
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Pretty print the output (formatted JSON/YAML).
    /// </summary>
    [JsonPropertyName("prettyPrint")]
    public bool PrettyPrint { get; set; } = true;
}

/// <summary>
/// Response from export operation.
/// </summary>
public sealed class ExportResponse
{
    /// <summary>
    /// Export format used (json or yaml).
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Exported configuration content.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// File name suggestion.
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of export.
    /// </summary>
    [JsonPropertyName("exportedAt")]
    public DateTimeOffset ExportedAt { get; set; }

    /// <summary>
    /// Summary of exported items.
    /// </summary>
    [JsonPropertyName("summary")]
    public ExportSummary Summary { get; set; } = new();
}

/// <summary>
/// Summary of exported items.
/// </summary>
public sealed class ExportSummary
{
    /// <summary>
    /// Number of services exported.
    /// </summary>
    [JsonPropertyName("serviceCount")]
    public int ServiceCount { get; set; }

    /// <summary>
    /// Number of layers exported.
    /// </summary>
    [JsonPropertyName("layerCount")]
    public int LayerCount { get; set; }

    /// <summary>
    /// Number of folders exported.
    /// </summary>
    [JsonPropertyName("folderCount")]
    public int FolderCount { get; set; }

    /// <summary>
    /// Number of styles exported.
    /// </summary>
    [JsonPropertyName("styleCount")]
    public int StyleCount { get; set; }

    /// <summary>
    /// Number of data sources exported.
    /// </summary>
    [JsonPropertyName("dataSourceCount")]
    public int DataSourceCount { get; set; }

    /// <summary>
    /// Total size in bytes.
    /// </summary>
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}

/// <summary>
/// Request for importing metadata configurations.
/// </summary>
public sealed class ImportRequest
{
    /// <summary>
    /// Configuration content to import (JSON or YAML).
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Import mode (merge, replace, skip).
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "merge";

    /// <summary>
    /// Validate only without applying changes (dry-run).
    /// </summary>
    [JsonPropertyName("dryRun")]
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Skip validation and force import.
    /// </summary>
    [JsonPropertyName("skipValidation")]
    public bool SkipValidation { get; set; } = false;

    /// <summary>
    /// Create backup snapshot before import.
    /// </summary>
    [JsonPropertyName("createBackup")]
    public bool CreateBackup { get; set; } = true;

    /// <summary>
    /// Backup label (if createBackup is true).
    /// </summary>
    [JsonPropertyName("backupLabel")]
    public string? BackupLabel { get; set; }
}

/// <summary>
/// Response from import operation.
/// </summary>
public sealed class ImportResponse
{
    /// <summary>
    /// Whether the import was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Whether this was a dry-run.
    /// </summary>
    [JsonPropertyName("dryRun")]
    public bool DryRun { get; set; }

    /// <summary>
    /// Import mode used.
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Validation results.
    /// </summary>
    [JsonPropertyName("validation")]
    public ImportValidationResult Validation { get; set; } = new();

    /// <summary>
    /// Changes that were applied or would be applied.
    /// </summary>
    [JsonPropertyName("changes")]
    public ImportChanges Changes { get; set; } = new();

    /// <summary>
    /// Error messages if import failed.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Warning messages.
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Backup snapshot label (if backup was created).
    /// </summary>
    [JsonPropertyName("backupLabel")]
    public string? BackupLabel { get; set; }

    /// <summary>
    /// Timestamp of import.
    /// </summary>
    [JsonPropertyName("importedAt")]
    public DateTimeOffset ImportedAt { get; set; }
}

/// <summary>
/// Validation results for import.
/// </summary>
public sealed class ImportValidationResult
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings.
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Format detected (json or yaml).
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Schema version detected.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; set; }
}

/// <summary>
/// Validation error.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Error code.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Item ID related to the error.
    /// </summary>
    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    /// <summary>
    /// Item type (service, layer, folder, style).
    /// </summary>
    [JsonPropertyName("itemType")]
    public string? ItemType { get; set; }

    /// <summary>
    /// Field path related to the error.
    /// </summary>
    [JsonPropertyName("fieldPath")]
    public string? FieldPath { get; set; }
}

/// <summary>
/// Validation warning.
/// </summary>
public sealed class ValidationWarning
{
    /// <summary>
    /// Warning code.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Warning message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Item ID related to the warning.
    /// </summary>
    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    /// <summary>
    /// Item type (service, layer, folder, style).
    /// </summary>
    [JsonPropertyName("itemType")]
    public string? ItemType { get; set; }
}

/// <summary>
/// Changes that were applied or would be applied.
/// </summary>
public sealed class ImportChanges
{
    /// <summary>
    /// Services that would be created.
    /// </summary>
    [JsonPropertyName("servicesCreated")]
    public List<string> ServicesCreated { get; set; } = new();

    /// <summary>
    /// Services that would be updated.
    /// </summary>
    [JsonPropertyName("servicesUpdated")]
    public List<string> ServicesUpdated { get; set; } = new();

    /// <summary>
    /// Services that would be deleted (replace mode only).
    /// </summary>
    [JsonPropertyName("servicesDeleted")]
    public List<string> ServicesDeleted { get; set; } = new();

    /// <summary>
    /// Services that would be skipped.
    /// </summary>
    [JsonPropertyName("servicesSkipped")]
    public List<string> ServicesSkipped { get; set; } = new();

    /// <summary>
    /// Layers that would be created.
    /// </summary>
    [JsonPropertyName("layersCreated")]
    public List<string> LayersCreated { get; set; } = new();

    /// <summary>
    /// Layers that would be updated.
    /// </summary>
    [JsonPropertyName("layersUpdated")]
    public List<string> LayersUpdated { get; set; } = new();

    /// <summary>
    /// Layers that would be deleted (replace mode only).
    /// </summary>
    [JsonPropertyName("layersDeleted")]
    public List<string> LayersDeleted { get; set; } = new();

    /// <summary>
    /// Layers that would be skipped.
    /// </summary>
    [JsonPropertyName("layersSkipped")]
    public List<string> LayersSkipped { get; set; } = new();

    /// <summary>
    /// Folders that would be created.
    /// </summary>
    [JsonPropertyName("foldersCreated")]
    public List<string> FoldersCreated { get; set; } = new();

    /// <summary>
    /// Folders that would be updated.
    /// </summary>
    [JsonPropertyName("foldersUpdated")]
    public List<string> FoldersUpdated { get; set; } = new();

    /// <summary>
    /// Folders that would be deleted (replace mode only).
    /// </summary>
    [JsonPropertyName("foldersDeleted")]
    public List<string> FoldersDeleted { get; set; } = new();

    /// <summary>
    /// Styles that would be created.
    /// </summary>
    [JsonPropertyName("stylesCreated")]
    public List<string> StylesCreated { get; set; } = new();

    /// <summary>
    /// Styles that would be updated.
    /// </summary>
    [JsonPropertyName("stylesUpdated")]
    public List<string> StylesUpdated { get; set; } = new();

    /// <summary>
    /// Styles that would be deleted (replace mode only).
    /// </summary>
    [JsonPropertyName("stylesDeleted")]
    public List<string> StylesDeleted { get; set; } = new();

    /// <summary>
    /// Total count of all changes.
    /// </summary>
    [JsonIgnore]
    public int TotalChanges =>
        ServicesCreated.Count + ServicesUpdated.Count + ServicesDeleted.Count +
        LayersCreated.Count + LayersUpdated.Count + LayersDeleted.Count +
        FoldersCreated.Count + FoldersUpdated.Count + FoldersDeleted.Count +
        StylesCreated.Count + StylesUpdated.Count + StylesDeleted.Count;
}

/// <summary>
/// Options for export scope.
/// </summary>
public static class ExportScope
{
    public const string All = "all";
    public const string Services = "services";
    public const string Layers = "layers";
    public const string Folders = "folders";
    public const string Styles = "styles";
    public const string DataSources = "datasources";

    public static readonly List<string> AllScopes = new()
    {
        All, Services, Layers, Folders, Styles, DataSources
    };
}

/// <summary>
/// Options for export format.
/// </summary>
public static class ExportFormat
{
    public const string Json = "json";
    public const string Yaml = "yaml";

    public static readonly List<string> AllFormats = new() { Json, Yaml };
}

/// <summary>
/// Options for import mode.
/// </summary>
public static class ImportMode
{
    /// <summary>
    /// Merge with existing items (create new, update existing, keep others).
    /// </summary>
    public const string Merge = "merge";

    /// <summary>
    /// Replace all items (delete existing not in import, create/update from import).
    /// </summary>
    public const string Replace = "replace";

    /// <summary>
    /// Skip existing items (only create new items).
    /// </summary>
    public const string Skip = "skip";

    public static readonly List<string> AllModes = new() { Merge, Replace, Skip };
}

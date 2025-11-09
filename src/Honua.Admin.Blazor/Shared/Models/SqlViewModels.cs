// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Model for SQL View configuration in the admin UI.
/// </summary>
public sealed class SqlViewModel
{
    [JsonPropertyName("sql")]
    public string Sql { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public List<SqlViewParameterModel> Parameters { get; set; } = new();

    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; } = 30;

    [JsonPropertyName("readOnly")]
    public bool ReadOnly { get; set; } = true;

    [JsonPropertyName("securityFilter")]
    public string? SecurityFilter { get; set; }

    [JsonPropertyName("validateGeometry")]
    public bool ValidateGeometry { get; set; }

    [JsonPropertyName("hints")]
    public string? Hints { get; set; }
}

/// <summary>
/// Model for SQL View parameter configuration.
/// </summary>
public sealed class SqlViewParameterModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("validation")]
    public SqlViewParameterValidationModel Validation { get; set; } = new();
}

/// <summary>
/// Model for SQL View parameter validation rules.
/// </summary>
public sealed class SqlViewParameterValidationModel
{
    [JsonPropertyName("min")]
    public double? Min { get; set; }

    [JsonPropertyName("max")]
    public double? Max { get; set; }

    [JsonPropertyName("minLength")]
    public int? MinLength { get; set; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("allowedValues")]
    public List<string> AllowedValues { get; set; } = new();

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request to test a SQL query with sample parameters.
/// </summary>
public sealed class TestSqlQueryRequest
{
    [JsonPropertyName("sql")]
    public required string Sql { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new();

    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; }

    [JsonPropertyName("maxRows")]
    public int MaxRows { get; set; } = 100;
}

/// <summary>
/// Response from testing a SQL query.
/// </summary>
public sealed class QueryTestResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("rowCount")]
    public int RowCount { get; set; }

    [JsonPropertyName("executionTimeMs")]
    public long ExecutionTimeMs { get; set; }

    [JsonPropertyName("columns")]
    public List<string> Columns { get; set; } = new();

    [JsonPropertyName("rows")]
    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }
}

/// <summary>
/// Response from schema detection.
/// </summary>
public sealed class SchemaDetectionResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("fields")]
    public List<DetectedFieldInfo> Fields { get; set; } = new();

    [JsonPropertyName("geometryField")]
    public string? GeometryField { get; set; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; set; }

    [JsonPropertyName("idField")]
    public string? IdField { get; set; }
}

/// <summary>
/// Information about a detected field from schema detection.
/// </summary>
public sealed class DetectedFieldInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("nullable")]
    public bool Nullable { get; set; }

    [JsonPropertyName("isGeometry")]
    public bool IsGeometry { get; set; }
}

/// <summary>
/// Request to update or set SQL View for a layer.
/// </summary>
public sealed class UpdateLayerSqlViewRequest
{
    [JsonPropertyName("sqlView")]
    public SqlViewModel? SqlView { get; set; }
}

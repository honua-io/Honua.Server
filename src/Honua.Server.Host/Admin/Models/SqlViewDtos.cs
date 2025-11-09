// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Request to update SQL View configuration for a layer.
/// </summary>
public sealed record UpdateLayerSqlViewRequest
{
    public SqlViewModel? SqlView { get; init; }
}

/// <summary>
/// SQL View configuration model.
/// </summary>
public sealed record SqlViewModel
{
    public required string Sql { get; init; }
    public string? Description { get; init; }
    public List<SqlViewParameterModel> Parameters { get; init; } = new();
    public int? TimeoutSeconds { get; init; }
    public bool ReadOnly { get; init; } = true;
    public string? SecurityFilter { get; init; }
    public bool ValidateGeometry { get; init; }
    public string? Hints { get; init; }
}

/// <summary>
/// SQL View parameter model.
/// </summary>
public sealed record SqlViewParameterModel
{
    public required string Name { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; }
    public string? DefaultValue { get; init; }
    public bool Required { get; init; }
    public SqlViewParameterValidationModel Validation { get; init; } = new();
}

/// <summary>
/// SQL View parameter validation model.
/// </summary>
public sealed record SqlViewParameterValidationModel
{
    public double? Min { get; init; }
    public double? Max { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public string? Pattern { get; init; }
    public List<string> AllowedValues { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Request to test a SQL query.
/// </summary>
public sealed record TestSqlQueryRequest
{
    public required string Sql { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = new();
    public int? TimeoutSeconds { get; init; }
    public int MaxRows { get; init; } = 100;
}

/// <summary>
/// Result of testing a SQL query.
/// </summary>
public sealed record QueryTestResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int RowCount { get; init; }
    public long ExecutionTimeMs { get; init; }
    public List<string> Columns { get; init; } = new();
    public List<Dictionary<string, object?>> Rows { get; init; } = new();
    public bool Truncated { get; init; }
}

/// <summary>
/// Result of schema detection from SQL.
/// </summary>
public sealed record SchemaDetectionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<DetectedFieldInfo> Fields { get; init; } = new();
    public string? GeometryField { get; init; }
    public string? GeometryType { get; init; }
    public string? IdField { get; init; }
}

/// <summary>
/// Information about a detected field.
/// </summary>
public sealed record DetectedFieldInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Nullable { get; init; }
    public bool IsGeometry { get; init; }
}

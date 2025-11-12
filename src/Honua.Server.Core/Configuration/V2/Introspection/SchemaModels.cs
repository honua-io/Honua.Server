// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.Server.Core.Configuration.V2.Introspection;

/// <summary>
/// Represents a database schema introspection result.
/// </summary>
public sealed record class DatabaseSchema
{
    public string Provider { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
    public List<TableSchema> Tables { get; init; } = new();
}

/// <summary>
/// Represents a single table in the database schema.
/// </summary>
public sealed record class TableSchema
{
    public string SchemaName { get; init; } = "public";
    public string TableName { get; init; } = string.Empty;
    public string FullyQualifiedName => $"{SchemaName}.{TableName}";
    public List<ColumnSchema> Columns { get; init; } = new();
    public List<string> PrimaryKeyColumns { get; init; } = new();
    public GeometryColumnInfo? GeometryColumn { get; init; }
    public long? RowCount { get; init; }
}

/// <summary>
/// Represents a single column in a table.
/// </summary>
public sealed record class ColumnSchema
{
    public string ColumnName { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public bool IsNullable { get; init; }
    public bool IsPrimaryKey { get; init; }
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Represents geometry column metadata.
/// </summary>
public sealed record class GeometryColumnInfo
{
    public string ColumnName { get; init; } = string.Empty;
    public string GeometryType { get; init; } = string.Empty;
    public int Srid { get; init; } = 4326;
    public int? CoordinateDimension { get; init; }
    public bool HasZ { get; init; }
    public bool HasM { get; init; }
}

/// <summary>
/// Options for database schema introspection.
/// </summary>
public sealed record class IntrospectionOptions
{
    /// <summary>
    /// Filter tables by name pattern (SQL LIKE pattern).
    /// </summary>
    public string? TableNamePattern { get; init; }

    /// <summary>
    /// Filter tables by schema name.
    /// </summary>
    public string? SchemaName { get; init; }

    /// <summary>
    /// Include system tables (default: false).
    /// </summary>
    public bool IncludeSystemTables { get; init; }

    /// <summary>
    /// Include row counts for tables (default: true, can be slow for large databases).
    /// </summary>
    public bool IncludeRowCounts { get; init; } = true;

    /// <summary>
    /// Include views in addition to tables (default: false).
    /// </summary>
    public bool IncludeViews { get; init; }

    /// <summary>
    /// Maximum number of tables to introspect (default: unlimited).
    /// </summary>
    public int? MaxTables { get; init; }

    public static IntrospectionOptions Default => new()
    {
        IncludeSystemTables = false,
        IncludeRowCounts = true,
        IncludeViews = false
    };
}

/// <summary>
/// Represents the result of a schema introspection operation.
/// </summary>
public sealed record class IntrospectionResult
{
    public bool Success { get; init; }
    public DatabaseSchema? Schema { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

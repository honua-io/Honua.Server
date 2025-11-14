// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Response for a data source
/// </summary>
public sealed class DataSourceResponse
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public required string ConnectionString { get; init; }
}

/// <summary>
/// Request to create a new data source
/// </summary>
public sealed class CreateDataSourceRequest
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public required string ConnectionString { get; init; }
}

/// <summary>
/// Request to update an existing data source
/// </summary>
public sealed class UpdateDataSourceRequest
{
    public string? Provider { get; init; }
    public string? ConnectionString { get; init; }
}

/// <summary>
/// Response for testing a data source connection
/// </summary>
public sealed class TestConnectionResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public string? Provider { get; init; }
    public int ConnectionTime { get; init; } // milliseconds
    public string? ErrorDetails { get; init; }
    public string? ErrorType { get; init; }
    public Dictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// Information about a database table
/// </summary>
public sealed class TableInfo
{
    public required string Schema { get; init; }
    public required string Table { get; init; }
    public string? GeometryColumn { get; init; }
    public string? GeometryType { get; init; }
    public int? Srid { get; init; }
    public long? RowCount { get; init; }
    public List<ColumnInfo> Columns { get; init; } = new();
}

/// <summary>
/// Information about a database column
/// </summary>
public sealed class ColumnInfo
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public bool IsNullable { get; init; }
    public bool IsPrimaryKey { get; init; }
    public int? MaxLength { get; init; }
}

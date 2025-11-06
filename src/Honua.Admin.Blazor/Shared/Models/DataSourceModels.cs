// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Data source information
/// </summary>
public sealed class DataSourceListItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("provider")]
    public required string Provider { get; set; }

    [JsonPropertyName("connectionString")]
    public required string ConnectionString { get; set; }
}

/// <summary>
/// Detailed data source response
/// </summary>
public sealed class DataSourceResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("provider")]
    public required string Provider { get; set; }

    [JsonPropertyName("connectionString")]
    public required string ConnectionString { get; set; }
}

/// <summary>
/// Request to create a new data source
/// </summary>
public sealed class CreateDataSourceRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("provider")]
    public required string Provider { get; set; }

    [JsonPropertyName("connectionString")]
    public required string ConnectionString { get; set; }
}

/// <summary>
/// Request to update a data source
/// </summary>
public sealed class UpdateDataSourceRequest
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; set; }
}

/// <summary>
/// Response from testing a database connection
/// </summary>
public sealed class TestConnectionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("connectionTime")]
    public int ConnectionTime { get; set; } // milliseconds
}

/// <summary>
/// Information about a database table
/// </summary>
public sealed class TableInfo
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("table")]
    public string Table { get; set; } = string.Empty;

    [JsonPropertyName("geometryColumn")]
    public string? GeometryColumn { get; set; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; set; }

    [JsonPropertyName("srid")]
    public int? Srid { get; set; }

    [JsonPropertyName("rowCount")]
    public long? RowCount { get; set; }

    [JsonPropertyName("columns")]
    public List<ColumnInfo> Columns { get; set; } = new();
}

/// <summary>
/// Information about a database column
/// </summary>
public sealed class ColumnInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = string.Empty;

    [JsonPropertyName("isNullable")]
    public bool IsNullable { get; set; }

    [JsonPropertyName("isPrimaryKey")]
    public bool IsPrimaryKey { get; set; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }
}

/// <summary>
/// Response containing list of tables
/// </summary>
public sealed class TableListResponse
{
    [JsonPropertyName("tables")]
    public List<TableInfo> Tables { get; set; } = new();
}

/// <summary>
/// Connection string builder parameters for different providers
/// </summary>
public sealed class ConnectionStringParameters
{
    // PostgreSQL / PostGIS
    public string? Host { get; set; }
    public int? Port { get; set; } = 5432;
    public string? Database { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? SslMode { get; set; } = "Prefer";

    // SQL Server
    public string? Server { get; set; }
    public string? UserId { get; set; }
    public bool? TrustServerCertificate { get; set; } = false;
    public bool? Encrypt { get; set; } = true;

    // MySQL
    public string? User { get; set; }

    // SQLite
    public string? DataSource { get; set; }

    /// <summary>
    /// Build connection string based on provider
    /// </summary>
    public string Build(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "postgis" or "postgresql" => BuildPostgreSqlConnectionString(),
            "sqlserver" => BuildSqlServerConnectionString(),
            "mysql" => BuildMySqlConnectionString(),
            "sqlite" => BuildSqliteConnectionString(),
            _ => string.Empty
        };
    }

    private string BuildPostgreSqlConnectionString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Host)) parts.Add($"Host={Host}");
        if (Port.HasValue) parts.Add($"Port={Port}");
        if (!string.IsNullOrEmpty(Database)) parts.Add($"Database={Database}");
        if (!string.IsNullOrEmpty(Username)) parts.Add($"Username={Username}");
        if (!string.IsNullOrEmpty(Password)) parts.Add($"Password={Password}");
        if (!string.IsNullOrEmpty(SslMode)) parts.Add($"SSL Mode={SslMode}");
        return string.Join(";", parts);
    }

    private string BuildSqlServerConnectionString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Server)) parts.Add($"Server={Server}");
        if (!string.IsNullOrEmpty(Database)) parts.Add($"Database={Database}");
        if (!string.IsNullOrEmpty(UserId)) parts.Add($"User Id={UserId}");
        if (!string.IsNullOrEmpty(Password)) parts.Add($"Password={Password}");
        if (Encrypt.HasValue) parts.Add($"Encrypt={Encrypt.Value}");
        if (TrustServerCertificate.HasValue) parts.Add($"TrustServerCertificate={TrustServerCertificate.Value}");
        return string.Join(";", parts);
    }

    private string BuildMySqlConnectionString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Server)) parts.Add($"Server={Server}");
        if (Port.HasValue) parts.Add($"Port={Port}");
        if (!string.IsNullOrEmpty(Database)) parts.Add($"Database={Database}");
        if (!string.IsNullOrEmpty(User)) parts.Add($"User={User}");
        if (!string.IsNullOrEmpty(Password)) parts.Add($"Password={Password}");
        return string.Join(";", parts);
    }

    private string BuildSqliteConnectionString()
    {
        return !string.IsNullOrEmpty(DataSource) ? $"Data Source={DataSource}" : string.Empty;
    }
}

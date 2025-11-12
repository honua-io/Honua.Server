// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Configuration.V2.Validation;

/// <summary>
/// Validates runtime aspects (database connectivity, table existence, etc.).
/// This validation can be slow and requires infrastructure to be available.
/// </summary>
public sealed class RuntimeValidator : IConfigurationValidator
{
    private readonly int _timeoutSeconds;
    private readonly IDbConnectionFactory? _connectionFactory;

    public RuntimeValidator(int timeoutSeconds = 10, IDbConnectionFactory? connectionFactory = null)
    {
        _timeoutSeconds = timeoutSeconds;
        _connectionFactory = connectionFactory;
    }

    public async Task<ValidationResult> ValidateAsync(HonuaConfig config, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        if (config == null)
        {
            result.AddError("Configuration cannot be null");
            return result;
        }

        if (_connectionFactory == null)
        {
            result.AddWarning(
                "Runtime validation skipped: no connection factory provided",
                suggestion: "Provide an IDbConnectionFactory to enable runtime validation");
            return result;
        }

        // Validate data source connectivity
        var dataSourceConnections = await ValidateDataSourceConnectivity(config, result, cancellationToken);

        // Validate layer table existence
        await ValidateLayerTables(config, dataSourceConnections, result, cancellationToken);

        // Close all connections
        foreach (var connection in dataSourceConnections.Values)
        {
            if (connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }
            await connection.DisposeAsync();
        }

        return result;
    }

    private async Task<Dictionary<string, DbConnection>> ValidateDataSourceConnectivity(
        HonuaConfig config,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        var connections = new Dictionary<string, DbConnection>();

        foreach (var (dataSourceId, dataSource) in config.DataSources)
        {
            var location = $"data_source.{dataSourceId}";

            try
            {
                var connection = _connectionFactory!.CreateConnection(dataSource.Provider, dataSource.Connection);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

                await connection.OpenAsync(cts.Token);

                // Test health check query if provided
                if (!string.IsNullOrWhiteSpace(dataSource.HealthCheck))
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = dataSource.HealthCheck;
                    command.CommandTimeout = _timeoutSeconds;

                    await command.ExecuteScalarAsync(cts.Token);
                }

                connections[dataSourceId] = connection;
            }
            catch (OperationCanceledException)
            {
                result.AddError(
                    $"Connection to data source '{dataSourceId}' timed out after {_timeoutSeconds} seconds",
                    $"{location}.connection",
                    "Check that the database server is running and accessible");
            }
            catch (Exception ex)
            {
                result.AddError(
                    $"Failed to connect to data source '{dataSourceId}': {ex.Message}",
                    $"{location}.connection",
                    "Verify connection string and ensure database is accessible");
            }
        }

        return connections;
    }

    private async Task ValidateLayerTables(
        HonuaConfig config,
        Dictionary<string, DbConnection> connections,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        foreach (var (layerId, layer) in config.Layers)
        {
            var location = $"layer.{layerId}";
            var dataSourceRef = ExtractReference(layer.DataSource);

            if (!connections.TryGetValue(dataSourceRef, out var connection))
            {
                // Connection failed, already reported in connectivity validation
                continue;
            }

            try
            {
                // Check if table exists
                var tableExists = await CheckTableExists(connection, layer.Table, cancellationToken);

                if (!tableExists)
                {
                    result.AddError(
                        $"Table '{layer.Table}' does not exist in data source '{dataSourceRef}'",
                        $"{location}.table",
                        "Create the table or update the table name");
                    continue;
                }

                // If not introspecting, validate explicit fields
                if (!layer.IntrospectFields && layer.Fields != null)
                {
                    await ValidateLayerFields(connection, layer, location, result, cancellationToken);
                }

                // Validate geometry column
                if (layer.Geometry != null)
                {
                    var geometryExists = await CheckColumnExists(connection, layer.Table, layer.Geometry.Column, cancellationToken);

                    if (!geometryExists)
                    {
                        result.AddError(
                            $"Geometry column '{layer.Geometry.Column}' does not exist in table '{layer.Table}'",
                            $"{location}.geometry.column",
                            "Check column name or update geometry configuration");
                    }
                }

                // Validate ID field
                var idFieldExists = await CheckColumnExists(connection, layer.Table, layer.IdField, cancellationToken);

                if (!idFieldExists)
                {
                    result.AddError(
                        $"ID field '{layer.IdField}' does not exist in table '{layer.Table}'",
                        $"{location}.id_field",
                        "Check field name or update id_field");
                }

                // Validate display field
                if (!string.IsNullOrWhiteSpace(layer.DisplayField))
                {
                    var displayFieldExists = await CheckColumnExists(connection, layer.Table, layer.DisplayField, cancellationToken);

                    if (!displayFieldExists)
                    {
                        result.AddWarning(
                            $"Display field '{layer.DisplayField}' does not exist in table '{layer.Table}'",
                            $"{location}.display_field");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddWarning(
                    $"Failed to validate layer table '{layer.Table}': {ex.Message}",
                    location);
            }
        }
    }

    private async Task ValidateLayerFields(
        DbConnection connection,
        LayerBlock layer,
        string location,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        if (layer.Fields == null) return;

        foreach (var (fieldName, _) in layer.Fields)
        {
            var columnExists = await CheckColumnExists(connection, layer.Table, fieldName, cancellationToken);

            if (!columnExists)
            {
                result.AddError(
                    $"Field '{fieldName}' does not exist in table '{layer.Table}'",
                    $"{location}.fields.{fieldName}",
                    "Check field name or remove from explicit fields");
            }
        }
    }

    private async Task<bool> CheckTableExists(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        try
        {
            using var command = connection.CreateCommand();

            // Handle schema-qualified table names (e.g., "public.buildings")
            var parts = tableName.Split('.');
            var schema = parts.Length > 1 ? parts[0] : null;
            var table = parts.Length > 1 ? parts[1] : tableName;

            command.CommandText = connection switch
            {
                // PostgreSQL
                _ when connection.GetType().Name.Contains("Npgsql") =>
                    schema != null
                        ? $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = '{schema}' AND table_name = '{table}')"
                        : $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = '{table}')",

                // SQLite
                _ when connection.GetType().Name.Contains("SQLite") =>
                    $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'",

                // SQL Server
                _ when connection.GetType().Name.Contains("SqlConnection") =>
                    schema != null
                        ? $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{schema}' AND TABLE_NAME = '{table}'"
                        : $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{table}'",

                // MySQL
                _ when connection.GetType().Name.Contains("MySql") =>
                    $"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{table}'",

                _ => throw new NotSupportedException($"Unsupported database type: {connection.GetType().Name}")
            };

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckColumnExists(DbConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        try
        {
            using var command = connection.CreateCommand();

            var parts = tableName.Split('.');
            var schema = parts.Length > 1 ? parts[0] : null;
            var table = parts.Length > 1 ? parts[1] : tableName;

            command.CommandText = connection switch
            {
                // PostgreSQL
                _ when connection.GetType().Name.Contains("Npgsql") =>
                    schema != null
                        ? $"SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_schema = '{schema}' AND table_name = '{table}' AND column_name = '{columnName}')"
                        : $"SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_name = '{table}' AND column_name = '{columnName}')",

                // SQLite
                _ when connection.GetType().Name.Contains("SQLite") =>
                    $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{columnName}'",

                // SQL Server
                _ when connection.GetType().Name.Contains("SqlConnection") =>
                    schema != null
                        ? $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{schema}' AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{columnName}'"
                        : $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = '{columnName}'",

                // MySQL
                _ when connection.GetType().Name.Contains("MySql") =>
                    $"SELECT COUNT(*) FROM information_schema.columns WHERE table_name = '{table}' AND column_name = '{columnName}'",

                _ => throw new NotSupportedException($"Unsupported database type: {connection.GetType().Name}")
            };

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return reference;
        }

        var parts = reference.Split('.');
        return parts.Length > 1 ? parts[^1] : reference;
    }
}

/// <summary>
/// Factory interface for creating database connections.
/// </summary>
public interface IDbConnectionFactory
{
    DbConnection CreateConnection(string provider, string connectionString);
}

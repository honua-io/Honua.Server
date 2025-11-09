// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Data;

/// <summary>
/// Detects schema (fields and their types) from SQL View queries by executing them
/// and inspecting the result metadata.
/// </summary>
public class SqlViewSchemaDetector
{
    /// <summary>
    /// Detects the schema of a SQL view by executing it with default parameters
    /// and inspecting the result set metadata.
    /// </summary>
    /// <param name="connection">Database connection (must be open).</param>
    /// <param name="sqlView">SQL view definition containing the query and parameters.</param>
    /// <param name="databaseProvider">Provider name: "postgres", "sqlserver", "mysql", or "sqlite".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of detected field definitions.</returns>
    public async Task<IReadOnlyList<FieldDefinition>> DetectSchemaAsync(
        IDbConnection connection,
        SqlViewDefinition sqlView,
        string databaseProvider,
        CancellationToken ct)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (sqlView == null)
            throw new ArgumentNullException(nameof(sqlView));
        if (string.IsNullOrWhiteSpace(databaseProvider))
            throw new ArgumentNullException(nameof(databaseProvider));

        // Build a query to get just the schema (no data)
        var schemaQuery = BuildSchemaQuery(sqlView.Sql, databaseProvider);

        // Execute and read schema
        using var command = connection.CreateCommand();
        command.CommandText = schemaQuery;
        command.CommandTimeout = 5; // Fast timeout for schema queries

        // Add default parameter values for schema detection
        foreach (var param in sqlView.Parameters)
        {
            AddParameterToCommand(command, param);
        }

        using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo,
            ct);

        var schemaTable = reader.GetSchemaTable();
        if (schemaTable == null)
        {
            throw new InvalidOperationException("Failed to retrieve schema from SQL view");
        }

        var fields = new List<FieldDefinition>();

        foreach (DataRow row in schemaTable.Rows)
        {
            var columnName = row["ColumnName"]?.ToString();
            var dataType = (Type)row["DataType"];
            var allowDbNull = (bool)row["AllowDBNull"];
            var maxLength = row["ColumnSize"] as int?;

            if (columnName == null) continue;

            fields.Add(new FieldDefinition
            {
                Name = columnName,
                DataType = MapDotNetTypeToEsriType(dataType),
                StorageType = MapDotNetTypeToSqlType(dataType, databaseProvider),
                Nullable = allowDbNull,
                MaxLength = maxLength,
                Editable = false // SQL views are typically read-only
            });
        }

        return fields;
    }

    /// <summary>
    /// Builds a query that returns schema information without fetching data.
    /// Uses LIMIT 0 or TOP 0 depending on the database provider.
    /// </summary>
    /// <remarks>
    /// SECURITY NOTE: While this method uses string interpolation of SQL, it is safe because:
    /// 1. The SQL parameter comes from SqlViewDefinition which is validated in MetadataSnapshot.cs
    /// 2. Validation ensures SQL starts with SELECT and blocks dangerous keywords (DROP, INSERT, UPDATE, etc.)
    /// 3. The query is executed with CommandBehavior.SchemaOnly which prevents data modification
    /// 4. Parameters are properly added using parameterized queries (AddParameterToCommand)
    /// This approach is necessary because SQL subquery structure cannot be parameterized.
    /// </remarks>
    private string BuildSchemaQuery(string sql, string provider)
    {
        // Wrap the SQL in a subquery with LIMIT 0 to get schema only
        // Note: sql parameter is pre-validated by SqlViewDefinition validation rules
        return provider.ToLowerInvariant() switch
        {
            "postgres" => $"SELECT * FROM ({sql}) AS schema_detect LIMIT 0",
            "sqlserver" => $"SELECT TOP 0 * FROM ({sql}) AS schema_detect",
            "mysql" => $"SELECT * FROM ({sql}) AS schema_detect LIMIT 0",
            "sqlite" => $"SELECT * FROM ({sql}) AS schema_detect LIMIT 0",
            _ => throw new NotSupportedException($"Provider {provider} not supported for schema detection")
        };
    }

    /// <summary>
    /// Adds a parameter to the command with a default or dummy value.
    /// </summary>
    private void AddParameterToCommand(IDbCommand command, SqlViewParameterDefinition param)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = param.Name;

        // Use default value or a safe dummy value based on type
        parameter.Value = param.DefaultValue ?? GetDummyValueForType(param.Type);

        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Gets a safe dummy value for a parameter type.
    /// Used when no default value is specified.
    /// </summary>
    private object GetDummyValueForType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "integer" => 0,
            "long" => 0L,
            "double" => 0.0,
            "decimal" => 0m,
            "boolean" => false,
            "date" => DateTime.Today,
            "datetime" => DateTime.UtcNow,
            "string" => "",
            _ => DBNull.Value
        };
    }

    /// <summary>
    /// Maps .NET type to Esri field type.
    /// </summary>
    private string MapDotNetTypeToEsriType(Type type)
    {
        if (type == typeof(int) || type == typeof(short) || type == typeof(byte))
            return "esriFieldTypeInteger";
        if (type == typeof(long))
            return "esriFieldTypeBigInteger";
        if (type == typeof(float) || type == typeof(double))
            return "esriFieldTypeDouble";
        if (type == typeof(decimal))
            return "esriFieldTypeDouble";
        if (type == typeof(string))
            return "esriFieldTypeString";
        if (type == typeof(DateTime))
            return "esriFieldTypeDate";
        if (type == typeof(bool))
            return "esriFieldTypeSmallInteger";
        if (type == typeof(Guid))
            return "esriFieldTypeGUID";
        if (type == typeof(byte[]))
            return "esriFieldTypeBlob";

        return "esriFieldTypeString"; // Default fallback
    }

    /// <summary>
    /// Maps .NET type to SQL type based on the database provider.
    /// </summary>
    private string MapDotNetTypeToSqlType(Type type, string provider)
    {
        // Provider-specific mappings
        if (provider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            if (type == typeof(int)) return "integer";
            if (type == typeof(short)) return "smallint";
            if (type == typeof(long)) return "bigint";
            if (type == typeof(double)) return "double precision";
            if (type == typeof(float)) return "real";
            if (type == typeof(decimal)) return "numeric";
            if (type == typeof(string)) return "text";
            if (type == typeof(DateTime)) return "timestamp";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(Guid)) return "uuid";
            if (type == typeof(byte[])) return "bytea";
        }
        else if (provider.Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(short)) return "smallint";
            if (type == typeof(long)) return "bigint";
            if (type == typeof(double)) return "float";
            if (type == typeof(float)) return "real";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(string)) return "nvarchar";
            if (type == typeof(DateTime)) return "datetime2";
            if (type == typeof(bool)) return "bit";
            if (type == typeof(Guid)) return "uniqueidentifier";
            if (type == typeof(byte[])) return "varbinary";
        }
        else if (provider.Equals("mysql", StringComparison.OrdinalIgnoreCase))
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(short)) return "smallint";
            if (type == typeof(long)) return "bigint";
            if (type == typeof(double)) return "double";
            if (type == typeof(float)) return "float";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(string)) return "text";
            if (type == typeof(DateTime)) return "datetime";
            if (type == typeof(bool)) return "tinyint";
            if (type == typeof(Guid)) return "char(36)";
            if (type == typeof(byte[])) return "blob";
        }
        else if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            if (type == typeof(int) || type == typeof(short) || type == typeof(long) || type == typeof(bool))
                return "integer";
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
                return "real";
            if (type == typeof(string) || type == typeof(Guid))
                return "text";
            if (type == typeof(byte[]))
                return "blob";
        }

        return type.Name; // Fallback to .NET type name
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Text;

namespace Honua.Server.Core.Configuration.V2.Introspection;

/// <summary>
/// Generates Honua Configuration 2.0 (.hcl) files from introspected database schemas.
/// </summary>
public static class ConfigurationGenerator
{
    /// <summary>
    /// Generates a complete .honua configuration from a database schema.
    /// </summary>
    /// <param name="schema">Introspected database schema.</param>
    /// <param name="options">Generation options.</param>
    /// <returns>Generated .hcl configuration as string.</returns>
    public static string GenerateConfiguration(
        DatabaseSchema schema,
        GenerationOptions? options = null)
    {
        options ??= GenerationOptions.Default;

        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# Honua Configuration 2.0 - Auto-generated from database");
        sb.AppendLine($"# Database: {schema.DatabaseName}");
        sb.AppendLine($"# Provider: {schema.Provider}");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"# Tables: {schema.Tables.Count}");
        sb.AppendLine();

        // Generate data source block (if requested)
        if (options.IncludeDataSourceBlock)
        {
            GenerateDataSourceBlock(sb, schema, options);
            sb.AppendLine();
        }

        // Generate service blocks (if requested)
        if (options.IncludeServiceBlocks && options.EnabledServices.Count > 0)
        {
            GenerateServiceBlocks(sb, options);
            sb.AppendLine();
        }

        // Generate layer blocks
        foreach (var table in schema.Tables)
        {
            GenerateLayerBlock(sb, table, schema.Provider, options);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void GenerateDataSourceBlock(
        StringBuilder sb,
        DatabaseSchema schema,
        GenerationOptions options)
    {
        sb.AppendLine($"# Data source for {schema.DatabaseName}");
        sb.AppendLine($"data_source \"{options.DataSourceId}\" {{");
        sb.AppendLine($"  provider   = \"{schema.Provider}\"");

        if (options.UseEnvironmentVariable)
        {
            sb.AppendLine($"  connection = env(\"{options.ConnectionStringEnvVar}\")");
        }
        else
        {
            sb.AppendLine($"  connection = \"<YOUR_CONNECTION_STRING>\"");
        }

        if (options.IncludeConnectionPool)
        {
            sb.AppendLine();
            sb.AppendLine("  pool {");
            sb.AppendLine("    min_size = 5");
            sb.AppendLine("    max_size = 20");
            sb.AppendLine("  }");
        }

        sb.AppendLine("}");
    }

    private static void GenerateServiceBlocks(StringBuilder sb, GenerationOptions options)
    {
        sb.AppendLine("# Services to enable");

        foreach (var serviceName in options.EnabledServices)
        {
            sb.AppendLine($"service \"{serviceName}\" {{");
            sb.AppendLine("  enabled = true");

            // Add service-specific defaults
            if (serviceName == "odata")
            {
                sb.AppendLine("  max_page_size = 1000");
            }
            else if (serviceName == "ogc_api")
            {
                sb.AppendLine("  conformance = [\"core\", \"geojson\", \"crs\"]");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }
    }

    private static void GenerateLayerBlock(
        StringBuilder sb,
        TableSchema table,
        string provider,
        GenerationOptions options)
    {
        var layerId = SanitizeIdentifier(table.TableName);
        var hasGeometry = table.GeometryColumn != null;

        // Comment with metadata
        sb.AppendLine($"# Table: {table.FullyQualifiedName}");
        if (table.RowCount.HasValue)
        {
            sb.AppendLine($"# Rows: {table.RowCount:N0}");
        }
        if (hasGeometry)
        {
            sb.AppendLine($"# Geometry: {table.GeometryColumn!.GeometryType} (SRID:{table.GeometryColumn.Srid})");
        }

        sb.AppendLine($"layer \"{layerId}\" {{");
        sb.AppendLine($"  title            = \"{ToTitleCase(table.TableName)}\"");
        sb.AppendLine($"  data_source      = data_source.{options.DataSourceId}");
        sb.AppendLine($"  table            = \"{table.FullyQualifiedName}\"");

        // ID field (primary key)
        var idField = table.PrimaryKeyColumns.FirstOrDefault() ?? table.Columns.FirstOrDefault()?.ColumnName ?? "id";
        sb.AppendLine($"  id_field         = \"{idField}\"");

        // Display field (first string column or fallback to ID)
        var displayField = table.Columns.FirstOrDefault(c =>
            !c.IsPrimaryKey &&
            TypeMapper.MapToHonuaType(provider, c.DataType) == "string")?.ColumnName ?? idField;
        sb.AppendLine($"  display_field    = \"{displayField}\"");

        // Introspect fields option
        sb.AppendLine($"  introspect_fields = {(options.GenerateExplicitFields ? "false" : "true").ToString().ToLowerInvariant()}");

        // Geometry block
        if (hasGeometry)
        {
            sb.AppendLine();
            sb.AppendLine("  geometry {");
            sb.AppendLine($"    column = \"{table.GeometryColumn!.ColumnName}\"");
            sb.AppendLine($"    type   = \"{TypeMapper.NormalizeGeometryType(table.GeometryColumn.GeometryType)}\"");
            sb.AppendLine($"    srid   = {table.GeometryColumn.Srid}");
            sb.AppendLine("  }");
        }

        // Explicit fields (if requested)
        if (options.GenerateExplicitFields)
        {
            sb.AppendLine();
            sb.AppendLine("  fields {");

            foreach (var column in table.Columns)
            {
                // Skip geometry columns (handled separately)
                if (column.ColumnName == table.GeometryColumn?.ColumnName)
                {
                    continue;
                }

                var honuaType = TypeMapper.MapToHonuaType(provider, column.DataType);
                var nullable = column.IsNullable ? "true" : "false";

                sb.AppendLine($"    {column.ColumnName.PadRight(20)} = {{ type = \"{honuaType}\", nullable = {nullable} }}");
            }

            sb.AppendLine("  }");
        }

        // Services
        if (options.EnabledServices.Count > 0)
        {
            sb.AppendLine();
            sb.Append("  services = [");

            var serviceRefs = options.EnabledServices.Select(s => $"service.{s}");
            sb.Append(string.Join(", ", serviceRefs));

            sb.AppendLine("]");
        }

        sb.AppendLine("}");
    }

    private static string SanitizeIdentifier(string input)
    {
        // Remove invalid characters and convert to lowercase
        var sanitized = new StringBuilder();
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sanitized.Append(char.ToLowerInvariant(c));
            }
            else if (c == ' ' || c == '-')
            {
                sanitized.Append('_');
            }
        }

        var result = sanitized.ToString();

        // Ensure it doesn't start with a digit
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }

    private static string ToTitleCase(string input)
    {
        // Convert snake_case or PascalCase to Title Case
        var words = input.Split('_', ' ', '-')
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant());

        return string.Join(" ", words);
    }
}

/// <summary>
/// Options for generating configuration from database schema.
/// </summary>
public sealed class GenerationOptions
{
    /// <summary>
    /// Data source ID to use in generated configuration.
    /// </summary>
    public string DataSourceId { get; init; } = "db";

    /// <summary>
    /// Include data_source block in generated configuration.
    /// </summary>
    public bool IncludeDataSourceBlock { get; init; } = true;

    /// <summary>
    /// Include service blocks in generated configuration.
    /// </summary>
    public bool IncludeServiceBlocks { get; init; } = true;

    /// <summary>
    /// Services to enable in generated configuration.
    /// </summary>
    public System.Collections.Generic.HashSet<string> EnabledServices { get; init; } = new() { "odata" };

    /// <summary>
    /// Use environment variable for connection string (default: true).
    /// </summary>
    public bool UseEnvironmentVariable { get; init; } = true;

    /// <summary>
    /// Environment variable name for connection string.
    /// </summary>
    public string ConnectionStringEnvVar { get; init; } = "DATABASE_URL";

    /// <summary>
    /// Include connection pool configuration.
    /// </summary>
    public bool IncludeConnectionPool { get; init; } = true;

    /// <summary>
    /// Generate explicit field definitions (instead of introspect_fields = true).
    /// </summary>
    public bool GenerateExplicitFields { get; init; } = false;

    public static GenerationOptions Default => new()
    {
        DataSourceId = "db",
        IncludeDataSourceBlock = true,
        IncludeServiceBlocks = true,
        EnabledServices = new() { "odata" },
        UseEnvironmentVariable = true,
        IncludeConnectionPool = true,
        GenerateExplicitFields = false
    };
}

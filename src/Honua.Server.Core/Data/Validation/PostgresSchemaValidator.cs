// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Validation;

/// <summary>
/// PostgreSQL/PostGIS schema validator.
/// Uses PostgresSchemaDiscoveryService to discover schema and validate against metadata.
/// </summary>
public sealed class PostgresSchemaValidator : ISchemaValidator
{
    private readonly PostgresSchemaDiscoveryService _discoveryService;
    private readonly ILogger<PostgresSchemaValidator> _logger;

    public PostgresSchemaValidator(
        PostgresSchemaDiscoveryService discoveryService,
        ILogger<PostgresSchemaValidator> logger)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SchemaValidationResult> ValidateLayerAsync(
        LayerDefinition layer,
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layer);
        Guard.NotNull(dataSource);

        var result = new SchemaValidationResult();
        var tableName = layer.Storage?.Table ?? layer.Id;

        try
        {
            // Use discovery service to get schema info
            var schemaInfo = await _discoveryService.DiscoverTableSchemaAsync(dataSource, tableName, cancellationToken);

            if (schemaInfo.Columns.Count == 0)
            {
                result.AddError(SchemaValidationErrorType.TableNotFound,
                    $"Table '{tableName}' does not exist or has no columns.", null);
                return result;
            }

            var columnLookup = schemaInfo.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

            // Validate primary key
            var primaryKey = layer.Storage?.PrimaryKey ?? layer.IdField;
            if (!string.IsNullOrWhiteSpace(primaryKey) && !columnLookup.ContainsKey(primaryKey))
            {
                result.AddError(SchemaValidationErrorType.PrimaryKeyNotFound,
                    $"Primary key column '{primaryKey}' not found in table '{tableName}'.", primaryKey);
            }

            // Validate geometry column
            var geometryColumn = layer.Storage?.GeometryColumn ?? layer.GeometryField;
            if (!string.IsNullOrWhiteSpace(geometryColumn))
            {
                if (!columnLookup.ContainsKey(geometryColumn) && geometryColumn != schemaInfo.GeometryColumn)
                {
                    result.AddError(SchemaValidationErrorType.GeometryColumnNotFound,
                        $"Geometry column '{geometryColumn}' not found in table '{tableName}'.", geometryColumn);
                }
            }

            // Validate all field definitions
            foreach (var field in layer.Fields)
            {
                if (!columnLookup.TryGetValue(field.Name, out var column))
                {
                    result.AddError(SchemaValidationErrorType.ColumnNotFound,
                        $"Column '{field.Name}' defined in metadata not found in table '{tableName}'.", field.Name);
                    continue;
                }

                // Check type compatibility using suggested types from discovery
                var metadataStorageType = (field.StorageType ?? field.DataType ?? "").ToLowerInvariant();
                var discoveredStorageType = (column.SuggestedStorageType ?? "").ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(metadataStorageType) &&
                    !string.IsNullOrWhiteSpace(discoveredStorageType) &&
                    metadataStorageType != discoveredStorageType)
                {
                    result.AddWarning($"Column '{field.Name}' type mismatch: metadata='{metadataStorageType}', database='{column.DbType}' (suggested: '{discoveredStorageType}')");
                }

                // Check nullability
                if (!field.Nullable && column.IsNullable)
                {
                    result.AddWarning($"Column '{field.Name}' nullability mismatch: metadata requires NOT NULL, database allows NULL");
                }
            }

            _logger.LogInformation(
                "Schema validation for layer {LayerId} table {TableName}: {ErrorCount} errors, {WarningCount} warnings",
                layer.Id, tableName, result.Errors.Count, result.Warnings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating schema for layer {LayerId}", layer.Id);
            result.AddError(SchemaValidationErrorType.ValidationError,
                $"Validation error: {ex.Message}", null);
        }

        return result;
    }
}

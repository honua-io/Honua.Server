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
/// MySQL schema validator.
/// Uses MySqlSchemaDiscoveryService to discover schema and validate against metadata.
/// </summary>
public sealed class MySqlSchemaValidator : ISchemaValidator
{
    private readonly MySqlSchemaDiscoveryService _discoveryService;
    private readonly ILogger<MySqlSchemaValidator> _logger;

    public MySqlSchemaValidator(
        MySqlSchemaDiscoveryService discoveryService,
        ILogger<MySqlSchemaValidator> logger)
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

            // Validate each field in the layer
            foreach (var field in layer.Fields)
            {
                if (!columnLookup.TryGetValue(field.Name, out var column))
                {
                    result.AddError(SchemaValidationErrorType.ColumnNotFound,
                        $"Field '{field.Name}' does not exist in table '{tableName}'.", field.Name);
                    continue;
                }

                // Check type compatibility using suggested types from discovery
                var metadataStorageType = (field.StorageType ?? field.DataType ?? "").ToLowerInvariant();
                var discoveredStorageType = (column.SuggestedStorageType ?? "").ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(metadataStorageType) &&
                    !string.IsNullOrWhiteSpace(discoveredStorageType) &&
                    metadataStorageType != discoveredStorageType)
                {
                    result.AddWarning($"Field '{field.Name}' type mismatch: metadata='{metadataStorageType}', database='{column.DbType}' (suggested: '{discoveredStorageType}')");
                }

                // Validate nullability
                if (!field.Nullable && column.IsNullable)
                {
                    result.AddWarning($"Field '{field.Name}' nullability mismatch: metadata requires NOT NULL, database allows NULL");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema validation failed for layer {LayerId}", layer.Id);
            result.AddError(SchemaValidationErrorType.ValidationError,
                $"Schema validation failed: {ex.Message}", null);
            return result;
        }
    }
}

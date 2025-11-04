// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Validation;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Hosting;

/// <summary>
/// Hosted service that validates database schemas match metadata definitions at startup.
/// </summary>
public sealed class SchemaValidationHostedService : IHostedService
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly ISchemaValidatorFactory _schemaValidatorFactory;
    private readonly ISchemaDiscoveryServiceFactory _schemaDiscoveryServiceFactory;
    private readonly ILogger<SchemaValidationHostedService> _logger;
    private readonly SchemaValidationOptions _options;

    public SchemaValidationHostedService(
        IMetadataRegistry metadataRegistry,
        ISchemaValidatorFactory schemaValidatorFactory,
        ISchemaDiscoveryServiceFactory schemaDiscoveryServiceFactory,
        ILogger<SchemaValidationHostedService> logger,
        IOptions<SchemaValidationOptions> options)
    {
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _schemaValidatorFactory = Guard.NotNull(schemaValidatorFactory);
        _schemaDiscoveryServiceFactory = Guard.NotNull(schemaDiscoveryServiceFactory);
        _logger = Guard.NotNull(logger);
        _options = Guard.NotNull(options?.Value);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Schema validation is disabled");
            return;
        }

        _logger.LogInformation("Starting database schema validation for all layers");

        await _metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var totalErrors = 0;
        var totalWarnings = 0;
        var validatedLayers = 0;

        foreach (var layer in snapshot.Layers)
        {
            // Find the data source for this layer
            var service = snapshot.Services.FirstOrDefault(s => s.Id == layer.ServiceId);
            if (service is null)
            {
                _logger.LogWarning("Layer {LayerId} references unknown service {ServiceId}", layer.Id, layer.ServiceId);
                continue;
            }

            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id == service.DataSourceId);
            if (dataSource is null)
            {
                _logger.LogWarning("Service {ServiceId} references unknown data source {DataSourceId}", service.Id, service.DataSourceId);
                continue;
            }

            // Skip non-database providers (e.g., "stub" for tests)
            if (string.IsNullOrWhiteSpace(dataSource.ConnectionString))
            {
                continue;
            }

            // Get provider-specific validator
            var validator = _schemaValidatorFactory.GetValidator(dataSource);
            if (validator == null)
            {
                _logger.LogDebug(
                    "Schema validation not supported for provider '{Provider}' (layer: {LayerId}). Skipping.",
                    dataSource.Provider,
                    layer.Id);
                continue;
            }

            try
            {
                var result = await validator.ValidateLayerAsync(layer, dataSource, cancellationToken).ConfigureAwait(false);

                validatedLayers++;
                totalErrors += result.Errors.Count;
                totalWarnings += result.Warnings.Count;

                if (result.Errors.Count > 0)
                {
                    _logger.LogError(
                        "Schema validation FAILED for layer {LayerId} (table: {TableName}): {ErrorCount} errors",
                        layer.Id,
                        layer.Storage?.Table ?? layer.Id,
                        result.Errors.Count);

                    foreach (var error in result.Errors)
                    {
                        _logger.LogError(
                            "  [{ErrorType}] {Message} {FieldName}",
                            error.Type,
                            error.Message,
                            error.FieldName is not null ? $"(field: {error.FieldName})" : "");
                    }

                    // Provide sync recommendation if enabled
                    if (_options.AutoSyncSchema)
                    {
                        _logger.LogWarning(
                            "Schema drift detected for layer {LayerId} with {ErrorCount} validation errors. " +
                            "Run 'honua metadata sync-schema' CLI command to automatically fix metadata, " +
                            "or use ISchemaDiscoveryService.SyncLayerFieldsAsync() programmatically.",
                            layer.Id,
                            result.Errors.Count);
                    }
                    else if (_options.FailOnError)
                    {
                        throw new InvalidOperationException(
                            $"Schema validation failed for layer '{layer.Id}': {result.Errors.Count} error(s). " +
                            "Fix metadata, enable auto-sync warning, or set SchemaValidation:FailOnError=false to continue.");
                    }
                }

                if (result.Warnings.Count > 0)
                {
                    foreach (var warning in result.Warnings)
                    {
                        _logger.LogWarning("  {Warning}", warning);
                    }
                }
            }
            catch (Exception ex) when (!_options.FailOnError)
            {
                _logger.LogError(ex, "Error validating schema for layer {LayerId}", layer.Id);
            }
        }

        if (totalErrors > 0 || totalWarnings > 0)
        {
            _logger.LogWarning(
                "Schema validation completed: {ValidatedLayers} layers, {TotalErrors} errors, {TotalWarnings} warnings",
                validatedLayers,
                totalErrors,
                totalWarnings);
        }
        else
        {
            _logger.LogInformation(
                "Schema validation completed successfully: {ValidatedLayers} layers validated",
                validatedLayers);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Configuration options for schema validation.
/// </summary>
public sealed class SchemaValidationOptions
{
    // Note: Boolean properties don't need validation attributes as they can only be true/false
    /// <summary>
    /// Whether schema validation is enabled (default: true).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to fail application startup if schema validation finds errors (default: false).
    /// When false, errors are logged but startup continues.
    /// </summary>
    public bool FailOnError { get; set; } = false;

    /// <summary>
    /// Whether to log warnings about schema drift and recommend using sync tools (default: true).
    /// When enabled, validation errors trigger warnings that admins should run schema sync.
    /// Admins can use ISchemaDiscoveryService.SyncLayerFieldsAsync() programmatically or via CLI tools.
    /// </summary>
    public bool AutoSyncSchema { get; set; } = true;

    /// <summary>
    /// When true, schema drift (errors) will surface as a degraded readiness health status even if FailOnError is false.
    /// Defaults to false to keep readiness green in environments with known drift while still surfacing details in the payload.
    /// </summary>
    public bool DegradeOnSchemaDrift { get; set; } = true;
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Host.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Validation;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Health;

/// <summary>
/// Health check that validates database schemas match metadata definitions.
/// Returns Degraded status when schema drift is detected.
/// </summary>
public sealed class SchemaHealthCheck : IHealthCheck
{
    private readonly IMetadataRegistry metadataRegistry;
    private readonly ISchemaValidatorFactory schemaValidatorFactory;
    private readonly ILogger<SchemaHealthCheck> logger;
    private readonly SchemaValidationOptions options;

    public SchemaHealthCheck(
        IMetadataRegistry metadataRegistry,
        ISchemaValidatorFactory schemaValidatorFactory,
        IOptions<SchemaValidationOptions> options,
        ILogger<SchemaHealthCheck> logger)
    {
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.schemaValidatorFactory = Guard.NotNull(schemaValidatorFactory);
        this.options = options?.Value ?? new SchemaValidationOptions();
        this.logger = Guard.NotNull(logger);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

            var layerDetails = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var overallStatus = HealthStatus.Healthy;
            var totalErrors = 0;
            var totalWarnings = 0;
            var validatedLayers = 0;
            var driftLayers = 0;

            foreach (var layer in snapshot.Layers)
            {
                // Find the data source for this layer
                var service = snapshot.Services.FirstOrDefault(s => s.Id == layer.ServiceId);
                if (service is null)
                {
                    continue;
                }

                var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id == service.DataSourceId);
                if (dataSource is null || dataSource.ConnectionString.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var schemaValidator = this.schemaValidatorFactory.GetValidator(dataSource);
                if (schemaValidator is null)
                {
                    this.logger.LogWarning("No schema validator registered for provider {Provider}", dataSource.Provider);
                    continue;
                }

                try
                {
                    var result = await schemaValidator.ValidateLayerAsync(layer, dataSource, cancellationToken).ConfigureAwait(false);

                    validatedLayers++;
                    totalErrors += result.Errors.Count;
                    totalWarnings += result.Warnings.Count;

                    if (result.Errors.Count > 0)
                    {
                        var layerStatus = DetermineLayerStatus(hasErrors: true);
                        overallStatus = PromoteStatus(overallStatus, layerStatus);
                        driftLayers++;

                        var layerData = new Dictionary<string, object>
                        {
                            ["status"] = layerStatus.ToString().ToLowerInvariant(),
                            ["hasErrors"] = result.Errors.Count > 0
                        };

                        layerDetails[layer.Id] = layerData;

                        // Log details server-side only
                        this.logger.LogValidationFailure(layer.Id, $"{result.Errors.Count} schema errors detected");
                    }
                    else if (result.Warnings.Count > 0)
                    {
                        var layerData = new Dictionary<string, object>
                        {
                            ["status"] = "healthy_with_warnings",
                            ["warnings"] = result.Warnings.Count
                        };

                        layerDetails[layer.Id] = layerData;
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogOperationFailure(ex, "Schema validation", layer.Id);
                    var layerStatus = DetermineLayerStatus(hasErrors: true);
                    overallStatus = PromoteStatus(overallStatus, layerStatus);
                    driftLayers++;

                    layerDetails[layer.Id] = new Dictionary<string, object>
                    {
                        ["status"] = layerStatus.ToString().ToLowerInvariant(),
                        ["hasErrors"] = true
                    };
                }
            }

            var data = new Dictionary<string, object>
            {
                ["validatedLayers"] = validatedLayers,
                ["totalErrors"] = totalErrors,
                ["totalWarnings"] = totalWarnings,
                ["driftLayers"] = driftLayers
            };

            if (layerDetails.Count > 0)
            {
                data["layers"] = layerDetails;
            }

            var description = BuildDescription(overallStatus, validatedLayers, totalErrors, driftLayers);

            return new HealthCheckResult(overallStatus, description, data: data);
        }
        catch (Exception ex)
        {
            this.logger.LogOperationFailure(ex, "Schema health check");
            return HealthCheckResult.Unhealthy("Schema validation is unavailable.", ex);
        }
    }

    private HealthStatus DetermineLayerStatus(bool hasErrors)
    {
        if (!hasErrors)
        {
            return HealthStatus.Healthy;
        }

        if (this.options.FailOnError)
        {
            return HealthStatus.Unhealthy;
        }

        return this.options.DegradeOnSchemaDrift ? HealthStatus.Degraded : HealthStatus.Healthy;
    }

    private static HealthStatus PromoteStatus(HealthStatus current, HealthStatus candidate)
    {
        return candidate > current ? candidate : current;
    }

    private static string BuildDescription(HealthStatus status, int validatedLayers, int totalErrors, int driftLayers)
    {
        return status switch
        {
            HealthStatus.Healthy when totalErrors > 0 =>
                driftLayers > 0
                    ? $"Schema drift detected on {driftLayers} layers (errors={totalErrors}) but allowed by configuration."
                    : $"Schema drift detected (errors={totalErrors}) but allowed by configuration.",
            HealthStatus.Healthy => validatedLayers > 0
                ? $"All {validatedLayers} layer schemas validated successfully."
                : "No layers to validate.",
            HealthStatus.Degraded => $"Schema drift detected: {totalErrors} errors across {driftLayers} layers. Auto-sync may be required.",
            HealthStatus.Unhealthy => $"Schema validation failed with {totalErrors} errors across {driftLayers} layers.",
            _ => "Schema validation state unknown."
        };
    }
}

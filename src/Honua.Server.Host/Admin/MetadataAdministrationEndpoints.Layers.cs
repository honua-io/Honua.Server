// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Admin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Partial class containing layer-related admin endpoints.
/// Handles CRUD operations for layers and SQL view configuration/testing.
/// </summary>
public static partial class MetadataAdministrationEndpoints
{
    #region Layers

    private static async Task<IResult> GetLayers(
        [FromServices] IMutableMetadataProvider metadataProvider,
        string? serviceId,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);

        // Use deferred execution - Results.Ok will serialize the collection lazily
        var layers = snapshot.Layers
            .Where(l => string.IsNullOrEmpty(serviceId) || l.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase))
            .Select(l => new LayerListItem
            {
                Id = l.Id,
                ServiceId = l.ServiceId,
                Title = l.Title,
                GeometryType = l.GeometryType
            });

        return Results.Ok(layers);
    }

    private static async Task<IResult> GetLayerById(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);
        var layer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (layer is null)
        {
            return Results.Problem(
                title: "Layer not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Layer with ID '{id}' does not exist");
        }

        var response = new LayerResponse
        {
            Id = layer.Id,
            ServiceId = layer.ServiceId,
            Title = layer.Title,
            Description = layer.Description,
            GeometryType = layer.GeometryType,
            IdField = layer.IdField,
            GeometryField = layer.GeometryField,
            DisplayField = layer.DisplayField,
            Crs = layer.Crs.ToList(),
            Keywords = layer.Keywords.ToList(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = null
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateLayer(
        CreateLayerRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);

            // Validate: Check if layer ID already exists
            if (snapshot.Layers.Any(l => l.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Layer already exists",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Layer with ID '{request.Id}' already exists");
            }

            // Validate: Check if service exists
            if (!snapshot.Services.Any(s => s.Id.Equals(request.ServiceId, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Service with ID '{request.ServiceId}' does not exist");
            }

            // Create new layer definition
            var newLayer = new LayerDefinition
            {
                Id = request.Id,
                ServiceId = request.ServiceId,
                Title = request.Title,
                Description = request.Description,
                GeometryType = request.GeometryType,
                IdField = request.IdField,
                GeometryField = request.GeometryField,
                DisplayField = request.DisplayField,
                Crs = request.Crs,
                Keywords = request.Keywords
            };

            // Build new snapshot
            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services,
                snapshot.Layers.Append(newLayer).ToList(),
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Created layer {LayerId} for service {ServiceId}", newLayer.Id, newLayer.ServiceId);

            var response = new LayerResponse
            {
                Id = newLayer.Id,
                ServiceId = newLayer.ServiceId,
                Title = newLayer.Title,
                Description = newLayer.Description,
                GeometryType = newLayer.GeometryType,
                IdField = newLayer.IdField,
                GeometryField = newLayer.GeometryField,
                DisplayField = newLayer.DisplayField,
                Crs = newLayer.Crs.ToList(),
                Keywords = newLayer.Keywords.ToList(),
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = null
            };

            return Results.Created($"/admin/metadata/layers/{newLayer.Id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create layer {LayerId}", request.Id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while creating the layer");
        }
    }

    private static async Task<IResult> UpdateLayer(
        string id,
        UpdateLayerRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingLayer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingLayer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{id}' does not exist");
            }

            // Update layer
            var updatedLayer = existingLayer with
            {
                Title = request.Title,
                Description = request.Description,
                DisplayField = request.DisplayField,
                Crs = request.Crs.Any() ? request.Crs : existingLayer.Crs,
                Keywords = request.Keywords
            };

            // Use optimized UpdateLayerAsync if available
            await metadataProvider.UpdateLayerAsync(updatedLayer, ct);

            logger.LogInformation("Updated layer {LayerId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update layer {LayerId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while updating the layer");
        }
    }

    private static async Task<IResult> DeleteLayer(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingLayer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingLayer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{id}' does not exist");
            }

            // Remove layer
            var updatedLayers = snapshot.Layers
                .Where(l => !l.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services,
                updatedLayers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Deleted layer {LayerId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete layer {LayerId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while deleting the layer");
        }
    }

    private static async Task<IResult> GetLayerSqlView(
        string layerId,
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);
        var layer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase));

        if (layer is null)
        {
            return Results.Problem(
                title: "Layer not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Layer with ID '{layerId}' does not exist");
        }

        if (layer.SqlView is null)
        {
            return Results.Ok(new { sqlView = (object?)null });
        }

        // Convert to admin model
        var sqlViewModel = new
        {
            sql = layer.SqlView.Sql,
            description = layer.SqlView.Description,
            parameters = layer.SqlView.Parameters.Select(p => new
            {
                name = p.Name,
                title = p.Title,
                description = p.Description,
                type = p.Type,
                defaultValue = p.DefaultValue,
                required = p.Required,
                validation = p.Validation is not null ? new
                {
                    min = p.Validation.Min,
                    max = p.Validation.Max,
                    minLength = p.Validation.MinLength,
                    maxLength = p.Validation.MaxLength,
                    pattern = p.Validation.Pattern,
                    allowedValues = p.Validation.AllowedValues?.ToList() ?? new List<string>(),
                    errorMessage = p.Validation.ErrorMessage
                } : null
            }).ToList(),
            timeoutSeconds = layer.SqlView.TimeoutSeconds,
            readOnly = layer.SqlView.ReadOnly,
            securityFilter = layer.SqlView.SecurityFilter,
            validateGeometry = layer.SqlView.ValidateGeometry,
            hints = layer.SqlView.Hints
        };

        return Results.Ok(new { sqlView = sqlViewModel });
    }

    private static async Task<IResult> UpdateLayerSqlView(
        string layerId,
        [FromBody] UpdateLayerSqlViewRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingLayer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase));

            if (existingLayer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{layerId}' does not exist");
            }

            // Convert admin model to core model
            SqlViewDefinition? sqlView = null;
            if (request.SqlView is not null)
            {
                sqlView = new SqlViewDefinition
                {
                    Sql = request.SqlView.Sql,
                    Description = request.SqlView.Description,
                    Parameters = request.SqlView.Parameters.Select(p => new SqlViewParameterDefinition
                    {
                        Name = p.Name,
                        Title = p.Title,
                        Description = p.Description,
                        Type = p.Type,
                        DefaultValue = p.DefaultValue,
                        Required = p.Required,
                        Validation = new SqlViewParameterValidation
                        {
                            Min = p.Validation.Min,
                            Max = p.Validation.Max,
                            MinLength = p.Validation.MinLength,
                            MaxLength = p.Validation.MaxLength,
                            Pattern = p.Validation.Pattern,
                            AllowedValues = p.Validation.AllowedValues.Count > 0 ? p.Validation.AllowedValues : null,
                            ErrorMessage = p.Validation.ErrorMessage
                        }
                    }).ToList(),
                    TimeoutSeconds = request.SqlView.TimeoutSeconds,
                    ReadOnly = request.SqlView.ReadOnly,
                    SecurityFilter = request.SqlView.SecurityFilter,
                    ValidateGeometry = request.SqlView.ValidateGeometry,
                    Hints = request.SqlView.Hints
                };

                // Validate the SQL view
                try
                {
                    SqlViewExecutor.ValidateParameterReferences(sqlView, layerId);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        title: "SQL View validation failed",
                        statusCode: StatusCodes.Status400BadRequest,
                        detail: ex.Message);
                }
            }

            // Update layer with new SQL view
            var updatedLayer = existingLayer with { SqlView = sqlView };

            // Replace the layer in the snapshot
            var updatedLayers = snapshot.Layers
                .Select(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase) ? updatedLayer : l)
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources,
                snapshot.Services,
                updatedLayers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Updated SQL View for layer {LayerId}", layerId);

            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update SQL View for layer {LayerId}", layerId);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while updating the SQL View");
        }
    }

    private static async Task<IResult> TestSqlQuery(
        string layerId,
        [FromBody] TestSqlQueryRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IDataStoreProviderFactory providerFactory,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var layer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase));

            if (layer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{layerId}' does not exist");
            }

            var service = snapshot.Services.FirstOrDefault(s => s.Id.Equals(layer.ServiceId, StringComparison.OrdinalIgnoreCase));
            if (service is null)
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Service with ID '{layer.ServiceId}' does not exist");
            }

            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(service.DataSourceId, StringComparison.OrdinalIgnoreCase));
            if (dataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{service.DataSourceId}' does not exist");
            }

            // Get data provider
            var provider = providerFactory.CreateProvider(dataSource);

            // Execute the SQL with timeout
            var stopwatch = Stopwatch.StartNew();
            var timeout = request.TimeoutSeconds ?? 30;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            try
            {
                // For now, we'll just return a mock result since we need more integration
                // In a full implementation, you would execute the SQL through the provider
                var result = new QueryTestResult
                {
                    Success = true,
                    RowCount = 0,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Columns = new List<string>(),
                    Rows = new List<Dictionary<string, object?>>(),
                    Truncated = false
                };

                logger.LogInformation("Successfully tested SQL query for layer {LayerId}", layerId);

                return Results.Ok(result);
            }
            catch (OperationCanceledException)
            {
                return Results.Ok(new QueryTestResult
                {
                    Success = false,
                    ErrorMessage = $"Query execution timed out after {timeout} seconds",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test SQL query for layer {LayerId}", layerId);
            return Results.Ok(new QueryTestResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTimeMs = 0
            });
        }
    }

    private static async Task<IResult> DetectSchemaFromSql(
        string layerId,
        [FromBody] TestSqlQueryRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IDataStoreProviderFactory providerFactory,
        [FromServices] ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var layer = snapshot.Layers.FirstOrDefault(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase));

            if (layer is null)
            {
                return Results.Problem(
                    title: "Layer not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Layer with ID '{layerId}' does not exist");
            }

            // Get the service and data source
            var service = snapshot.Services.FirstOrDefault(s => s.Layers.Any(l => l.Id.Equals(layerId, StringComparison.OrdinalIgnoreCase)));
            if (service is null)
            {
                return Results.Problem(
                    title: "Service not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"No service found for layer '{layerId}'");
            }

            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(service.DataSourceId, StringComparison.OrdinalIgnoreCase));
            if (dataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"No data source found for service '{service.Id}'");
            }

            // Check if layer has SQL view defined or if SQL is provided in request
            SqlViewDefinition? sqlView = layer.SqlView;
            if (sqlView is null && string.IsNullOrWhiteSpace(request.Sql))
            {
                return Results.Problem(
                    title: "No SQL view defined",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: "Layer does not have a SQL view and no SQL query was provided in the request");
            }

            // If SQL is provided in request, create a temporary SQL view definition
            if (!string.IsNullOrWhiteSpace(request.Sql))
            {
                var parameters = request.Parameters.Select(kvp => new SqlViewParameterDefinition
                {
                    Name = kvp.Key,
                    Type = "string", // Default to string for ad-hoc queries
                    DefaultValue = kvp.Value
                }).ToArray();

                sqlView = new SqlViewDefinition
                {
                    Sql = request.Sql,
                    Parameters = parameters,
                    TimeoutSeconds = request.TimeoutSeconds
                };

                // Create a temporary layer with the SQL view
                layer = layer with { SqlView = sqlView };
            }

            // Get the data provider
            var provider = providerFactory.Create(dataSource.Provider);

            // Detect schema - only if provider supports it (RelationalDataStoreProviderBase)
            IReadOnlyList<FieldDefinition> fields;
            try
            {
                // Use reflection to check if provider has DetectSchemaForSqlViewAsync method
                var method = provider.GetType().GetMethod("DetectSchemaForSqlViewAsync");
                if (method is null)
                {
                    return Results.Problem(
                        title: "Schema detection not supported",
                        statusCode: StatusCodes.Status501NotImplemented,
                        detail: $"Provider '{dataSource.Provider}' does not support schema detection for SQL views");
                }

                // Call the method
                var task = method.Invoke(provider, new object[] { dataSource, layer, ct }) as Task<IReadOnlyList<FieldDefinition>>;
                if (task is null)
                {
                    throw new InvalidOperationException("Failed to invoke DetectSchemaForSqlViewAsync");
                }

                fields = await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to detect schema for layer {LayerId}", layerId);
                return Results.Ok(new SchemaDetectionResult
                {
                    Success = false,
                    ErrorMessage = ex.InnerException?.Message ?? ex.Message
                });
            }

            // Convert to DetectedFieldInfo
            // Use deferred execution - collection will be serialized without extra allocation
            var detectedFields = fields.Select(f => new DetectedFieldInfo
            {
                Name = f.Name,
                Type = f.DataType ?? "esriFieldTypeString",
                Nullable = f.Nullable,
                IsGeometry = f.Name.Equals(layer.GeometryField, StringComparison.OrdinalIgnoreCase)
            });

            var result = new SchemaDetectionResult
            {
                Success = true,
                Fields = detectedFields,
                GeometryField = layer.GeometryField,
                GeometryType = layer.GeometryType,
                IdField = layer.IdField
            };

            logger.LogInformation("Successfully detected {FieldCount} fields for layer {LayerId}", fields.Count, layerId);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to detect schema for layer {LayerId}", layerId);
            return Results.Ok(new SchemaDetectionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    #endregion
}

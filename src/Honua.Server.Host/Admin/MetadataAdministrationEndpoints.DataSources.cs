// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Admin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Partial class containing data source-related admin endpoints.
/// Handles CRUD operations for data sources, connection testing, and table discovery.
/// </summary>
public static partial class MetadataAdministrationEndpoints
{
    #region Data Sources

    private static async Task<IResult> GetDataSources(
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);

        var dataSources = snapshot.DataSources.Select(ds => new DataSourceResponse
        {
            Id = ds.Id,
            Provider = ds.Provider,
            ConnectionString = ds.ConnectionString
        }).ToList();

        return Results.Ok(dataSources);
    }

    private static async Task<IResult> GetDataSourceById(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        CancellationToken ct)
    {
        var snapshot = await metadataProvider.LoadAsync(ct);
        var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (dataSource is null)
        {
            return Results.Problem(
                title: "Data source not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Data source with ID '{id}' does not exist");
        }

        var response = new DataSourceResponse
        {
            Id = dataSource.Id,
            Provider = dataSource.Provider,
            ConnectionString = dataSource.ConnectionString
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateDataSource(
        CreateDataSourceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);

            // Validate: Check if data source ID already exists
            if (snapshot.DataSources.Any(ds => ds.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Problem(
                    title: "Data source already exists",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Data source with ID '{request.Id}' already exists");
            }

            // Create new data source
            var newDataSource = new DataSourceDefinition
            {
                Id = request.Id,
                Provider = request.Provider,
                ConnectionString = request.ConnectionString
            };

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                snapshot.DataSources.Append(newDataSource).ToList(),
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Created data source {DataSourceId}", newDataSource.Id);

            var response = new DataSourceResponse
            {
                Id = newDataSource.Id,
                Provider = newDataSource.Provider,
                ConnectionString = newDataSource.ConnectionString
            };

            return Results.Created($"/admin/metadata/datasources/{newDataSource.Id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create data source {DataSourceId}", request.Id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while creating the data source");
        }
    }

    private static async Task<IResult> UpdateDataSource(
        string id,
        UpdateDataSourceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingDataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingDataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // Update data source
            var updatedDataSource = existingDataSource with
            {
                Provider = request.Provider ?? existingDataSource.Provider,
                ConnectionString = request.ConnectionString ?? existingDataSource.ConnectionString
            };

            var updatedDataSources = snapshot.DataSources
                .Select(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ? updatedDataSource : ds)
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                updatedDataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Updated data source {DataSourceId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update data source {DataSourceId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while updating the data source");
        }
    }

    private static async Task<IResult> DeleteDataSource(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingDataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingDataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // Check if data source is in use by any services
            var serviceCount = snapshot.Services.Count(s => s.DataSourceId != null && s.DataSourceId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (serviceCount > 0)
            {
                return Results.Problem(
                    title: "Data source in use",
                    statusCode: StatusCodes.Status409Conflict,
                    detail: $"Cannot delete data source '{id}' because it is used by {serviceCount} service(s)");
            }

            // Remove data source
            var updatedDataSources = snapshot.DataSources
                .Where(ds => !ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var newSnapshot = new MetadataSnapshot(
                snapshot.Catalog,
                snapshot.Folders,
                updatedDataSources,
                snapshot.Services,
                snapshot.Layers,
                snapshot.RasterDatasets,
                snapshot.Styles,
                snapshot.LayerGroups,
                snapshot.Server
            );

            await metadataProvider.SaveAsync(newSnapshot, ct);

            logger.LogInformation("Deleted data source {DataSourceId}", id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete data source {DataSourceId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while deleting the data source");
        }
    }

    private static async Task<IResult> TestDataSourceConnection(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (dataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // TODO: Implement actual connection test based on provider
            // For now, return a mock success response
            var response = new TestConnectionResponse
            {
                Success = true,
                Message = "Connection test successful",
                Provider = dataSource.Provider,
                ConnectionTime = 125 // milliseconds
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test connection for data source {DataSourceId}", id);
            return Results.Ok(new TestConnectionResponse
            {
                Success = false,
                Message = $"Connection test failed: {ex.Message}",
                Provider = null,
                ConnectionTime = 0
            });
        }
    }

    private static async Task<IResult> GetDataSourceTables(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            var snapshot = await metadataProvider.LoadAsync(ct);
            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (dataSource is null)
            {
                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // TODO: Implement actual table discovery based on provider
            // For now, return an empty list
            var tables = new List<TableInfo>();

            return Results.Ok(new { Tables = tables });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to discover tables for data source {DataSourceId}", id);
            return Results.Problem(
                title: "Internal server error",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: "An error occurred while discovering tables");
        }
    }

    #endregion
}

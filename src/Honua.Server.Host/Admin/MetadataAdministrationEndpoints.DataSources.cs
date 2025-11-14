// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.ConnectionTesting;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
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
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetDataSources",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            var snapshot = await metadataProvider.LoadAsync(ct);

            var dataSources = snapshot.DataSources.Select(ds => new DataSourceResponse
            {
                Id = ds.Id,
                Provider = ds.Provider,
                ConnectionString = ds.ConnectionString
            }).ToList();

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "DataSource",
                resourceId: "list",
                operation: "Read");

            return Results.Ok(dataSources);
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetDataSources",
                resourceType: "DataSource",
                details: "Failed to retrieve data sources",
                exception: ex);

            throw;
        }
    }

    private static async Task<IResult> GetDataSourceById(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetDataSourceById",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid data source ID format");
            }

            var snapshot = await metadataProvider.LoadAsync(ct);
            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (dataSource is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "GetDataSourceById",
                    resourceType: "DataSource",
                    resourceId: id,
                    details: "Data source not found");

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

            // Audit logging
            await auditLoggingService.LogDataAccessAsync(
                resourceType: "DataSource",
                resourceId: id,
                operation: "Read");

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetDataSourceById",
                resourceType: "DataSource",
                resourceId: id,
                details: "Failed to retrieve data source",
                exception: ex);

            throw;
        }
    }

    private static async Task<IResult> CreateDataSource(
        CreateDataSourceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "CreateDataSource",
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            InputValidationHelpers.ThrowIfUnsafeInput(request.Id, nameof(request.Id));
            InputValidationHelpers.ThrowIfUnsafeInput(request.Provider, nameof(request.Provider));

            if (!InputValidationHelpers.IsValidLength(request.Id, minLength: 1, maxLength: 100))
            {
                return Results.BadRequest("Data source ID must be between 1 and 100 characters");
            }

            if (!InputValidationHelpers.IsValidLength(request.Provider, minLength: 1, maxLength: 100))
            {
                return Results.BadRequest("Provider must be between 1 and 100 characters");
            }

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

            logger.LogInformation("User {UserId} created data source {DataSourceId}", identity.UserId, newDataSource.Id);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "CreateDataSource",
                resourceType: "DataSource",
                resourceId: newDataSource.Id,
                details: $"Created data source: {newDataSource.Id}",
                additionalData: new Dictionary<string, object>
                {
                    ["dataSourceId"] = newDataSource.Id,
                    ["provider"] = newDataSource.Provider
                });

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
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "CreateDataSource",
                resourceType: "DataSource",
                resourceId: request.Id,
                details: "Failed to create data source",
                exception: ex);

            logger.LogError(ex, "Failed to create data source {DataSourceId}", request.Id);
            throw;
        }
    }

    private static async Task<IResult> UpdateDataSource(
        string id,
        UpdateDataSourceRequest request,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "UpdateDataSource",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid data source ID format");
            }

            if (request.Provider != null)
            {
                InputValidationHelpers.ThrowIfUnsafeInput(request.Provider, nameof(request.Provider));
                if (!InputValidationHelpers.IsValidLength(request.Provider, minLength: 1, maxLength: 100))
                {
                    return Results.BadRequest("Provider must be between 1 and 100 characters");
                }
            }

            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingDataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingDataSource is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "UpdateDataSource",
                    resourceType: "DataSource",
                    resourceId: id,
                    details: "Data source not found");

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

            logger.LogInformation("User {UserId} updated data source {DataSourceId}", identity.UserId, id);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "UpdateDataSource",
                resourceType: "DataSource",
                resourceId: id,
                details: $"Updated data source: {id}",
                additionalData: new Dictionary<string, object>
                {
                    ["dataSourceId"] = id,
                    ["provider"] = updatedDataSource.Provider
                });

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "UpdateDataSource",
                resourceType: "DataSource",
                resourceId: id,
                details: "Failed to update data source",
                exception: ex);

            logger.LogError(ex, "Failed to update data source {DataSourceId}", id);
            throw;
        }
    }

    private static async Task<IResult> DeleteDataSource(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "DeleteDataSource",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid data source ID format");
            }

            var snapshot = await metadataProvider.LoadAsync(ct);
            var existingDataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existingDataSource is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "DeleteDataSource",
                    resourceType: "DataSource",
                    resourceId: id,
                    details: "Data source not found");

                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // Check if data source is in use by any services
            var serviceCount = snapshot.Services.Count(s => s.DataSourceId != null && s.DataSourceId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (serviceCount > 0)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "DeleteDataSource",
                    resourceType: "DataSource",
                    resourceId: id,
                    details: $"Data source in use by {serviceCount} service(s)");

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

            logger.LogInformation("User {UserId} deleted data source {DataSourceId}", identity.UserId, id);

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "DeleteDataSource",
                resourceType: "DataSource",
                resourceId: id,
                details: $"Deleted data source: {id}",
                additionalData: new Dictionary<string, object>
                {
                    ["dataSourceId"] = id
                });

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "DeleteDataSource",
                resourceType: "DataSource",
                resourceId: id,
                details: "Failed to delete data source",
                exception: ex);

            logger.LogError(ex, "Failed to delete data source {DataSourceId}", id);
            throw;
        }
    }

    private static async Task<IResult> TestDataSourceConnection(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] ConnectionTesterFactory connectionTesterFactory,
        [FromServices] IAuditLoggingService auditLoggingService,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "TestDataSourceConnection",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid data source ID format");
            }

            var snapshot = await metadataProvider.LoadAsync(ct);
            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (dataSource is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    "TestDataSourceConnection",
                    "DataSource",
                    id,
                    "Data source not found",
                    cancellationToken: ct);

                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            logger.LogInformation(
                "User {UserId} testing connection to data source {DataSourceId} with provider {Provider}",
                identity.UserId,
                id,
                dataSource.Provider);

            // Get the appropriate connection tester for this provider
            var tester = connectionTesterFactory.GetTester(dataSource.Provider);

            if (tester is null)
            {
                var supportedProviders = string.Join(", ", connectionTesterFactory.GetSupportedProviders());

                await auditLoggingService.LogAdminActionFailureAsync(
                    "TestDataSourceConnection",
                    "DataSource",
                    id,
                    $"Unsupported provider: {dataSource.Provider}",
                    cancellationToken: ct);

                return Results.Problem(
                    title: "Unsupported provider",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: $"Connection testing is not supported for provider '{dataSource.Provider}'. Supported providers: {supportedProviders}");
            }

            // Execute the connection test
            var result = await tester.TestConnectionAsync(dataSource, ct);

            // Log audit event
            if (result.Success)
            {
                await auditLoggingService.LogAdminActionAsync(
                    "TestDataSourceConnection",
                    "DataSource",
                    id,
                    $"Connection test successful in {result.ResponseTime.TotalMilliseconds:F0}ms",
                    new Dictionary<string, object>
                    {
                        ["provider"] = dataSource.Provider,
                        ["responseTimeMs"] = result.ResponseTime.TotalMilliseconds
                    },
                    ct);

                logger.LogInformation(
                    "Connection test succeeded for data source {DataSourceId} in {Duration}ms",
                    id,
                    result.ResponseTime.TotalMilliseconds);
            }
            else
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    "TestDataSourceConnection",
                    "DataSource",
                    id,
                    $"Connection test failed: {result.ErrorType}",
                    cancellationToken: ct);

                logger.LogWarning(
                    "Connection test failed for data source {DataSourceId}: {ErrorType} - {ErrorDetails}",
                    id,
                    result.ErrorType,
                    result.ErrorDetails);
            }

            // Build response
            var response = new TestConnectionResponse
            {
                Success = result.Success,
                Message = result.Message,
                Provider = dataSource.Provider,
                ConnectionTime = (int)result.ResponseTime.TotalMilliseconds,
                ErrorDetails = result.ErrorDetails,
                ErrorType = result.ErrorType,
                Metadata = result.Metadata
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while testing connection for data source {DataSourceId}", id);

            await auditLoggingService.LogAdminActionFailureAsync(
                "TestDataSourceConnection",
                "DataSource",
                id,
                "Unexpected error during connection test",
                ex,
                ct);

            return Results.Ok(new TestConnectionResponse
            {
                Success = false,
                Message = "Connection test failed with unexpected error",
                Provider = null,
                ConnectionTime = 0,
                ErrorDetails = ex.Message,
                ErrorType = "unknown"
            });
        }
    }

    private static async Task<IResult> GetDataSourceTables(
        string id,
        [FromServices] IMutableMetadataProvider metadataProvider,
        [FromServices] IUserIdentityService userIdentityService,
        [FromServices] IAuditLoggingService auditLoggingService,
        ILogger<MetadataSnapshot> logger,
        CancellationToken ct)
    {
        try
        {
            // Extract user identity
            var identity = userIdentityService.GetCurrentUserIdentity();
            if (identity == null)
            {
                await auditLoggingService.LogAuthorizationDeniedAsync(
                    action: "GetDataSourceTables",
                    resourceId: id,
                    reason: "User not authenticated");

                return Results.Unauthorized();
            }

            // Input validation
            if (!InputValidationHelpers.IsValidResourceId(id))
            {
                return Results.BadRequest("Invalid data source ID format");
            }

            var snapshot = await metadataProvider.LoadAsync(ct);
            var dataSource = snapshot.DataSources.FirstOrDefault(ds => ds.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (dataSource is null)
            {
                await auditLoggingService.LogAdminActionFailureAsync(
                    action: "GetDataSourceTables",
                    resourceType: "DataSource",
                    resourceId: id,
                    details: "Data source not found");

                return Results.Problem(
                    title: "Data source not found",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: $"Data source with ID '{id}' does not exist");
            }

            // TODO: Implement actual table discovery based on provider
            // For now, return an empty list
            var tables = new List<TableInfo>();

            // Audit logging
            await auditLoggingService.LogAdminActionAsync(
                action: "GetDataSourceTables",
                resourceType: "DataSource",
                resourceId: id,
                details: $"Retrieved tables for data source: {id}",
                additionalData: new Dictionary<string, object>
                {
                    ["dataSourceId"] = id,
                    ["provider"] = dataSource.Provider,
                    ["tableCount"] = tables.Count
                });

            return Results.Ok(new { Tables = tables });
        }
        catch (Exception ex)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "GetDataSourceTables",
                resourceType: "DataSource",
                resourceId: id,
                details: "Failed to discover tables",
                exception: ex);

            logger.LogError(ex, "Failed to discover tables for data source {DataSourceId}", id);
            throw;
        }
    }

    #endregion
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure.Core;
using Azure.Identity;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using Honua.Server.Enterprise.BIConnectors.PowerBI.Configuration;
using System.Text.Json;

namespace Honua.Server.Enterprise.BIConnectors.PowerBI.Services;

/// <summary>
/// Implementation of Power BI dataset management service
/// </summary>
public class PowerBIDatasetService : IPowerBIDatasetService
{
    private readonly PowerBIOptions _options;
    private readonly ILogger<PowerBIDatasetService> _logger;
    private readonly TokenCredential _credential;
    private const string PowerBIResourceId = "https://analysis.windows.net/powerbi/api";

    public PowerBIDatasetService(
        PowerBIOptions options,
        ILogger<PowerBIDatasetService> logger)
    {
        _options = options;
        _logger = logger;

        // Use ClientSecretCredential for Service Principal authentication
        _credential = new ClientSecretCredential(
            _options.TenantId,
            _options.ClientId,
            _options.ClientSecret);
    }

    private async Task<PowerBIClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        // Get Azure AD token for Power BI API
        var tokenRequestContext = new TokenRequestContext(new[] { $"{PowerBIResourceId}/.default" });
        var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);

        var tokenCredentials = new TokenCredentials(token.Token, "Bearer");
        return new PowerBIClient(new Uri(_options.ApiUrl), tokenCredentials);
    }

    public async Task<string> CreateOrUpdateDatasetAsync(
        string dashboardType,
        IEnumerable<string> collectionIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = await CreateClientAsync(cancellationToken);

            var datasetName = $"Honua {dashboardType} Dashboard";
            var tables = new List<Table>();

            // Create table schema based on dashboard type
            var table = CreateTableSchema(dashboardType, collectionIds);
            tables.Add(table);

            var dataset = new CreateDatasetRequest
            {
                Name = datasetName,
                Tables = tables,
                DefaultMode = DatasetMode.Push
            };

            // Check if dataset already exists
            var existingDatasets = await client.Datasets.GetDatasetsInGroupAsync(_options.WorkspaceId);
            var existingDataset = existingDatasets.Value.FirstOrDefault(d => d.Name == datasetName);

            if (existingDataset != null)
            {
                _logger.LogInformation("Dataset {DatasetName} already exists with ID {DatasetId}", datasetName, existingDataset.Id);
                return existingDataset.Id;
            }

            // Create new dataset
            var createdDataset = await client.Datasets.PostDatasetInGroupAsync(
                _options.WorkspaceId,
                dataset,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created Power BI dataset {DatasetName} with ID {DatasetId}", datasetName, createdDataset.Id);
            return createdDataset.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating Power BI dataset for {DashboardType}", dashboardType);
            throw;
        }
    }

    public async Task<(string DatasetId, string PushUrl)> CreateStreamingDatasetAsync(
        string datasetName,
        object schema,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = await CreateClientAsync(cancellationToken);

            var tables = new List<Table>();

            // Convert schema object to Table
            if (schema is Table table)
            {
                tables.Add(table);
            }
            else
            {
                throw new ArgumentException("Schema must be a Power BI Table object", nameof(schema));
            }

            var dataset = new CreateDatasetRequest
            {
                Name = datasetName,
                Tables = tables,
                DefaultMode = DatasetMode.PushStreaming
            };

            var createdDataset = await client.Datasets.PostDatasetInGroupAsync(
                _options.WorkspaceId,
                dataset,
                cancellationToken: cancellationToken);

            // Get push URL
            var pushUrl = $"{_options.ApiUrl}/v1.0/myorg/groups/{_options.WorkspaceId}/datasets/{createdDataset.Id}/tables/{tables[0].Name}/rows";

            _logger.LogInformation("Created streaming dataset {DatasetName} with ID {DatasetId}", datasetName, createdDataset.Id);
            return (createdDataset.Id, pushUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating streaming dataset {DatasetName}", datasetName);
            throw;
        }
    }

    public async Task PushRowsAsync(
        string datasetId,
        string tableName,
        IEnumerable<object> rows,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = await CreateClientAsync(cancellationToken);

            var rowsList = rows.ToList();
            if (rowsList.Count == 0)
            {
                return;
            }

            // Power BI limits: 10,000 rows per hour, 15 requests per second
            // Batch into chunks of 100 rows
            var batches = rowsList.Chunk(_options.StreamingBatchSize);

            foreach (var batch in batches)
            {
                var request = new PostRowsRequest { Rows = batch.ToList<object>() };

                await client.Datasets.PostRowsInGroupAsync(
                    _options.WorkspaceId,
                    datasetId,
                    tableName,
                    request,
                    cancellationToken: cancellationToken);

                _logger.LogDebug("Pushed {Count} rows to dataset {DatasetId}, table {TableName}",
                    batch.Count(), datasetId, tableName);

                // Small delay to respect rate limits
                await Task.Delay(100, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing rows to dataset {DatasetId}, table {TableName}", datasetId, tableName);
            throw;
        }
    }

    public async Task<IEnumerable<Dataset>> GetDatasetsAsync(CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        var datasets = await client.Datasets.GetDatasetsInGroupAsync(_options.WorkspaceId);
        return datasets.Value;
    }

    public async Task DeleteDatasetAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        await client.Datasets.DeleteDatasetInGroupAsync(_options.WorkspaceId, datasetId);
        _logger.LogInformation("Deleted dataset {DatasetId}", datasetId);
    }

    public async Task RefreshDatasetAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        await client.Datasets.RefreshDatasetInGroupAsync(_options.WorkspaceId, datasetId);
        _logger.LogInformation("Triggered refresh for dataset {DatasetId}", datasetId);
    }

    public async Task<string> GenerateEmbedTokenAsync(
        string reportId,
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = await CreateClientAsync(cancellationToken);

            var generateTokenRequest = new GenerateTokenRequest(
                accessLevel: "View",
                datasetId: datasetId);

            var embedToken = await client.Reports.GenerateTokenInGroupAsync(
                _options.WorkspaceId,
                reportId,
                generateTokenRequest,
                cancellationToken: cancellationToken);

            return embedToken.Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embed token for report {ReportId}", reportId);
            throw;
        }
    }

    private Table CreateTableSchema(string dashboardType, IEnumerable<string> collectionIds)
    {
        return dashboardType.ToLowerInvariant() switch
        {
            "traffic" => CreateTrafficTableSchema(),
            "airquality" => CreateAirQualityTableSchema(),
            "311requests" => Create311RequestsTableSchema(),
            "assetmanagement" => CreateAssetManagementTableSchema(),
            "buildingoccupancy" => CreateBuildingOccupancyTableSchema(),
            _ => CreateGenericTableSchema()
        };
    }

    private Table CreateTrafficTableSchema()
    {
        return new Table
        {
            Name = "TrafficSensors",
            Columns = new List<Column>
            {
                new Column { Name = "Id", DataType = "string" },
                new Column { Name = "SensorName", DataType = "string" },
                new Column { Name = "Latitude", DataType = "double" },
                new Column { Name = "Longitude", DataType = "double" },
                new Column { Name = "VehicleCount", DataType = "int64" },
                new Column { Name = "AverageSpeed", DataType = "double" },
                new Column { Name = "CongestionLevel", DataType = "string" },
                new Column { Name = "Timestamp", DataType = "datetime" },
                new Column { Name = "LocationName", DataType = "string" },
                new Column { Name = "Direction", DataType = "string" }
            }
        };
    }

    private Table CreateAirQualityTableSchema()
    {
        return new Table
        {
            Name = "AirQualitySensors",
            Columns = new List<Column>
            {
                new Column { Name = "Id", DataType = "string" },
                new Column { Name = "SensorName", DataType = "string" },
                new Column { Name = "Latitude", DataType = "double" },
                new Column { Name = "Longitude", DataType = "double" },
                new Column { Name = "PM25", DataType = "double" },
                new Column { Name = "PM10", DataType = "double" },
                new Column { Name = "NO2", DataType = "double" },
                new Column { Name = "O3", DataType = "double" },
                new Column { Name = "AQI", DataType = "int64" },
                new Column { Name = "AQICategory", DataType = "string" },
                new Column { Name = "Timestamp", DataType = "datetime" },
                new Column { Name = "LocationName", DataType = "string" }
            }
        };
    }

    private Table Create311RequestsTableSchema()
    {
        return new Table
        {
            Name = "ServiceRequests",
            Columns = new List<Column>
            {
                new Column { Name = "Id", DataType = "string" },
                new Column { Name = "RequestNumber", DataType = "string" },
                new Column { Name = "RequestType", DataType = "string" },
                new Column { Name = "Status", DataType = "string" },
                new Column { Name = "Priority", DataType = "string" },
                new Column { Name = "Latitude", DataType = "double" },
                new Column { Name = "Longitude", DataType = "double" },
                new Column { Name = "CreatedAt", DataType = "datetime" },
                new Column { Name = "ResolvedAt", DataType = "datetime" },
                new Column { Name = "TimeToResolution", DataType = "int64" }, // minutes
                new Column { Name = "Department", DataType = "string" },
                new Column { Name = "District", DataType = "string" }
            }
        };
    }

    private Table CreateAssetManagementTableSchema()
    {
        return new Table
        {
            Name = "FieldAssets",
            Columns = new List<Column>
            {
                new Column { Name = "Id", DataType = "string" },
                new Column { Name = "AssetName", DataType = "string" },
                new Column { Name = "AssetType", DataType = "string" },
                new Column { Name = "Status", DataType = "string" },
                new Column { Name = "Latitude", DataType = "double" },
                new Column { Name = "Longitude", DataType = "double" },
                new Column { Name = "LastMaintenanceDate", DataType = "datetime" },
                new Column { Name = "NextMaintenanceDate", DataType = "datetime" },
                new Column { Name = "Condition", DataType = "string" },
                new Column { Name = "InstallationDate", DataType = "datetime" },
                new Column { Name = "Department", DataType = "string" }
            }
        };
    }

    private Table CreateBuildingOccupancyTableSchema()
    {
        return new Table
        {
            Name = "BuildingOccupancy",
            Columns = new List<Column>
            {
                new Column { Name = "Id", DataType = "string" },
                new Column { Name = "BuildingName", DataType = "string" },
                new Column { Name = "FloorNumber", DataType = "int64" },
                new Column { Name = "CurrentOccupancy", DataType = "int64" },
                new Column { Name = "MaxCapacity", DataType = "int64" },
                new Column { Name = "OccupancyPercentage", DataType = "double" },
                new Column { Name = "Latitude", DataType = "double" },
                new Column { Name = "Longitude", DataType = "double" },
                new Column { Name = "Timestamp", DataType = "datetime" },
                new Column { Name = "BuildingType", DataType = "string" }
            }
        };
    }

    private Table CreateGenericTableSchema()
    {
        return new Table
        {
            Name = "Features",
            Columns = new List<Column>
            {
                new Column { Name = "Id", DataType = "string" },
                new Column { Name = "DisplayName", DataType = "string" },
                new Column { Name = "GeometryType", DataType = "string" },
                new Column { Name = "Latitude", DataType = "double" },
                new Column { Name = "Longitude", DataType = "double" },
                new Column { Name = "Properties", DataType = "string" },
                new Column { Name = "CreatedAt", DataType = "datetime" },
                new Column { Name = "UpdatedAt", DataType = "datetime" }
            }
        };
    }
}

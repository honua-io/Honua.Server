// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.PowerBI.Api.Models;
using Moq;
using Honua.Integration.PowerBI.Configuration;
using Honua.Integration.PowerBI.Services;

namespace Honua.Integration.PowerBI.Tests.TestFixtures;

/// <summary>
/// Shared test fixture for Power BI tests
/// </summary>
public class PowerBITestFixture : IDisposable
{
    public Mock<IPowerBIDatasetService> MockDatasetService { get; }
    public Mock<ILogger<PowerBIDatasetService>> MockDatasetLogger { get; }
    public Mock<ILogger<PowerBIStreamingService>> MockStreamingLogger { get; }
    public PowerBIOptions DefaultOptions { get; }

    public PowerBITestFixture()
    {
        MockDatasetService = new Mock<IPowerBIDatasetService>();
        MockDatasetLogger = new Mock<ILogger<PowerBIDatasetService>>();
        MockStreamingLogger = new Mock<ILogger<PowerBIStreamingService>>();

        DefaultOptions = new PowerBIOptions
        {
            TenantId = "test-tenant-id",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            WorkspaceId = "test-workspace-id",
            ApiUrl = "https://api.powerbi.com",
            EnableODataFeeds = true,
            EnablePushDatasets = true,
            EnableDatasetRefresh = true,
            MaxODataPageSize = 5000,
            PushDatasetRateLimitPerHour = 10000,
            StreamingBatchSize = 100,
            HonuaServerBaseUrl = "https://localhost:5001",
            Datasets = new List<PowerBIDatasetConfig>
            {
                new PowerBIDatasetConfig
                {
                    Name = "Traffic Dashboard",
                    DatasetId = "traffic-dataset-id",
                    Type = "Traffic",
                    CollectionIds = new List<string> { "traffic-collection" },
                    EnableIncrementalRefresh = true,
                    IncrementalRefreshColumn = "Timestamp",
                    RefreshSchedule = "0 */6 * * *"
                },
                new PowerBIDatasetConfig
                {
                    Name = "Air Quality Dashboard",
                    DatasetId = "airquality-dataset-id",
                    Type = "AirQuality",
                    CollectionIds = new List<string> { "airquality-collection" },
                    EnableIncrementalRefresh = true,
                    IncrementalRefreshColumn = "Timestamp",
                    RefreshSchedule = "0 */6 * * *"
                }
            },
            StreamingDatasets = new List<PowerBIStreamingDatasetConfig>
            {
                new PowerBIStreamingDatasetConfig
                {
                    Name = "Real-time Observations",
                    DatasetId = "observations-dataset-id",
                    PushUrl = "https://api.powerbi.com/pushdata/observations",
                    SourceType = "Observations",
                    DatastreamIds = new List<string> { "datastream-1", "datastream-2" },
                    AutoStream = true,
                    RetentionPolicy = 200000
                },
                new PowerBIStreamingDatasetConfig
                {
                    Name = "Anomaly Alerts",
                    DatasetId = "alerts-dataset-id",
                    PushUrl = "https://api.powerbi.com/pushdata/alerts",
                    SourceType = "Alerts",
                    DatastreamIds = new List<string>(),
                    AutoStream = true,
                    RetentionPolicy = 100000
                }
            }
        };
    }

    public Table CreateTestTableSchema(string tableName)
    {
        return new Table
        {
            Name = tableName,
            Columns = new List<Column>
            {
                new Column { Name = "Id", DataType = "string" },
                new Column { Name = "Name", DataType = "string" },
                new Column { Name = "Value", DataType = "double" },
                new Column { Name = "Timestamp", DataType = "datetime" },
                new Column { Name = "Latitude", DataType = "double" },
                new Column { Name = "Longitude", DataType = "double" }
            }
        };
    }

    public Dataset CreateTestDataset(string id, string name)
    {
        return new Dataset
        {
            Id = id,
            Name = name,
            AddRowsApiEnabled = true,
            ConfiguredBy = "test-user",
            IsRefreshable = true,
            IsEffectiveIdentityRequired = false,
            IsEffectiveIdentityRolesRequired = false,
            IsOnPremGatewayRequired = false,
            TargetStorageMode = "Push"
        };
    }

    public GenerateTokenResponse CreateTestEmbedToken(string token)
    {
        return new GenerateTokenResponse
        {
            Token = token,
            TokenId = Guid.NewGuid().ToString(),
            Expiration = DateTime.UtcNow.AddHours(1)
        };
    }

    public void Dispose()
    {
        // Cleanup resources if needed
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Mock HTTP response builders for Power BI API tests
/// </summary>
public static class PowerBIMockResponseBuilder
{
    public static Datasets CreateDatasetsResponse(params Dataset[] datasets)
    {
        return new Datasets
        {
            Value = new List<Dataset>(datasets)
        };
    }

    public static Exception CreateUnauthorizedException()
    {
        return new UnauthorizedAccessException("Unauthorized: Invalid credentials or insufficient permissions");
    }

    public static Exception CreateForbiddenException()
    {
        return new InvalidOperationException("Forbidden: Access to workspace denied");
    }

    public static Exception CreateRateLimitException()
    {
        return new InvalidOperationException("Rate limit exceeded: Too many requests");
    }

    public static Exception CreateInternalServerException()
    {
        return new Exception("Internal Server Error: Power BI service error");
    }

    public static Exception CreateNotFoundException()
    {
        return new KeyNotFoundException("Not Found: Dataset or resource does not exist");
    }
}

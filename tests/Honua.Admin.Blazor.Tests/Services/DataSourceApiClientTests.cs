// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using System.Net;

namespace Honua.Admin.Blazor.Tests.Services;

/// <summary>
/// Tests for DataSourceApiClient.
/// Validates API client operations for data source management including CRUD operations,
/// connection testing, and table discovery.
/// </summary>
[Trait("Category", "Unit")]
public class DataSourceApiClientTests
{
    private const string BaseUrl = "https://localhost:5001";

    [Fact]
    public async Task GetDataSourcesAsync_Success_ReturnsDataSources()
    {
        // Arrange
        var expectedDataSources = new List<DataSourceListItem>
        {
            new DataSourceListItem
            {
                Id = "postgres-main",
                Provider = "postgis",
                ConnectionString = "Host=localhost;Port=5432;Database=gis;Username=postgres;Password=***"
            },
            new DataSourceListItem
            {
                Id = "sqlserver-prod",
                Provider = "sqlserver",
                ConnectionString = "Server=localhost;Database=gisdb;User Id=sa;Password=***"
            }
        };

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockGetJson("/admin/metadata/datasources", expectedDataSources);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var result = await apiClient.GetDataSourcesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("postgres-main");
        result[0].Provider.Should().Be("postgis");
        result[1].Id.Should().Be("sqlserver-prod");
        result[1].Provider.Should().Be("sqlserver");
    }

    [Fact]
    public async Task GetDataSourcesAsync_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var emptyList = new List<DataSourceListItem>();

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockGetJson("/admin/metadata/datasources", emptyList);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var result = await apiClient.GetDataSourcesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDataSourceByIdAsync_ExistingId_ReturnsDataSource()
    {
        // Arrange
        var expectedDataSource = new DataSourceResponse
        {
            Id = "postgres-main",
            Provider = "postgis",
            ConnectionString = "Host=localhost;Port=5432;Database=gis;Username=postgres;Password=***"
        };

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockGetJson("/admin/metadata/datasources/postgres-main", expectedDataSource);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var result = await apiClient.GetDataSourceByIdAsync("postgres-main");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("postgres-main");
        result.Provider.Should().Be("postgis");
    }

    [Fact]
    public async Task GetDataSourceByIdAsync_NotFound_ThrowsException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockError(HttpMethod.Get, "/admin/metadata/datasources/nonexistent", HttpStatusCode.NotFound);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var act = async () => await apiClient.GetDataSourceByIdAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateDataSourceAsync_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateDataSourceRequest
        {
            Id = "new-datasource",
            Provider = "postgis",
            ConnectionString = "Host=localhost;Port=5432;Database=newdb;Username=user;Password=pass"
        };

        var expectedResponse = new DataSourceResponse
        {
            Id = "new-datasource",
            Provider = "postgis",
            ConnectionString = "Host=localhost;Port=5432;Database=newdb;Username=user;Password=pass"
        };

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockPostJson("/admin/metadata/datasources", expectedResponse, HttpStatusCode.Created);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var result = await apiClient.CreateDataSourceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("new-datasource");
        result.Provider.Should().Be("postgis");
    }

    [Fact]
    public async Task CreateDataSourceAsync_DuplicateId_ThrowsException()
    {
        // Arrange
        var request = new CreateDataSourceRequest
        {
            Id = "existing-datasource",
            Provider = "postgis",
            ConnectionString = "Host=localhost;Port=5432;Database=db;Username=user;Password=pass"
        };

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockError(HttpMethod.Post, "/admin/metadata/datasources", HttpStatusCode.Conflict, "Data source ID already exists");

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var act = async () => await apiClient.CreateDataSourceAsync(request);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task UpdateDataSourceAsync_ValidRequest_UpdatesSuccessfully()
    {
        // Arrange
        var request = new UpdateDataSourceRequest
        {
            Provider = "postgis",
            ConnectionString = "Host=updated-host;Port=5432;Database=gis;Username=user;Password=pass"
        };

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockPutJson("/admin/metadata/datasources/postgres-main", new object(), HttpStatusCode.NoContent);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var act = async () => await apiClient.UpdateDataSourceAsync("postgres-main", request);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateDataSourceAsync_NotFound_ThrowsException()
    {
        // Arrange
        var request = new UpdateDataSourceRequest
        {
            Provider = "postgis",
            ConnectionString = "Host=localhost;Port=5432;Database=gis;Username=user;Password=pass"
        };

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockError(HttpMethod.Put, "/admin/metadata/datasources/nonexistent", HttpStatusCode.NotFound);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var act = async () => await apiClient.UpdateDataSourceAsync("nonexistent", request);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DeleteDataSourceAsync_UnusedDataSource_DeletesSuccessfully()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockDelete("/admin/metadata/datasources/unused-datasource", HttpStatusCode.NoContent);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var act = async () => await apiClient.DeleteDataSourceAsync("unused-datasource");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteDataSourceAsync_InUseByServices_ThrowsException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockError(HttpMethod.Delete, "/admin/metadata/datasources/in-use-datasource",
                HttpStatusCode.Conflict, "Cannot delete data source that is in use by services");

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var act = async () => await apiClient.DeleteDataSourceAsync("in-use-datasource");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task TestConnectionAsync_ValidConnection_ReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new TestConnectionResponse
        {
            Success = true,
            Message = "Connection successful",
            Provider = "postgis",
            ConnectionTime = 234
        };

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockPostJson("/admin/metadata/datasources/postgres-main/test", expectedResponse);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var result = await apiClient.TestConnectionAsync("postgres-main");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Connection successful");
        result.Provider.Should().Be("postgis");
        result.ConnectionTime.Should().Be(234);
    }

    [Fact]
    public async Task TestConnectionAsync_InvalidCredentials_ReturnsFailure()
    {
        // Arrange
        var expectedResponse = new TestConnectionResponse
        {
            Success = false,
            Message = "Authentication failed: Invalid username or password",
            Provider = "postgis",
            ConnectionTime = 0
        };

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockPostJson("/admin/metadata/datasources/postgres-main/test", expectedResponse);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var result = await apiClient.TestConnectionAsync("postgres-main");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Authentication failed");
    }

    [Fact]
    public async Task TestConnectionAsync_NetworkError_ReturnsFailure()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockException(HttpMethod.Post, "/admin/metadata/datasources/postgres-main/test",
                new HttpRequestException("Network error"));

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var result = await apiClient.TestConnectionAsync("postgres-main");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Connection test failed");
    }

    [Fact]
    public async Task GetTablesAsync_ValidDataSource_ReturnsTables()
    {
        // Arrange
        var expectedResponse = new TableListResponse
        {
            Tables = new List<TableInfo>
            {
                new TableInfo
                {
                    Schema = "public",
                    Table = "cities",
                    GeometryColumn = "geom",
                    GeometryType = "Point",
                    Srid = 4326,
                    RowCount = 1000,
                    Columns = new List<ColumnInfo>
                    {
                        new ColumnInfo { Name = "id", DataType = "integer", IsPrimaryKey = true },
                        new ColumnInfo { Name = "name", DataType = "varchar", MaxLength = 255 }
                    }
                },
                new TableInfo
                {
                    Schema = "public",
                    Table = "countries",
                    GeometryColumn = "geom",
                    GeometryType = "Polygon",
                    Srid = 4326,
                    RowCount = 195,
                    Columns = new List<ColumnInfo>()
                }
            }
        };

        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockGetJson("/admin/metadata/datasources/postgres-main/tables", expectedResponse);

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var result = await apiClient.GetTablesAsync("postgres-main");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Schema.Should().Be("public");
        result[0].Table.Should().Be("cities");
        result[0].GeometryType.Should().Be("Point");
        result[0].Columns.Should().HaveCount(2);
        result[1].Table.Should().Be("countries");
    }

    [Fact]
    public async Task GetTablesAsync_InvalidDataSource_ThrowsException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory(BaseUrl)
            .MockError(HttpMethod.Get, "/admin/metadata/datasources/invalid-datasource/tables",
                HttpStatusCode.NotFound, "Data source not found");

        var httpClientFactory = new TestHttpClientFactory(mockFactory.CreateClient());
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataSourceApiClient>();
        var apiClient = new DataSourceApiClient(httpClientFactory, logger);

        // Act
        var act = async () => await apiClient.GetTablesAsync("invalid-datasource");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}

/// <summary>
/// Test implementation of IHttpClientFactory for testing.
/// </summary>
internal class TestHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _httpClient;

    public TestHttpClientFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public HttpClient CreateClient(string name)
    {
        return _httpClient;
    }
}

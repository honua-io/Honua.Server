// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text.Json;

namespace Honua.Admin.Blazor.Tests.Services;

/// <summary>
/// Tests for ImportApiClient.
/// </summary>
[Trait("Category", "Unit")]
public class ImportApiClientTests
{
    private readonly Mock<ILogger<ImportApiClient>> _loggerMock;

    public ImportApiClientTests()
    {
        _loggerMock = new Mock<ILogger<ImportApiClient>>();
    }

    [Fact]
    public async Task CreateImportJobAsync_ValidFile_ReturnsJob()
    {
        // Arrange
        var expectedJob = new ImportJobSnapshot
        {
            JobId = Guid.NewGuid(),
            ServiceId = "test-service",
            LayerId = "test-layer",
            FileName = "test.geojson",
            FileSizeBytes = 1024,
            Status = "Queued",
            Progress = 0,
            CreatedAt = DateTime.UtcNow
        };

        var responseWrapper = new { job = expectedJob };
        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Post, "https://localhost:5001/admin/ingestion/jobs")
            .Respond(HttpStatusCode.Accepted, "application/json", JsonSerializer.Serialize(responseWrapper, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("test.geojson");
        mockFile.Setup(f => f.Size).Returns(1024);
        mockFile.Setup(f => f.ContentType).Returns("application/geo+json");
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(new MemoryStream(new byte[1024]));

        // Act
        var result = await apiClient.CreateImportJobAsync(
            "test-service",
            "test-layer",
            mockFile.Object,
            overwrite: false);

        // Assert
        result.Should().NotBeNull();
        result!.JobId.Should().Be(expectedJob.JobId);
        result.ServiceId.Should().Be("test-service");
        result.LayerId.Should().Be("test-layer");
        result.FileName.Should().Be("test.geojson");
        result.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task CreateImportJobAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var expectedJob = new ImportJobSnapshot
        {
            JobId = Guid.NewGuid(),
            ServiceId = "test-service",
            LayerId = "test-layer",
            Status = "Queued",
            CreatedAt = DateTime.UtcNow
        };

        var responseWrapper = new { job = expectedJob };
        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Post, "https://localhost:5001/admin/ingestion/jobs")
            .Respond(HttpStatusCode.Accepted, "application/json", JsonSerializer.Serialize(responseWrapper, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("test.geojson");
        mockFile.Setup(f => f.Size).Returns(1024);
        mockFile.Setup(f => f.ContentType).Returns("application/geo+json");
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(new MemoryStream(new byte[1024]));

        var progressReports = new List<double>();
        var progress = new Progress<double>(p => progressReports.Add(p));

        // Act
        var result = await apiClient.CreateImportJobAsync(
            "test-service",
            "test-layer",
            mockFile.Object,
            overwrite: false,
            progress: progress);

        // Assert
        result.Should().NotBeNull();
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(100); // Final progress report
    }

    [Fact]
    public async Task CreateImportJobAsync_OversizedFile_ThrowsException()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("huge-file.geojson");
        mockFile.Setup(f => f.Size).Returns(600L * 1024 * 1024); // 600 MB
        mockFile.Setup(f => f.ContentType).Returns("application/geo+json");
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Throws(new IOException("File size exceeds maximum allowed size"));

        // Act
        var act = async () => await apiClient.CreateImportJobAsync(
            "test-service",
            "test-layer",
            mockFile.Object);

        // Assert
        await act.Should().ThrowAsync<IOException>()
            .WithMessage("*exceeds maximum*");
    }

    [Fact]
    public async Task CreateImportJobAsync_NetworkError_ThrowsException()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Post, "https://localhost:5001/admin/ingestion/jobs")
            .Throw(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("test.geojson");
        mockFile.Setup(f => f.Size).Returns(1024);
        mockFile.Setup(f => f.ContentType).Returns("application/geo+json");
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(new MemoryStream(new byte[1024]));

        // Act
        var act = async () => await apiClient.CreateImportJobAsync(
            "test-service",
            "test-layer",
            mockFile.Object);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*Network error*");
    }

    [Fact]
    public async Task GetImportJobAsync_ExistingId_ReturnsJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var expectedJob = new ImportJobSnapshot
        {
            JobId = jobId,
            ServiceId = "test-service",
            LayerId = "test-layer",
            Status = "Running",
            Progress = 50,
            RecordsProcessed = 5000,
            RecordsTotal = 10000,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow
        };

        var responseWrapper = new { job = expectedJob };
        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Get, $"https://localhost:5001/admin/ingestion/jobs/{jobId}")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(responseWrapper, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        // Act
        var result = await apiClient.GetImportJobAsync(jobId);

        // Assert
        result.Should().NotBeNull();
        result!.JobId.Should().Be(jobId);
        result.Status.Should().Be("Running");
        result.Progress.Should().Be(50);
        result.RecordsProcessed.Should().Be(5000);
        result.RecordsTotal.Should().Be(10000);
    }

    [Fact]
    public async Task GetImportJobAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Get, $"https://localhost:5001/admin/ingestion/jobs/{jobId}")
            .Respond(HttpStatusCode.NotFound);

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        // Act
        var result = await apiClient.GetImportJobAsync(jobId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListImportJobsAsync_WithoutPagination_ReturnsJobs()
    {
        // Arrange
        var expectedJobs = new PaginatedImportJobs
        {
            Items = new List<ImportJobSnapshot>
            {
                new ImportJobSnapshot
                {
                    JobId = Guid.NewGuid(),
                    ServiceId = "service-1",
                    LayerId = "layer-1",
                    Status = "Completed",
                    Progress = 100,
                    CreatedAt = DateTime.UtcNow
                },
                new ImportJobSnapshot
                {
                    JobId = Guid.NewGuid(),
                    ServiceId = "service-2",
                    LayerId = "layer-2",
                    Status = "Running",
                    Progress = 75,
                    CreatedAt = DateTime.UtcNow
                }
            },
            TotalCount = 2,
            NextPageToken = null
        };

        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Get, "https://localhost:5001/admin/ingestion/jobs?pageSize=25")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(expectedJobs, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        // Act
        var result = await apiClient.ListImportJobsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.NextPageToken.Should().BeNull();
        result.Items[0].Status.Should().Be("Completed");
        result.Items[1].Status.Should().Be("Running");
    }

    [Fact]
    public async Task ListImportJobsAsync_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        var pageToken = "page-token-123";
        var expectedJobs = new PaginatedImportJobs
        {
            Items = new List<ImportJobSnapshot>
            {
                new ImportJobSnapshot
                {
                    JobId = Guid.NewGuid(),
                    ServiceId = "service-3",
                    LayerId = "layer-3",
                    Status = "Queued",
                    Progress = 0,
                    CreatedAt = DateTime.UtcNow
                }
            },
            TotalCount = 1,
            NextPageToken = "next-page-token"
        };

        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Get, $"https://localhost:5001/admin/ingestion/jobs?pageSize=25&pageToken={Uri.EscapeDataString(pageToken)}")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(expectedJobs, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        // Act
        var result = await apiClient.ListImportJobsAsync(pageToken: pageToken);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.NextPageToken.Should().Be("next-page-token");
    }

    [Fact]
    public async Task ListImportJobsAsync_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var expectedJobs = new PaginatedImportJobs
        {
            Items = new List<ImportJobSnapshot>(),
            TotalCount = 0,
            NextPageToken = null
        };

        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Get, "https://localhost:5001/admin/ingestion/jobs?pageSize=25")
            .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(expectedJobs, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        // Act
        var result = await apiClient.ListImportJobsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelImportJobAsync_RunningJob_CancelsSuccessfully()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Delete, $"https://localhost:5001/admin/ingestion/jobs/{jobId}")
            .Respond(HttpStatusCode.OK);

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        // Act
        var result = await apiClient.CancelImportJobAsync(jobId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CancelImportJobAsync_CompletedJob_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Delete, $"https://localhost:5001/admin/ingestion/jobs/{jobId}")
            .Respond(HttpStatusCode.BadRequest);

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        // Act
        var result = await apiClient.CancelImportJobAsync(jobId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelImportJobAsync_NotFound_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mockHandler = new MockHttpMessageHandler();
        mockHandler
            .When(HttpMethod.Delete, $"https://localhost:5001/admin/ingestion/jobs/{jobId}")
            .Respond(HttpStatusCode.NotFound);

        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("https://localhost:5001") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AdminApi")).Returns(httpClient);

        var apiClient = new ImportApiClient(factoryMock.Object, _loggerMock.Object);

        // Act
        var result = await apiClient.CancelImportJobAsync(jobId);

        // Assert
        result.Should().BeFalse();
    }
}

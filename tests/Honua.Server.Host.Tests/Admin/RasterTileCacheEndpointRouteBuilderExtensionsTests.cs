using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Host.Admin;
using Honua.Server.Host.Raster;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Admin;

/// <summary>
/// Tests for RasterTileCacheEndpointRouteBuilderExtensions to ensure proper error handling and sanitization
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Unit")]
public sealed class RasterTileCacheEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public async Task CreateJob_ShouldReturnAccepted_WhenRequestIsValid()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        var expectedSnapshot = new RasterTilePreseedJobSnapshot
        {
            JobId = Guid.NewGuid(),
            DatasetIds = new List<string> { "test-dataset" },
            Status = RasterTilePreseedJobStatus.Queued,
            Progress = 0.0,
            TilesProcessed = 0,
            TilesTotal = 100,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = null
        };

        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<RasterTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSnapshot);

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = new[] { "test-dataset" },
            tileMatrixSetId = "WorldWebMercatorQuad",
            minZoom = 0,
            maxZoom = 5,
            format = "image/png"
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/raster-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var content = await response.Content.ReadFromJsonAsync<JobResponse>();
        content.Should().NotBeNull();
        content!.Job.Should().NotBeNull();
        content.Job!.JobId.Should().Be(expectedSnapshot.JobId);
    }

    [Fact]
    public async Task CreateJob_ShouldReturnBadRequest_WhenDatasetIdsIsMissing()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = Array.Empty<string>(),
            minZoom = 0,
            maxZoom = 5
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/raster-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Be("datasetIds array is required.");
    }

    [Fact]
    public async Task CreateJob_ShouldSanitizeError_WhenInvalidOperationExceptionThrown()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();

        // Simulate an InvalidOperationException with potentially sensitive info
        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<RasterTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Dataset 'rainfall-2024' not found in database table dbo.datasets on server db-prod-01.internal.company.com:5432"));

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = new[] { "rainfall-2024" },
            minZoom = 0,
            maxZoom = 10
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/raster-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();

        // Should NOT leak the internal error details
        error!.Error.Should().NotContain("dbo.datasets");
        error.Error.Should().NotContain("db-prod-01");
        error.Error.Should().NotContain("internal.company.com");
        error.Error.Should().Be("The preseed request could not be processed. Please check your dataset IDs and parameters.");
    }

    [Fact]
    public async Task CreateJob_ShouldSanitizeError_WhenArgumentExceptionThrown()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();

        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<RasterTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException(
                "Invalid zoom configuration: minZoom (15) exceeds maxZoom (10). Internal cache key: /mnt/storage/tiles/cache"));

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = new[] { "test-dataset" },
            minZoom = 15,
            maxZoom = 10
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/raster-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();

        // Should NOT leak internal details like cache keys or file paths
        error!.Error.Should().NotContain("/mnt/storage");
        error.Error.Should().NotContain("cache key");
        error.Error.Should().Be("Invalid request parameters. Please verify your tile configuration.");
    }

    [Fact]
    public async Task CreateJob_ShouldReturnProblemDetails_WhenUnexpectedExceptionThrown()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();

        // Simulate unexpected exception with stack trace and file paths
        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<RasterTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NullReferenceException(
                "Object reference not set to an instance of an object at RasterTilePreseedService.EnqueueAsync() in /app/src/Honua.Server.Host/Raster/RasterTilePreseedService.cs:line 127"));

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = new[] { "test-dataset" },
            minZoom = 0,
            maxZoom = 10
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/raster-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();

        // Should NOT leak internal details like file paths, stack traces, or line numbers
        content.Should().NotContain("/app/src/");
        content.Should().NotContain("RasterTilePreseedService.cs");
        content.Should().NotContain("line 127");
        content.Should().NotContain("Object reference not set");
        content.Should().Contain("unexpected error occurred");
    }

    [Fact]
    public async Task CreateJob_ShouldLogException_WhenErrorOccurs()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        var mockLogger = new Mock<ILogger<IRasterTilePreseedService>>();

        var expectedException = new InvalidOperationException("Test exception");
        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<RasterTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        await using var factory = CreateTestFactoryWithLogger(mockService.Object, mockLogger.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = new[] { "test-dataset" },
            minZoom = 0,
            maxZoom = 10
        };

        // Act
        await client.PostAsJsonAsync("/admin/raster-cache/jobs", request);

        // Assert - Verify logging occurred
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Raster tile preseed job creation failed validation")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PurgeCache_ShouldReturnOk_WhenRequestIsValid()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        var purgeResult = new RasterTileCachePurgeResult
        {
            PurgedDatasets = new List<string> { "dataset1", "dataset2" },
            FailedDatasets = new List<string>()
        };

        mockService
            .Setup(s => s.PurgeAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(purgeResult);

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = new[] { "dataset1", "dataset2" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/raster-cache/datasets/purge", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<PurgeResponse>();
        content.Should().NotBeNull();
        content!.Purged.Should().HaveCount(2);
        content.Failed.Should().BeEmpty();
    }

    [Fact]
    public async Task PurgeCache_ShouldReturnBadRequest_WhenDatasetIdsIsMissing()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = Array.Empty<string>()
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/raster-cache/datasets/purge", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Be("datasetIds array is required.");
    }

    [Fact]
    public async Task PurgeCache_ShouldSanitizeError_WhenInvalidOperationExceptionThrown()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();

        // Simulate an InvalidOperationException with storage backend details
        mockService
            .Setup(s => s.PurgeAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Failed to connect to S3 bucket honua-tiles-prod in region us-west-2 using access key AKIAIOSFODNN7EXAMPLE"));

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = new[] { "test-dataset" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/raster-cache/datasets/purge", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();

        // Should NOT leak storage backend details, bucket names, or access keys
        error!.Error.Should().NotContain("S3");
        error.Error.Should().NotContain("honua-tiles-prod");
        error.Error.Should().NotContain("us-west-2");
        error.Error.Should().NotContain("AKIA");
        error.Error.Should().Be("The cache purge request could not be processed. Please check your dataset IDs.");
    }

    [Fact]
    public async Task PurgeCache_ShouldReturnProblemDetails_WhenUnexpectedExceptionThrown()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();

        // Simulate unexpected exception with Azure Blob Storage details
        mockService
            .Setup(s => s.PurgeAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(
                "Azure.Storage.Blobs.BlobContainerClient failed: Account=honuaprodstore;Container=tiles;SAS=sv=2021-06-08&st=2024-01..."));

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = new[] { "test-dataset" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/raster-cache/datasets/purge", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();

        // Should NOT leak Azure storage details, account names, or SAS tokens
        content.Should().NotContain("honuaprodstore");
        content.Should().NotContain("Container=tiles");
        content.Should().NotContain("SAS=");
        content.Should().NotContain("sv=2021");
        content.Should().Contain("unexpected error occurred");
    }

    [Fact]
    public async Task PurgeCache_ShouldLogException_WhenErrorOccurs()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        var mockLogger = new Mock<ILogger<IRasterTilePreseedService>>();

        var expectedException = new Exception("Storage backend failure");
        mockService
            .Setup(s => s.PurgeAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        await using var factory = CreateTestFactoryWithLogger(mockService.Object, mockLogger.Object);
        using var client = factory.CreateClient();

        var request = new
        {
            datasetIds = new[] { "test-dataset" }
        };

        // Act
        await client.PostAsJsonAsync("/admin/raster-cache/datasets/purge", request);

        // Assert - Verify logging occurred
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Raster tile cache purge failed unexpectedly")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ListJobs_ShouldReturnAllJobs()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        var jobs = new[]
        {
            new RasterTilePreseedJobSnapshot
            {
                JobId = Guid.NewGuid(),
                DatasetIds = new List<string> { "dataset1" },
                Status = RasterTilePreseedJobStatus.Running,
                Progress = 0.5,
                TilesProcessed = 50,
                TilesTotal = 100,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = null
            }
        };

        mockService
            .Setup(s => s.ListJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/raster-cache/jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JobsResponse>();
        content.Should().NotBeNull();
        content!.Jobs.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetJob_ShouldReturnOk_WhenJobExists()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        var jobId = Guid.NewGuid();
        var snapshot = new RasterTilePreseedJobSnapshot
        {
            JobId = jobId,
            DatasetIds = new List<string> { "dataset1" },
            Status = RasterTilePreseedJobStatus.Running,
            Progress = 0.5,
            TilesProcessed = 50,
            TilesTotal = 100,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = null
        };

        mockService
            .Setup(s => s.TryGetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/admin/raster-cache/jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetJob_ShouldReturnNotFound_WhenJobDoesNotExist()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        mockService
            .Setup(s => s.TryGetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RasterTilePreseedJobSnapshot?)null);

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var jobId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/admin/raster-cache/jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelJob_ShouldReturnOk_WhenJobExists()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        var jobId = Guid.NewGuid();
        var snapshot = new RasterTilePreseedJobSnapshot
        {
            JobId = jobId,
            DatasetIds = new List<string> { "dataset1" },
            Status = RasterTilePreseedJobStatus.Cancelled,
            Progress = 0.5,
            TilesProcessed = 50,
            TilesTotal = 100,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTimeOffset.UtcNow
        };

        mockService
            .Setup(s => s.CancelAsync(jobId, It.IsAny<string>()))
            .ReturnsAsync(snapshot);

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync($"/admin/raster-cache/jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JobResponse>();
        content.Should().NotBeNull();
        content!.Job.Should().NotBeNull();
        content.Job!.Status.Should().Be(RasterTilePreseedJobStatus.Cancelled);
    }

    [Fact]
    public async Task CancelJob_ShouldReturnNotFound_WhenJobDoesNotExist()
    {
        // Arrange
        var mockService = new Mock<IRasterTilePreseedService>();
        mockService
            .Setup(s => s.CancelAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((RasterTilePreseedJobSnapshot?)null);

        await using var factory = CreateTestFactory(mockService.Object);
        using var client = factory.CreateClient();

        var jobId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/admin/raster-cache/jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static WebApplicationFactory<RasterTileTestStartup> CreateTestFactory(
        IRasterTilePreseedService preseedService)
    {
        return CreateTestFactoryWithLogger(preseedService, NullLogger<IRasterTilePreseedService>.Instance);
    }

    private static WebApplicationFactory<RasterTileTestStartup> CreateTestFactoryWithLogger(
        IRasterTilePreseedService preseedService,
        ILogger<IRasterTilePreseedService> logger)
    {
        return new WebApplicationFactory<RasterTileTestStartup>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(preseedService);
                    services.AddSingleton(logger);
                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("RequireAdministrator", policy =>
                            policy.RequireAssertion(_ => true)); // Allow all for tests
                    });
                });
            });
    }

    private sealed class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
    }

    private sealed class JobResponse
    {
        public RasterTilePreseedJobSnapshot? Job { get; set; }
    }

    private sealed class JobsResponse
    {
        public RasterTilePreseedJobSnapshot[] Jobs { get; set; } = Array.Empty<RasterTilePreseedJobSnapshot>();
    }

    private sealed class PurgeResponse
    {
        public List<string> Purged { get; set; } = new();
        public List<string> Failed { get; set; } = new();
    }
}

/// <summary>
/// Minimal test startup for raster tile cache endpoint testing
/// </summary>
internal sealed class RasterTileTestStartup
{
    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRasterTileCacheAdministration();
        });
    }
}

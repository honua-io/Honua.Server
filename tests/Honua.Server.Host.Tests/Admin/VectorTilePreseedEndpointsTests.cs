using System;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Logging;
using Honua.Server.Host.Admin;
using Honua.Server.Host.VectorTiles;
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
/// Tests for VectorTilePreseedEndpoints to ensure proper error handling and sanitization
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Unit")]
public sealed class VectorTilePreseedEndpointsTests
{
    [Fact]
    public async Task EnqueueJob_ShouldReturnAccepted_WhenRequestIsValid()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();
        var expectedSnapshot = new VectorTilePreseedJobSnapshot
        {
            JobId = Guid.NewGuid(),
            ServiceId = "test-service",
            LayerId = "test-layer",
            Status = VectorTilePreseedJobStatus.Queued,
            Progress = 0.0,
            TilesProcessed = 0,
            TilesTotal = 100,
            Message = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = null
        };

        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<VectorTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSnapshot);

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/vector-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var snapshot = await response.Content.ReadFromJsonAsync<VectorTilePreseedJobSnapshot>();
        snapshot.Should().NotBeNull();
        snapshot!.JobId.Should().Be(expectedSnapshot.JobId);

        // Verify audit log was called
        mockAuditLogger.Verify(
            a => a.LogAdminOperation(
                "enqueue_vector_preseed_job",
                It.IsAny<string>(),
                "vector_tile_preseed",
                expectedSnapshot.JobId.ToString(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueJob_ShouldSanitizeError_WhenMaxZoomExceeded()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<VectorTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Maximum zoom level (20) exceeds allowed limit (18). Higher zoom levels generate exponentially more tiles and can cause resource exhaustion."));

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 20,
            Overwrite = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/vector-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        // Should return the original safe validation message
        error!.Error.Should().StartWith("Maximum zoom level");

        // Verify no audit log for failed requests
        mockAuditLogger.Verify(
            a => a.LogAdminOperation(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task EnqueueJob_ShouldSanitizeError_WhenMaxConcurrentJobsExceeded()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<VectorTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Maximum concurrent jobs (10) reached. Please wait for existing jobs to complete or cancel them."));

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/vector-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().StartWith("Maximum concurrent jobs");
    }

    [Fact]
    public async Task EnqueueJob_ShouldSanitizeError_WhenRateLimitExceeded()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<VectorTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Rate limit exceeded for test-service/test-layer. Please wait 30 seconds before submitting another job."));

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/vector-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().StartWith("Rate limit exceeded");
    }

    [Fact]
    public async Task EnqueueJob_ShouldReturnGenericError_ForUnknownInvalidOperationException()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        // Simulate an internal error with potentially sensitive information
        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<VectorTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Database connection failed: Server=internal-db-01.company.local;Port=5432"));

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/vector-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        // Should NOT leak the internal error message
        error!.Error.Should().NotContain("Database connection");
        error!.Error.Should().NotContain("internal-db-01");
        error!.Error.Should().Be("The request could not be processed. Please check your parameters and try again.");
    }

    [Fact]
    public async Task EnqueueJob_ShouldReturnGenericError_ForArgumentException()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<VectorTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid internal state: cache key mismatch"));

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/vector-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().NotContain("cache key");
        error!.Error.Should().Be("Invalid request parameters. Please check your input and try again.");
    }

    [Fact]
    public async Task EnqueueJob_ShouldReturnProblemDetails_ForUnexpectedException()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        // Simulate unexpected exception with stack trace
        mockService
            .Setup(s => s.EnqueueAsync(It.IsAny<VectorTilePreseedRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NullReferenceException("Object reference not set at DbContext.SaveChanges() in /app/Data/Repository.cs:line 42"));

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/vector-cache/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        // Should NOT leak internal details like file paths or stack traces
        content.Should().NotContain("/app/Data/Repository.cs");
        content.Should().NotContain("SaveChanges");
        content.Should().Contain("An error occurred while enqueueing the preseed job");
    }

    [Fact]
    public async Task GetJob_ShouldReturnNotFound_WithGenericMessage()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        mockService
            .Setup(s => s.TryGetJobAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorTilePreseedJobSnapshot?)null);

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var jobId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/admin/vector-cache/jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        // Should NOT include the job ID in the error message
        error!.Error.Should().Be("Job not found");
        error!.Error.Should().NotContain(jobId.ToString());
    }

    [Fact]
    public async Task CancelJob_ShouldReturnOk_WhenJobExists()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        var jobId = Guid.NewGuid();
        var snapshot = new VectorTilePreseedJobSnapshot
        {
            JobId = jobId,
            ServiceId = "test-service",
            LayerId = "test-layer",
            Status = VectorTilePreseedJobStatus.Cancelled,
            Progress = 0.5,
            TilesProcessed = 50,
            TilesTotal = 100,
            Message = "User requested cancellation",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTimeOffset.UtcNow
        };

        mockService
            .Setup(s => s.CancelAsync(jobId, It.IsAny<string>()))
            .ReturnsAsync(snapshot);

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync($"/admin/vector-cache/jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VectorTilePreseedJobSnapshot>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(VectorTilePreseedJobStatus.Cancelled);

        // Verify audit log
        mockAuditLogger.Verify(
            a => a.LogAdminOperation(
                "cancel_vector_preseed_job",
                It.IsAny<string>(),
                "vector_tile_preseed",
                jobId.ToString(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelJob_ShouldReturnNotFound_WhenJobDoesNotExist()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        mockService
            .Setup(s => s.CancelAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((VectorTilePreseedJobSnapshot?)null);

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var jobId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/admin/vector-cache/jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Be("Job not found");
        error!.Error.Should().NotContain(jobId.ToString());

        // No audit log for not found
        mockAuditLogger.Verify(
            a => a.LogAdminOperation(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelJob_ShouldReturnProblemDetails_OnException()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        mockService
            .Setup(s => s.CancelAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Internal cancellation state error"));

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        var jobId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/admin/vector-cache/jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("Internal cancellation state error");
        content.Should().Contain("An error occurred while cancelling the job");
    }

    [Fact]
    public async Task ListJobs_ShouldReturnAllJobs()
    {
        // Arrange
        var mockService = new Mock<IVectorTilePreseedService>();
        var mockAuditLogger = new Mock<ISecurityAuditLogger>();

        var jobs = new[]
        {
            new VectorTilePreseedJobSnapshot
            {
                JobId = Guid.NewGuid(),
                ServiceId = "service1",
                LayerId = "layer1",
                Status = VectorTilePreseedJobStatus.Running,
                Progress = 0.5,
                TilesProcessed = 50,
                TilesTotal = 100,
                Message = null,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                CompletedAtUtc = null
            },
            new VectorTilePreseedJobSnapshot
            {
                JobId = Guid.NewGuid(),
                ServiceId = "service2",
                LayerId = "layer2",
                Status = VectorTilePreseedJobStatus.Completed,
                Progress = 1.0,
                TilesProcessed = 200,
                TilesTotal = 200,
                Message = null,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-20),
                CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
            }
        };

        mockService
            .Setup(s => s.ListJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        await using var factory = CreateTestFactory(mockService.Object, mockAuditLogger.Object);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/vector-cache/jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VectorTilePreseedJobSnapshot[]>();
        result.Should().NotBeNull();
        result!.Length.Should().Be(2);
    }

    private static WebApplicationFactory<TestStartup> CreateTestFactory(
        IVectorTilePreseedService preseedService,
        ISecurityAuditLogger auditLogger)
    {
        return new WebApplicationFactory<TestStartup>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(preseedService);
                    services.AddSingleton(auditLogger);
                    services.AddSingleton<ILogger<IVectorTilePreseedService>>(NullLogger<IVectorTilePreseedService>.Instance);
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
}

/// <summary>
/// Minimal test startup for endpoint testing
/// </summary>
internal sealed class TestStartup
{
    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapVectorTilePreseed();
        });
    }
}

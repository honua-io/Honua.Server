// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Intake.BackgroundServices;
using Honua.Server.Intake.Configuration;
using Honua.Server.Intake.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Intake.Tests;

/// <summary>
/// Tests for BuildQueueProcessor - background service that processes build jobs from the queue.
/// </summary>
[Trait("Category", "Unit")]
public class BuildQueueProcessorTests
{
    private readonly Mock<IBuildQueueManager> _mockQueueManager;
    private readonly Mock<IBuildNotificationService> _mockNotificationService;
    private readonly Mock<ILogger<BuildQueueProcessor>> _mockLogger;
    private readonly BuildQueueOptions _options;
    private readonly IServiceProvider _serviceProvider;

    public BuildQueueProcessorTests()
    {
        _mockQueueManager = new Mock<IBuildQueueManager>();
        _mockNotificationService = new Mock<IBuildNotificationService>();
        _mockLogger = new Mock<ILogger<BuildQueueProcessor>>();

        _options = new BuildQueueOptions
        {
            MaxConcurrentBuilds = 2,
            PollIntervalSeconds = 1,
            BuildTimeoutMinutes = 10,
            MaxRetryAttempts = 3,
            RetryDelayMinutes = 5,
            WorkspaceDirectory = "/tmp/builds",
            OutputDirectory = "/tmp/output",
            CleanupWorkspaceAfterBuild = true,
            EnableGracefulShutdown = true,
            GracefulShutdownTimeoutSeconds = 30
        };

        // Setup ServiceProvider mock
        var services = new ServiceCollection();
        services.AddSingleton(_mockQueueManager.Object);
        services.AddSingleton(_mockNotificationService.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Constructor_ValidOptions_Initializes()
    {
        // Arrange & Act
        var processor = new BuildQueueProcessor(
            _serviceProvider,
            Options.Create(_options),
            _mockLogger.Object);

        // Assert
        processor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_InvalidOptions_ThrowsException()
    {
        // Arrange
        var invalidOptions = new BuildQueueOptions
        {
            MaxConcurrentBuilds = 0, // Invalid
            WorkspaceDirectory = "",
            OutputDirectory = ""
        };

        // Act
        var act = () => new BuildQueueProcessor(
            _serviceProvider,
            Options.Create(invalidOptions),
            _mockLogger.Object);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public async Task ExecuteAsync_NoPendingBuilds_WaitsForNextPoll()
    {
        // Arrange
        _mockQueueManager
            .Setup(x => x.GetNextBuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((BuildJob?)null);

        var processor = new BuildQueueProcessor(
            _serviceProvider,
            Options.Create(_options),
            _mockLogger.Object);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        // Act
        var executeTask = processor.StartAsync(cts.Token);
        await Task.Delay(100); // Let it run briefly
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockQueueManager.Verify(
            x => x.GetNextBuildAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_PendingBuild_ProcessesBuild()
    {
        // Arrange
        var buildJob = new BuildJob
        {
            Id = Guid.NewGuid(),
            CustomerId = "customer-123",
            ConfigurationName = "honua-server-pro",
            ManifestPath = "/tmp/manifest.json",
            CloudProvider = "aws",
            Priority = 1,
            Status = BuildJobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var callCount = 0;
        _mockQueueManager
            .Setup(x => x.GetNextBuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? buildJob : null;
            });

        _mockQueueManager
            .Setup(x => x.MarkBuildStartedAsync(buildJob.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockQueueManager
            .Setup(x => x.UpdateBuildStatusAsync(
                buildJob.Id,
                It.IsAny<BuildJobStatus>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockQueueManager
            .Setup(x => x.UpdateProgressAsync(buildJob.Id, It.IsAny<BuildProgress>()))
            .Returns(Task.CompletedTask);

        _mockNotificationService
            .Setup(x => x.SendBuildStartedAsync(It.IsAny<BuildJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockNotificationService
            .Setup(x => x.SendBuildCompletedAsync(It.IsAny<BuildJob>(), It.IsAny<BuildResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Create manifest file
        System.IO.Directory.CreateDirectory("/tmp");
        await System.IO.File.WriteAllTextAsync(
            "/tmp/manifest.json",
            "{\"id\":\"test\",\"name\":\"test-build\",\"version\":\"1.0\"}");

        var processor = new BuildQueueProcessor(
            _serviceProvider,
            Options.Create(_options),
            _mockLogger.Object);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        // Act
        var executeTask = processor.StartAsync(cts.Token);
        await Task.Delay(12000); // Wait for build to complete (simulated build takes ~11 seconds)
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockQueueManager.Verify(
            x => x.MarkBuildStartedAsync(buildJob.Id, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockNotificationService.Verify(
            x => x.SendBuildStartedAsync(It.Is<BuildJob>(j => j.Id == buildJob.Id), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify progress updates were called
        _mockQueueManager.Verify(
            x => x.UpdateProgressAsync(buildJob.Id, It.IsAny<BuildProgress>()),
            Times.AtLeastOnce);

        // Clean up
        if (System.IO.File.Exists("/tmp/manifest.json"))
        {
            System.IO.File.Delete("/tmp/manifest.json");
        }
    }

    [Fact]
    public async Task ProcessBuildAsync_ManifestNotFound_FailsBuild()
    {
        // Arrange
        var buildJob = new BuildJob
        {
            Id = Guid.NewGuid(),
            CustomerId = "customer-123",
            ConfigurationName = "test",
            ManifestPath = "/nonexistent/manifest.json",
            CloudProvider = "aws",
            Status = BuildJobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockQueueManager
            .Setup(x => x.GetNextBuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildJob);

        _mockQueueManager
            .Setup(x => x.MarkBuildStartedAsync(buildJob.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockQueueManager
            .Setup(x => x.UpdateBuildStatusAsync(
                buildJob.Id,
                It.IsAny<BuildJobStatus>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockQueueManager
            .Setup(x => x.IncrementRetryCountAsync(buildJob.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockNotificationService
            .Setup(x => x.SendBuildStartedAsync(It.IsAny<BuildJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor = new BuildQueueProcessor(
            _serviceProvider,
            Options.Create(_options),
            _mockLogger.Object);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // Act
        var executeTask = processor.StartAsync(cts.Token);
        await Task.Delay(2000);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockQueueManager.Verify(
            x => x.IncrementRetryCountAsync(buildJob.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessBuildAsync_MaxRetriesExceeded_MarksPermanentFailure()
    {
        // Arrange
        var buildJob = new BuildJob
        {
            Id = Guid.NewGuid(),
            CustomerId = "customer-123",
            ConfigurationName = "test",
            ManifestPath = "/nonexistent/manifest.json",
            CloudProvider = "aws",
            Status = BuildJobStatus.Pending,
            RetryCount = 3, // Already at max retries
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockQueueManager
            .Setup(x => x.GetNextBuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildJob);

        _mockQueueManager
            .Setup(x => x.MarkBuildStartedAsync(buildJob.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockQueueManager
            .Setup(x => x.UpdateBuildStatusAsync(
                buildJob.Id,
                BuildJobStatus.Failed,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockNotificationService
            .Setup(x => x.SendBuildStartedAsync(It.IsAny<BuildJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockNotificationService
            .Setup(x => x.SendBuildFailedAsync(It.IsAny<BuildJob>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor = new BuildQueueProcessor(
            _serviceProvider,
            Options.Create(_options),
            _mockLogger.Object);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // Act
        var executeTask = processor.StartAsync(cts.Token);
        await Task.Delay(2000);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockQueueManager.Verify(
            x => x.UpdateBuildStatusAsync(
                buildJob.Id,
                BuildJobStatus.Failed,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockNotificationService.Verify(
            x => x.SendBuildFailedAsync(It.IsAny<BuildJob>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("aws")]
    [InlineData("azure")]
    [InlineData("gcp")]
    public async Task ProcessBuildAsync_GeneratesCorrectDeploymentInstructions(string cloudProvider)
    {
        // Arrange
        var buildJob = new BuildJob
        {
            Id = Guid.NewGuid(),
            CustomerId = "customer-123",
            ConfigurationName = "test",
            ManifestPath = "/tmp/manifest-deploy.json",
            CloudProvider = cloudProvider,
            Status = BuildJobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockQueueManager
            .Setup(x => x.GetNextBuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildJob);

        _mockQueueManager
            .Setup(x => x.MarkBuildStartedAsync(buildJob.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockQueueManager
            .Setup(x => x.UpdateBuildStatusAsync(
                buildJob.Id,
                BuildJobStatus.Success,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockQueueManager
            .Setup(x => x.UpdateProgressAsync(buildJob.Id, It.IsAny<BuildProgress>()))
            .Returns(Task.CompletedTask);

        _mockNotificationService
            .Setup(x => x.SendBuildStartedAsync(It.IsAny<BuildJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        BuildResult? capturedResult = null;
        _mockNotificationService
            .Setup(x => x.SendBuildCompletedAsync(It.IsAny<BuildJob>(), It.IsAny<BuildResult>(), It.IsAny<CancellationToken>()))
            .Callback<BuildJob, BuildResult, CancellationToken>((job, result, ct) => capturedResult = result)
            .Returns(Task.CompletedTask);

        // Create manifest file
        await System.IO.File.WriteAllTextAsync(
            "/tmp/manifest-deploy.json",
            "{\"id\":\"test\",\"name\":\"test-build\",\"version\":\"1.0\"}");

        var processor = new BuildQueueProcessor(
            _serviceProvider,
            Options.Create(_options),
            _mockLogger.Object);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        // Act
        var executeTask = processor.StartAsync(cts.Token);
        await Task.Delay(12000);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        capturedResult.Should().NotBeNull();
        capturedResult!.DeploymentInstructions.Should().NotBeNullOrEmpty();

        // Verify cloud provider specific instructions
        if (cloudProvider == "aws")
        {
            capturedResult.DeploymentInstructions.Should().Contain("aws ecs");
        }
        else if (cloudProvider == "azure")
        {
            capturedResult.DeploymentInstructions.Should().Contain("az container");
        }
        else if (cloudProvider == "gcp")
        {
            capturedResult.DeploymentInstructions.Should().Contain("gcloud run");
        }

        // Clean up
        if (System.IO.File.Exists("/tmp/manifest-deploy.json"))
        {
            System.IO.File.Delete("/tmp/manifest-deploy.json");
        }
    }

    [Fact]
    public async Task StopAsync_WithActiveBuilds_WaitsForGracefulShutdown()
    {
        // Arrange
        var buildJob = new BuildJob
        {
            Id = Guid.NewGuid(),
            CustomerId = "customer-123",
            ConfigurationName = "test",
            ManifestPath = "/tmp/manifest-graceful.json",
            CloudProvider = "aws",
            Status = BuildJobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockQueueManager
            .Setup(x => x.GetNextBuildAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildJob);

        _mockQueueManager
            .Setup(x => x.MarkBuildStartedAsync(buildJob.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockQueueManager
            .Setup(x => x.UpdateBuildStatusAsync(
                buildJob.Id,
                It.IsAny<BuildJobStatus>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockQueueManager
            .Setup(x => x.UpdateProgressAsync(buildJob.Id, It.IsAny<BuildProgress>()))
            .Returns(Task.CompletedTask);

        _mockNotificationService
            .Setup(x => x.SendBuildStartedAsync(It.IsAny<BuildJob>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockNotificationService
            .Setup(x => x.SendBuildCompletedAsync(It.IsAny<BuildJob>(), It.IsAny<BuildResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Create manifest file
        await System.IO.File.WriteAllTextAsync(
            "/tmp/manifest-graceful.json",
            "{\"id\":\"test\",\"name\":\"test-build\",\"version\":\"1.0\"}");

        var processor = new BuildQueueProcessor(
            _serviceProvider,
            Options.Create(_options),
            _mockLogger.Object);

        var cts = new CancellationTokenSource();

        // Act
        await processor.StartAsync(cts.Token);
        await Task.Delay(1000); // Let build start
        await processor.StopAsync(CancellationToken.None);

        // Assert - Should complete without throwing
        // The processor should have waited for active builds to complete

        // Clean up
        if (System.IO.File.Exists("/tmp/manifest-graceful.json"))
        {
            System.IO.File.Delete("/tmp/manifest-graceful.json");
        }
    }
}

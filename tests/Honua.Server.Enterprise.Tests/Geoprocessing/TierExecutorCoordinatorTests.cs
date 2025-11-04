using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Enterprise.Geoprocessing.Executors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Geoprocessing;

public class TierExecutorCoordinatorTests
{
    private readonly Mock<INtsExecutor> _mockNtsExecutor;
    private readonly Mock<IPostGisExecutor> _mockPostGisExecutor;
    private readonly Mock<ICloudBatchExecutor> _mockCloudBatchExecutor;
    private readonly TierExecutorCoordinator _coordinator;

    public TierExecutorCoordinatorTests()
    {
        _mockNtsExecutor = new Mock<INtsExecutor>();
        _mockPostGisExecutor = new Mock<IPostGisExecutor>();
        _mockCloudBatchExecutor = new Mock<ICloudBatchExecutor>();

        _coordinator = new TierExecutorCoordinator(
            _mockNtsExecutor.Object,
            NullLogger<TierExecutorCoordinator>.Instance,
            _mockPostGisExecutor.Object,
            _mockCloudBatchExecutor.Object);
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_NTSTier_ShouldDelegateToNtsExecutor()
    {
        // Arrange
        var run = CreateProcessRun("buffer");
        var process = CreateProcessDefinition("buffer");
        var expectedResult = CreateSuccessResult(run.JobId);

        _mockNtsExecutor
            .Setup(e => e.ExecuteAsync(run, process, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _coordinator.ExecuteAsync(run, process, ProcessExecutionTier.NTS);

        // Assert
        result.Should().Be(expectedResult);
        _mockNtsExecutor.Verify(e => e.ExecuteAsync(run, process, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockPostGisExecutor.Verify(e => e.ExecuteAsync(It.IsAny<ProcessRun>(), It.IsAny<ProcessDefinition>(), It.IsAny<IProgress<ProcessProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCloudBatchExecutor.Verify(e => e.SubmitAsync(It.IsAny<ProcessRun>(), It.IsAny<ProcessDefinition>(), It.IsAny<IProgress<ProcessProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PostGISTier_ShouldDelegateToPostGisExecutor()
    {
        // Arrange
        var run = CreateProcessRun("buffer");
        var process = CreateProcessDefinition("buffer");
        var expectedResult = CreateSuccessResult(run.JobId);

        _mockPostGisExecutor
            .Setup(e => e.ExecuteAsync(run, process, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _coordinator.ExecuteAsync(run, process, ProcessExecutionTier.PostGIS);

        // Assert
        result.Should().Be(expectedResult);
        _mockPostGisExecutor.Verify(e => e.ExecuteAsync(run, process, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockNtsExecutor.Verify(e => e.ExecuteAsync(It.IsAny<ProcessRun>(), It.IsAny<ProcessDefinition>(), It.IsAny<IProgress<ProcessProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CloudBatchTier_ShouldDelegateToCloudBatchExecutor()
    {
        // Arrange
        var run = CreateProcessRun("complex-analysis");
        var process = CreateProcessDefinition("complex-analysis");
        var expectedResult = CreateSuccessResult(run.JobId);

        _mockCloudBatchExecutor
            .Setup(e => e.SubmitAsync(run, process, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _coordinator.ExecuteAsync(run, process, ProcessExecutionTier.CloudBatch);

        // Assert
        result.Should().Be(expectedResult);
        _mockCloudBatchExecutor.Verify(e => e.SubmitAsync(run, process, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockNtsExecutor.Verify(e => e.ExecuteAsync(It.IsAny<ProcessRun>(), It.IsAny<ProcessDefinition>(), It.IsAny<IProgress<ProcessProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PostGISTierNotConfigured_ShouldThrowException()
    {
        // Arrange - Create coordinator without PostGIS executor
        var coordinatorWithoutPostGis = new TierExecutorCoordinator(
            _mockNtsExecutor.Object,
            NullLogger<TierExecutorCoordinator>.Instance);

        var run = CreateProcessRun("buffer");
        var process = CreateProcessDefinition("buffer");

        // Act
        Func<Task> act = async () => await coordinatorWithoutPostGis.ExecuteAsync(run, process, ProcessExecutionTier.PostGIS);

        // Assert - TierUnavailableException gets wrapped in TierExecutionException
        var exception = await act.Should().ThrowAsync<TierExecutionException>();
        exception.Which.Tier.Should().Be(ProcessExecutionTier.PostGIS);
        exception.Which.InnerException.Should().BeOfType<TierUnavailableException>();
    }

    [Fact]
    public async Task ExecuteAsync_CloudBatchTierNotConfigured_ShouldThrowException()
    {
        // Arrange - Create coordinator without CloudBatch executor
        var coordinatorWithoutCloudBatch = new TierExecutorCoordinator(
            _mockNtsExecutor.Object,
            NullLogger<TierExecutorCoordinator>.Instance,
            _mockPostGisExecutor.Object);

        var run = CreateProcessRun("complex-analysis");
        var process = CreateProcessDefinition("complex-analysis");

        // Act
        Func<Task> act = async () => await coordinatorWithoutCloudBatch.ExecuteAsync(run, process, ProcessExecutionTier.CloudBatch);

        // Assert - TierUnavailableException gets wrapped in TierExecutionException
        var exception = await act.Should().ThrowAsync<TierExecutionException>();
        exception.Which.Tier.Should().Be(ProcessExecutionTier.CloudBatch);
        exception.Which.InnerException.Should().BeOfType<TierUnavailableException>();
    }

    [Fact]
    public async Task ExecuteAsync_ExecutorThrowsException_ShouldWrapInTierExecutionException()
    {
        // Arrange
        var run = CreateProcessRun("buffer");
        var process = CreateProcessDefinition("buffer");
        var originalException = new InvalidOperationException("Database connection failed");

        _mockNtsExecutor
            .Setup(e => e.ExecuteAsync(run, process, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(originalException);

        // Act
        Func<Task> act = async () => await _coordinator.ExecuteAsync(run, process, ProcessExecutionTier.NTS);

        // Assert
        await act.Should().ThrowAsync<TierExecutionException>()
            .WithMessage("*Execution failed on tier NTS*")
            .Where(e => e.Tier == ProcessExecutionTier.NTS)
            .Where(e => e.ProcessId == "buffer")
            .Where(e => e.InnerException == originalException);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutorThrowsTierExecutionException_ShouldNotWrap()
    {
        // Arrange
        var run = CreateProcessRun("buffer");
        var process = CreateProcessDefinition("buffer");
        var tierException = new TierExecutionException(ProcessExecutionTier.NTS, "buffer", "Specific tier error");

        _mockNtsExecutor
            .Setup(e => e.ExecuteAsync(run, process, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(tierException);

        // Act
        Func<Task> act = async () => await _coordinator.ExecuteAsync(run, process, ProcessExecutionTier.NTS);

        // Assert
        var exception = await act.Should().ThrowAsync<TierExecutionException>();
        exception.Which.Should().BeSameAs(tierException); // Same instance, not wrapped
    }

    [Fact]
    public async Task ExecuteAsync_WithProgressReporting_ShouldPassThroughToExecutor()
    {
        // Arrange
        var run = CreateProcessRun("buffer");
        var process = CreateProcessDefinition("buffer");
        var expectedResult = CreateSuccessResult(run.JobId);
        var progressReports = new List<ProcessProgress>();
        var progress = new Progress<ProcessProgress>(p => progressReports.Add(p));

        _mockNtsExecutor
            .Setup(e => e.ExecuteAsync(run, process, It.IsAny<IProgress<ProcessProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _coordinator.ExecuteAsync(run, process, ProcessExecutionTier.NTS, progress);

        // Assert
        result.Should().Be(expectedResult);
        _mockNtsExecutor.Verify(e => e.ExecuteAsync(run, process, progress, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SelectTierAsync Tests

    [Fact]
    public async Task SelectTierAsync_PreferredTierSpecified_ShouldUsePreferredTier()
    {
        // Arrange
        var process = CreateProcessDefinition("buffer",
            supportedTiers: new List<ProcessExecutionTier> { ProcessExecutionTier.NTS, ProcessExecutionTier.PostGIS });
        var request = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PreferredTier = ProcessExecutionTier.PostGIS,
            Inputs = new Dictionary<string, object>()
        };

        // Act
        var selectedTier = await _coordinator.SelectTierAsync(process, request);

        // Assert
        selectedTier.Should().Be(ProcessExecutionTier.PostGIS);
    }

    [Fact]
    public async Task SelectTierAsync_PreferredTierUnavailable_ShouldFallbackToAutoSelection()
    {
        // Arrange - Coordinator without CloudBatch
        var coordinatorWithoutCloudBatch = new TierExecutorCoordinator(
            _mockNtsExecutor.Object,
            NullLogger<TierExecutorCoordinator>.Instance,
            _mockPostGisExecutor.Object);

        var process = CreateProcessDefinition("buffer",
            supportedTiers: new List<ProcessExecutionTier> { ProcessExecutionTier.NTS, ProcessExecutionTier.CloudBatch });
        var request = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PreferredTier = ProcessExecutionTier.CloudBatch, // Unavailable
            Inputs = new Dictionary<string, object>()
        };

        _mockNtsExecutor
            .Setup(e => e.CanExecuteAsync(process, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var selectedTier = await coordinatorWithoutCloudBatch.SelectTierAsync(process, request);

        // Assert
        selectedTier.Should().Be(ProcessExecutionTier.NTS); // Falls back to available tier
    }

    [Fact]
    public async Task SelectTierAsync_NtsCanExecute_ShouldSelectNts()
    {
        // Arrange
        var process = CreateProcessDefinition("buffer",
            supportedTiers: new List<ProcessExecutionTier> { ProcessExecutionTier.NTS, ProcessExecutionTier.PostGIS });
        var request = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        _mockNtsExecutor
            .Setup(e => e.CanExecuteAsync(process, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var selectedTier = await _coordinator.SelectTierAsync(process, request);

        // Assert
        selectedTier.Should().Be(ProcessExecutionTier.NTS);
    }

    [Fact]
    public async Task SelectTierAsync_NtsCannotExecuteButPostGisCan_ShouldSelectPostGis()
    {
        // Arrange
        var process = CreateProcessDefinition("spatial-join",
            supportedTiers: new List<ProcessExecutionTier> { ProcessExecutionTier.NTS, ProcessExecutionTier.PostGIS });
        var request = new ProcessExecutionRequest
        {
            ProcessId = "spatial-join",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        _mockNtsExecutor
            .Setup(e => e.CanExecuteAsync(process, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockPostGisExecutor
            .Setup(e => e.CanExecuteAsync(process, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var selectedTier = await _coordinator.SelectTierAsync(process, request);

        // Assert
        selectedTier.Should().Be(ProcessExecutionTier.PostGIS);
    }

    [Fact]
    public async Task SelectTierAsync_OnlyCloudBatchSupported_ShouldSelectCloudBatch()
    {
        // Arrange
        var process = CreateProcessDefinition("gpu-intensive-analysis",
            supportedTiers: new List<ProcessExecutionTier> { ProcessExecutionTier.CloudBatch });
        var request = new ProcessExecutionRequest
        {
            ProcessId = "gpu-intensive-analysis",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        // Act
        var selectedTier = await _coordinator.SelectTierAsync(process, request);

        // Assert
        selectedTier.Should().Be(ProcessExecutionTier.CloudBatch);
    }

    [Fact]
    public async Task SelectTierAsync_NoTiersCanExecute_ShouldDefaultToNts()
    {
        // Arrange
        var process = CreateProcessDefinition("buffer",
            supportedTiers: new List<ProcessExecutionTier> { ProcessExecutionTier.NTS });
        var request = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        _mockNtsExecutor
            .Setup(e => e.CanExecuteAsync(process, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var selectedTier = await _coordinator.SelectTierAsync(process, request);

        // Assert
        selectedTier.Should().Be(ProcessExecutionTier.NTS); // Default fallback
    }

    [Fact]
    public async Task SelectTierAsync_PostGisNotConfigured_ShouldSkipToCloudBatch()
    {
        // Arrange - Coordinator without PostGIS
        var coordinatorWithoutPostGis = new TierExecutorCoordinator(
            _mockNtsExecutor.Object,
            NullLogger<TierExecutorCoordinator>.Instance,
            postGisExecutor: null,
            cloudBatchExecutor: _mockCloudBatchExecutor.Object);

        var process = CreateProcessDefinition("complex-operation",
            supportedTiers: new List<ProcessExecutionTier> { ProcessExecutionTier.NTS, ProcessExecutionTier.PostGIS, ProcessExecutionTier.CloudBatch });
        var request = new ProcessExecutionRequest
        {
            ProcessId = "complex-operation",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        _mockNtsExecutor
            .Setup(e => e.CanExecuteAsync(process, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var selectedTier = await coordinatorWithoutPostGis.SelectTierAsync(process, request);

        // Assert
        selectedTier.Should().Be(ProcessExecutionTier.CloudBatch); // Skips unavailable PostGIS
    }

    #endregion

    #region IsTierAvailableAsync Tests

    [Fact]
    public async Task IsTierAvailableAsync_NtsTier_ShouldAlwaysReturnTrue()
    {
        // Act
        var available = await _coordinator.IsTierAvailableAsync(ProcessExecutionTier.NTS);

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsTierAvailableAsync_PostGisTierConfigured_ShouldReturnTrue()
    {
        // Act
        var available = await _coordinator.IsTierAvailableAsync(ProcessExecutionTier.PostGIS);

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsTierAvailableAsync_PostGisTierNotConfigured_ShouldReturnFalse()
    {
        // Arrange - Coordinator without PostGIS
        var coordinatorWithoutPostGis = new TierExecutorCoordinator(
            _mockNtsExecutor.Object,
            NullLogger<TierExecutorCoordinator>.Instance);

        // Act
        var available = await coordinatorWithoutPostGis.IsTierAvailableAsync(ProcessExecutionTier.PostGIS);

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public async Task IsTierAvailableAsync_CloudBatchTierConfigured_ShouldReturnTrue()
    {
        // Act
        var available = await _coordinator.IsTierAvailableAsync(ProcessExecutionTier.CloudBatch);

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsTierAvailableAsync_CloudBatchTierNotConfigured_ShouldReturnFalse()
    {
        // Arrange - Coordinator without CloudBatch
        var coordinatorWithoutCloudBatch = new TierExecutorCoordinator(
            _mockNtsExecutor.Object,
            NullLogger<TierExecutorCoordinator>.Instance,
            _mockPostGisExecutor.Object);

        // Act
        var available = await coordinatorWithoutCloudBatch.IsTierAvailableAsync(ProcessExecutionTier.CloudBatch);

        // Assert
        available.Should().BeFalse();
    }

    #endregion

    #region GetTierStatusAsync Tests

    [Fact]
    public async Task GetTierStatusAsync_NtsTier_ShouldReturnAvailableStatus()
    {
        // Act
        var status = await _coordinator.GetTierStatusAsync(ProcessExecutionTier.NTS);

        // Assert
        status.Should().NotBeNull();
        status.Tier.Should().Be(ProcessExecutionTier.NTS);
        status.Available.Should().BeTrue();
        status.HealthMessage.Should().Be("OK");
        status.LastCheckAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetTierStatusAsync_PostGisTierConfigured_ShouldReturnAvailableStatus()
    {
        // Act
        var status = await _coordinator.GetTierStatusAsync(ProcessExecutionTier.PostGIS);

        // Assert
        status.Should().NotBeNull();
        status.Tier.Should().Be(ProcessExecutionTier.PostGIS);
        status.Available.Should().BeTrue();
    }

    [Fact]
    public async Task GetTierStatusAsync_PostGisTierNotConfigured_ShouldReturnUnavailableStatus()
    {
        // Arrange - Coordinator without PostGIS
        var coordinatorWithoutPostGis = new TierExecutorCoordinator(
            _mockNtsExecutor.Object,
            NullLogger<TierExecutorCoordinator>.Instance);

        // Act
        var status = await coordinatorWithoutPostGis.GetTierStatusAsync(ProcessExecutionTier.PostGIS);

        // Assert
        status.Should().NotBeNull();
        status.Tier.Should().Be(ProcessExecutionTier.PostGIS);
        status.Available.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private ProcessRun CreateProcessRun(string processId)
    {
        return new ProcessRun
        {
            JobId = $"job-test-{Guid.NewGuid():N}",
            ProcessId = processId,
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ProcessRunStatus.Running,
            Inputs = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private ProcessDefinition CreateProcessDefinition(string processId, List<ProcessExecutionTier>? supportedTiers = null)
    {
        return new ProcessDefinition
        {
            Id = processId,
            Title = processId,
            Description = $"Test {processId} operation",
            Version = "1.0.0",
            Category = "test",
            Keywords = new List<string>(),
            Inputs = new List<ProcessParameter>(),
            OutputFormats = new List<string> { "geojson" },
            ExecutionConfig = new ProcessExecutionConfig
            {
                SupportedTiers = supportedTiers ?? new List<ProcessExecutionTier>
                {
                    ProcessExecutionTier.NTS,
                    ProcessExecutionTier.PostGIS,
                    ProcessExecutionTier.CloudBatch
                },
                DefaultTimeoutSeconds = 300
            },
            Enabled = true
        };
    }

    private ProcessResult CreateSuccessResult(string jobId)
    {
        return new ProcessResult
        {
            JobId = jobId,
            ProcessId = "test-process",
            Status = ProcessRunStatus.Completed,
            Success = true,
            Output = new Dictionary<string, object>
            {
                ["result"] = "success"
            },
            DurationMs = 100
        };
    }

    #endregion
}

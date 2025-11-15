// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Coordination;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Coordination;

[Trait("Category", "Unit")]
public class LeaderElectionServiceTests : IDisposable
{
    private readonly Mock<ILeaderElection> _mockLeaderElection;
    private readonly Mock<ILogger<LeaderElectionService>> _mockLogger;
    private readonly LeaderElectionOptions _options;
    private readonly LeaderElectionService _service;

    public LeaderElectionServiceTests()
    {
        _mockLeaderElection = new Mock<ILeaderElection>();
        _mockLogger = new Mock<ILogger<LeaderElectionService>>();

        _options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 10,
            ResourceName = "test-resource",
            KeyPrefix = "honua:leader:",
            EnableDetailedLogging = true
        };

        _mockLeaderElection.Setup(le => le.InstanceId)
            .Returns("test-instance-123");

        _service = new LeaderElectionService(
            _mockLeaderElection.Object,
            _mockLogger.Object,
            Options.Create(_options));
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLeaderElection_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LeaderElectionService(
            null!,
            _mockLogger.Object,
            Options.Create(_options));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("leaderElection");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LeaderElectionService(
            _mockLeaderElection.Object,
            null!,
            Options.Create(_options));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new LeaderElectionService(
            _mockLeaderElection.Object,
            _mockLogger.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidOptions = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 10,
            RenewalIntervalSeconds = 15 // Invalid: greater than lease duration
        };

        // Act
        var act = () => new LeaderElectionService(
            _mockLeaderElection.Object,
            _mockLogger.Object,
            Options.Create(invalidOptions));

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsLeader_InitiallyFalse()
    {
        // Assert
        _service.IsLeader.Should().BeFalse();
    }

    [Fact]
    public void InstanceId_ReturnsLeaderElectionInstanceId()
    {
        // Assert
        _service.InstanceId.Should().Be("test-instance-123");
    }

    #endregion

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_AttemptsToAcquireLeadership()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Start the service and give it a moment to execute
        var startTask = _service.StartAsync(cts.Token);

        // Wait briefly for acquisition
        await Task.Delay(100);

        // Stop the service
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _mockLeaderElection.Verify(
            le => le.TryAcquireLeadershipAsync("test-resource", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StartAsync_WhenLeadershipAcquired_SetsIsLeaderTrue()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var startTask = _service.StartAsync(cts.Token);

        // Wait for acquisition
        await Task.Delay(100);

        var isLeader = _service.IsLeader;

        // Cleanup
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        isLeader.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenLeadershipNotAcquired_RetriesUntilSuccessful()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var attemptCount = 0;

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attemptCount++;
                return attemptCount >= 3; // Succeed on third attempt
            });

        // Act
        var startTask = _service.StartAsync(cts.Token);

        // Wait for multiple acquisition attempts (renewal interval is 10s in tests, but runs immediately in reality)
        await Task.Delay(500);

        // Cleanup
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        attemptCount.Should().BeGreaterOrEqualTo(3);
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_RenewsLeadershipPeriodically()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.RenewLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var startTask = _service.StartAsync(cts.Token);

        // Wait for acquisition and at least one renewal
        await Task.Delay(200);

        // Cleanup
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _mockLeaderElection.Verify(
            le => le.RenewLeadershipAsync("test-resource", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRenewalSucceeds_MaintainsLeadership()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.RenewLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var startTask = _service.StartAsync(cts.Token);

        // Wait for acquisition and renewals
        await Task.Delay(200);

        var isLeaderDuringRenewal = _service.IsLeader;

        // Cleanup
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        isLeaderDuringRenewal.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRenewalFails_SetsIsLeaderFalse()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var renewalCount = 0;
        _mockLeaderElection.Setup(le => le.RenewLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                renewalCount++;
                return renewalCount == 1; // First renewal succeeds, second fails
            });

        // Act
        var startTask = _service.StartAsync(cts.Token);

        // Wait for acquisition and failed renewal
        await Task.Delay(300);

        var isLeaderAfterFailedRenewal = _service.IsLeader;

        // Cleanup
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        isLeaderAfterFailedRenewal.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRenewalFails_AttemptsReacquisition()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var acquisitionCount = 0;
        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                acquisitionCount++;
                return true;
            });

        _mockLeaderElection.Setup(le => le.RenewLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Renewal always fails

        // Act
        var startTask = _service.StartAsync(cts.Token);

        // Wait for initial acquisition, failed renewal, and reacquisition
        await Task.Delay(500);

        // Cleanup
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - Should have initial acquisition + reacquisition attempt
        acquisitionCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRenewalThrowsException_AttemptsReacquisition()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var acquisitionCount = 0;
        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                acquisitionCount++;
                return true;
            });

        _mockLeaderElection.Setup(le => le.RenewLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Renewal error"));

        // Act
        var startTask = _service.StartAsync(cts.Token);

        // Wait for error and reacquisition
        await Task.Delay(500);

        // Cleanup
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        acquisitionCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_StopsGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.RenewLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var startTask = _service.StartAsync(cts.Token);
        await Task.Delay(100);

        cts.Cancel();

        // Should complete without throwing
        var act = async () => await _service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_WhenLeader_ReleasesLeadership()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.ReleaseLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var startTask = _service.StartAsync(cts.Token);
        await Task.Delay(100); // Wait for leadership acquisition

        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _mockLeaderElection.Verify(
            le => le.ReleaseLeadershipAsync("test-resource", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_WhenLeadershipReleased_SetsIsLeaderFalse()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.ReleaseLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var startTask = _service.StartAsync(cts.Token);
        await Task.Delay(100);

        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _service.IsLeader.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenNotLeader_DoesNotReleaseLeadership()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Never becomes leader

        // Act
        var startTask = _service.StartAsync(cts.Token);
        await Task.Delay(100);

        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert
        _mockLeaderElection.Verify(
            le => le.ReleaseLeadershipAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StopAsync_WhenReleaseLeadershipFails_DoesNotThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.ReleaseLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Release fails

        // Act
        var startTask = _service.StartAsync(cts.Token);
        await Task.Delay(100);

        cts.Cancel();
        var act = async () => await _service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_WhenReleaseLeadershipThrowsException_DoesNotThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.ReleaseLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Release error"));

        // Act
        var startTask = _service.StartAsync(cts.Token);
        await Task.Delay(100);

        cts.Cancel();
        var act = async () => await _service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task IsLeader_IsThreadSafe()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.RenewLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var startTask = _service.StartAsync(cts.Token);
        await Task.Delay(100);

        // Read IsLeader from multiple threads
        var readTasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var _ = _service.IsLeader;
                }
            }))
            .ToArray();

        await Task.WhenAll(readTasks);

        // Cleanup
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - Should not throw
        // The test passes if no exceptions were thrown
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public async Task FullLifecycle_AcquireRenewRelease()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.RenewLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockLeaderElection.Setup(le => le.ReleaseLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var startTask = _service.StartAsync(cts.Token);
        await Task.Delay(100); // Acquisition

        var isLeaderDuringOperation = _service.IsLeader;

        await Task.Delay(200); // Renewals

        cts.Cancel();
        await _service.StopAsync(CancellationToken.None); // Release

        var isLeaderAfterStop = _service.IsLeader;

        // Assert
        isLeaderDuringOperation.Should().BeTrue();
        isLeaderAfterStop.Should().BeFalse();

        _mockLeaderElection.Verify(
            le => le.TryAcquireLeadershipAsync("test-resource", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _mockLeaderElection.Verify(
            le => le.RenewLeadershipAsync("test-resource", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        _mockLeaderElection.Verify(
            le => le.ReleaseLeadershipAsync("test-resource", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LeadershipLoss_TriggersReacquisition()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var renewalCount = 0;
        _mockLeaderElection.Setup(le => le.RenewLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                renewalCount++;
                // Fail on second renewal to simulate leadership loss
                return renewalCount != 2;
            });

        // Act
        var startTask = _service.StartAsync(cts.Token);

        // Wait for leadership loss and reacquisition
        await Task.Delay(500);

        // Cleanup
        cts.Cancel();
        await _service.StopAsync(CancellationToken.None);

        // Assert - Should have tried to acquire at least twice (initial + reacquisition)
        _mockLeaderElection.Verify(
            le => le.TryAcquireLeadershipAsync("test-resource", It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task CancellationDuringAcquisition_StopsGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _mockLeaderElection.Setup(le => le.TryAcquireLeadershipAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Never acquires leadership

        // Act
        var startTask = _service.StartAsync(cts.Token);
        await Task.Delay(50);

        cts.Cancel();

        // Should complete without throwing
        var act = async () => await _service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _service.IsLeader.Should().BeFalse();
    }

    #endregion
}

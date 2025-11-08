// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Progress;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL;

/// <summary>
/// Tests for SignalRWorkflowProgressBroadcaster
/// </summary>
public class SignalRWorkflowProgressBroadcasterTests
{
    private readonly Mock<IHubContext<GeoEtlProgressHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<ILogger<SignalRWorkflowProgressBroadcaster>> _mockLogger;
    private readonly SignalRWorkflowProgressBroadcaster _broadcaster;

    public SignalRWorkflowProgressBroadcasterTests()
    {
        _mockHubContext = new Mock<IHubContext<GeoEtlProgressHub>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockLogger = new Mock<ILogger<SignalRWorkflowProgressBroadcaster>>();

        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

        _broadcaster = new SignalRWorkflowProgressBroadcaster(_mockHubContext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task BroadcastWorkflowStartedAsync_ShouldSendToCorrectGroups()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var metadata = new WorkflowStartedMetadata
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "Test Workflow",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TotalNodes = 5,
            StartedAt = DateTimeOffset.UtcNow,
            Parameters = new Dictionary<string, object> { { "param1", "value1" } }
        };

        // Act
        await _broadcaster.BroadcastWorkflowStartedAsync(runId, metadata);

        // Assert
        _mockClients.Verify(
            c => c.Group($"workflow:{runId}"),
            Times.Once);

        _mockClients.Verify(
            c => c.Group("all-workflows"),
            Times.Once);

        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "WorkflowStarted",
                It.IsAny<object[]>(),
                default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastNodeStartedAsync_ShouldSendToCorrectGroups()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var nodeId = "node-1";
        var nodeName = "Data Source";
        var nodeType = "data_source.postgis";

        // Act
        await _broadcaster.BroadcastNodeStartedAsync(runId, nodeId, nodeName, nodeType);

        // Assert
        _mockClients.Verify(
            c => c.Group($"workflow:{runId}"),
            Times.Once);

        _mockClients.Verify(
            c => c.Group("all-workflows"),
            Times.Once);

        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "NodeStarted",
                It.IsAny<object[]>(),
                default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastNodeProgressAsync_ShouldSendProgressUpdates()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var nodeId = "node-1";
        var percent = 50;
        var message = "Processing features...";
        var featuresProcessed = 1000L;
        var totalFeatures = 2000L;

        // Act
        await _broadcaster.BroadcastNodeProgressAsync(runId, nodeId, percent, message, featuresProcessed, totalFeatures);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "NodeProgress",
                It.Is<object[]>(args =>
                    args.Length == 1 &&
                    args[0] != null),
                default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastNodeCompletedAsync_ShouldSendCompletionData()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var nodeId = "node-1";
        var result = new NodeCompletedResult
        {
            DurationMs = 5000,
            FeaturesProcessed = 2000,
            BytesRead = 1024 * 1024,
            BytesWritten = 512 * 1024,
            Metadata = new Dictionary<string, object> { { "key", "value" } }
        };

        // Act
        await _broadcaster.BroadcastNodeCompletedAsync(runId, nodeId, result);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "NodeCompleted",
                It.IsAny<object[]>(),
                default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastNodeFailedAsync_ShouldSendErrorInformation()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var nodeId = "node-1";
        var error = "Connection to database failed";

        // Act
        await _broadcaster.BroadcastNodeFailedAsync(runId, nodeId, error);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "NodeFailed",
                It.IsAny<object[]>(),
                default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastWorkflowCompletedAsync_ShouldSendSummary()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var summary = new WorkflowCompletedSummary
        {
            CompletedAt = DateTimeOffset.UtcNow,
            TotalDurationMs = 30000,
            NodesCompleted = 5,
            TotalNodes = 5,
            TotalFeaturesProcessed = 10000,
            TotalBytesRead = 5 * 1024 * 1024,
            TotalBytesWritten = 2 * 1024 * 1024
        };

        // Act
        await _broadcaster.BroadcastWorkflowCompletedAsync(runId, summary);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "WorkflowCompleted",
                It.IsAny<object[]>(),
                default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastWorkflowFailedAsync_ShouldSendErrorInformation()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var error = "Workflow validation failed";

        // Act
        await _broadcaster.BroadcastWorkflowFailedAsync(runId, error);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "WorkflowFailed",
                It.IsAny<object[]>(),
                default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastWorkflowCancelledAsync_ShouldSendCancellationReason()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var reason = "Cancelled by user";

        // Act
        await _broadcaster.BroadcastWorkflowCancelledAsync(runId, reason);

        // Assert
        _mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "WorkflowCancelled",
                It.IsAny<object[]>(),
                default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastWorkflowStartedAsync_ShouldLogInformation()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var metadata = new WorkflowStartedMetadata
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "Test Workflow",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TotalNodes = 5,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Act
        await _broadcaster.BroadcastWorkflowStartedAsync(runId, metadata);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastNodeProgressAsync_ShouldHandleExceptions()
    {
        // Arrange
        _mockClientProxy
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                default))
            .ThrowsAsync(new Exception("SignalR error"));

        var runId = Guid.NewGuid();
        var nodeId = "node-1";

        // Act - should not throw
        await _broadcaster.BroadcastNodeProgressAsync(runId, nodeId, 50, "test");

        // Assert - error should be logged
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

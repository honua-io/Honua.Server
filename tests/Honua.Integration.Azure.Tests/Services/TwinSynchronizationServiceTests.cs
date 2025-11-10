// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure;
using Azure.DigitalTwins.Core;
using FluentAssertions;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.Models;
using Honua.Integration.Azure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Integration.Azure.Tests.Services;

public class TwinSynchronizationServiceTests
{
    private readonly Mock<IAzureDigitalTwinsClient> _mockAdtClient;
    private readonly Mock<IDtdlModelMapper> _mockModelMapper;
    private readonly Mock<ILogger<TwinSynchronizationService>> _mockLogger;
    private readonly AzureDigitalTwinsOptions _options;
    private readonly TwinSynchronizationService _service;

    public TwinSynchronizationServiceTests()
    {
        _mockAdtClient = new Mock<IAzureDigitalTwinsClient>();
        _mockModelMapper = new Mock<IDtdlModelMapper>();
        _mockLogger = new Mock<ILogger<TwinSynchronizationService>>();

        _options = new AzureDigitalTwinsOptions
        {
            InstanceUrl = "https://test.api.wus2.digitaltwins.azure.net",
            DefaultNamespace = "dtmi:com:honua",
            LayerMappings = new List<LayerModelMapping>
            {
                new()
                {
                    ServiceId = "smart-city",
                    LayerId = "sensors",
                    ModelId = "dtmi:com:honua:sensor;1",
                    TwinIdTemplate = "{serviceId}-{layerId}-{featureId}",
                    PropertyMappings = new Dictionary<string, string>()
                }
            },
            Sync = new SyncOptions
            {
                ConflictStrategy = ConflictResolution.LastWriteWins,
                SyncDeletions = true,
                SyncRelationships = true
            }
        };

        _service = new TwinSynchronizationService(
            _mockAdtClient.Object,
            _mockModelMapper.Object,
            _mockLogger.Object,
            Options.Create(_options));
    }

    [Fact]
    public async Task SyncFeatureToTwinAsync_ShouldCreateNewTwin_WhenTwinDoesNotExist()
    {
        // Arrange
        var serviceId = "smart-city";
        var layerId = "sensors";
        var featureId = "SENSOR-001";
        var attributes = new Dictionary<string, object?>
        {
            ["temperature"] = 25.5,
            ["is_active"] = true
        };

        var twinProperties = new Dictionary<string, object>
        {
            ["temperature"] = 25.5,
            ["is_active"] = true
        };

        _mockModelMapper
            .Setup(m => m.MapFeatureToTwinProperties(attributes, It.IsAny<LayerModelMapping>()))
            .Returns(twinProperties);

        _mockAdtClient
            .Setup(c => c.GetDigitalTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        _mockAdtClient
            .Setup(c => c.CreateOrReplaceDigitalTwinAsync(
                It.IsAny<string>(),
                It.IsAny<BasicDigitalTwin>(),
                It.IsAny<ETag?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new BasicDigitalTwin(), Mock.Of<Response>()));

        // Act
        var result = await _service.SyncFeatureToTwinAsync(serviceId, layerId, featureId, attributes);

        // Assert
        result.Success.Should().BeTrue();
        result.Operation.Should().Be(SyncOperationType.Created);
        result.TwinId.Should().Be("smart-city-sensors-SENSOR-001");

        _mockAdtClient.Verify(
            c => c.CreateOrReplaceDigitalTwinAsync(
                "smart-city-sensors-SENSOR-001",
                It.Is<BasicDigitalTwin>(t =>
                    t.Id == "smart-city-sensors-SENSOR-001" &&
                    t.Metadata.ModelId == "dtmi:com:honua:sensor;1"),
                It.IsAny<ETag?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncFeatureToTwinAsync_ShouldReturnSkipped_WhenNoMappingExists()
    {
        // Arrange
        var serviceId = "unknown";
        var layerId = "unknown";
        var featureId = "FEATURE-001";
        var attributes = new Dictionary<string, object?>();

        // Act
        var result = await _service.SyncFeatureToTwinAsync(serviceId, layerId, featureId, attributes);

        // Assert
        result.Success.Should().BeFalse();
        result.Operation.Should().Be(SyncOperationType.Skipped);
        result.ErrorMessage.Should().Contain("No layer mapping configured");

        _mockAdtClient.Verify(
            c => c.CreateOrReplaceDigitalTwinAsync(
                It.IsAny<string>(),
                It.IsAny<BasicDigitalTwin>(),
                It.IsAny<ETag?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteTwinAsync_ShouldDeleteTwinSuccessfully()
    {
        // Arrange
        var serviceId = "smart-city";
        var layerId = "sensors";
        var featureId = "SENSOR-001";
        var twinId = "smart-city-sensors-SENSOR-001";

        var mockRelationships = new List<BasicRelationship>
        {
            new() { Id = "rel-1", SourceId = twinId, TargetId = "target-1", Name = "relatedTo" }
        };

        _mockAdtClient
            .Setup(c => c.GetRelationshipsAsync(twinId, null, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncPageable(mockRelationships));

        _mockAdtClient
            .Setup(c => c.DeleteRelationshipAsync(
                twinId,
                It.IsAny<string>(),
                It.IsAny<ETag?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        _mockAdtClient
            .Setup(c => c.DeleteDigitalTwinAsync(
                twinId,
                It.IsAny<ETag?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        // Act
        var result = await _service.DeleteTwinAsync(serviceId, layerId, featureId);

        // Assert
        result.Success.Should().BeTrue();
        result.Operation.Should().Be(SyncOperationType.Deleted);
        result.TwinId.Should().Be(twinId);

        _mockAdtClient.Verify(
            c => c.DeleteRelationshipAsync(twinId, "rel-1", It.IsAny<ETag?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockAdtClient.Verify(
            c => c.DeleteDigitalTwinAsync(twinId, It.IsAny<ETag?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncFeaturesToTwinsAsync_ShouldProcessBatchSuccessfully()
    {
        // Arrange
        var serviceId = "smart-city";
        var layerId = "sensors";
        var features = new List<(string featureId, Dictionary<string, object?> attributes)>
        {
            ("SENSOR-001", new Dictionary<string, object?> { ["temp"] = 20.0 }),
            ("SENSOR-002", new Dictionary<string, object?> { ["temp"] = 21.0 }),
            ("SENSOR-003", new Dictionary<string, object?> { ["temp"] = 22.0 })
        };

        _mockModelMapper
            .Setup(m => m.MapFeatureToTwinProperties(It.IsAny<Dictionary<string, object?>>(), It.IsAny<LayerModelMapping>()))
            .Returns(new Dictionary<string, object>());

        _mockAdtClient
            .Setup(c => c.GetDigitalTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        _mockAdtClient
            .Setup(c => c.CreateOrReplaceDigitalTwinAsync(
                It.IsAny<string>(),
                It.IsAny<BasicDigitalTwin>(),
                It.IsAny<ETag?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new BasicDigitalTwin(), Mock.Of<Response>()));

        // Act
        var stats = await _service.SyncFeaturesToTwinsAsync(serviceId, layerId, features);

        // Assert
        stats.TotalProcessed.Should().Be(3);
        stats.Succeeded.Should().Be(3);
        stats.Failed.Should().Be(0);
        stats.OperationBreakdown.Should().ContainKey(SyncOperationType.Created);
        stats.OperationBreakdown[SyncOperationType.Created].Should().Be(3);
    }

    private static AsyncPageable<T> CreateAsyncPageable<T>(IEnumerable<T> items)
    {
        return new MockAsyncPageable<T>(items);
    }

    private class MockAsyncPageable<T> : AsyncPageable<T>
    {
        private readonly IEnumerable<T> _items;

        public MockAsyncPageable(IEnumerable<T> items)
        {
            _items = items;
        }

        public override async IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
        {
            await Task.CompletedTask;
            yield return new MockPage<T>(_items);
        }
    }

    private class MockPage<T> : Page<T>
    {
        private readonly IEnumerable<T> _items;

        public MockPage(IEnumerable<T> items)
        {
            _items = items;
        }

        public override IReadOnlyList<T> Values => _items.ToList();
        public override string? ContinuationToken => null;
        public override Response GetRawResponse() => Mock.Of<Response>();
    }
}

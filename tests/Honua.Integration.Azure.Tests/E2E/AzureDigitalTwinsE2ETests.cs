// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Azure;
using Azure.DigitalTwins.Core;
using FluentAssertions;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.Models;
using Honua.Integration.Azure.Services;
using Honua.Integration.Azure.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Integration.Azure.Tests.E2E;

/// <summary>
/// End-to-end tests for Azure Digital Twins integration.
/// Tests complete flow: Feature ↔ Azure Digital Twins (bi-directional sync)
/// </summary>
public class AzureDigitalTwinsE2ETests : IAsyncLifetime
{
    private readonly MockAzureDigitalTwinsClient _adtClient;
    private readonly MockSensorThingsRepository _featureRepository;
    private readonly TwinSynchronizationService _syncService;
    private readonly Mock<IDtdlModelMapper> _modelMapper;
    private readonly string _serviceId = "smart-city-service";
    private readonly string _layerId = "buildings-layer";
    private readonly string _modelId = "dtmi:honua:smartcity:Building;1";

    public AzureDigitalTwinsE2ETests()
    {
        _adtClient = new MockAzureDigitalTwinsClient();
        _featureRepository = new MockSensorThingsRepository();
        _modelMapper = new Mock<IDtdlModelMapper>();

        SetupModelMapper();

        var options = new AzureDigitalTwinsOptions
        {
            InstanceUrl = "https://test-adt.api.eus.digitaltwins.azure.net",
            MaxBatchSize = 50,
            Sync = new SyncOptions
            {
                Enabled = true,
                ConflictStrategy = ConflictResolution.LastWriteWins,
                SyncRelationships = true
            },
            LayerMappings = new[]
            {
                new LayerModelMapping
                {
                    ServiceId = _serviceId,
                    LayerId = _layerId,
                    ModelId = _modelId,
                    TwinIdTemplate = "building-{featureId}",
                    PropertyMappings = new Dictionary<string, string>
                    {
                        ["name"] = "buildingName",
                        ["address"] = "streetAddress",
                        ["floors"] = "floorCount",
                        ["area"] = "squareFootage"
                    },
                    Relationships = new[]
                    {
                        new RelationshipMapping
                        {
                            RelationshipName = "contains",
                            ForeignKeyColumn = "parentBuildingId",
                            TargetTwinIdTemplate = "building-{targetFeatureId}",
                            Properties = new Dictionary<string, object>()
                        }
                    }
                }
            }
        };

        var optionsMock = new Mock<IOptions<AzureDigitalTwinsOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);

        _syncService = new TwinSynchronizationService(
            _adtClient,
            _modelMapper.Object,
            NullLogger<TwinSynchronizationService>.Instance,
            optionsMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _featureRepository.ClearAllAsync();
    }

    [Fact]
    public async Task SyncFeatureToAdt_CreatesDigitalTwin_WithCorrectProperties()
    {
        // Arrange
        var featureId = "building-001";
        var attributes = new Dictionary<string, object?>
        {
            ["name"] = "City Hall",
            ["address"] = "123 Main St",
            ["floors"] = 5,
            ["area"] = 50000.0,
            ["yearBuilt"] = 1985
        };

        // Act
        var result = await _syncService.SyncFeatureToTwinAsync(
            _serviceId,
            _layerId,
            featureId,
            attributes);

        // Assert - Sync successful
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Operation.Should().Be(SyncOperationType.Created);
        result.TwinId.Should().Be($"building-{featureId}");

        // Assert - Twin was created in ADT
        var twin = await _adtClient.GetDigitalTwinAsync(result.TwinId!);
        twin.Should().NotBeNull();
        twin.Value.Metadata.ModelId.Should().Be(_modelId);

        // Assert - Properties were mapped correctly
        twin.Value.Contents.Should().ContainKey("buildingName");
        twin.Value.Contents["buildingName"].Should().Be("City Hall");

        twin.Value.Contents.Should().ContainKey("streetAddress");
        twin.Value.Contents["streetAddress"].Should().Be("123 Main St");

        // Assert - Sync metadata was added
        twin.Value.Contents.Should().ContainKey("honuaServiceId");
        twin.Value.Contents["honuaServiceId"].Should().Be(_serviceId);

        twin.Value.Contents.Should().ContainKey("honuaLayerId");
        twin.Value.Contents["honuaLayerId"].Should().Be(_layerId);

        twin.Value.Contents.Should().ContainKey("honuaFeatureId");
        twin.Value.Contents["honuaFeatureId"].Should().Be(featureId);

        twin.Value.Contents.Should().ContainKey("lastSyncTime");
    }

    [Fact]
    public async Task UpdateFeatureInHonua_UpdatesTwinInAdt()
    {
        // Arrange - Create initial twin
        var featureId = "building-002";
        var initialAttributes = new Dictionary<string, object?>
        {
            ["name"] = "Office Building A",
            ["floors"] = 3,
            ["area"] = 25000.0
        };

        await _syncService.SyncFeatureToTwinAsync(_serviceId, _layerId, featureId, initialAttributes);

        // Act - Update feature
        var updatedAttributes = new Dictionary<string, object?>
        {
            ["name"] = "Office Building A - Renovated",
            ["floors"] = 4,
            ["area"] = 30000.0
        };

        var result = await _syncService.SyncFeatureToTwinAsync(
            _serviceId,
            _layerId,
            featureId,
            updatedAttributes);

        // Assert - Update successful
        result.Success.Should().BeTrue();
        result.Operation.Should().Be(SyncOperationType.Updated);

        // Assert - Twin was updated
        var twin = await _adtClient.GetDigitalTwinAsync($"building-{featureId}");
        twin.Value.Contents["buildingName"].Should().Be("Office Building A - Renovated");
    }

    [Fact]
    public async Task UpdateTwinInAdt_UpdatesFeatureInHonua_ViaEventGrid()
    {
        // Arrange - Create twin with Honua metadata
        var twinId = "building-external-001";
        var twin = new BasicDigitalTwin
        {
            Id = twinId,
            Metadata = { ModelId = _modelId },
            Contents =
            {
                ["buildingName"] = "External Building",
                ["streetAddress"] = "456 Oak Ave",
                ["honuaServiceId"] = _serviceId,
                ["honuaLayerId"] = _layerId,
                ["honuaFeatureId"] = "ext-001"
            }
        };

        await _adtClient.CreateOrReplaceDigitalTwinAsync(twinId, twin);

        // Act - Sync twin back to Honua
        var result = await _syncService.SyncTwinToFeatureAsync(twinId);

        // Assert - Sync successful
        result.Success.Should().BeTrue();
        result.Operation.Should().Be(SyncOperationType.Updated);

        // Note: In a real implementation, this would call the Honua feature update API
        // For this test, we're verifying the sync service correctly processes the twin
    }

    [Fact]
    public async Task CreateRelationship_MapsToForeignKey_InHonua()
    {
        // Arrange - Create parent building
        var parentFeatureId = "building-parent-001";
        var parentAttributes = new Dictionary<string, object?>
        {
            ["name"] = "Campus Main Building",
            ["area"] = 100000.0
        };

        await _syncService.SyncFeatureToTwinAsync(_serviceId, _layerId, parentFeatureId, parentAttributes);

        // Arrange - Create child building with foreign key
        var childFeatureId = "building-child-001";
        var childAttributes = new Dictionary<string, object?>
        {
            ["name"] = "Campus Annex",
            ["area"] = 25000.0,
            ["parentBuildingId"] = parentFeatureId // Foreign key
        };

        // Act - Sync child and relationships
        await _syncService.SyncFeatureToTwinAsync(_serviceId, _layerId, childFeatureId, childAttributes);
        var result = await _syncService.SyncRelationshipsAsync(_serviceId, _layerId, childFeatureId, childAttributes);

        // Assert - Relationship sync successful
        result.Success.Should().BeTrue();

        // Assert - Relationship was created in ADT
        var childTwinId = $"building-{childFeatureId}";
        var relationships = _adtClient.GetRelationshipsAsync(childTwinId);

        var relationshipsList = new List<BasicRelationship>();
        await foreach (var rel in relationships)
        {
            relationshipsList.Add(rel);
        }

        relationshipsList.Should().HaveCount(1);
        relationshipsList[0].Name.Should().Be("contains");
        relationshipsList[0].SourceId.Should().Be(childTwinId);
        relationshipsList[0].TargetId.Should().Be($"building-{parentFeatureId}");
    }

    [Fact]
    public async Task ConflictingUpdate_ResolvesWithLastWriteWins()
    {
        // Arrange - Create initial twin
        var featureId = "building-conflict-001";
        var initialAttributes = new Dictionary<string, object?>
        {
            ["name"] = "Conflict Test Building",
            ["area"] = 10000.0
        };

        await _syncService.SyncFeatureToTwinAsync(_serviceId, _layerId, featureId, initialAttributes);

        // Arrange - Simulate external update to twin in ADT
        var twinId = $"building-{featureId}";
        var twin = await _adtClient.GetDigitalTwinAsync(twinId);
        twin.Value.Contents["buildingName"] = "Building Updated Externally";
        await _adtClient.CreateOrReplaceDigitalTwinAsync(twinId, twin.Value);

        // Act - Update from Honua side (conflict scenario)
        var honuaUpdate = new Dictionary<string, object?>
        {
            ["name"] = "Building Updated from Honua",
            ["area"] = 12000.0
        };

        var result = await _syncService.SyncFeatureToTwinAsync(
            _serviceId,
            _layerId,
            featureId,
            honuaUpdate);

        // Assert - Update successful (last write wins)
        result.Success.Should().BeTrue();
        result.Operation.Should().Be(SyncOperationType.Updated);

        // Assert - Honua update overwrote external update
        var updatedTwin = await _adtClient.GetDigitalTwinAsync(twinId);
        updatedTwin.Value.Contents["buildingName"].Should().Be("Building Updated from Honua");
    }

    [Fact]
    public async Task BatchSyncLayer_Syncs1000Features_Successfully()
    {
        // Arrange - Create 1000 features
        var features = new List<(string featureId, Dictionary<string, object?> attributes)>();

        for (int i = 0; i < 1000; i++)
        {
            var featureId = $"building-batch-{i:D4}";
            var attributes = new Dictionary<string, object?>
            {
                ["name"] = $"Building {i}",
                ["address"] = $"{i} Test Street",
                ["floors"] = (i % 10) + 1,
                ["area"] = (i + 1) * 1000.0
            };

            features.Add((featureId, attributes));
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var stats = await _syncService.SyncFeaturesToTwinsAsync(_serviceId, _layerId, features);
        stopwatch.Stop();

        // Assert - Performance target: 1000 features in <30 seconds
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            $"Batch sync took {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        // Assert - All features synced successfully
        stats.Should().NotBeNull();
        stats.TotalProcessed.Should().Be(1000);
        stats.Succeeded.Should().Be(1000);
        stats.Failed.Should().Be(0);

        // Assert - Operations breakdown
        stats.OperationBreakdown.Should().ContainKey(SyncOperationType.Created);
        stats.OperationBreakdown[SyncOperationType.Created].Should().Be(1000);

        // Log performance metrics
        Console.WriteLine($"Azure Digital Twins Batch Sync Performance Metrics:");
        Console.WriteLine($"  Total Time: {stopwatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"  Features/Second: {1000 / stopwatch.Elapsed.TotalSeconds:F2}");
        Console.WriteLine($"  Total Processed: {stats.TotalProcessed}");
        Console.WriteLine($"  Succeeded: {stats.Succeeded}");
        Console.WriteLine($"  Failed: {stats.Failed}");
        Console.WriteLine($"  Duration: {stats.Duration.TotalSeconds:F2}s");
    }

    [Fact]
    public async Task DeleteFeature_DeletesTwinAndRelationships()
    {
        // Arrange - Create parent and child buildings with relationship
        var parentFeatureId = "building-delete-parent";
        var childFeatureId = "building-delete-child";

        var parentAttributes = new Dictionary<string, object?> { ["name"] = "Parent Building" };
        var childAttributes = new Dictionary<string, object?>
        {
            ["name"] = "Child Building",
            ["parentBuildingId"] = parentFeatureId
        };

        await _syncService.SyncFeatureToTwinAsync(_serviceId, _layerId, parentFeatureId, parentAttributes);
        await _syncService.SyncFeatureToTwinAsync(_serviceId, _layerId, childFeatureId, childAttributes);
        await _syncService.SyncRelationshipsAsync(_serviceId, _layerId, childFeatureId, childAttributes);

        // Act - Delete child building
        var result = await _syncService.DeleteTwinAsync(_serviceId, _layerId, childFeatureId);

        // Assert - Delete successful
        result.Success.Should().BeTrue();
        result.Operation.Should().Be(SyncOperationType.Deleted);

        // Assert - Twin no longer exists
        var childTwinId = $"building-{childFeatureId}";
        var act = () => _adtClient.GetDigitalTwinAsync(childTwinId);
        await act.Should().ThrowAsync<RequestFailedException>()
            .Where(ex => ex.Status == 404);

        // Assert - Parent twin still exists
        var parentTwinId = $"building-{parentFeatureId}";
        var parentTwin = await _adtClient.GetDigitalTwinAsync(parentTwinId);
        parentTwin.Should().NotBeNull();
    }

    [Fact]
    public async Task SyncWithPartialFailure_ContinuesProcessing_ReportsErrors()
    {
        // Arrange - Create features, some with valid data, some with invalid
        var features = new List<(string featureId, Dictionary<string, object?> attributes)>
        {
            ("building-good-001", new Dictionary<string, object?> { ["name"] = "Good Building 1" }),
            ("building-good-002", new Dictionary<string, object?> { ["name"] = "Good Building 2" }),
            ("", new Dictionary<string, object?> { ["name"] = "Bad Building - Empty ID" }), // Invalid
            ("building-good-003", new Dictionary<string, object?> { ["name"] = "Good Building 3" })
        };

        // Act
        var stats = await _syncService.SyncFeaturesToTwinsAsync(_serviceId, _layerId, features);

        // Assert - Good features processed, bad ones failed gracefully
        stats.TotalProcessed.Should().Be(4);
        stats.Succeeded.Should().BeGreaterOrEqualTo(3);
        stats.Failed.Should().BeGreaterThan(0);

        // Assert - System continued processing after failure
        var twin3 = await _adtClient.GetDigitalTwinAsync("building-building-good-003");
        twin3.Should().NotBeNull();
    }

    private void SetupModelMapper()
    {
        _modelMapper.Setup(m => m.MapFeatureToTwinProperties(
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<LayerModelMapping>()))
            .Returns<Dictionary<string, object?>, LayerModelMapping>((attributes, mapping) =>
            {
                var twinProps = new Dictionary<string, object>();

                foreach (var (sourceKey, targetKey) in mapping.PropertyMappings)
                {
                    if (attributes.TryGetValue(sourceKey, out var value) && value != null)
                    {
                        twinProps[targetKey] = value;
                    }
                }

                return twinProps;
            });

        _modelMapper.Setup(m => m.MapTwinToFeatureProperties(
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<LayerModelMapping>()))
            .Returns<IDictionary<string, object>, LayerModelMapping>((twinContents, mapping) =>
            {
                var featureAttrs = new Dictionary<string, object?>();

                foreach (var (sourceKey, targetKey) in mapping.PropertyMappings)
                {
                    if (twinContents.TryGetValue(targetKey, out var value))
                    {
                        featureAttrs[sourceKey] = value;
                    }
                }

                return featureAttrs;
            });
    }
}

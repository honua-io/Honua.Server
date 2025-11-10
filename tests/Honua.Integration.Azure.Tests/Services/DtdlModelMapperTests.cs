// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Integration.Azure.Tests.Services;

public class DtdlModelMapperTests
{
    private readonly Mock<ILogger<DtdlModelMapper>> _mockLogger;
    private readonly AzureDigitalTwinsOptions _options;
    private readonly DtdlModelMapper _mapper;

    public DtdlModelMapperTests()
    {
        _mockLogger = new Mock<ILogger<DtdlModelMapper>>();
        _options = new AzureDigitalTwinsOptions
        {
            InstanceUrl = "https://test.api.wus2.digitaltwins.azure.net",
            DefaultNamespace = "dtmi:com:honua",
            UseNgsiLdOntology = true
        };
        _mapper = new DtdlModelMapper(_mockLogger.Object, Options.Create(_options));
    }

    [Fact]
    public async Task GenerateModelFromLayerAsync_ShouldGenerateValidDtdlModel()
    {
        // Arrange
        var serviceId = "smart-city";
        var layerId = "traffic-sensors";
        var layerSchema = new Dictionary<string, object>
        {
            ["title"] = "Traffic Sensors",
            ["description"] = "IoT traffic sensors for smart city monitoring",
            ["properties"] = new Dictionary<string, object>
            {
                ["sensor_id"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["title"] = "Sensor ID",
                    ["description"] = "Unique sensor identifier"
                },
                ["temperature"] = new Dictionary<string, object>
                {
                    ["type"] = "double",
                    ["title"] = "Temperature",
                    ["description"] = "Current temperature reading"
                },
                ["vehicle_count"] = new Dictionary<string, object>
                {
                    ["type"] = "integer",
                    ["title"] = "Vehicle Count",
                    ["description"] = "Number of vehicles detected"
                },
                ["is_active"] = new Dictionary<string, object>
                {
                    ["type"] = "boolean",
                    ["title"] = "Is Active",
                    ["description"] = "Whether the sensor is currently active"
                },
                ["last_reading"] = new Dictionary<string, object>
                {
                    ["type"] = "datetime",
                    ["title"] = "Last Reading",
                    ["description"] = "Timestamp of last reading"
                }
            }
        };

        // Act
        var model = await _mapper.GenerateModelFromLayerAsync(serviceId, layerId, layerSchema);

        // Assert
        model.Should().NotBeNull();
        model.Id.Should().Be("dtmi:com:honua:smart_city:traffic_sensors;1");
        model.DisplayName.Should().Be("Traffic Sensors");
        model.Description.Should().Contain("Traffic sensors");
        model.Type.Should().Be("Interface");
        model.Context.Should().Be("dtmi:dtdl:context;3");
        model.Contents.Should().NotBeEmpty();

        // Check for NGSI-LD properties
        model.Contents.Should().Contain(c => c.Name == "type");
        model.Contents.Should().Contain(c => c.Name == "location");

        // Check for mapped properties
        model.Contents.Should().Contain(c => c.Name == "sensor_id");
        model.Contents.Should().Contain(c => c.Name == "temperature");
        model.Contents.Should().Contain(c => c.Name == "vehicle_count");
        model.Contents.Should().Contain(c => c.Name == "is_active");

        // Check for sync metadata
        model.Contents.Should().Contain(c => c.Name == "honuaServiceId");
        model.Contents.Should().Contain(c => c.Name == "honuaLayerId");
        model.Contents.Should().Contain(c => c.Name == "honuaFeatureId");
        model.Contents.Should().Contain(c => c.Name == "lastSyncTime");
    }

    [Fact]
    public async Task GenerateModelJsonFromLayerAsync_ShouldGenerateValidJson()
    {
        // Arrange
        var serviceId = "smart-city";
        var layerId = "parking-lots";
        var layerSchema = new Dictionary<string, object>
        {
            ["title"] = "Parking Lots",
            ["properties"] = new Dictionary<string, object>
            {
                ["name"] = new Dictionary<string, object> { ["type"] = "string" },
                ["capacity"] = new Dictionary<string, object> { ["type"] = "integer" }
            }
        };

        // Act
        var modelJson = await _mapper.GenerateModelJsonFromLayerAsync(serviceId, layerId, layerSchema);

        // Assert
        modelJson.Should().NotBeNullOrEmpty();
        modelJson.Should().Contain("@id");
        modelJson.Should().Contain("@type");
        modelJson.Should().Contain("@context");
        modelJson.Should().Contain("dtmi:com:honua:smart_city:parking_lots;1");
        modelJson.Should().Contain("Parking Lots");
    }

    [Fact]
    public async Task ValidateModelAsync_ShouldReturnTrueForValidModel()
    {
        // Arrange
        var validModelJson = @"{
            ""@id"": ""dtmi:com:example:test;1"",
            ""@type"": ""Interface"",
            ""@context"": ""dtmi:dtdl:context;3"",
            ""displayName"": ""Test Model"",
            ""contents"": []
        }";

        // Act
        var result = await _mapper.ValidateModelAsync(validModelJson);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateModelAsync_ShouldReturnFalseForInvalidModel()
    {
        // Arrange
        var invalidModelJson = @"{
            ""displayName"": ""Test Model"",
            ""contents"": []
        }";

        // Act
        var result = await _mapper.ValidateModelAsync(invalidModelJson);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MapFeatureToTwinProperties_ShouldMapCorrectly()
    {
        // Arrange
        var featureAttributes = new Dictionary<string, object?>
        {
            ["sensor_id"] = "SENSOR-001",
            ["temperature"] = 25.5,
            ["vehicle_count"] = 42,
            ["is_active"] = true,
            ["location_name"] = "Main Street"
        };

        var mapping = new LayerModelMapping
        {
            ServiceId = "smart-city",
            LayerId = "sensors",
            ModelId = "dtmi:com:honua:sensor;1",
            PropertyMappings = new Dictionary<string, string>
            {
                ["sensor_id"] = "sensorId",
                ["location_name"] = "locationName"
            }
        };

        // Act
        var twinProperties = _mapper.MapFeatureToTwinProperties(featureAttributes, mapping);

        // Assert
        twinProperties.Should().ContainKey("sensorId");
        twinProperties["sensorId"].Should().Be("SENSOR-001");
        twinProperties.Should().ContainKey("locationName");
        twinProperties["locationName"].Should().Be("Main Street");
        twinProperties.Should().ContainKey("temperature");
        twinProperties["temperature"].Should().Be(25.5);
        twinProperties.Should().ContainKey("vehicle_count");
        twinProperties["vehicle_count"].Should().Be(42);
    }

    [Fact]
    public void MapTwinToFeatureProperties_ShouldMapCorrectly()
    {
        // Arrange
        var twinProperties = new Dictionary<string, object>
        {
            ["sensorId"] = "SENSOR-001",
            ["locationName"] = "Main Street",
            ["temperature"] = 25.5,
            ["$metadata"] = new { } // Should be filtered out
        };

        var mapping = new LayerModelMapping
        {
            ServiceId = "smart-city",
            LayerId = "sensors",
            ModelId = "dtmi:com:honua:sensor;1",
            PropertyMappings = new Dictionary<string, string>
            {
                ["sensor_id"] = "sensorId",
                ["location_name"] = "locationName"
            }
        };

        // Act
        var featureAttributes = _mapper.MapTwinToFeatureProperties(twinProperties, mapping);

        // Assert
        featureAttributes.Should().ContainKey("sensor_id");
        featureAttributes["sensor_id"].Should().Be("SENSOR-001");
        featureAttributes.Should().ContainKey("location_name");
        featureAttributes["location_name"].Should().Be("Main Street");
        featureAttributes.Should().ContainKey("temperature");
        featureAttributes.Should().NotContainKey("$metadata");
    }

    [Fact]
    public async Task GenerateModelFromLayerAsync_ShouldSanitizeDtmiComponents()
    {
        // Arrange
        var serviceId = "smart-city-2024";
        var layerId = "traffic-sensors@v1";
        var layerSchema = new Dictionary<string, object>
        {
            ["title"] = "Test Layer"
        };

        // Act
        var model = await _mapper.GenerateModelFromLayerAsync(serviceId, layerId, layerSchema);

        // Assert
        model.Id.Should().MatchRegex(@"^dtmi:[a-zA-Z][a-zA-Z0-9_]*:[a-zA-Z][a-zA-Z0-9_]*:[a-zA-Z][a-zA-Z0-9_]*;\d+$");
    }
}

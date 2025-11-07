using FluentAssertions;
using Honua.MapSDK.Models;
using Honua.MapSDK.Services;
using Xunit;

namespace Honua.MapSDK.Tests.ServiceTests;

/// <summary>
/// Tests for MapConfigurationService
/// </summary>
public class MapConfigurationServiceTests
{
    private readonly IMapConfigurationService _service;

    public MapConfigurationServiceTests()
    {
        _service = new MapConfigurationService();
    }

    #region Export Tests

    [Fact]
    public void ExportAsJson_WithFormattedOption_ShouldReturnIndentedJson()
    {
        // Arrange
        var config = CreateSampleConfiguration();

        // Act
        var json = _service.ExportAsJson(config, formatted: true);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("  "); // Contains indentation
        json.Should().Contain("\"name\": \"Test Map\"");
    }

    [Fact]
    public void ExportAsJson_WithoutFormattedOption_ShouldReturnCompactJson()
    {
        // Arrange
        var config = CreateSampleConfiguration();

        // Act
        var json = _service.ExportAsJson(config, formatted: false);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().NotContain("  "); // No indentation
    }

    [Fact]
    public void ExportAsJson_ShouldExcludeNullValues()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Description = null;

        // Act
        var json = _service.ExportAsJson(config);

        // Assert
        json.Should().NotContain("description");
    }

    [Fact]
    public void ExportAsYaml_ShouldReturnValidYaml()
    {
        // Arrange
        var config = CreateSampleConfiguration();

        // Act
        var yaml = _service.ExportAsYaml(config);

        // Assert
        yaml.Should().NotBeNullOrEmpty();
        yaml.Should().Contain("name: Test Map");
        yaml.Should().Contain("settings:");
        yaml.Should().Contain("layers:");
    }

    [Fact]
    public void ExportAsHtmlEmbed_ShouldReturnValidHtml()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        var sdkUrl = "https://cdn.honua.io/sdk";

        // Act
        var html = _service.ExportAsHtmlEmbed(config, sdkUrl);

        // Assert
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html>");
        html.Should().Contain($"<script src=\"{sdkUrl}/honua-mapsdk.js\"></script>");
        html.Should().Contain($"<link rel=\"stylesheet\" href=\"{sdkUrl}/honua-mapsdk.css\">");
        html.Should().Contain("<div id=\"map\"></div>");
        html.Should().Contain("HonuaMap.create");
        html.Should().Contain(config.Name);
    }

    [Fact]
    public void ExportAsBlazorComponent_ShouldReturnValidRazorMarkup()
    {
        // Arrange
        var config = CreateSampleConfiguration();

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain("<HonuaMap");
        razor.Should().Contain($"Id=\"{config.Id}\"");
        razor.Should().Contain($"Style=\"{config.Settings.Style}\"");
        razor.Should().Contain($"Zoom=\"{config.Settings.Zoom}\"");
        razor.Should().Contain("</HonuaMap>");
    }

    [Fact]
    public void ExportAsBlazorComponent_WithLayers_ShouldIncludeLayerComponents()
    {
        // Arrange
        var config = CreateSampleConfiguration();

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        foreach (var layer in config.Layers)
        {
            razor.Should().Contain($"Id=\"{layer.Id}\"");
            razor.Should().Contain($"Name=\"{layer.Name}\"");
        }
    }

    [Fact]
    public void ExportAsBlazorComponent_WithControls_ShouldIncludeControlComponents()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Controls.Add(new ControlConfiguration
        {
            Type = ControlType.Navigation,
            Position = "top-right",
            Visible = true
        });

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain("<HonuaNavigationControl");
    }

    [Fact]
    public void ExportAsBlazorComponent_WithNonDefaultBearing_ShouldIncludeBearing()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Settings.Bearing = 45;

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain("Bearing=\"45\"");
    }

    [Fact]
    public void ExportAsBlazorComponent_WithNonDefaultPitch_ShouldIncludePitch()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Settings.Pitch = 30;

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain("Pitch=\"30\"");
    }

    #endregion

    #region Import Tests

    [Fact]
    public void ImportFromJson_WithValidJson_ShouldReturnConfiguration()
    {
        // Arrange
        var originalConfig = CreateSampleConfiguration();
        var json = _service.ExportAsJson(originalConfig);

        // Act
        var importedConfig = _service.ImportFromJson(json);

        // Assert
        importedConfig.Should().NotBeNull();
        importedConfig.Name.Should().Be(originalConfig.Name);
        importedConfig.Settings.Style.Should().Be(originalConfig.Settings.Style);
        importedConfig.Settings.Zoom.Should().Be(originalConfig.Settings.Zoom);
        importedConfig.Layers.Should().HaveCount(originalConfig.Layers.Count);
    }

    [Fact]
    public void ImportFromJson_WithInvalidJson_ShouldThrow()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.Throws<System.Text.Json.JsonException>(() => _service.ImportFromJson(invalidJson));
    }

    [Fact]
    public void ImportFromYaml_WithValidYaml_ShouldReturnConfiguration()
    {
        // Arrange
        var originalConfig = CreateSampleConfiguration();
        var yaml = _service.ExportAsYaml(originalConfig);

        // Act
        var importedConfig = _service.ImportFromYaml(yaml);

        // Assert
        importedConfig.Should().NotBeNull();
        importedConfig.Name.Should().Be(originalConfig.Name);
        importedConfig.Settings.Style.Should().Be(originalConfig.Settings.Style);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Validate_WithValidConfiguration_ShouldReturnValid()
    {
        // Arrange
        var config = CreateSampleConfiguration();

        // Act
        var result = _service.Validate(config);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithMissingName_ShouldReturnError()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Name = "";

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Map name is required");
    }

    [Fact]
    public void Validate_WithMissingStyle_ShouldReturnError()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Settings.Style = "";

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Map style is required");
    }

    [Fact]
    public void Validate_WithInvalidCenter_ShouldReturnError()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Settings.Center = new[] { 0.0 }; // Only one coordinate

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Map center must be [longitude, latitude]");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    public void Validate_WithInvalidZoom_ShouldReturnError(double zoom)
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Settings.Zoom = zoom;

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Map zoom must be between 0 and 22");
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(65)]
    public void Validate_WithInvalidPitch_ShouldReturnError(double pitch)
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Settings.Pitch = pitch;

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Map pitch must be between 0 and 60");
    }

    [Fact]
    public void Validate_WithLayerMissingName_ShouldReturnError()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Layers[0].Name = "";

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("name is required"));
    }

    [Fact]
    public void Validate_WithLayerMissingSource_ShouldReturnError()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Layers[0].Source = "";

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("source is required"));
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void Validate_WithInvalidLayerOpacity_ShouldReturnError(double opacity)
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Layers[0].Opacity = opacity;

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("opacity must be between 0 and 1"));
    }

    [Fact]
    public void Validate_WithDuplicateLayerIds_ShouldReturnError()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        var duplicateId = Guid.NewGuid().ToString();
        config.Layers.Add(new LayerConfiguration
        {
            Id = duplicateId,
            Name = "Layer 1",
            Type = LayerType.Vector,
            Source = "source1"
        });
        config.Layers.Add(new LayerConfiguration
        {
            Id = duplicateId, // Duplicate ID
            Name = "Layer 2",
            Type = LayerType.Vector,
            Source = "source2"
        });

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate layer ID"));
    }

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var config = CreateSampleConfiguration();
        config.Name = "";
        config.Settings.Style = "";
        config.Settings.Zoom = -1;

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    #endregion

    #region Helper Methods

    private MapConfiguration CreateSampleConfiguration()
    {
        return new MapConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Map",
            Description = "Test map configuration",
            Settings = new MapSettings
            {
                Style = "https://api.maptiler.com/maps/streets/style.json",
                Center = new[] { -122.4194, 37.7749 },
                Zoom = 10,
                Bearing = 0,
                Pitch = 0,
                Projection = "mercator"
            },
            Layers = new List<LayerConfiguration>
            {
                new LayerConfiguration
                {
                    Id = "layer-1",
                    Name = "Test Layer",
                    Type = LayerType.Vector,
                    Source = "test-source",
                    Visible = true,
                    Opacity = 1.0
                }
            },
            Controls = new List<ControlConfiguration>()
        };
    }

    #endregion
}

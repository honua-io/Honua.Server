// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models;
using Honua.MapSDK.Services;
using Honua.MapSDK.Tests.Utilities;

namespace Honua.MapSDK.Tests.Services;

/// <summary>
/// Tests for the MapConfigurationService.
/// Tests configuration export (JSON, YAML, HTML, Blazor), import, and validation.
/// </summary>
[Trait("Category", "Unit")]
public class MapConfigurationServiceTests
{
    private readonly IMapConfigurationService _service;

    public MapConfigurationServiceTests()
    {
        _service = new MapConfigurationService();
    }

    #region Export as JSON Tests

    [Fact]
    public void ExportAsJson_BasicConfiguration_ShouldReturnValidJson()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();

        // Act
        var json = _service.ExportAsJson(config);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"name\": \"Test Map\"");
        json.Should().Contain("\"style\":");
        json.Should().Contain("\"center\":");
        json.Should().Contain("\"zoom\":");
    }

    [Fact]
    public void ExportAsJson_FormattedFalse_ShouldReturnCompactJson()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();

        // Act
        var json = _service.ExportAsJson(config, formatted: false);

        // Assert
        json.Should().NotContain("\n");
        json.Should().NotContain("  "); // No indentation
    }

    [Fact]
    public void ExportAsJson_WithLayers_ShouldIncludeLayers()
    {
        // Arrange
        var config = MapTestFixture.CreateMapConfigurationWithLayers();

        // Act
        var json = _service.ExportAsJson(config);

        // Assert
        json.Should().Contain("\"layers\":");
        json.Should().Contain("\"parcels\"");
        json.Should().Contain("\"buildings\"");
    }

    [Fact]
    public void ExportAsJson_ComplexConfiguration_ShouldIncludeAllProperties()
    {
        // Arrange
        var config = MapTestFixture.CreateComplexMapConfiguration();

        // Act
        var json = _service.ExportAsJson(config);

        // Assert
        json.Should().Contain("\"layers\":");
        json.Should().Contain("\"controls\":");
        json.Should().Contain("\"filters\":");
        json.Should().Contain("\"metadata\":");
    }

    #endregion

    #region Export as YAML Tests

    [Fact]
    public void ExportAsYaml_BasicConfiguration_ShouldReturnValidYaml()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();

        // Act
        var yaml = _service.ExportAsYaml(config);

        // Assert
        yaml.Should().NotBeNullOrEmpty();
        yaml.Should().Contain("name: Test Map");
        yaml.Should().Contain("style:");
        yaml.Should().Contain("center:");
        yaml.Should().Contain("zoom:");
    }

    [Fact]
    public void ExportAsYaml_WithLayers_ShouldIncludeLayers()
    {
        // Arrange
        var config = MapTestFixture.CreateMapConfigurationWithLayers();

        // Act
        var yaml = _service.ExportAsYaml(config);

        // Assert
        yaml.Should().Contain("layers:");
        yaml.Should().Contain("name: Parcels");
        yaml.Should().Contain("name: Buildings");
    }

    [Fact]
    public void ExportAsYaml_ShouldUseCamelCase()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();

        // Act
        var yaml = _service.ExportAsYaml(config);

        // Assert
        yaml.Should().Contain("mapStyle:"); // camelCase, not MapStyle
    }

    #endregion

    #region Export as HTML Embed Tests

    [Fact]
    public void ExportAsHtmlEmbed_ShouldReturnValidHtml()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        var sdkUrl = "https://cdn.honua.io/mapsdk/v1";

        // Act
        var html = _service.ExportAsHtmlEmbed(config, sdkUrl);

        // Assert
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html>");
        html.Should().Contain("</html>");
        html.Should().Contain("<title>Test Map</title>");
    }

    [Fact]
    public void ExportAsHtmlEmbed_ShouldIncludeSdkReferences()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        var sdkUrl = "https://cdn.honua.io/mapsdk/v1";

        // Act
        var html = _service.ExportAsHtmlEmbed(config, sdkUrl);

        // Assert
        html.Should().Contain($"<script src=\"{sdkUrl}/honua-mapsdk.js\"></script>");
        html.Should().Contain($"<link rel=\"stylesheet\" href=\"{sdkUrl}/honua-mapsdk.css\">");
    }

    [Fact]
    public void ExportAsHtmlEmbed_ShouldIncludeMapConfiguration()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        config.Name = "My Custom Map";
        var sdkUrl = "https://cdn.honua.io/mapsdk/v1";

        // Act
        var html = _service.ExportAsHtmlEmbed(config, sdkUrl);

        // Assert
        html.Should().Contain("const config = ");
        html.Should().Contain("\"name\":\"My Custom Map\"");
        html.Should().Contain("HonuaMap.create('#map', config);");
    }

    [Fact]
    public void ExportAsHtmlEmbed_ShouldHaveMapContainer()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        var sdkUrl = "https://cdn.honua.io/mapsdk/v1";

        // Act
        var html = _service.ExportAsHtmlEmbed(config, sdkUrl);

        // Assert
        html.Should().Contain("<div id=\"map\"></div>");
        html.Should().Contain("width: 100vw");
        html.Should().Contain("height: 100vh");
    }

    #endregion

    #region Export as Blazor Component Tests

    [Fact]
    public void ExportAsBlazorComponent_ShouldReturnValidRazorMarkup()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain("<HonuaMap");
        razor.Should().Contain("</HonuaMap>");
        razor.Should().Contain($"Id=\"{config.Id}\"");
    }

    [Fact]
    public void ExportAsBlazorComponent_ShouldIncludeMapSettings()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain($"Style=\"{config.Settings.Style}\"");
        razor.Should().Contain($"Zoom=\"{config.Settings.Zoom}\"");
        razor.Should().Contain("Center=");
    }

    [Fact]
    public void ExportAsBlazorComponent_WithLayers_ShouldIncludeLayerComponents()
    {
        // Arrange
        var config = MapTestFixture.CreateMapConfigurationWithLayers();

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain("<HonuaLayer");
        razor.Should().Contain("Id=\"parcels\"");
        razor.Should().Contain("Name=\"Parcels\"");
        razor.Should().Contain("Type=\"LayerType.Vector\"");
    }

    [Fact]
    public void ExportAsBlazorComponent_WithControls_ShouldIncludeControlComponents()
    {
        // Arrange
        var config = MapTestFixture.CreateMapConfigurationWithControls();

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain("<HonuaNavigationControl");
        razor.Should().Contain("<HonuaScaleControl");
        razor.Should().Contain("<HonuaSearchControl");
    }

    [Fact]
    public void ExportAsBlazorComponent_ShouldIncludeGeneratedComment()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration("Production Map");

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain("@* Production Map *@");
        razor.Should().Contain("@* Generated from Honua MapSDK *@");
    }

    [Fact]
    public void ExportAsBlazorComponent_DefaultBearing_ShouldNotInclude()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        config.Settings.Bearing = 0; // Default

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().NotContain("Bearing=\"0\"");
    }

    [Fact]
    public void ExportAsBlazorComponent_NonDefaultBearing_ShouldInclude()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        config.Settings.Bearing = 45;

        // Act
        var razor = _service.ExportAsBlazorComponent(config);

        // Assert
        razor.Should().Contain("Bearing=\"45\"");
    }

    #endregion

    #region Import from JSON Tests

    [Fact]
    public void ImportFromJson_ValidJson_ShouldReturnConfiguration()
    {
        // Arrange
        var original = MapTestFixture.CreateBasicMapConfiguration();
        var json = _service.ExportAsJson(original);

        // Act
        var imported = _service.ImportFromJson(json);

        // Assert
        imported.Should().NotBeNull();
        imported.Name.Should().Be(original.Name);
        imported.Settings.Zoom.Should().Be(original.Settings.Zoom);
    }

    [Fact]
    public void ImportFromJson_WithLayers_ShouldPreserveLayers()
    {
        // Arrange
        var original = MapTestFixture.CreateMapConfigurationWithLayers();
        var json = _service.ExportAsJson(original);

        // Act
        var imported = _service.ImportFromJson(json);

        // Assert
        imported.Layers.Should().HaveCount(2);
        imported.Layers[0].Id.Should().Be("parcels");
        imported.Layers[1].Id.Should().Be("buildings");
    }

    [Fact]
    public void ImportFromJson_InvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        _service.Invoking(s => s.ImportFromJson(invalidJson))
            .Should().Throw<Exception>();
    }

    [Fact]
    public void ImportFromJson_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var original = MapTestFixture.CreateComplexMapConfiguration();
        var json = _service.ExportAsJson(original);

        // Act
        var imported = _service.ImportFromJson(json);

        // Assert
        imported.Name.Should().Be(original.Name);
        imported.Settings.Center.Should().BeEquivalentTo(original.Settings.Center);
        imported.Settings.Zoom.Should().Be(original.Settings.Zoom);
        imported.Layers.Should().HaveCount(original.Layers.Count);
        imported.Controls.Should().HaveCount(original.Controls.Count);
    }

    #endregion

    #region Import from YAML Tests

    [Fact]
    public void ImportFromYaml_ValidYaml_ShouldReturnConfiguration()
    {
        // Arrange
        var original = MapTestFixture.CreateBasicMapConfiguration();
        var yaml = _service.ExportAsYaml(original);

        // Act
        var imported = _service.ImportFromYaml(yaml);

        // Assert
        imported.Should().NotBeNull();
        imported.Name.Should().Be(original.Name);
    }

    [Fact]
    public void ImportFromYaml_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var original = MapTestFixture.CreateMapConfigurationWithLayers();
        var yaml = _service.ExportAsYaml(original);

        // Act
        var imported = _service.ImportFromYaml(yaml);

        // Assert
        imported.Name.Should().Be(original.Name);
        imported.Layers.Should().HaveCount(original.Layers.Count);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Validate_ValidConfiguration_ShouldPass()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingName_ShouldFail()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        config.Name = "";

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("name is required"));
    }

    [Fact]
    public void Validate_MissingStyle_ShouldFail()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        config.Settings.Style = "";

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("style is required"));
    }

    [Fact]
    public void Validate_InvalidCenter_ShouldFail()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        config.Settings.Center = new[] { -122.4 }; // Only one coordinate

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("center must be"));
    }

    [Fact]
    public void Validate_InvalidZoom_ShouldFail()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        config.Settings.Zoom = 25; // Out of range

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("zoom must be"));
    }

    [Fact]
    public void Validate_InvalidPitch_ShouldFail()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        config.Settings.Pitch = 90; // Too high

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("pitch must be"));
    }

    [Fact]
    public void Validate_LayerMissingName_ShouldFail()
    {
        // Arrange
        var config = MapTestFixture.CreateMapConfigurationWithLayers();
        config.Layers[0].Name = "";

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("name is required"));
    }

    [Fact]
    public void Validate_LayerMissingSource_ShouldFail()
    {
        // Arrange
        var config = MapTestFixture.CreateMapConfigurationWithLayers();
        config.Layers[0].Source = "";

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("source is required"));
    }

    [Fact]
    public void Validate_LayerInvalidOpacity_ShouldFail()
    {
        // Arrange
        var config = MapTestFixture.CreateMapConfigurationWithLayers();
        config.Layers[0].Opacity = 1.5; // Out of range

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("opacity must be"));
    }

    [Fact]
    public void Validate_DuplicateLayerIds_ShouldFail()
    {
        // Arrange
        var config = MapTestFixture.CreateMapConfigurationWithLayers();
        config.Layers[1].Id = config.Layers[0].Id; // Same ID

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate layer ID"));
    }

    [Fact]
    public void Validate_ComplexValidConfiguration_ShouldPass()
    {
        // Arrange
        var config = MapTestFixture.CreateComplexMapConfiguration();

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleErrors_ShouldReportAll()
    {
        // Arrange
        var config = MapTestFixture.CreateBasicMapConfiguration();
        config.Name = "";
        config.Settings.Style = "";
        config.Settings.Zoom = 30;

        // Act
        var result = _service.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(2);
    }

    #endregion
}

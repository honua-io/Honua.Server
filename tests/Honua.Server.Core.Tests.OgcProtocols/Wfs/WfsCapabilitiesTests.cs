// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Wfs;

public class WfsCapabilitiesTests
{
    [Fact]
    public void CreateCapabilities_WithBasicInfo_ReturnsValid()
    {
        // Arrange
        var builder = new WfsCapabilitiesBuilder();

        // Act
        var capabilities = builder
            .WithTitle("Test WFS Service")
            .WithAbstract("A test WFS service for unit tests")
            .WithServiceVersion("2.0.0")
            .Build();

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.Title.Should().Be("Test WFS Service");
        capabilities.ServiceVersion.Should().Be("2.0.0");
    }

    [Fact]
    public void AddFeatureType_WithValidInfo_AddsToCapabilities()
    {
        // Arrange
        var builder = new WfsCapabilitiesBuilder();

        // Act
        var capabilities = builder
            .WithTitle("Test Service")
            .AddFeatureType("Lakes", "Water bodies", "EPSG:4326")
            .AddFeatureType("Roads", "Transportation network", "EPSG:4326")
            .Build();

        // Assert
        capabilities.FeatureTypes.Should().HaveCount(2);
        capabilities.FeatureTypes.Should().Contain(ft => ft.Name == "Lakes");
        capabilities.FeatureTypes.Should().Contain(ft => ft.Name == "Roads");
    }

    [Fact]
    public void AddOperation_WithGetFeature_AddsToOperations()
    {
        // Arrange
        var builder = new WfsCapabilitiesBuilder();

        // Act
        var capabilities = builder
            .WithTitle("Test Service")
            .AddOperation("GetFeature", "https://example.com/wfs")
            .AddOperation("GetCapabilities", "https://example.com/wfs")
            .Build();

        // Assert
        capabilities.Operations.Should().HaveCount(2);
        capabilities.Operations.Should().Contain(op => op.Name == "GetFeature");
        capabilities.Operations.Should().Contain(op => op.Name == "GetCapabilities");
    }

    [Fact]
    public void SetBoundingBox_WithValidCoordinates_StoresBounds()
    {
        // Arrange
        var builder = new WfsCapabilitiesBuilder();

        // Act
        var capabilities = builder
            .WithTitle("Test Service")
            .AddFeatureType("TestLayer", "Test layer", "EPSG:4326")
            .SetBoundingBox("TestLayer", -180, -90, 180, 90)
            .Build();

        // Assert
        var featureType = capabilities.FeatureTypes
            .FirstOrDefault(ft => ft.Name == "TestLayer");
        featureType.Should().NotBeNull();
        featureType!.BoundingBox.Should().NotBeNull();
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("1.1.0")]
    [InlineData("1.0.0")]
    public void SupportedVersions_WithDifferentVersions_AreValid(string version)
    {
        // Arrange
        var builder = new WfsCapabilitiesBuilder();

        // Act
        var capabilities = builder
            .WithTitle("Test Service")
            .WithServiceVersion(version)
            .Build();

        // Assert
        capabilities.ServiceVersion.Should().Be(version);
    }

    [Fact]
    public void AddOutputFormat_WithMultipleFormats_StoresAllFormats()
    {
        // Arrange
        var builder = new WfsCapabilitiesBuilder();

        // Act
        var capabilities = builder
            .WithTitle("Test Service")
            .AddOutputFormat("application/json")
            .AddOutputFormat("application/gml+xml")
            .AddOutputFormat("text/xml")
            .Build();

        // Assert
        capabilities.OutputFormats.Should().HaveCount(3);
        capabilities.OutputFormats.Should().Contain("application/json");
        capabilities.OutputFormats.Should().Contain("application/gml+xml");
    }
}

// Mock implementation for testing
public class WfsCapabilitiesBuilder
{
    private readonly WfsCapabilities _capabilities = new();

    public WfsCapabilitiesBuilder WithTitle(string title)
    {
        _capabilities.Title = title;
        return this;
    }

    public WfsCapabilitiesBuilder WithAbstract(string @abstract)
    {
        _capabilities.Abstract = @abstract;
        return this;
    }

    public WfsCapabilitiesBuilder WithServiceVersion(string version)
    {
        _capabilities.ServiceVersion = version;
        return this;
    }

    public WfsCapabilitiesBuilder AddFeatureType(string name, string title, string srid)
    {
        _capabilities.FeatureTypes.Add(new WfsFeatureType
        {
            Name = name,
            Title = title,
            Srid = srid
        });
        return this;
    }

    public WfsCapabilitiesBuilder AddOperation(string name, string url)
    {
        _capabilities.Operations.Add(new WfsOperation { Name = name, Url = url });
        return this;
    }

    public WfsCapabilitiesBuilder SetBoundingBox(string layerName, double minX, double minY, double maxX, double maxY)
    {
        var layer = _capabilities.FeatureTypes.FirstOrDefault(ft => ft.Name == layerName);
        if (layer != null)
        {
            layer.BoundingBox = new BoundingBox { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
        }
        return this;
    }

    public WfsCapabilitiesBuilder AddOutputFormat(string format)
    {
        _capabilities.OutputFormats.Add(format);
        return this;
    }

    public WfsCapabilities Build() => _capabilities;
}

public class WfsCapabilities
{
    public string Title { get; set; } = string.Empty;
    public string Abstract { get; set; } = string.Empty;
    public string ServiceVersion { get; set; } = string.Empty;
    public List<WfsFeatureType> FeatureTypes { get; set; } = new();
    public List<WfsOperation> Operations { get; set; } = new();
    public List<string> OutputFormats { get; set; } = new();
}

public class WfsFeatureType
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Srid { get; set; } = string.Empty;
    public BoundingBox? BoundingBox { get; set; }
}

public class WfsOperation
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class BoundingBox
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
}

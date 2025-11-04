using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Ogc.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

public class OgcCrsServiceTests
{
    private readonly OgcCrsService _service;

    public OgcCrsServiceTests()
    {
        _service = new OgcCrsService();
    }

    [Fact]
    public void ResolveSupportedCrs_WithLayerCrs_ReturnsLayerCrs()
    {
        // Arrange
        var service = CreateServiceDefinition();
        var layer = CreateLayerDefinition(new[] { "EPSG:4326", "EPSG:3857" });

        // Act
        var result = _service.ResolveSupportedCrs(service, layer);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("http://www.opengis.net/def/crs/EPSG/0/4326", result);
        Assert.Contains("http://www.opengis.net/def/crs/EPSG/0/3857", result);
    }

    [Fact]
    public void ResolveSupportedCrs_WithNoLayerCrs_ReturnsServiceDefaultCrs()
    {
        // Arrange
        var service = CreateServiceDefinition(defaultCrs: "EPSG:4326");
        var layer = CreateLayerDefinition(Array.Empty<string>());

        // Act
        var result = _service.ResolveSupportedCrs(service, layer);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/4326", result[0]);
    }

    [Fact]
    public void ResolveSupportedCrs_WithNoCrsConfigured_ReturnsDefaultWgs84()
    {
        // Arrange
        var service = CreateServiceDefinition();
        var layer = CreateLayerDefinition(Array.Empty<string>());

        // Act
        var result = _service.ResolveSupportedCrs(service, layer);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://www.opengis.net/def/crs/OGC/1.3/CRS84", result[0]);
    }

    [Fact]
    public void ResolveSupportedCrs_WithDuplicates_ReturnsDeduplicated()
    {
        // Arrange
        var service = CreateServiceDefinition(defaultCrs: "EPSG:4326", additionalCrs: new[] { "EPSG:4326" });
        var layer = CreateLayerDefinition(new[] { "EPSG:4326" });

        // Act
        var result = _service.ResolveSupportedCrs(service, layer);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/4326", result[0]);
    }

    [Fact]
    public void DetermineDefaultCrs_WithServiceDefaultCrsInSupported_ReturnsServiceDefault()
    {
        // Arrange
        var service = CreateServiceDefinition(defaultCrs: "EPSG:3857");
        var supported = new List<string>
        {
            "http://www.opengis.net/def/crs/EPSG/0/4326",
            "http://www.opengis.net/def/crs/EPSG/0/3857"
        };

        // Act
        var result = _service.DetermineDefaultCrs(service, supported);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/3857", result);
    }

    [Fact]
    public void DetermineDefaultCrs_WithServiceDefaultCrsNotInSupported_ReturnsFirstSupported()
    {
        // Arrange
        var service = CreateServiceDefinition(defaultCrs: "EPSG:2154");
        var supported = new List<string>
        {
            "http://www.opengis.net/def/crs/EPSG/0/4326",
            "http://www.opengis.net/def/crs/EPSG/0/3857"
        };

        // Act
        var result = _service.DetermineDefaultCrs(service, supported);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/4326", result);
    }

    [Fact]
    public void DetermineDefaultCrs_WithEmptySupported_ReturnsWgs84()
    {
        // Arrange
        var service = CreateServiceDefinition();
        var supported = new List<string>();

        // Act
        var result = _service.DetermineDefaultCrs(service, supported);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/OGC/1.3/CRS84", result);
    }

    [Fact]
    public void DetermineStorageCrs_WithSrid_ReturnsEpsgCrs()
    {
        // Arrange
        var layer = CreateLayerDefinition(Array.Empty<string>(), srid: 4326);

        // Act
        var result = _service.DetermineStorageCrs(layer);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/4326", result);
    }

    [Fact]
    public void DetermineStorageCrs_WithLayerCrs_ReturnsFirstLayerCrs()
    {
        // Arrange
        var layer = CreateLayerDefinition(new[] { "EPSG:3857", "EPSG:4326" });

        // Act
        var result = _service.DetermineStorageCrs(layer);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/3857", result);
    }

    [Fact]
    public void DetermineStorageCrs_WithNoCrsInfo_ReturnsDefaultWgs84()
    {
        // Arrange
        var layer = CreateLayerDefinition(Array.Empty<string>());

        // Act
        var result = _service.DetermineStorageCrs(layer);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/OGC/1.3/CRS84", result);
    }

    [Fact]
    public void ResolveAcceptCrs_WithNoHeader_ReturnsNull()
    {
        // Arrange
        var request = CreateHttpRequest();
        var supported = new List<string> { "http://www.opengis.net/def/crs/EPSG/0/4326" };

        // Act
        var (value, error) = _service.ResolveAcceptCrs(request, supported);

        // Assert
        Assert.Null(value);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveAcceptCrs_WithSupportedCrs_ReturnsCrs()
    {
        // Arrange
        var request = CreateHttpRequest(acceptCrs: "EPSG:4326");
        var supported = new List<string> { "http://www.opengis.net/def/crs/EPSG/0/4326" };

        // Act
        var (value, error) = _service.ResolveAcceptCrs(request, supported);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/4326", value);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveAcceptCrs_WithQualityValues_ReturnsHighestQuality()
    {
        // Arrange
        var request = CreateHttpRequest(acceptCrs: "EPSG:3857;q=0.8,EPSG:4326;q=1.0");
        var supported = new List<string>
        {
            "http://www.opengis.net/def/crs/EPSG/0/4326",
            "http://www.opengis.net/def/crs/EPSG/0/3857"
        };

        // Act
        var (value, error) = _service.ResolveAcceptCrs(request, supported);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/4326", value);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveAcceptCrs_WithUnsupportedCrs_Returns406()
    {
        // Arrange
        var request = CreateHttpRequest(acceptCrs: "EPSG:2154");
        var supported = new List<string> { "http://www.opengis.net/def/crs/EPSG/0/4326" };

        // Act
        var (value, error) = _service.ResolveAcceptCrs(request, supported);

        // Assert
        Assert.Null(value);
        Assert.NotNull(error);
    }

    [Fact]
    public void ResolveContentCrs_WithNullRequested_ReturnsDefaultCrs()
    {
        // Arrange
        var service = CreateServiceDefinition(defaultCrs: "EPSG:4326");
        var layer = CreateLayerDefinition(new[] { "EPSG:4326" });

        // Act
        var (value, error) = _service.ResolveContentCrs(null, service, layer);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/4326", value);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveContentCrs_WithSupportedCrs_ReturnsCrs()
    {
        // Arrange
        var service = CreateServiceDefinition();
        var layer = CreateLayerDefinition(new[] { "EPSG:4326", "EPSG:3857" });

        // Act
        var (value, error) = _service.ResolveContentCrs("EPSG:3857", service, layer);

        // Assert
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/3857", value);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveContentCrs_WithUnsupportedCrs_ReturnsError()
    {
        // Arrange
        var service = CreateServiceDefinition();
        var layer = CreateLayerDefinition(new[] { "EPSG:4326" });

        // Act
        var (value, error) = _service.ResolveContentCrs("EPSG:2154", service, layer);

        // Assert
        Assert.Equal(string.Empty, value);
        Assert.NotNull(error);
    }

    [Fact]
    public void FormatContentCrs_WithValue_ReturnsFormatted()
    {
        // Act
        var result = _service.FormatContentCrs("http://www.opengis.net/def/crs/EPSG/0/4326");

        // Assert
        Assert.Equal("<http://www.opengis.net/def/crs/EPSG/0/4326>", result);
    }

    [Fact]
    public void FormatContentCrs_WithNull_ReturnsEmpty()
    {
        // Act
        var result = _service.FormatContentCrs(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildDefaultCrs_WithServiceDefaultCrs_ReturnsConfiguredCrs()
    {
        // Arrange
        var service = CreateServiceDefinition(defaultCrs: "EPSG:3857");

        // Act
        var result = _service.BuildDefaultCrs(service);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/3857", result[0]);
    }

    [Fact]
    public void BuildDefaultCrs_WithNoServiceDefaultCrs_ReturnsWgs84()
    {
        // Arrange
        var service = CreateServiceDefinition();

        // Act
        var result = _service.BuildDefaultCrs(service);

        // Assert
        Assert.Single(result);
        Assert.Equal("http://www.opengis.net/def/crs/OGC/1.3/CRS84", result[0]);
    }

    // Helper methods
    private ServiceDefinition CreateServiceDefinition(string? defaultCrs = null, string[]? additionalCrs = null)
    {
        return new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            ServiceType = "OGC API",
            Ogc = new ServiceOgcDefinition
            {
                DefaultCrs = defaultCrs,
                AdditionalCrs = additionalCrs?.ToList() ?? new List<string>()
            }
        };
    }

    private LayerDefinition CreateLayerDefinition(string[] crs, int? srid = null)
    {
        return new LayerDefinition
        {
            Id = "test-layer",
            Title = "Test Layer",
            Crs = crs.ToList(),
            Storage = srid.HasValue ? new LayerStorageDefinition { Srid = srid.Value } : null
        };
    }

    private HttpRequest CreateHttpRequest(string? acceptCrs = null)
    {
        var context = new DefaultHttpContext();
        if (acceptCrs != null)
        {
            context.Request.Headers["Accept-Crs"] = new StringValues(acceptCrs);
        }
        return context.Request;
    }
}

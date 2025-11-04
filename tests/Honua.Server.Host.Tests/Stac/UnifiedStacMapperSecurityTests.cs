using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Stac;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

[Collection("HostTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
[Trait("Feature", "Security")]
[Trait("Speed", "Fast")]
public sealed class UnifiedStacMapperSecurityTests
{
    private readonly DefaultHttpContext _httpContext;

    public UnifiedStacMapperSecurityTests()
    {
        _httpContext = new DefaultHttpContext();
        _httpContext.Request.Scheme = "https";
        _httpContext.Request.Host = new HostString("example.com");
        _httpContext.Request.PathBase = "/api";
    }

    #region XSS Prevention Tests

    [Fact]
    public void CreateCollectionFromLayer_WithHtmlInTitle_SanitizesOutput()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "<script>alert('xss')</script>MyLayer",
            description: "Test layer");

        // Act
        var collection = UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);
        var json = collection.GetRawText();

        // Assert
        json.Should().NotContain("<script>");
        json.Should().Contain("&lt;script&gt;");
        json.Should().Contain("alert(&#39;xss&#39;)");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithJavaScriptInDescription_SanitizesOutput()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "javascript:void(0)");

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert - should throw because dangerous pattern detected after encoding
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous patterns*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithOnEventHandlerInKeywords_SanitizesOutput()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description",
            keywords: new[] { "normal", "onclick=alert(1)", "test" });

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous patterns*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithDataUriInProviderUrl_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Providers = new List<StacProviderDefinition>
        {
            new()
            {
                Name = "Test Provider",
                Url = "data:text/html,<script>alert(1)</script>"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous protocol*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithJavaScriptUrlInLink_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Links = new List<LayerLinkDefinition>
        {
            new()
            {
                Rel = "related",
                Href = "javascript:alert(document.cookie)",
                Title = "Malicious Link"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous protocol*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithXssInProviderDescription_SanitizesOutput()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Providers = new List<StacProviderDefinition>
        {
            new()
            {
                Name = "Test Provider",
                Description = "<img src=x onerror=alert('xss')>",
                Url = "https://example.com"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous patterns*");
    }

    [Fact]
    public void CreateCollectionFromRaster_WithScriptTagInTitle_SanitizesOutput()
    {
        // Arrange
        var raster = CreateTestRaster(
            title: "<script>alert('xss')</script>RasterData",
            description: "Test raster");

        // Act
        var collection = UnifiedStacMapper.CreateCollectionFromRaster(raster, _httpContext.Request);
        var json = collection.GetRawText();

        // Assert
        json.Should().NotContain("<script>");
        json.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithSvgXssInKeywords_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description",
            keywords: new[] { "<svg onload=alert(1)>" });

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous patterns*");
    }

    #endregion

    #region AdditionalProperties Validation Tests

    [Fact]
    public void CreateCollectionFromLayer_WithReservedKeyInAdditionalProperties_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.AdditionalProperties = new Dictionary<string, object>
        {
            ["id"] = "malicious-override",
            ["custom_field"] = "safe value"
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reserved STAC property 'id'*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithTypeOverrideInAdditionalProperties_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.AdditionalProperties = new Dictionary<string, object>
        {
            ["type"] = "MaliciousType"
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reserved STAC property 'type'*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithStacVersionOverrideInAdditionalProperties_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.AdditionalProperties = new Dictionary<string, object>
        {
            ["stac_version"] = "0.0.0-malicious"
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reserved STAC property 'stac_version'*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithXssInAdditionalPropertyValue_SanitizesOutput()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.AdditionalProperties = new Dictionary<string, object>
        {
            ["custom_field"] = "<script>alert('xss')</script>"
        };

        // Act
        var collection = UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);
        var json = collection.GetRawText();

        // Assert
        json.Should().NotContain("<script>");
        json.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithVeryLongAdditionalPropertyKey_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        var longKey = new string('a', 300); // 300 characters - exceeds 256 limit
        layer.Stac!.AdditionalProperties = new Dictionary<string, object>
        {
            [longKey] = "value"
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid additional property key*");
    }

    #endregion

    #region Asset Sanitization Tests

    [Fact]
    public void CreateCollectionFromLayer_WithXssInAssetTitle_SanitizesOutput()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Assets = new Dictionary<string, StacAssetDefinition>
        {
            ["thumbnail"] = new()
            {
                Title = "<img src=x onerror=alert(1)>",
                Type = "image/png",
                Href = "https://example.com/thumb.png"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous patterns*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithJavaScriptUrlInAssetHref_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Assets = new Dictionary<string, StacAssetDefinition>
        {
            ["data"] = new()
            {
                Title = "Data Asset",
                Type = "application/json",
                Href = "javascript:void(0)"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous protocol*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithXssInAssetRole_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Assets = new Dictionary<string, StacAssetDefinition>
        {
            ["data"] = new()
            {
                Title = "Data Asset",
                Type = "application/json",
                Href = "https://example.com/data.json",
                Roles = new List<string> { "data", "<script>alert(1)</script>" }
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dangerous patterns*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithReservedKeyInAssetAdditionalProperties_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Assets = new Dictionary<string, StacAssetDefinition>
        {
            ["data"] = new()
            {
                Title = "Data Asset",
                Type = "application/json",
                Href = "https://example.com/data.json",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["type"] = "malicious-override"
                }
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reserved STAC property 'type'*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithPathTraversalInAssetHref_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Assets = new Dictionary<string, StacAssetDefinition>
        {
            ["data"] = new()
            {
                Title = "Data Asset",
                Type = "application/json",
                Href = "https://example.com/data/../../etc/passwd"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithUrlEncodedPathTraversalInAssetHref_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Assets = new Dictionary<string, StacAssetDefinition>
        {
            ["data"] = new()
            {
                Title = "Data Asset",
                Type = "application/json",
                Href = "https://example.com/data/%2e%2e/sensitive"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithWindowsPathTraversalInAssetHref_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Assets = new Dictionary<string, StacAssetDefinition>
        {
            ["data"] = new()
            {
                Title = "Data Asset",
                Type = "application/json",
                Href = "https://example.com/data/..\\..\\windows\\system32"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithPathTraversalInLinkHref_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Links = new List<LayerLinkDefinition>
        {
            new()
            {
                Rel = "related",
                Href = "https://example.com/docs/../../../etc/passwd",
                Title = "Related Link"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithPathTraversalInProviderUrl_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Providers = new List<StacProviderDefinition>
        {
            new()
            {
                Name = "Test Provider",
                Url = "https://example.com/../../../sensitive",
                Description = "Provider with path traversal"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithS3PathTraversalInAssetHref_ThrowsException()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Assets = new Dictionary<string, StacAssetDefinition>
        {
            ["data"] = new()
            {
                Title = "Data Asset",
                Type = "application/geotiff",
                Href = "s3://bucket/../../../other-bucket/sensitive.tif"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*path traversal pattern*");
    }

    #endregion

    #region Safe Content Tests

    [Fact]
    public void CreateCollectionFromLayer_WithSafeHtmlEntities_HandlesCorrectly()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test & Sample Layer",
            description: "Data from <Company Name> with special chars: & < >");

        // Act
        var collection = UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);
        var json = collection.GetRawText();

        // Assert
        json.Should().Contain("&amp;");
        json.Should().Contain("&lt;");
        json.Should().Contain("&gt;");
        json.Should().NotContain("<Company Name>");
    }

    [Fact]
    public void CreateCollectionFromLayer_WithSafeUrls_AcceptsValidSchemes()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.Providers = new List<StacProviderDefinition>
        {
            new()
            {
                Name = "HTTP Provider",
                Url = "http://example.com"
            },
            new()
            {
                Name = "HTTPS Provider",
                Url = "https://example.com"
            },
            new()
            {
                Name = "FTP Provider",
                Url = "ftp://example.com"
            },
            new()
            {
                Name = "S3 Provider",
                Url = "s3://bucket/key"
            },
            new()
            {
                Name = "GCS Provider",
                Url = "gs://bucket/key"
            }
        };

        // Act
        var action = () => UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void CreateCollectionFromLayer_WithSafeAdditionalProperties_PreservesValues()
    {
        // Arrange
        var layer = CreateTestLayer(
            title: "Test Layer",
            description: "Test description");

        layer.Stac!.AdditionalProperties = new Dictionary<string, object>
        {
            ["custom_string"] = "Safe Value",
            ["custom_number"] = 42,
            ["custom_boolean"] = true,
            ["custom_array"] = new List<string> { "value1", "value2" }
        };

        // Act
        var collection = UnifiedStacMapper.CreateCollectionFromLayer(layer, CreateTestService(), _httpContext.Request);
        var jsonElement = collection;

        // Assert
        jsonElement.GetProperty("custom_string").GetString().Should().Be("Safe Value");
        jsonElement.GetProperty("custom_number").GetInt32().Should().Be(42);
        jsonElement.GetProperty("custom_boolean").GetBoolean().Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static LayerDefinition CreateTestLayer(
        string title,
        string description,
        string[]? keywords = null)
    {
        return new LayerDefinition
        {
            Id = "test-layer",
            Title = title,
            Description = description,
            Keywords = keywords?.ToList(),
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<List<double>> { new() { -180, -90, 180, 90 } }
            },
            Stac = new StacLayerDefinition
            {
                Enabled = true,
                CollectionId = "test-collection",
                License = "MIT"
            }
        };
    }

    private static RasterDatasetDefinition CreateTestRaster(
        string title,
        string description,
        string[]? keywords = null)
    {
        return new RasterDatasetDefinition
        {
            Id = "test-raster",
            Title = title,
            Description = description,
            Keywords = keywords?.ToList(),
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<List<double>> { new() { -180, -90, 180, 90 } }
            },
            Stac = new StacLayerDefinition
            {
                Enabled = true,
                CollectionId = "test-raster-collection",
                License = "MIT"
            }
        };
    }

    private static ServiceDefinition CreateTestService()
    {
        return new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            Description = "Test service for unit tests"
        };
    }

    #endregion
}

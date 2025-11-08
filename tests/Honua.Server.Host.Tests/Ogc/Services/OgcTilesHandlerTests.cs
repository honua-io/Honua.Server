// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Raster;
using Honua.Server.Host.Ogc.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

public class OgcTilesHandlerTests
{
    private readonly OgcTilesHandler _handler;

    public OgcTilesHandlerTests()
    {
        _handler = new OgcTilesHandler();
    }

    [Fact]
    public void ResolveTileSize_WithValidSize_ReturnsSize()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?tileSize=512");

        // Act
        var result = _handler.ResolveTileSize(httpContext.Request);

        // Assert
        Assert.Equal(512, result);
    }

    [Fact]
    public void ResolveTileSize_WithNoParameter_ReturnsDefault256()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var result = _handler.ResolveTileSize(httpContext.Request);

        // Assert
        Assert.Equal(256, result);
    }

    [Fact]
    public void ResolveTileSize_WithInvalidSize_ReturnsDefault256()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?tileSize=abc");

        // Act
        var result = _handler.ResolveTileSize(httpContext.Request);

        // Assert
        Assert.Equal(256, result);
    }

    [Fact]
    public void ResolveTileSize_WithNegativeSize_ReturnsDefault256()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?tileSize=-1");

        // Act
        var result = _handler.ResolveTileSize(httpContext.Request);

        // Assert
        Assert.Equal(256, result);
    }

    [Fact]
    public void ResolveTileSize_WithOversizedValue_ReturnsDefault256()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?tileSize=3000");

        // Act
        var result = _handler.ResolveTileSize(httpContext.Request);

        // Assert
        Assert.Equal(256, result);
    }

    [Fact]
    public void ResolveTileFormat_WithFormatParameter_ReturnsNormalizedFormat()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?format=png");

        // Act
        var result = _handler.ResolveTileFormat(httpContext.Request);

        // Assert
        Assert.Equal("png", result);
    }

    [Fact]
    public void ResolveTileFormat_WithFParameter_ReturnsNormalizedFormat()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?f=jpeg");

        // Act
        var result = _handler.ResolveTileFormat(httpContext.Request);

        // Assert
        Assert.Equal("jpeg", result);
    }

    [Fact]
    public void BuildTileMatrixSetSummary_WithValidParams_ReturnsObject()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.PathBase = "";

        // Act
        var result = _handler.BuildTileMatrixSetSummary(
            httpContext.Request,
            "WorldCRS84Quad",
            "http://www.opengis.net/def/tilematrixset/OGC/1.0/WorldCRS84Quad",
            "http://www.opengis.net/def/crs/OGC/1.3/CRS84");

        // Assert
        Assert.NotNull(result);
        var summary = result as dynamic;
        Assert.NotNull(summary);
        Assert.Equal("WorldCRS84Quad", summary.id);
        Assert.Equal("WorldCRS84Quad", summary.title);
    }

    [Fact]
    public void DatasetMatchesCollection_WithMatchingDataset_ReturnsTrue()
    {
        // Arrange
        var dataset = new RasterDatasetDefinition
        {
            Id = "test-dataset",
            ServiceId = "test-service",
            LayerId = "test-layer"
        };
        var service = new ServiceDefinition { Id = "test-service" };
        var layer = new LayerDefinition { Id = "test-layer" };

        // Act
        var result = _handler.DatasetMatchesCollection(dataset, service, layer);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DatasetMatchesCollection_WithDifferentService_ReturnsFalse()
    {
        // Arrange
        var dataset = new RasterDatasetDefinition
        {
            Id = "test-dataset",
            ServiceId = "different-service",
            LayerId = "test-layer"
        };
        var service = new ServiceDefinition { Id = "test-service" };
        var layer = new LayerDefinition { Id = "test-layer" };

        // Act
        var result = _handler.DatasetMatchesCollection(dataset, service, layer);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DatasetMatchesCollection_WithDifferentLayer_ReturnsFalse()
    {
        // Arrange
        var dataset = new RasterDatasetDefinition
        {
            Id = "test-dataset",
            ServiceId = "test-service",
            LayerId = "different-layer"
        };
        var service = new ServiceDefinition { Id = "test-service" };
        var layer = new LayerDefinition { Id = "test-layer" };

        // Act
        var result = _handler.DatasetMatchesCollection(dataset, service, layer);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DatasetMatchesCollection_WithEmptyServiceId_ReturnsFalse()
    {
        // Arrange
        var dataset = new RasterDatasetDefinition
        {
            Id = "test-dataset",
            ServiceId = "",
            LayerId = "test-layer"
        };
        var service = new ServiceDefinition { Id = "test-service" };
        var layer = new LayerDefinition { Id = "test-layer" };

        // Act
        var result = _handler.DatasetMatchesCollection(dataset, service, layer);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void NormalizeTileMatrixSet_WithWorldCRS84Quad_ReturnsNormalized()
    {
        // Act
        var result = _handler.NormalizeTileMatrixSet("WorldCRS84Quad");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("WorldCRS84Quad", result.Value.Id);
    }

    [Fact]
    public void NormalizeTileMatrixSet_WithWebMercatorQuad_ReturnsNormalized()
    {
        // Act
        var result = _handler.NormalizeTileMatrixSet("WebMercatorQuad");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("WebMercatorQuad", result.Value.Id);
    }

    [Fact]
    public void NormalizeTileMatrixSet_WithUnknownId_ReturnsNull()
    {
        // Act
        var result = _handler.NormalizeTileMatrixSet("UnknownTileMatrixSet");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryResolveStyle_WithValidStyle_ReturnsTrue()
    {
        // Arrange
        var dataset = new RasterDatasetDefinition
        {
            Id = "test-dataset",
            DefaultStyleId = "default-style",
            Styles = new List<RasterStyleReference>
            {
                new() { Id = "default-style", Title = "Default Style" }
            }
        };

        // Act
        var result = _handler.TryResolveStyle(dataset, "default-style", out var styleId, out var unresolvedStyle);

        // Assert
        Assert.True(result);
        Assert.Equal("default-style", styleId);
        Assert.Null(unresolvedStyle);
    }

    [Fact]
    public void TryResolveStyle_WithNullRequestedStyle_UsesDefault()
    {
        // Arrange
        var dataset = new RasterDatasetDefinition
        {
            Id = "test-dataset",
            DefaultStyleId = "default-style"
        };

        // Act
        var result = _handler.TryResolveStyle(dataset, null, out var styleId, out var unresolvedStyle);

        // Assert
        Assert.True(result);
        Assert.NotEmpty(styleId);
    }

    [Fact]
    public void RequiresVectorOverlay_WithNullStyle_ReturnsFalse()
    {
        // Act
        var result = _handler.RequiresVectorOverlay(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RequiresVectorOverlay_WithRasterStyle_ReturnsFalse()
    {
        // Arrange
        var style = new StyleDefinition { GeometryType = "raster" };

        // Act
        var result = _handler.RequiresVectorOverlay(style);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RequiresVectorOverlay_WithPointStyle_ReturnsTrue()
    {
        // Arrange
        var style = new StyleDefinition { GeometryType = "point" };

        // Act
        var result = _handler.RequiresVectorOverlay(style);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ResolveBounds_WithDatasetExtent_ReturnsDatasetBounds()
    {
        // Arrange
        var dataset = new RasterDatasetDefinition
        {
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } }
            }
        };
        var layer = new LayerDefinition();

        // Act
        var result = _handler.ResolveBounds(layer, dataset);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal(-180.0, result[0]);
        Assert.Equal(-90.0, result[1]);
        Assert.Equal(180.0, result[2]);
        Assert.Equal(90.0, result[3]);
    }

    [Fact]
    public void ResolveBounds_WithLayerExtent_ReturnsLayerBounds()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -100.0, -50.0, 100.0, 50.0 } }
            }
        };

        // Act
        var result = _handler.ResolveBounds(layer, null);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal(-100.0, result[0]);
        Assert.Equal(-50.0, result[1]);
        Assert.Equal(100.0, result[2]);
        Assert.Equal(50.0, result[3]);
    }

    [Fact]
    public void ResolveBounds_WithNoExtent_ReturnsGlobalBounds()
    {
        // Arrange
        var layer = new LayerDefinition();

        // Act
        var result = _handler.ResolveBounds(layer, null);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Equal(-180.0, result[0]);
        Assert.Equal(-90.0, result[1]);
        Assert.Equal(180.0, result[2]);
        Assert.Equal(90.0, result[3]);
    }

    [Fact]
    public async Task RenderVectorTileAsync_WithNullServiceId_ReturnsEmptyTile()
    {
        // Arrange
        var service = new ServiceDefinition { Id = "test-service" };
        var layer = new LayerDefinition { Id = "test-layer" };
        var dataset = new RasterDatasetDefinition { ServiceId = null };
        var bbox = new double[] { -180, -90, 180, 90 };
        var mockRepo = new Mock<IFeatureRepository>();

        // Act
        var result = await _handler.RenderVectorTileAsync(
            service, layer, dataset, bbox, 0, 0, 0, null, mockRepo.Object, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CollectVectorGeometriesAsync_WithEmptyBbox_ReturnsEmpty()
    {
        // Arrange
        var dataset = new RasterDatasetDefinition { ServiceId = "test" };
        var bbox = new double[] { };
        var mockRegistry = new Mock<IMetadataRegistry>();
        var mockRepo = new Mock<IFeatureRepository>();

        // Act
        var result = await _handler.CollectVectorGeometriesAsync(
            dataset, bbox, mockRegistry.Object, mockRepo.Object, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task CollectVectorGeometriesAsync_WithNullServiceId_ReturnsEmpty()
    {
        // Arrange
        var dataset = new RasterDatasetDefinition { ServiceId = null };
        var bbox = new double[] { -180, -90, 180, 90 };
        var mockSnapshot = new MetadataSnapshot
        {
            Catalog = new CatalogDefinition(),
            Services = new List<ServiceDefinition>()
        };
        var mockRepo = new Mock<IFeatureRepository>();

        // Act
        var result = await _handler.CollectVectorGeometriesAsync(
            dataset, bbox, mockSnapshot, mockRepo.Object, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}

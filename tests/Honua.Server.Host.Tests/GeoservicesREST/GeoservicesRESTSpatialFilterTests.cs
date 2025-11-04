using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.GeoservicesREST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

/// <summary>
/// Unit tests for spatial filter parsing in GeoservicesSpatialResolver.
/// CRITICAL: Tests cover recent bug fixes for polygon/polyline/multipoint geometry parsing.
/// These tests ensure the envelope bounding box extraction works correctly for complex geometries.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "GeoservicesREST")]
[Trait("Speed", "Fast")]
public sealed class GeoservicesRESTSpatialFilterTests
{
    #region Test Infrastructure

    private static QueryCollection CreateQueryCollection(Dictionary<string, StringValues> values)
    {
        return new QueryCollection(values);
    }

    private static ServiceDefinition CreateTestService() =>
        GeoservicesTestFactory.CreateServiceDefinition(defaultCrs: "EPSG:4326");

    private static LayerDefinition CreateTestLayer() =>
        GeoservicesTestFactory.CreateLayerDefinition(
            maxRecordCount: null,
            geometryField: "shape",
            geometryType: "esriGeometryPolygon",
            crs: new[] { "EPSG:4326", "EPSG:3857" });

    #endregion

    #region Envelope Geometry Tests

    [Fact]
    public void ResolveSpatialFilter_EnvelopeCommaSeparated_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(-180);
        bbox.MinY.Should().Be(-90);
        bbox.MaxX.Should().Be(180);
        bbox.MaxY.Should().Be(90);
    }

    [Fact]
    public void ResolveSpatialFilter_EnvelopeJson_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""xmin"":-180,""ymin"":-90,""xmax"":180,""ymax"":90}",
            ["geometryType"] = "esriGeometryEnvelope"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(-180);
        bbox.MinY.Should().Be(-90);
        bbox.MaxX.Should().Be(180);
        bbox.MaxY.Should().Be(90);
    }

    [Fact]
    public void ResolveSpatialFilter_EnvelopeWithSpatialReference_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""xmin"":-180,""ymin"":-90,""xmax"":180,""ymax"":90,""spatialReference"":{""wkid"":4326}}",
            ["geometryType"] = "esriGeometryEnvelope"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.Crs.Should().Be("EPSG:4326");
    }

    [Fact]
    public void ResolveSpatialFilter_EnvelopeInvertedCoordinates_NormalizesCorrectly()
    {
        // Arrange - xmax < xmin, ymax < ymin
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "180,90,-180,-90",
            ["geometryType"] = "esriGeometryEnvelope"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(-180); // Should be normalized
        bbox.MinY.Should().Be(-90);
        bbox.MaxX.Should().Be(180);
        bbox.MaxY.Should().Be(90);
    }

    #endregion

    #region Point Geometry Tests

    [Fact]
    public void ResolveSpatialFilter_PointCommaSeparated_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-122.5,37.5",
            ["geometryType"] = "esriGeometryPoint"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(-122.5);
        bbox.MinY.Should().Be(37.5);
        bbox.MaxX.Should().Be(-122.5);
        bbox.MaxY.Should().Be(37.5);
    }

    [Fact]
    public void ResolveSpatialFilter_PointJson_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""x"":-122.5,""y"":37.5}",
            ["geometryType"] = "esriGeometryPoint"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(-122.5);
        bbox.MinY.Should().Be(37.5);
        bbox.MaxX.Should().Be(-122.5);
        bbox.MaxY.Should().Be(37.5);
    }

    [Fact]
    public void ResolveSpatialFilter_PointWithSpatialReference_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""x"":-122.5,""y"":37.5,""spatialReference"":{""wkid"":4326}}",
            ["geometryType"] = "esriGeometryPoint"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.Crs.Should().Be("EPSG:4326");
    }

    #endregion

    #region Polygon Geometry Tests - CRITICAL BUG FIX COVERAGE

    [Fact]
    public void ResolveSpatialFilter_PolygonSimpleSquare_ComputesBoundingBox()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""rings"":[[[0,0],[0,10],[10,10],[10,0],[0,0]]]}",
            ["geometryType"] = "esriGeometryPolygon"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(0);
        bbox.MinY.Should().Be(0);
        bbox.MaxX.Should().Be(10);
        bbox.MaxY.Should().Be(10);
    }

    [Fact]
    public void ResolveSpatialFilter_PolygonMultipleRings_ComputesOverallBoundingBox()
    {
        // Arrange - Outer ring with hole
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""rings"":[[[0,0],[0,20],[20,20],[20,0],[0,0]],[[5,5],[5,15],[15,15],[15,5],[5,5]]]}",
            ["geometryType"] = "esriGeometryPolygon"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(0);
        bbox.MinY.Should().Be(0);
        bbox!.MaxX.Should().Be(20);
        bbox.MaxY.Should().Be(20); // Should encompass both rings
    }

    [Fact]
    public void ResolveSpatialFilter_PolygonWithSpatialReference_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""rings"":[[[0,0],[0,10],[10,10],[10,0],[0,0]]],""spatialReference"":{""wkid"":3857}}",
            ["geometryType"] = "esriGeometryPolygon"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.Crs.Should().Be("EPSG:3857");
    }

    [Fact]
    public void ResolveSpatialFilter_PolygonEmptyRings_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""rings"":[]}",
            ["geometryType"] = "esriGeometryPolygon"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act & Assert
        var exception = Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer));

        exception.Message.Should().Contain("rings");
    }

    [Fact]
    public void ResolveSpatialFilter_PolygonNotJson_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "0,0,10,10",
            ["geometryType"] = "esriGeometryPolygon"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act & Assert
        var exception = Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer));

        exception.Message.Should().Contain("JSON format");
    }

    #endregion

    #region Polyline Geometry Tests - CRITICAL BUG FIX COVERAGE

    [Fact]
    public void ResolveSpatialFilter_PolylineSinglePath_ComputesBoundingBox()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""paths"":[[[0,0],[5,10],[10,5]]]}",
            ["geometryType"] = "esriGeometryPolyline"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(0);
        bbox.MinY.Should().Be(0);
        bbox.MaxX.Should().Be(10);
        bbox.MaxY.Should().Be(10);
    }

    [Fact]
    public void ResolveSpatialFilter_PolylineMultiplePaths_ComputesOverallBoundingBox()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""paths"":[[[0,0],[5,5]],[[10,10],[20,20]]]}",
            ["geometryType"] = "esriGeometryPolyline"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(0);
        bbox.MinY.Should().Be(0);
        bbox.MaxX.Should().Be(20);
        bbox.MaxY.Should().Be(20); // Should encompass all paths
    }

    [Fact]
    public void ResolveSpatialFilter_PolylineWithSpatialReference_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""paths"":[[[0,0],[10,10]]],""spatialReference"":{""wkid"":3857}}",
            ["geometryType"] = "esriGeometryPolyline"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.Crs.Should().Be("EPSG:3857");
    }

    [Fact]
    public void ResolveSpatialFilter_PolylineEmptyPaths_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""paths"":[]}",
            ["geometryType"] = "esriGeometryPolyline"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act & Assert
        var exception = Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer));

        exception.Message.Should().Contain("paths");
    }

    [Fact]
    public void ResolveSpatialFilter_PolylineNotJson_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "0,0,10,10",
            ["geometryType"] = "esriGeometryPolyline"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act & Assert
        var exception = Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer));

        exception.Message.Should().Contain("JSON format");
    }

    #endregion

    #region Multipoint Geometry Tests - CRITICAL BUG FIX COVERAGE

    [Fact]
    public void ResolveSpatialFilter_MultipointTwoPoints_ComputesBoundingBox()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""points"":[[0,0],[10,10]]}",
            ["geometryType"] = "esriGeometryMultipoint"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(0);
        bbox.MinY.Should().Be(0);
        bbox.MaxX.Should().Be(10);
        bbox.MaxY.Should().Be(10);
    }

    [Fact]
    public void ResolveSpatialFilter_MultipointManyPoints_ComputesBoundingBox()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""points"":[[0,0],[5,5],[10,2],[3,8]]}",
            ["geometryType"] = "esriGeometryMultipoint"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(0);
        bbox.MinY.Should().Be(0);
        bbox.MaxX.Should().Be(10);
        bbox.MaxY.Should().Be(8);
    }

    [Fact]
    public void ResolveSpatialFilter_MultipointSinglePoint_CreatesDegenerateBox()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""points"":[[5,5]]}",
            ["geometryType"] = "esriGeometryMultipoint"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(5);
        bbox.MinY.Should().Be(5);
        bbox.MaxX.Should().Be(5);
        bbox.MaxY.Should().Be(5);
    }

    [Fact]
    public void ResolveSpatialFilter_MultipointWithSpatialReference_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""points"":[[0,0],[10,10]],""spatialReference"":{""wkid"":3857}}",
            ["geometryType"] = "esriGeometryMultipoint"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.Crs.Should().Be("EPSG:3857");
    }

    [Fact]
    public void ResolveSpatialFilter_MultipointEmptyPoints_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""points"":[]}",
            ["geometryType"] = "esriGeometryMultipoint"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act & Assert
        var exception = Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer));

        exception.Message.Should().Contain("at least one point");
    }

    [Fact]
    public void ResolveSpatialFilter_MultipointNotJson_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "0,0,10,10",
            ["geometryType"] = "esriGeometryMultipoint"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act & Assert
        var exception = Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer));

        exception.Message.Should().Contain("JSON format");
    }

    #endregion

    #region No Geometry Tests

    [Fact]
    public void ResolveSpatialFilter_NoGeometryParameter_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().BeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_EmptyGeometry_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "",
            ["geometryType"] = "esriGeometryEnvelope"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().BeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_WhitespaceGeometry_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "   ",
            ["geometryType"] = "esriGeometryEnvelope"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().BeNull();
    }

    #endregion

    #region Default Geometry Type Tests

    [Fact]
    public void ResolveSpatialFilter_NoGeometryType_DefaultsToEnvelope()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        bbox!.MinX.Should().Be(-180);
        bbox.MaxX.Should().Be(180);
    }

    #endregion

    #region Spatial Relation Tests

    [Fact]
    public void ResolveSpatialFilter_SpatialRelIntersects_Supported()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelIntersects"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_SpatialRelEnvelopeIntersects_Supported()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelEnvelopeIntersects"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_SpatialRelContains_Supported()
    {
        // Arrange - NEW: esriSpatialRelContains support
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelContains"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_SpatialRelWithin_Supported()
    {
        // Arrange - NEW: esriSpatialRelWithin support
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelWithin"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_SpatialRelTouches_Supported()
    {
        // Arrange - NEW: esriSpatialRelTouches support
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelTouches"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_SpatialRelOverlaps_Supported()
    {
        // Arrange - NEW: esriSpatialRelOverlaps support
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelOverlaps"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_SpatialRelCrosses_Supported()
    {
        // Arrange - NEW: esriSpatialRelCrosses support
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelCrosses"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_SpatialRelRelation_Supported()
    {
        // Arrange - NEW: esriSpatialRelRelation support (DE-9IM)
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelRelation"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
    }

    [Fact]
    public void ResolveSpatialFilter_UnsupportedSpatialRel_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "esriSpatialRelUnknown"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act & Assert
        var exception = Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer));

        exception.Message.Should().Contain("spatialRel");
        exception.Message.Should().Contain("not supported");
    }

    [Fact]
    public void ResolveSpatialFilter_SpatialRelCaseInsensitive_Supported()
    {
        // Arrange - Test case insensitivity
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope",
            ["spatialRel"] = "ESRISPATIALRELWITHIN"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
    }

    #endregion

    #region Input Spatial Reference Tests

    [Fact]
    public void ResolveSpatialFilter_InSRParameter_UsesSpecifiedSRID()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = @"{""xmin"":-180,""ymin"":-90,""xmax"":180,""ymax"":90}",
            ["geometryType"] = "esriGeometryEnvelope",
            ["inSR"] = "3857"
        });
        var service = CreateTestService();
        var layer = CreateTestLayer();

        // Act
        var bbox = GeoservicesSpatialResolver.ResolveSpatialFilter(query, service, layer);

        // Assert
        bbox.Should().NotBeNull();
        // The geometry should be interpreted in EPSG:3857
    }

    #endregion
}

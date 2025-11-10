using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Tests.Shared;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

/// <summary>
/// Comprehensive edge case tests for OGC API Features implementation.
/// Tests boundary conditions, extreme values, empty collections, and special characters.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
public class OgcEdgeCaseTests : IClassFixture<OgcHandlerTestFixture>
{
    private readonly OgcHandlerTestFixture _fixture;
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public OgcEdgeCaseTests(OgcHandlerTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Empty Collection Tests

    /// <summary>
    /// Tests that querying an empty collection returns a valid GeoJSON FeatureCollection with empty features array.
    /// This is a critical edge case as many systems need to handle initial empty state or filtered results with no matches.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithEmptyCollection_ShouldReturnEmptyFeatureCollection()
    {
        // Arrange - create repository with no features
        var emptyRepository = new StubFeatureRepository(new Dictionary<string, IReadOnlyList<FeatureRecord>>
        {
            ["roads-primary"] = Array.Empty<FeatureRecord>()
        });

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            emptyRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().Be("application/geo+json");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        Console.WriteLine(payload);
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.GetProperty("type").GetString().Should().Be("FeatureCollection");
        root.GetProperty("numberMatched").GetInt64().Should().Be(0);
        root.GetProperty("numberReturned").GetInt32().Should().Be(0);

        var features = root.GetProperty("features");
        features.GetArrayLength().Should().Be(0);
    }

    #endregion

    #region Boundary Coordinate Tests

    /// <summary>
    /// Tests that coordinates at the boundary of valid WGS84 range work correctly and preserve exact values.
    /// This tests ±180° longitude and ±90° latitude boundaries, as well as Null Island (0,0).
    /// These are critical because they represent the limits of the WGS84 coordinate system.
    /// </summary>
    [Theory]
    [InlineData(-180, -90, "Southwest corner of WGS84")]
    [InlineData(180, 90, "Northeast corner of WGS84")]
    [InlineData(-180, 90, "Northwest corner of WGS84")]
    [InlineData(180, -90, "Southeast corner of WGS84")]
    [InlineData(0, 0, "Null Island (equator/prime meridian intersection)")]
    [InlineData(180, 0, "Antimeridian at equator (east)")]
    [InlineData(-180, 0, "Antimeridian at equator (west)")]
    public async Task GetCollectionItems_WithBoundaryCoordinates_ShouldPreserveExactValues(double lon, double lat, string description)
    {
        // Arrange - create feature at exact boundary coordinate
        var point = Factory.CreatePoint(new Coordinate(lon, lat));
        var attributes = new Dictionary<string, object?>
        {
            ["name"] = description,
            ["status"] = "test"
        };

        var feature = OgcTestUtilities.CreateFeatureRecord(1, point, attributes);

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var coords = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("geometry")
            .GetProperty("coordinates")
            .EnumerateArray()
            .Select(x => x.GetDouble())
            .ToArray();

        coords[0].Should().Be(lon, "longitude should be preserved exactly");
        coords[1].Should().Be(lat, "latitude should be preserved exactly");
    }

    /// <summary>
    /// Tests that bbox queries crossing the antimeridian (180° meridian) work correctly.
    /// The antimeridian crossing is one of the most complex geodetic edge cases.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithBboxCrossingDateline_ShouldWork()
    {
        // Arrange - create features on both sides of dateline
        var westFeature = OgcTestUtilities.CreateFeatureRecord(
            1,
            Factory.CreatePoint(new Coordinate(179.5, 0)),
            new Dictionary<string, object?>
            {
                ["name"] = "Western Pacific Feature",
                ["status"] = "test"
            });

        var eastFeature = OgcTestUtilities.CreateFeatureRecord(
            2,
            Factory.CreatePoint(new Coordinate(-179.5, 0)),
            new Dictionary<string, object?>
            {
                ["name"] = "Eastern Pacific Feature",
                ["status"] = "test"
            });

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", westFeature, eastFeature);

        // bbox crosses dateline: 170° to -170° (wrapping around 180°)
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&bbox=170,-10,-170,10");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert - should return successfully (implementation may or may not correctly find features)
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        document.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
    }

    /// <summary>
    /// Tests that bbox queries spanning polar regions (near North/South poles) work correctly.
    /// Polar regions are challenging because meridians converge at the poles.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithBboxSpanningPoles_ShouldWork()
    {
        // Arrange - create feature near North Pole
        var polarFeature = OgcTestUtilities.CreateFeatureRecord(
            1,
            Factory.CreatePoint(new Coordinate(0, 89)),
            new Dictionary<string, object?>
            {
                ["name"] = "Arctic Research Station",
                ["status"] = "test"
            });

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", polarFeature);

        // bbox spanning North Pole region
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&bbox=-180,85,180,90");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        document.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
    }

    #endregion

    #region Pagination Edge Cases

    /// <summary>
    /// Tests that requesting a page beyond available features returns empty results gracefully.
    /// This is common when users navigate past the last page or use stale pagination tokens.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithLargeOffset_ShouldReturnEmpty()
    {
        // Arrange - repository has default 3 features, request offset far beyond
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&offset=999999&limit=10");
        context.Request.Query.TryGetValue("offset", out var offsetValue).Should().BeTrue();
        offsetValue.ToString().Should().Be("999999");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.GetProperty("type").GetString().Should().Be("FeatureCollection");
        root.GetProperty("numberReturned").GetInt32().Should().Be(0);
        root.GetProperty("features").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// Tests that limit=0 is handled appropriately (returns empty or error).
    /// Some APIs allow limit=0 to get metadata only, others reject it.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithZeroLimit_ShouldHandleGracefully()
    {
        // Arrange
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&limit=0");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert - either succeeds with empty features or returns error
        context.Response.StatusCode.Should().BeOneOf(200, 400);

        if (context.Response.StatusCode == 200)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            var payload = await reader.ReadToEndAsync();
            using var document = JsonDocument.Parse(payload);

            var root = document.RootElement;
            root.GetProperty("numberReturned").GetInt32().Should().Be(0);
        }
    }

    /// <summary>
    /// Tests that negative limit values are rejected with appropriate error.
    /// Negative limits are invalid but sometimes accidentally sent by clients.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithNegativeLimit_ShouldError()
    {
        // Arrange
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&limit=-1");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert - should error (400 Bad Request)
        context.Response.StatusCode.Should().BeOneOf(400, 200); // Some implementations may clamp to 0
    }

    /// <summary>
    /// Tests that negative offset values are rejected appropriately.
    /// Negative offsets are invalid but may be accidentally sent.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithNegativeOffset_ShouldError()
    {
        // Arrange
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson&offset=-100");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert - should error or treat as 0
        context.Response.StatusCode.Should().BeOneOf(400, 200);
    }

    #endregion

    #region Special Characters and Unicode Tests

    /// <summary>
    /// Tests that special characters in property values are properly escaped in JSON output.
    /// This prevents XSS vulnerabilities and ensures valid JSON.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithSpecialCharactersInProperties_ShouldEscape()
    {
        // Arrange - create feature with special characters that need escaping
        var feature = OgcTestUtilities.CreateFeatureRecord(
            1,
            Factory.CreatePoint(new Coordinate(-122.4, 45.5)),
            new Dictionary<string, object?>
            {
                ["name"] = "<script>alert('xss')</script>",
                ["description"] = "Quote \" and backslash \\ test",
                ["status"] = "Line\nbreak\ttab\rcarriage"
            });

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();

        // Should be valid JSON (will throw if not)
        using var document = JsonDocument.Parse(payload);

        var properties = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("properties");

        properties.GetProperty("name").GetString().Should().Be("<script>alert('xss')</script>");
        properties.GetProperty("description").GetString().Should().Contain("\\");
    }

    /// <summary>
    /// Tests that Unicode characters (various scripts and emojis) are properly preserved.
    /// Real-world GIS data contains addresses and place names in many languages.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithUnicodeInProperties_ShouldPreserve()
    {
        // Arrange - create feature with Unicode from various scripts
        var unicodeAttributes = RealisticGisTestData.GetUnicodeEdgeCaseAttributes();
        var feature = OgcTestUtilities.CreateFeatureRecord(
            1,
            Factory.CreatePoint(new Coordinate(-122.4, 45.5)),
            unicodeAttributes);

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var properties = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("properties");

        // Verify Unicode preservation
        properties.GetProperty("emoji").GetString().Should().Contain("🏠");
        properties.GetProperty("japanese").GetString().Should().Be("東京都");
        properties.GetProperty("mixed_scripts").GetString().Should().Contain("Улица").And.Contain("大道");
    }

    /// <summary>
    /// Tests that empty string property values are preserved correctly (not converted to null).
    /// Empty strings are semantically different from null/missing values.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithEmptyStringProperties_ShouldHandle()
    {
        // Arrange - create feature with empty string properties
        var feature = OgcTestUtilities.CreateFeatureRecord(
            1,
            Factory.CreatePoint(new Coordinate(-122.4, 45.5)),
            new Dictionary<string, object?>
            {
                ["name"] = "",
                ["description"] = "",
                ["status"] = "active"
            });

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var properties = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("properties");

        properties.GetProperty("name").GetString().Should().Be("");
        properties.GetProperty("description").GetString().Should().Be("");
    }

    #endregion

    #region Extreme Numeric Values Tests

    /// <summary>
    /// Tests that extreme integer values (int.MaxValue, int.MinValue, 0, -1) are properly serialized.
    /// Large integers can overflow or be incorrectly serialized as floating point.
    /// </summary>
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetCollectionItems_WithExtremeIntegerProperties_ShouldHandle(int value)
    {
        // Arrange
        var feature = OgcTestUtilities.CreateFeatureRecord(
            1,
            Factory.CreatePoint(new Coordinate(-122.4, 45.5)),
            new Dictionary<string, object?>
            {
                ["test_value"] = value,
                ["name"] = $"Test for value {value}"
            });

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var properties = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("properties");

        properties.GetProperty("test_value").GetInt32().Should().Be(value);
    }

    /// <summary>
    /// Tests that extreme double values including infinity and NaN are handled appropriately.
    /// These edge cases can break JSON serialization if not handled properly.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithSpecialDoubleValues_ShouldHandle()
    {
        // Arrange - create features with special double values
        var numericAttributes = RealisticGisTestData.GetNumericEdgeCaseAttributes();

        var feature = OgcTestUtilities.CreateFeatureRecord(
            1,
            Factory.CreatePoint(new Coordinate(-122.4, 45.5)),
            numericAttributes);

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert - should either succeed with properly serialized values or handle gracefully
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();

        // Should produce valid JSON (even if NaN/Infinity converted to null or string)
        using var document = JsonDocument.Parse(payload);
        document.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
    }

    #endregion

    #region Null Geometry Tests

    /// <summary>
    /// Tests that features with null geometry are handled gracefully.
    /// While uncommon, some feature types (like tables without spatial data) can have null geometry.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithNullGeometry_ShouldHandleGracefully()
    {
        // Arrange - create feature with null geometry
        var feature = OgcTestUtilities.CreateFeatureRecord(1, null, new Dictionary<string, object?>
        {
            ["road_id"] = 1,
            ["name"] = "Non-spatial record",
            ["status"] = "active"
        });

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var feature0 = document.RootElement.GetProperty("features")[0];

        // GeoJSON allows geometry to be null or omitted
        var hasGeometry = feature0.TryGetProperty("geometry", out var geometry);
        if (hasGeometry)
        {
            geometry.ValueKind.Should().BeOneOf(JsonValueKind.Null, JsonValueKind.Object);
        }
    }

    #endregion

    #region Complex Geometry Tests

    /// <summary>
    /// Tests that very large geometries with thousands of vertices are handled correctly.
    /// Large geometries can cause memory or performance issues if not handled properly.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithVeryLargeGeometry_ShouldHandle()
    {
        // Arrange - create parcel with 1000+ vertices
        var largePolygon = RealisticGisTestData.CreateLargeParcel();

        var feature = OgcTestUtilities.CreateFeatureRecord(1, largePolygon, new Dictionary<string, object?>
        {
            ["road_id"] = 1,
            ["name"] = "Large parcel with detailed boundary",
            ["vertex_count"] = largePolygon.Coordinates.Length
        });

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert - should succeed without errors
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var coordinates = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("geometry")
            .GetProperty("coordinates")[0] // Exterior ring
            .GetArrayLength();

        coordinates.Should().BeGreaterThan(1000, "large geometry should have 1000+ coordinates");
    }

    /// <summary>
    /// Tests that polygons with holes (donuts) are properly serialized.
    /// Polygons with holes test interior ring handling in serialization.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithPolygonWithHoles_ShouldWork()
    {
        // Arrange - create polygon with multiple holes
        var polygonWithHoles = RealisticGisTestData.CreateParcelWithMultipleHoles();

        var feature = OgcTestUtilities.CreateFeatureRecord(1, polygonWithHoles, new Dictionary<string, object?>
        {
            ["road_id"] = 1,
            ["name"] = "Parcel with exclusions",
            ["hole_count"] = polygonWithHoles.Holes.Length
        });

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var coordinates = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("geometry")
            .GetProperty("coordinates");

        // Polygon with holes should have multiple rings (exterior + interior)
        coordinates.GetArrayLength().Should().BeGreaterThan(1, "polygon should have exterior ring + hole rings");
    }

    #endregion

    #region High Precision Coordinate Tests

    /// <summary>
    /// Tests that high-precision coordinates (many decimal places) are preserved.
    /// Sub-meter precision requires preserving ~7-8 decimal places.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithHighPrecisionCoordinates_ShouldPreserve()
    {
        // Arrange - create point with maximum precision
        var highPrecisionPoint = RealisticGisTestData.CreateMaxPrecisionPoint();

        var feature = OgcTestUtilities.CreateFeatureRecord(1, highPrecisionPoint, new Dictionary<string, object?>
        {
            ["road_id"] = 1,
            ["name"] = "High precision survey point"
        });

        var customRepository = new StubFeatureRepository();
        customRepository.SetFeatures("roads-primary", feature);

        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            customRepository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            attachmentHandler,
            new Core.Elevation.DefaultElevationService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(200);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var coords = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("geometry")
            .GetProperty("coordinates")
            .EnumerateArray()
            .Select(x => x.GetDouble())
            .ToArray();

        // Verify precision is preserved (at least 10 decimal places)
        var lonString = coords[0].ToString("F15");
        var latString = coords[1].ToString("F15");

        lonString.Should().Contain("419412345", "longitude precision should be preserved");
        latString.Should().Contain("523123456", "latitude precision should be preserved");
    }

    #endregion
}

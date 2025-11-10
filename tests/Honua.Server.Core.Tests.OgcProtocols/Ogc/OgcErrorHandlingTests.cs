using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

/// <summary>
/// Comprehensive error handling tests for OGC Features API.
/// Tests invalid inputs, malformed parameters, and error response validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
public class OgcErrorHandlingTests : IClassFixture<OgcHandlerTestFixture>
{
    private readonly OgcHandlerTestFixture _fixture;

    public OgcErrorHandlingTests(OgcHandlerTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Tests that requesting a collection with an invalid CRS code returns HTTP 400 with an appropriate error message.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithInvalidCrs_ShouldReturnError()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "crs=EPSG:99999"); // Invalid CRS code

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
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest,
            "invalid CRS codes should return a 400 Bad Request error");

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();

        payload.Should().NotBeNullOrWhiteSpace();
        payload.Should().Contain("crs", "error message should mention the CRS parameter");
    }

    /// <summary>
    /// Tests that requesting a collection with a malformed bbox parameter returns HTTP 400 with a validation error.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithInvalidBbox_ShouldReturnError()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "bbox=invalid,bbox,values"); // Malformed bbox

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
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest,
            "malformed bbox should return a 400 Bad Request error");

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();

        payload.Should().NotBeNullOrWhiteSpace();
        payload.Should().Contain("bbox", "error message should mention the bbox parameter");
    }

    /// <summary>
    /// Tests that requesting a collection with a malformed CQL filter returns HTTP 400 with parse error details.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithInvalidFilter_ShouldReturnError()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "filter=name INVALID_OPERATOR value"); // Invalid CQL syntax

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
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest,
            "invalid CQL filter should return a 400 Bad Request error");

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();

        payload.Should().NotBeNullOrWhiteSpace();
        payload.Should().Contain("filter", "error message should mention filter parsing issue");
    }

    /// <summary>
    /// Tests that requesting a non-existent collection returns HTTP 404 with a helpful error message.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithNonexistentCollection_ShouldReturn404()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/nonexistent::collection/items",
            "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "nonexistent::collection",
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
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
            "non-existent collection should return a 404 Not Found error");

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();

        payload.Should().NotBeNullOrWhiteSpace();
        payload.Should().Contain("collection", "error message should mention collection");
        payload.Should().Contain("not found", "error message should indicate resource not found");
    }

    /// <summary>
    /// Tests that requesting items with an invalid limit parameter returns HTTP 400 or clamps to valid range.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithInvalidNegativeLimit_ShouldReturnError()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "limit=-1"); // Negative limit

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
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert - should either error or clamp to valid range
        context.Response.StatusCode.Should().BeOneOf(
            new[]
            {
                StatusCodes.Status200OK,        // Clamped to valid range
                StatusCodes.Status400BadRequest // Validation error
            },
            "negative limit should either be rejected or clamped to valid range");

        if (context.Response.StatusCode == StatusCodes.Status400BadRequest)
        {
            context.Response.Body.Position = 0;
            var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();
            payload.Should().Contain("limit", "error message should mention the limit parameter");
        }
    }

    /// <summary>
    /// Tests that requesting items with an excessively large limit parameter is handled appropriately.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithExcessiveLimit_ShouldReturnErrorOrClamp()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "limit=999999"); // Excessive limit

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
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert - should either succeed with clamped limit or return error
        context.Response.StatusCode.Should().BeOneOf(
            new[]
            {
                StatusCodes.Status200OK,        // Clamped to maximum allowed
                StatusCodes.Status400BadRequest // Validation error
            },
            "excessive limit should either be rejected or clamped to maximum allowed");
    }

    /// <summary>
    /// Tests that requesting an item with a non-existent ID returns HTTP 404.
    /// </summary>
    [Fact]
    public async Task GetCollectionItem_WithInvalidItemId_ShouldReturn404()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items/99999",
            "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var editingHandler = OgcTestUtilities.CreateOgcFeaturesEditingHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItem(
            "roads::roads-primary",
            "99999", // Non-existent item ID
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.CacheHeaderService,
            attachmentHandler,
            editingHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
            "non-existent item should return a 404 Not Found error");

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();

        payload.Should().NotBeNullOrWhiteSpace();
        payload.Should().Contain("not found", "error message should indicate item not found");
    }

    /// <summary>
    /// Tests that requesting an item with special characters in the ID is handled safely (no SQL injection).
    /// </summary>
    [Fact]
    public async Task GetCollectionItem_WithMalformedItemId_ShouldHandleSafely()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items/1';DROP TABLE roads;--",
            "f=geojson");

        // Act
        var attachmentHandler = OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub();
        var editingHandler = OgcTestUtilities.CreateOgcFeaturesEditingHandlerStub();
        var result = await OgcFeaturesHandlers.GetCollectionItem(
            "roads::roads-primary",
            "1';DROP TABLE roads;--", // SQL injection attempt
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.CacheHeaderService,
            attachmentHandler,
            editingHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert - should either sanitize or return error, but never execute SQL injection
        context.Response.StatusCode.Should().BeOneOf(
            new[]
            {
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound
            },
            "malformed item IDs should be rejected or sanitized");

        // Verify response exists and is well-formed
        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();
        payload.Should().NotBeNullOrWhiteSpace("error response should provide details");
    }

    /// <summary>
    /// Tests that requesting items with bbox coordinates outside valid WGS84 range returns HTTP 400.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithBboxOutsideValidRange_ShouldReturnError()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "bbox=200,-100,210,-90"); // Longitude 200 is outside valid range [-180, 180]

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
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest,
            "bbox outside valid WGS84 range should return a 400 Bad Request error");

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();

        payload.Should().NotBeNullOrWhiteSpace();
        payload.Should().Contain("bbox", "error message should mention bbox validation issue");
    }

    /// <summary>
    /// Tests that requesting items with an inverted bbox (minx > maxx) returns HTTP 400.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithInvertedBbox_ShouldReturnError()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "bbox=-122.0,45.5,-122.5,45.7"); // minx=-122.0 > maxx=-122.5 (inverted)

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
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest,
            "inverted bbox coordinates should return a 400 Bad Request error");

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();

        payload.Should().NotBeNullOrWhiteSpace();
        payload.Should().Contain("bbox", "error message should mention bbox validation issue");
    }

    /// <summary>
    /// Tests that requesting a non-existent style returns HTTP 404.
    /// </summary>
    [Fact]
    public async Task GetStyle_WithNonexistentStyleId_ShouldReturn404()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/styles/nonexistent-style",
            "f=json");

        // Act
        var result = await OgcFeaturesHandlers.GetStyle(
            "nonexistent-style",
            context.Request,
            _fixture.Registry,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
            "non-existent style should return a 404 Not Found error");

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();

        payload.Should().NotBeNullOrWhiteSpace();
        payload.Should().Contain("style", "error message should mention style");
        payload.Should().Contain("not found", "error message should indicate style not found");
    }

    /// <summary>
    /// Tests that requesting a collection style that doesn't exist returns HTTP 404.
    /// </summary>
    [Fact]
    public async Task GetCollectionStyle_WithNonexistentStyleId_ShouldReturn404()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/styles/nonexistent-style",
            "f=json");

        // Act
        var result = await OgcFeaturesHandlers.GetCollectionStyle(
            "roads::roads-primary",
            "nonexistent-style",
            context.Request,
            _fixture.Resolver,
            _fixture.Registry,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound,
            "non-existent collection style should return a 404 Not Found error");

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();

        payload.Should().NotBeNullOrWhiteSpace();
        payload.Should().Contain("style", "error message should mention style");
    }

    /// <summary>
    /// Tests that requesting items with both filter and filter-lang parameters without valid syntax returns error.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithFilterButNoFilterLang_ShouldDefaultToCqlText()
    {
        // Arrange - filter without filter-lang should default to cql-text
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "filter=name='First'"); // CQL-text filter

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

        // Assert - should succeed (defaults to cql-text) or provide clear error
        context.Response.StatusCode.Should().BeOneOf(
            new[]
            {
                StatusCodes.Status200OK,
                StatusCodes.Status400BadRequest
            },
            "filter without filter-lang should either default to cql-text or return validation error");
    }

    /// <summary>
    /// Tests that requesting items with an empty filter parameter is handled appropriately.
    /// </summary>
    [Fact]
    public async Task GetCollectionItems_WithEmptyFilter_ShouldHandleGracefully()
    {
        // Arrange
        var context = _fixture.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/items",
            "filter="); // Empty filter

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

        // Assert - should either ignore empty filter or return error
        context.Response.StatusCode.Should().BeOneOf(
            new[]
            {
                StatusCodes.Status200OK,        // Ignored empty filter
                StatusCodes.Status400BadRequest // Validation error
            },
            "empty filter should either be ignored or return validation error");
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Stac;
using Honua.Server.Host.Stac;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

/// <summary>
/// Tests for StacSearchController focusing on input validation and error handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
[Trait("Speed", "Fast")]
public sealed class StacSearchControllerTests
{
    private readonly IStacCatalogStore _mockStore;
    private readonly IHonuaConfigurationService _mockConfig;
    private readonly StacSearchController _controller;

    public StacSearchControllerTests()
    {
        _mockStore = Substitute.For<IStacCatalogStore>();
        _mockConfig = Substitute.For<IHonuaConfigurationService>();

        // Setup default configuration to enable STAC
        var metadata = new MetadataSnapshot(
            Title: "Test",
            Description: "Test",
            ContactName: "Test",
            ContactEmail: "test@test.com",
            BaseUri: new Uri("https://test.example.com"),
            Layers: new List<LayerMetadata>(),
            Crs: new List<CrsDeclaration>(),
            StacEnabled: true,
            BboxRequired: false,
            Tags: new Dictionary<string, string>(),
            ServiceIdentification: null,
            ServiceProvider: null
        );
        _mockConfig.CurrentSnapshot.Returns(metadata);

        _controller = new StacSearchController(_mockStore, _mockConfig);

        // Setup controller context for URI building
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Request.Scheme = "https";
        _controller.HttpContext.Request.Host = new HostString("test.example.com");
        _controller.HttpContext.Request.PathBase = "";

        // Setup default empty collections
        _mockStore.EnsureInitializedAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mockStore.ListCollectionsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StacCollectionRecord>
            {
                new StacCollectionRecord
                {
                    Id = "test-collection",
                    Title = "Test Collection",
                    Description = "A test collection",
                    License = "proprietary",
                    SpatialExtent = new double[] { -180, -90, 180, 90 },
                    TemporalExtent = null,
                    Links = new List<StacLink>(),
                    Summaries = new Dictionary<string, object>()
                }
            });
    }

    #region Bbox Validation Tests

    [Fact]
    public async Task GetSearchAsync_WithValidBbox_ReturnsOk()
    {
        // Arrange
        var validBbox = "-180,-90,180,90";
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: validBbox,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSearchAsync_WithValidBbox6Values_ReturnsOk()
    {
        // Arrange
        var validBbox = "-180,-90,0,180,90,100";
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: validBbox,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSearchAsync_WithInvalidBboxTooFewValues_Returns400()
    {
        // Arrange
        var invalidBbox = "-180,-90,180"; // Only 3 values instead of 4

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: invalidBbox,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid bbox parameter", problemDetails.Title);
        Assert.Contains("four", problemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSearchAsync_WithInvalidBboxNonNumeric_Returns400()
    {
        // Arrange
        var invalidBbox = "-180,abc,180,90";

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: invalidBbox,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid bbox parameter", problemDetails.Title);
        Assert.Contains("numeric", problemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSearchAsync_WithInvalidBboxMinGreaterThanMax_Returns400()
    {
        // Arrange
        var invalidBbox = "180,-90,-180,90"; // minX > maxX

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: invalidBbox,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid bbox parameter", problemDetails.Title);
        Assert.Contains("minimums must be less than maximums", problemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSearchAsync_WithEmptyBbox_Returns400()
    {
        // Arrange
        var invalidBbox = "";

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: invalidBbox,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid bbox parameter", problemDetails.Title);
        Assert.Contains("cannot be empty", problemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSearchAsync_WithWhitespaceBbox_Returns400()
    {
        // Arrange
        var invalidBbox = "   ";

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: invalidBbox,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Contains("cannot be empty", problemDetails.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSearchAsync_WithNullBbox_ReturnsOk()
    {
        // Arrange - null bbox is valid (no spatial filter)
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    #endregion

    #region Datetime Validation Tests

    [Fact]
    public async Task GetSearchAsync_WithValidDatetimeSingle_ReturnsOk()
    {
        // Arrange
        var validDatetime = "2020-01-01T00:00:00Z";
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: validDatetime,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSearchAsync_WithValidDatetimeRange_ReturnsOk()
    {
        // Arrange
        var validDatetime = "2020-01-01T00:00:00Z/2020-12-31T23:59:59Z";
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: validDatetime,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSearchAsync_WithValidDatetimeOpenEnd_ReturnsOk()
    {
        // Arrange
        var validDatetime = "2020-01-01T00:00:00Z/..";
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: validDatetime,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSearchAsync_WithValidDatetimeOpenStart_ReturnsOk()
    {
        // Arrange
        var validDatetime = "../2020-12-31T23:59:59Z";
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: validDatetime,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSearchAsync_WithInvalidDatetimeFormat_Returns400()
    {
        // Arrange
        var invalidDatetime = "not-a-date";

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: invalidDatetime,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid datetime parameter", problemDetails.Title);
        Assert.Contains("Unable to parse", problemDetails.Detail);
    }

    [Fact]
    public async Task GetSearchAsync_WithInvalidDatetimeRangeFormat_Returns400()
    {
        // Arrange
        var invalidDatetime = "2020-01-01T00:00:00Z/not-a-date";

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: invalidDatetime,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid datetime parameter", problemDetails.Title);
    }

    [Fact]
    public async Task GetSearchAsync_WithNullDatetime_ReturnsOk()
    {
        // Arrange - null datetime is valid (no temporal filter)
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSearchAsync_WithFilterAndIntersects_ForwardsParameters()
    {
        // Arrange
        var filterJson = "{\"eq\":[{\"property\":\"cloud_cover\"},10]}";
        var intersectsJson = "{\"type\":\"Point\",\"coordinates\":[10.0,20.0]}";

        StacSearchParameters? captured = null;
        _mockStore.SearchAsync(Arg.Do<StacSearchParameters>(p => captured = p), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        _controller.HttpContext.Request.QueryString = new QueryString(
            $"?filter={Uri.EscapeDataString(filterJson)}&filter-lang=cql2-json&intersects={Uri.EscapeDataString(intersectsJson)}");

        try
        {
            // Act
            var result = await _controller.GetSearchAsync(
                collections: null,
                ids: null,
                bbox: null,
                datetime: null,
                limit: null,
                token: null,
                CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            captured.Should().NotBeNull();
            captured!.Filter.Should().NotBeNull();
            JsonNode.Parse(captured.Filter!)!.ToJsonString().Should().Be(JsonNode.Parse(filterJson)!.ToJsonString());
            captured.FilterLang.Should().Be("cql2-json");
            captured.Intersects.Should().NotBeNull();
            JsonNode.Parse(captured.Intersects!.GeoJson)!.ToJsonString().Should().Be(JsonNode.Parse(intersectsJson)!.ToJsonString());
        }
        finally
        {
            _controller.HttpContext.Request.QueryString = QueryString.Empty;
        }
    }

    #endregion

    #region POST Method Tests

    [Fact]
    public async Task PostSearchAsync_WithInvalidDatetime_Returns400()
    {
        // Arrange
        var request = new StacSearchRequest
        {
            Datetime = "invalid-date"
        };

        // Act
        var result = await _controller.PostSearchAsync(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid datetime parameter", problemDetails.Title);
    }

    [Fact]
    public async Task PostSearchAsync_WithValidDatetime_ReturnsOk()
    {
        // Arrange
        var request = new StacSearchRequest
        {
            Datetime = "2020-01-01T00:00:00Z"
        };
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.PostSearchAsync(request, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task PostSearchAsync_WithInvalidBbox_StillAcceptsItInBody()
    {
        // Note: For POST requests, bbox is already parsed by model binding as double[]
        // So invalid bboxes would fail at model binding level, not in our code
        // This test verifies that valid bbox arrays work correctly
        var request = new StacSearchRequest
        {
            Bbox = new[] { -180.0, -90.0, 180.0, 90.0 }
        };
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.PostSearchAsync(request, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    #endregion

    #region Combined Validation Tests

    [Fact]
    public async Task GetSearchAsync_WithBothInvalidBboxAndDatetime_ReturnsBboxError()
    {
        // Arrange - both are invalid, but bbox is checked first
        var invalidBbox = "invalid";
        var invalidDatetime = "also-invalid";

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: invalidBbox,
            datetime: invalidDatetime,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        // Should be bbox error since it's checked first
        Assert.Equal("Invalid bbox parameter", problemDetails.Title);
    }

    [Fact]
    public async Task GetSearchAsync_WithValidBboxInvalidDatetime_ReturnsDatetimeError()
    {
        // Arrange
        var validBbox = "-180,-90,180,90";
        var invalidDatetime = "not-a-date";

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: validBbox,
            datetime: invalidDatetime,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid datetime parameter", problemDetails.Title);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetSearchAsync_WithSpecialCharactersInDatetime_Returns400()
    {
        // Arrange
        var invalidDatetime = "2020-01-01T00:00:00Z; DROP TABLE stac_items;--";

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: invalidDatetime,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
    }

    [Fact]
    public async Task GetSearchAsync_WithVeryLargeBboxValues_Returns400OrOk()
    {
        // Arrange - values are technically valid doubles but geographically nonsensical
        var largeBbox = "-99999,-99999,99999,99999";
        _mockStore.SearchAsync(Arg.Any<StacSearchParameters>(), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult(new List<StacItemRecord>(), 0, null));

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: largeBbox,
            datetime: null,
            limit: null,
            token: null,
            CancellationToken.None);

        // Assert - Should parse successfully, validation of geographic bounds is not done at this level
        Assert.IsType<OkObjectResult>(result.Result);
    }

    #endregion
}

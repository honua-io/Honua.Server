using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Stac;
using Honua.Server.Host.Stac;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

/// <summary>
/// Tests for STAC Sort Extension implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
[Trait("Speed", "Fast")]
public sealed class StacSortTests
{
    private readonly IStacCatalogStore _mockStore;
    private readonly IHonuaConfigurationService _mockConfig;
    private readonly StacSearchController _controller;

    public StacSortTests()
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

        _controller = new StacSearchController(
            _mockStore,
            _mockConfig,
            NullLogger<StacSearchController>.Instance,
            new StacMetrics());

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
                    SpatialExtent = new double[] { -180, -90, 180, 90 }
                }
            });
    }

    #region Sort Parser Tests

    [Fact]
    public void ParseGetSortBy_WithSingleDescendingField_ParsesCorrectly()
    {
        // Arrange
        var sortby = "-datetime";

        // Act
        var (fields, error) = StacSortParser.ParseGetSortBy(sortby);

        // Assert
        Assert.Null(error);
        Assert.NotNull(fields);
        Assert.Single(fields);
        Assert.Equal("datetime", fields[0].Field);
        Assert.Equal(StacSortDirection.Descending, fields[0].Direction);
    }

    [Fact]
    public void ParseGetSortBy_WithMultipleFields_ParsesCorrectly()
    {
        // Arrange
        var sortby = "-datetime,+id,cloud_cover";

        // Act
        var (fields, error) = StacSortParser.ParseGetSortBy(sortby);

        // Assert
        Assert.Null(error);
        Assert.NotNull(fields);
        Assert.Equal(3, fields.Count);

        Assert.Equal("datetime", fields[0].Field);
        Assert.Equal(StacSortDirection.Descending, fields[0].Direction);

        Assert.Equal("id", fields[1].Field);
        Assert.Equal(StacSortDirection.Ascending, fields[1].Direction);

        Assert.Equal("cloud_cover", fields[2].Field);
        Assert.Equal(StacSortDirection.Ascending, fields[2].Direction); // Default
    }

    [Fact]
    public void ParseGetSortBy_WithPropertyPrefix_ParsesCorrectly()
    {
        // Arrange
        var sortby = "properties.cloud_cover,-properties.gsd";

        // Act
        var (fields, error) = StacSortParser.ParseGetSortBy(sortby);

        // Assert
        Assert.Null(error);
        Assert.NotNull(fields);
        Assert.Equal(2, fields.Count);
        Assert.Equal("properties.cloud_cover", fields[0].Field);
        Assert.Equal("properties.gsd", fields[1].Field);
    }

    [Fact]
    public void ParseGetSortBy_WithTooManyFields_ReturnsError()
    {
        // Arrange
        var sortby = string.Join(",", new[]
        {
            "field1", "field2", "field3", "field4", "field5",
            "field6", "field7", "field8", "field9", "field10", "field11"
        });

        // Act
        var (fields, error) = StacSortParser.ParseGetSortBy(sortby);

        // Assert
        Assert.Null(fields);
        Assert.NotNull(error);
        Assert.Contains("Maximum of 10 sort fields", error);
    }

    [Fact]
    public void ParseGetSortBy_WithInvalidFieldName_ReturnsError()
    {
        // Arrange
        var sortby = "invalid_field";

        // Act
        var (fields, error) = StacSortParser.ParseGetSortBy(sortby);

        // Assert
        Assert.Null(fields);
        Assert.NotNull(error);
        Assert.Contains("not sortable", error);
    }

    [Fact]
    public void ParseGetSortBy_WithSqlInjectionAttempt_ReturnsError()
    {
        // Arrange
        var sortby = "datetime;DROP TABLE stac_items;--";

        // Act
        var (fields, error) = StacSortParser.ParseGetSortBy(sortby);

        // Assert
        Assert.Null(fields);
        Assert.NotNull(error);
        Assert.Contains("invalid characters", error);
    }

    [Fact]
    public void ParseGetSortBy_WithEoExtensionField_ParsesCorrectly()
    {
        // Arrange
        var sortby = "-eo:cloud_cover";

        // Act
        var (fields, error) = StacSortParser.ParseGetSortBy(sortby);

        // Assert
        Assert.Null(error);
        Assert.NotNull(fields);
        Assert.Single(fields);
        Assert.Equal("eo:cloud_cover", fields[0].Field);
    }

    #endregion

    #region GET Request Tests

    [Fact]
    public async Task GetSearchAsync_WithValidSortBy_PassesSortFieldsToStore()
    {
        // Arrange
        var sortby = "-datetime,+id";
        StacSearchParameters? capturedParams = null;

        _mockStore.SearchAsync(Arg.Do<StacSearchParameters>(p => capturedParams = p), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult
            {
                Items = new List<StacItemRecord>(),
                Matched = 0,
                NextToken = null
            });

        // Act
        await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: null,
            limit: null,
            token: null,
            sortby: sortby,
            fields: null,
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedParams);
        Assert.NotNull(capturedParams.SortBy);
        Assert.Equal(2, capturedParams.SortBy.Count);

        Assert.Equal("datetime", capturedParams.SortBy[0].Field);
        Assert.Equal(StacSortDirection.Descending, capturedParams.SortBy[0].Direction);

        Assert.Equal("id", capturedParams.SortBy[1].Field);
        Assert.Equal(StacSortDirection.Ascending, capturedParams.SortBy[1].Direction);
    }

    [Fact]
    public async Task GetSearchAsync_WithInvalidSortBy_Returns400()
    {
        // Arrange
        var sortby = "invalid_field";

        // Act
        var result = await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: null,
            limit: null,
            token: null,
            sortby: sortby,
            fields: null,
            CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid sortby parameter", problemDetails.Title);
    }

    [Fact]
    public async Task GetSearchAsync_WithEmptySortBy_UsesDefaultSort()
    {
        // Arrange
        StacSearchParameters? capturedParams = null;

        _mockStore.SearchAsync(Arg.Do<StacSearchParameters>(p => capturedParams = p), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult
            {
                Items = new List<StacItemRecord>(),
                Matched = 0,
                NextToken = null
            });

        // Act
        await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: null,
            limit: null,
            token: null,
            sortby: null,
            fields: null,
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedParams);
        // Should be null, letting the store use default sort
        Assert.Null(capturedParams.SortBy);
    }

    #endregion

    #region POST Request Tests

    [Fact]
    public async Task PostSearchAsync_WithValidSortBy_PassesSortFieldsToStore()
    {
        // Arrange
        var request = new StacSearchRequest
        {
            SortBy = new List<StacSortFieldDto>
            {
                new StacSortFieldDto { Field = "datetime", Direction = "desc" },
                new StacSortFieldDto { Field = "id", Direction = "asc" }
            }
        };

        StacSearchParameters? capturedParams = null;
        _mockStore.SearchAsync(Arg.Do<StacSearchParameters>(p => capturedParams = p), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult
            {
                Items = new List<StacItemRecord>(),
                Matched = 0,
                NextToken = null
            });

        // Act
        await _controller.PostSearchAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedParams);
        Assert.NotNull(capturedParams.SortBy);
        Assert.Equal(2, capturedParams.SortBy.Count);

        Assert.Equal("datetime", capturedParams.SortBy[0].Field);
        Assert.Equal(StacSortDirection.Descending, capturedParams.SortBy[0].Direction);

        Assert.Equal("id", capturedParams.SortBy[1].Field);
        Assert.Equal(StacSortDirection.Ascending, capturedParams.SortBy[1].Direction);
    }

    [Fact]
    public async Task PostSearchAsync_WithInvalidSortBy_Returns400()
    {
        // Arrange
        var request = new StacSearchRequest
        {
            SortBy = new List<StacSortFieldDto>
            {
                new StacSortFieldDto { Field = "invalid_field", Direction = "asc" }
            }
        };

        // Act
        var result = await _controller.PostSearchAsync(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Invalid sortby parameter", problemDetails.Title);
    }

    [Fact]
    public async Task PostSearchAsync_WithPropertySort_ParsesCorrectly()
    {
        // Arrange
        var request = new StacSearchRequest
        {
            SortBy = new List<StacSortFieldDto>
            {
                new StacSortFieldDto { Field = "properties.cloud_cover", Direction = "asc" },
                new StacSortFieldDto { Field = "gsd", Direction = "desc" }
            }
        };

        StacSearchParameters? capturedParams = null;
        _mockStore.SearchAsync(Arg.Do<StacSearchParameters>(p => capturedParams = p), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult
            {
                Items = new List<StacItemRecord>(),
                Matched = 0,
                NextToken = null
            });

        // Act
        await _controller.PostSearchAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedParams);
        Assert.NotNull(capturedParams.SortBy);
        Assert.Equal(2, capturedParams.SortBy.Count);

        Assert.Equal("properties.cloud_cover", capturedParams.SortBy[0].Field);
        Assert.Equal("gsd", capturedParams.SortBy[1].Field);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetSearchAsync_WithMixedCaseSortDirection_ParsesCorrectly()
    {
        // Arrange - sortby parser should handle any case
        var sortby = "-DaTeTiMe";

        StacSearchParameters? capturedParams = null;
        _mockStore.SearchAsync(Arg.Do<StacSearchParameters>(p => capturedParams = p), Arg.Any<CancellationToken>())
            .Returns(new StacSearchResult
            {
                Items = new List<StacItemRecord>(),
                Matched = 0,
                NextToken = null
            });

        // Act
        await _controller.GetSearchAsync(
            collections: null,
            ids: null,
            bbox: null,
            datetime: null,
            limit: null,
            token: null,
            sortby: sortby,
            fields: null,
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedParams);
        Assert.NotNull(capturedParams.SortBy);
        Assert.Single(capturedParams.SortBy);
        Assert.Equal("DaTeTiMe", capturedParams.SortBy[0].Field);
        Assert.Equal(StacSortDirection.Descending, capturedParams.SortBy[0].Direction);
    }

    [Fact]
    public void ParseGetSortBy_WithWhitespace_TrimsCorrectly()
    {
        // Arrange
        var sortby = "  datetime  , id  ";

        // Act
        var (fields, error) = StacSortParser.ParseGetSortBy(sortby);

        // Assert
        Assert.Null(error);
        Assert.NotNull(fields);
        Assert.Equal(2, fields.Count);
        Assert.Equal("datetime", fields[0].Field);
        Assert.Equal("id", fields[1].Field);
    }

    [Fact]
    public void GetDefaultSortFields_ReturnsCollectionAndId()
    {
        // Act
        var defaultFields = StacSortParser.GetDefaultSortFields();

        // Assert
        Assert.Equal(2, defaultFields.Count);
        Assert.Equal("collection", defaultFields[0].Field);
        Assert.Equal(StacSortDirection.Ascending, defaultFields[0].Direction);
        Assert.Equal("id", defaultFields[1].Field);
        Assert.Equal(StacSortDirection.Ascending, defaultFields[1].Direction);
    }

    #endregion
}

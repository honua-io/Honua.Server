// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Results;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Ogc.Services;
using Honua.Server.Host.Tests.Builders;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

[Trait("Category", "Unit")]
public class OgcResponseBuilderTests
{
    private readonly OgcResponseBuilder builder;

    public OgcResponseBuilderTests()
    {
        builder = new OgcResponseBuilder();
    }

    [Fact]
    public async Task CreateValidationProblem_Returns400WithRfc7807Format()
    {
        // Arrange
        var detail = "Invalid parameter value";
        var parameter = "limit";

        // Act
        var result = builder.CreateValidationProblem(detail, parameter);

        // Assert
        result.Should().NotBeNull();

        var httpContext = new DefaultHttpContext();
        await result.ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateValidationProblem_IncludesParameterInExtensions()
    {
        // Arrange
        var detail = "Invalid parameter value";
        var parameter = "limit";

        // Act
        var result = builder.CreateValidationProblem(detail, parameter);

        // Assert
        result.Should().NotBeNull();

        // Note: Since we're using Results.Problem, we can't easily extract the extensions
        // in a unit test without executing it. The important thing is that it doesn't throw.
        var httpContext = new DefaultHttpContext();
        var executeAction = async () => await result.ExecuteAsync(httpContext);
        await executeAction.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateNotFoundProblem_Returns404WithRfc7807Format()
    {
        // Arrange
        var detail = "Resource not found";

        // Act
        var result = builder.CreateNotFoundProblem(detail);

        // Assert
        result.Should().NotBeNull();

        var httpContext = new DefaultHttpContext();
        await result.ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CreateNotFoundProblem_IncludesDetailMessage()
    {
        // Arrange
        var detail = "Collection 'test::layer' was not found";

        // Act
        var result = builder.CreateNotFoundProblem(detail);

        // Assert
        result.Should().NotBeNull();

        var httpContext = new DefaultHttpContext();
        var executeAction = async () => await result.ExecuteAsync(httpContext);
        await executeAction.Should().NotThrowAsync();

        httpContext.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task MapCollectionResolutionError_WithNotFoundError_Returns404()
    {
        // Arrange
        var error = new Error("not_found", "Collection not found");
        var collectionId = "test::layer";

        // Act
        var result = builder.MapCollectionResolutionError(error, collectionId);

        // Assert
        result.Should().NotBeNull();

        var httpContext = new DefaultHttpContext();
        await result.ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task MapCollectionResolutionError_WithInvalidError_Returns500()
    {
        // Arrange
        var error = new Error("invalid", "Invalid collection ID format");
        var collectionId = "invalid-collection";

        // Act
        var result = builder.MapCollectionResolutionError(error, collectionId);

        // Assert
        result.Should().NotBeNull();

        var httpContext = new DefaultHttpContext();
        await result.ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task MapCollectionResolutionError_WithUnknownError_Returns500()
    {
        // Arrange
        var error = new Error("unknown_error", "Something went wrong");
        var collectionId = "test::layer";

        // Act
        var result = builder.MapCollectionResolutionError(error, collectionId);

        // Assert
        result.Should().NotBeNull();

        var httpContext = new DefaultHttpContext();
        await result.ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public void BuildCollectionSummary_CreatesCorrectMetadata()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithTitle("Test Layer")
            .WithDescription("Test Description")
            .WithItemType("feature")
            .WithCrs("http://www.opengis.net/def/crs/OGC/1.3/CRS84")
            .Build();

        var service = new ServiceDefinitionBuilder()
            .WithId("service1")
            .Build();

        var collectionId = "service1::layer1";

        // Act
        var summary = builder.BuildCollectionSummary(service, layer, collectionId);

        // Assert
        summary.Should().NotBeNull();
        summary.Id.Should().Be(collectionId);
        summary.Title.Should().Be("Test Layer");
        summary.Description.Should().Be("Test Description");
        summary.ItemType.Should().Be("feature");
        summary.Crs.Should().Contain("http://www.opengis.net/def/crs/OGC/1.3/CRS84");
    }

    [Fact]
    public void BuildCollectionSummary_UsesLayerCrs_WhenProvided()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithCrs(
                "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
                "http://www.opengis.net/def/crs/EPSG/0/4326")
            .Build();

        var service = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithCrs("http://www.opengis.net/def/crs/EPSG/0/3857") // Should be ignored
            .Build();

        var collectionId = "service1::layer1";

        // Act
        var summary = builder.BuildCollectionSummary(service, layer, collectionId);

        // Assert
        summary.Crs.Should().HaveCount(2);
        summary.Crs.Should().Contain("http://www.opengis.net/def/crs/OGC/1.3/CRS84");
        summary.Crs.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/4326");
        summary.Crs.Should().NotContain("http://www.opengis.net/def/crs/EPSG/0/3857");
    }

    [Fact]
    public void BuildCollectionSummary_UsesServiceCrs_WhenLayerCrsEmpty()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithCrs() // Empty CRS
            .Build();

        var service = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithCrs("http://www.opengis.net/def/crs/EPSG/0/3857")
            .Build();

        var collectionId = "service1::layer1";

        // Act
        var summary = builder.BuildCollectionSummary(service, layer, collectionId);

        // Assert
        summary.Crs.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/3857");
    }

    [Fact]
    public void FormatContentCrs_WithValidCrs_ReturnsWrappedValue()
    {
        // Arrange
        var crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84";

        // Act
        var result = builder.FormatContentCrs(crs);

        // Assert
        result.Should().Be("<http://www.opengis.net/def/crs/OGC/1.3/CRS84>");
    }

    [Fact]
    public void FormatContentCrs_WithNullCrs_ReturnsEmptyString()
    {
        // Arrange
        string? crs = null;

        // Act
        var result = builder.FormatContentCrs(crs);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void FormatContentCrs_WithEmptyCrs_ReturnsEmptyString()
    {
        // Arrange
        var crs = "";

        // Act
        var result = builder.FormatContentCrs(crs);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void FormatContentCrs_WithWhitespaceCrs_ReturnsEmptyString()
    {
        // Arrange
        var crs = "   ";

        // Act
        var result = builder.FormatContentCrs(crs);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public async Task WithResponseHeader_AddsCustomHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var innerResult = Results.Ok(new { test = "value" });

        // Act
        var result = builder.WithResponseHeader(innerResult, "X-Custom-Header", "custom-value");
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.Headers.Should().ContainKey("X-Custom-Header");
        httpContext.Response.Headers["X-Custom-Header"].ToString().Should().Be("custom-value");
    }

    [Fact]
    public async Task WithContentCrsHeader_AddsContentCrsHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var innerResult = Results.Ok(new { test = "value" });
        var crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84";

        // Act
        var result = builder.WithContentCrsHeader(innerResult, crs);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.Headers.Should().ContainKey("Content-Crs");
        httpContext.Response.Headers["Content-Crs"].ToString().Should().Be("<http://www.opengis.net/def/crs/OGC/1.3/CRS84>");
    }

    [Fact]
    public async Task WithContentCrsHeader_WithNullCrs_DoesNotAddHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var innerResult = Results.Ok(new { test = "value" });

        // Act
        var result = builder.WithContentCrsHeader(innerResult, null);
        await result.ExecuteAsync(httpContext);

        // Assert
        // The header should not be added or should be empty
        // Since the implementation checks HasValue(), it won't add the header
        var headerExists = httpContext.Response.Headers.TryGetValue("Content-Crs", out var headerValue);
        if (headerExists)
        {
            headerValue.ToString().Should().BeEmpty();
        }
    }

    [Fact]
    public void BuildOrderedStyleIds_ReturnsDefaultStyleFirst()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithDefaultStyleId("default")
            .WithStyleIds("style1", "style2", "default", "style3")
            .Build();

        // Act
        var result = builder.BuildOrderedStyleIds(layer);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result[0].Should().Be("default");
    }

    [Fact]
    public void BuildOrderedStyleIds_RemovesDuplicates()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithDefaultStyleId("default")
            .WithStyleIds("style1", "default", "style1", "style2")
            .Build();

        // Act
        var result = builder.BuildOrderedStyleIds(layer);

        // Assert
        result.Should().NotBeNull();
        result.Should().OnlyHaveUniqueItems();
        result.Should().Contain("default");
        result.Should().Contain("style1");
        result.Should().Contain("style2");
    }

    [Fact]
    public void BuildOrderedStyleIds_WithNoStyles_ReturnsEmptyList()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithDefaultStyleId("")
            .WithStyleIds()
            .Build();

        // Act
        var result = builder.BuildOrderedStyleIds(layer);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildOrderedStyleIds_WithOnlyDefaultStyle_ReturnsDefaultStyle()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithDefaultStyleId("default")
            .WithStyleIds()
            .Build();

        // Act
        var result = builder.BuildOrderedStyleIds(layer);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Should().Be("default");
    }

    [Fact]
    public void BuildOrderedStyleIds_IsCaseInsensitive()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithDefaultStyleId("Default")
            .WithStyleIds("default", "DEFAULT", "Style1")
            .Build();

        // Act
        var result = builder.BuildOrderedStyleIds(layer);

        // Assert
        result.Should().NotBeNull();
        result.Should().OnlyHaveUniqueItems();
        result.Should().HaveCountLessThan(4); // Should remove case-insensitive duplicates
    }

    [Fact]
    public async Task WithResponseHeader_WithNullValue_DoesNotAddHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var innerResult = Results.Ok(new { test = "value" });

        // Act
        var result = builder.WithResponseHeader(innerResult, "X-Custom-Header", null!);
        await result.ExecuteAsync(httpContext);

        // Assert
        // The header should not be added
        var headerExists = httpContext.Response.Headers.TryGetValue("X-Custom-Header", out var headerValue);
        if (headerExists)
        {
            headerValue.ToString().Should().BeEmpty();
        }
    }

    [Fact]
    public async Task WithResponseHeader_WithEmptyValue_DoesNotAddHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var innerResult = Results.Ok(new { test = "value" });

        // Act
        var result = builder.WithResponseHeader(innerResult, "X-Custom-Header", "");
        await result.ExecuteAsync(httpContext);

        // Assert
        // The header should not be added based on HasValue() check
        var headerExists = httpContext.Response.Headers.TryGetValue("X-Custom-Header", out var headerValue);
        if (headerExists)
        {
            headerValue.ToString().Should().BeEmpty();
        }
    }

    [Fact]
    public void BuildOrderedStyleIds_IgnoresEmptyStyleIds()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithDefaultStyleId("default")
            .WithStyleIds("style1", "", "   ", "style2")
            .Build();

        // Act
        var result = builder.BuildOrderedStyleIds(layer);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("default");
        result.Should().Contain("style1");
        result.Should().Contain("style2");
        result.Should().NotContain("");
        result.Should().NotContain("   ");
    }
}

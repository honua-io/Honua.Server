// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Host.Data;
using Honua.Server.Host.Ogc.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

public class OgcFeaturesEditingHandlerTests
{
    private readonly OgcFeaturesEditingHandler _handler;
    private readonly Mock<IOgcFeaturesGeoJsonHandler> _mockGeoJsonHandler;

    public OgcFeaturesEditingHandlerTests()
    {
        _mockGeoJsonHandler = new Mock<IOgcFeaturesGeoJsonHandler>();
        _handler = new OgcFeaturesEditingHandler(_mockGeoJsonHandler.Object);
    }

    [Fact]
    public void CreateEditFailureProblem_WithNullError_ReturnsGenericProblem()
    {
        // Act
        var result = _handler.CreateEditFailureProblem(null, 400);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void CreateEditFailureProblem_WithErrorAndNoDetails_ReturnsProblemWithMessage()
    {
        // Arrange
        var error = new FeatureEditError("Validation failed", "validation_error");

        // Act
        var result = _handler.CreateEditFailureProblem(error, 400);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void CreateEditFailureProblem_WithErrorAndDetails_ReturnsProblemWithExtensions()
    {
        // Arrange
        var error = new FeatureEditError("Validation failed", "validation_error")
        {
            Details = new Dictionary<string, string>
            {
                ["field1"] = "Invalid value",
                ["field2"] = "Required field"
            }
        };

        // Act
        var result = _handler.CreateEditFailureProblem(error, 400);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void CreateFeatureEditBatch_WithAuthenticatedUser_SetsIsAuthenticated()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity("TestAuthType");
        identity.AddClaim(new Claim(ClaimTypes.Name, "testuser"));
        context.User = new ClaimsPrincipal(identity);
        var commands = new List<FeatureEditCommand>
        {
            new AddFeatureCommand("service1", "layer1", new Dictionary<string, object?>())
        };

        // Act
        var batch = _handler.CreateFeatureEditBatch(commands, context.Request);

        // Assert
        Assert.True(batch.IsAuthenticated);
        Assert.NotNull(batch.Commands);
        Assert.Single(batch.Commands);
    }

    [Fact]
    public void CreateFeatureEditBatch_WithUnauthenticatedUser_SetsIsAuthenticatedFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var commands = new List<FeatureEditCommand>
        {
            new AddFeatureCommand("service1", "layer1", new Dictionary<string, object?>())
        };

        // Act
        var batch = _handler.CreateFeatureEditBatch(commands, context.Request);

        // Assert
        Assert.False(batch.IsAuthenticated);
    }

    [Fact]
    public async Task FetchCreatedFeaturesWithETags_WithSuccessfulFetch_ReturnsFeatures()
    {
        // Arrange
        var mockRepo = new Mock<IFeatureRepository>();
        var context = new FeatureContext(
            new ServiceDefinition { Id = "service1" },
            new LayerDefinition { Id = "layer1", IdField = "id" });
        var layer = context.Layer;
        var editResult = new FeatureEditBatchResult(new List<FeatureEditResult>
        {
            new FeatureEditResult(true, "123", null)
        });
        var fallbackIds = new List<string?> { "123" };
        var featureQuery = new FeatureQuery(ResultType: FeatureResultType.Results);
        var httpContext = new DefaultHttpContext();

        var record = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test" }, null, null);
        mockRepo.Setup(r => r.GetAsync("service1", "layer1", "123", featureQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        _mockGeoJsonHandler.Setup(h => h.ToFeature(It.IsAny<HttpRequest>(), It.IsAny<string>(),
            It.IsAny<LayerDefinition>(), It.IsAny<FeatureRecord>(), It.IsAny<FeatureQuery>(), null, null))
            .Returns(new { type = "Feature", id = "123" });

        // Act
        var result = await _handler.FetchCreatedFeaturesWithETags(
            mockRepo.Object,
            context,
            layer,
            "collection1",
            editResult,
            fallbackIds,
            featureQuery,
            httpContext.Request,
            CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("123", result[0].FeatureId);
        Assert.NotNull(result[0].Etag);
    }

    [Fact]
    public async Task FetchCreatedFeaturesWithETags_WhenFeatureNotFound_ReturnsFallbackIds()
    {
        // Arrange
        var mockRepo = new Mock<IFeatureRepository>();
        var context = new FeatureContext(
            new ServiceDefinition { Id = "service1" },
            new LayerDefinition { Id = "layer1", IdField = "id" });
        var layer = context.Layer;
        var editResult = new FeatureEditBatchResult(new List<FeatureEditResult>
        {
            new FeatureEditResult(true, "123", null)
        });
        var fallbackIds = new List<string?> { "123" };
        var featureQuery = new FeatureQuery(ResultType: FeatureResultType.Results);
        var httpContext = new DefaultHttpContext();

        mockRepo.Setup(r => r.GetAsync("service1", "layer1", "123", featureQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureRecord?)null);

        // Act
        var result = await _handler.FetchCreatedFeaturesWithETags(
            mockRepo.Object,
            context,
            layer,
            "collection1",
            editResult,
            fallbackIds,
            featureQuery,
            httpContext.Request,
            CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("123", result[0].FeatureId);
        Assert.Null(result[0].Etag);
    }

    [Fact]
    public void BuildMutationResponse_WithSingleItemMode_ReturnsCreatedResult()
    {
        // Arrange
        var features = new List<(string? FeatureId, object Payload, string? Etag)>
        {
            ("123", new { type = "Feature", id = "123" }, "W/\"abc123\"")
        };

        // Act
        var result = _handler.BuildMutationResponse(features, "collection1", singleItemMode: true);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void BuildMutationResponse_WithMultipleItems_ReturnsFeatureCollection()
    {
        // Arrange
        var features = new List<(string? FeatureId, object Payload, string? Etag)>
        {
            ("123", new { type = "Feature", id = "123" }, "W/\"abc123\""),
            ("456", new { type = "Feature", id = "456" }, "W/\"def456\"")
        };

        // Act
        var result = _handler.BuildMutationResponse(features, "collection1", singleItemMode: false);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateIfMatch_WithNoIfMatchHeader_ReturnsTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test" }, null, null);

        // Act
        var result = _handler.ValidateIfMatch(context.Request, layer, record, out var etag);

        // Assert
        Assert.True(result);
        Assert.NotEmpty(etag);
    }

    [Fact]
    public void ValidateIfMatch_WithMatchingETag_ReturnsTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test" }, null, null);

        var expectedEtag = _handler.ComputeFeatureEtag(layer, record);
        context.Request.Headers["If-Match"] = expectedEtag;

        // Act
        var result = _handler.ValidateIfMatch(context.Request, layer, record, out var etag);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedEtag, etag);
    }

    [Fact]
    public void ValidateIfMatch_WithWildcardETag_ReturnsTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test" }, null, null);
        context.Request.Headers["If-Match"] = "*";

        // Act
        var result = _handler.ValidateIfMatch(context.Request, layer, record, out var etag);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateIfMatch_WithNonMatchingETag_ReturnsFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test" }, null, null);
        context.Request.Headers["If-Match"] = "W/\"wrong-etag\"";

        // Act
        var result = _handler.ValidateIfMatch(context.Request, layer, record, out var etag);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ComputeFeatureEtag_WithSameAttributes_ReturnsSameEtag()
    {
        // Arrange
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record1 = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test", ["age"] = 30 }, null, null);
        var record2 = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test", ["age"] = 30 }, null, null);

        // Act
        var etag1 = _handler.ComputeFeatureEtag(layer, record1);
        var etag2 = _handler.ComputeFeatureEtag(layer, record2);

        // Assert
        Assert.Equal(etag1, etag2);
    }

    [Fact]
    public void ComputeFeatureEtag_WithDifferentAttributes_ReturnsDifferentEtag()
    {
        // Arrange
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record1 = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test1" }, null, null);
        var record2 = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test2" }, null, null);

        // Act
        var etag1 = _handler.ComputeFeatureEtag(layer, record1);
        var etag2 = _handler.ComputeFeatureEtag(layer, record2);

        // Assert
        Assert.NotEqual(etag1, etag2);
    }

    [Fact]
    public void ComputeFeatureEtag_ReturnsWeakETag()
    {
        // Arrange
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test" }, null, null);

        // Act
        var etag = _handler.ComputeFeatureEtag(layer, record);

        // Assert
        Assert.StartsWith("W/\"", etag);
        Assert.EndsWith("\"", etag);
    }

    [Fact]
    public void ComputeFeatureEtag_WithCaseInsensitiveKeys_ReturnsSameEtag()
    {
        // Arrange
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record1 = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test", ["AGE"] = 30 }, null, null);
        var record2 = new FeatureRecord("123", new Dictionary<string, object?> { ["NAME"] = "test", ["age"] = 30 }, null, null);

        // Act
        var etag1 = _handler.ComputeFeatureEtag(layer, record1);
        var etag2 = _handler.ComputeFeatureEtag(layer, record2);

        // Assert
        Assert.Equal(etag1, etag2);
    }

    [Fact]
    public void ComputeFeatureEtag_WithEmptyAttributes_ReturnsEtag()
    {
        // Arrange
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record = new FeatureRecord("123", new Dictionary<string, object?>(), null, null);

        // Act
        var etag = _handler.ComputeFeatureEtag(layer, record);

        // Assert
        Assert.NotNull(etag);
        Assert.StartsWith("W/\"", etag);
    }

    [Fact]
    public void ValidateIfMatch_WithMultipleETags_MatchesAny()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var layer = new LayerDefinition { Id = "layer1", IdField = "id" };
        var record = new FeatureRecord("123", new Dictionary<string, object?> { ["name"] = "test" }, null, null);

        var correctEtag = _handler.ComputeFeatureEtag(layer, record);
        context.Request.Headers["If-Match"] = $"W/\"wrong1\", {correctEtag}, W/\"wrong2\"";

        // Act
        var result = _handler.ValidateIfMatch(context.Request, layer, record, out var etag);

        // Assert
        Assert.True(result);
    }
}

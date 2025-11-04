using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Stac;
using Honua.Server.Host.Stac;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

[Collection("HostTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
[Trait("Speed", "Fast")]
public sealed class StacCollectionsControllerTests
{
    private readonly Mock<IStacCatalogStore> _mockStore;
    private readonly Mock<IHonuaConfigurationService> _mockConfigService;
    private readonly Mock<IStacValidationService> _mockValidationService;
    private readonly Mock<ISecurityAuditLogger> _mockAuditLogger;
    private readonly StacMetrics _metrics;
    private readonly StacCollectionsController _controller;
    private readonly MetricCollector<long> _operationsCollector;
    private readonly MetricCollector<long> _errorsCollector;
    private readonly MetricCollector<double> _durationCollector;

    public StacCollectionsControllerTests()
    {
        _mockStore = new Mock<IStacCatalogStore>();
        _mockConfigService = new Mock<IHonuaConfigurationService>();
        _mockValidationService = new Mock<IStacValidationService>();
        _mockAuditLogger = new Mock<ISecurityAuditLogger>();

        // Setup metrics with test meter factory
        var meterFactory = new TestMeterFactory();
        _metrics = new StacMetrics(meterFactory);

        _controller = new StacCollectionsController(
            _mockStore.Object,
            _mockConfigService.Object,
            _mockValidationService.Object,
            _mockAuditLogger.Object,
            _metrics);

        // Setup HttpContext
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "testuser")
                }, "TestAuth"))
            }
        };

        // Create metric collectors
        using var meterScope = meterFactory.CreateCollectorScope();
        _operationsCollector = meterScope.GetMetricCollector<long>("stac.write_operations.total");
        _errorsCollector = meterScope.GetMetricCollector<long>("stac.write_operations.errors.total");
        _durationCollector = meterScope.GetMetricCollector<double>("stac.write_operations.duration");

        // Setup default configuration
        var config = new HonuaConfiguration
        {
            Services = new ServicesConfiguration
            {
                Stac = new StacConfiguration { Enabled = true }
            }
        };
        _mockConfigService.Setup(x => x.Current).Returns(config);
    }

    [Fact]
    public async Task PostCollection_WithValidCollection_ReturnsCreated()
    {
        // Arrange
        var collectionJson = CreateValidCollectionJson("test-collection");
        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StacCollectionRecord?)null);
        _mockStore.Setup(x => x.UpsertCollectionAsync(It.IsAny<StacCollectionRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollection(collectionJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.StatusCode.Should().Be((int)HttpStatusCode.Created);
        createdResult.ActionName.Should().Be(nameof(StacCollectionsController.GetCollection));

        _mockAuditLogger.Verify(x => x.LogDataAccess(
            "testuser",
            "CREATE",
            "STAC_Collection",
            "test-collection",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PostCollection_WithInvalidCollection_ReturnsBadRequest()
    {
        // Arrange
        var collectionJson = new JsonObject { ["id"] = "test" };
        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Failure("Missing required field: description"));
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollection(collectionJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCollection_WithExistingCollection_ReturnsConflict()
    {
        // Arrange
        var collectionJson = CreateValidCollectionJson("existing-collection");
        var existingRecord = new StacCollectionRecord
        {
            Id = "existing-collection",
            Title = "Existing",
            Description = "Existing collection"
        };

        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("existing-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRecord);

        // Act
        var result = await _controller.PostCollection(collectionJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
        var conflict = result.Result as ConflictObjectResult;
        conflict!.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostCollection_WithMissingId_ReturnsBadRequest()
    {
        // Arrange
        var collectionJson = new JsonObject { ["title"] = "Test" };
        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Failure("Missing id"));
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollection(collectionJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PutCollection_WithValidCollection_ReturnsOk()
    {
        // Arrange
        var collectionJson = CreateValidCollectionJson("test-collection");
        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.UpsertCollectionAsync(It.IsAny<StacCollectionRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PutCollection("test-collection", collectionJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.StatusCode.Should().Be((int)HttpStatusCode.OK);

        _mockAuditLogger.Verify(x => x.LogDataAccess(
            "testuser",
            "UPDATE",
            "STAC_Collection",
            "test-collection",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PutCollection_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        var collectionJson = CreateValidCollectionJson("collection-a");
        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PutCollection("collection-b", collectionJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteCollection_WithExistingCollection_ReturnsNoContent()
    {
        // Arrange
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.DeleteCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteCollection("test-collection", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        var noContent = result as NoContentResult;
        noContent!.StatusCode.Should().Be((int)HttpStatusCode.NoContent);

        _mockAuditLogger.Verify(x => x.LogDataAccess(
            "testuser",
            "DELETE",
            "STAC_Collection",
            "test-collection",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCollection_WithNonExistingCollection_ReturnsNotFound()
    {
        // Arrange
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.DeleteCollectionAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteCollection("nonexistent", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PostCollectionItem_WithValidItem_ReturnsCreated()
    {
        // Arrange
        var itemJson = CreateValidItemJson("test-item");
        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test"
        };

        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);
        _mockStore.Setup(x => x.GetItemAsync("test-collection", "test-item", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StacItemRecord?)null);
        _mockStore.Setup(x => x.UpsertItemAsync(It.IsAny<StacItemRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollectionItem("test-collection", itemJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.StatusCode.Should().Be((int)HttpStatusCode.Created);

        _mockAuditLogger.Verify(x => x.LogDataAccess(
            "testuser",
            "CREATE",
            "STAC_Item",
            "test-collection/test-item",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PostCollectionItem_WithNonExistentCollection_ReturnsNotFound()
    {
        // Arrange
        var itemJson = CreateValidItemJson("test-item");
        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StacCollectionRecord?)null);

        // Act
        var result = await _controller.PostCollectionItem("nonexistent", itemJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PostCollectionItem_WithInvalidItem_ReturnsBadRequest()
    {
        // Arrange
        var itemJson = new JsonObject { ["id"] = "test" };
        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Failure("Missing required field: geometry"));
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollectionItem("test-collection", itemJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PutCollectionItem_WithValidItem_ReturnsOk()
    {
        // Arrange
        var itemJson = CreateValidItemJson("test-item");
        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test"
        };

        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);
        _mockStore.Setup(x => x.UpsertItemAsync(It.IsAny<StacItemRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PutCollectionItem("test-collection", "test-item", itemJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.StatusCode.Should().Be((int)HttpStatusCode.OK);

        _mockAuditLogger.Verify(x => x.LogDataAccess(
            "testuser",
            "UPDATE",
            "STAC_Item",
            "test-collection/test-item",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PutCollectionItem_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        var itemJson = CreateValidItemJson("item-a");
        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test"
        };

        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        // Act
        var result = await _controller.PutCollectionItem("test-collection", "item-b", itemJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteCollectionItem_WithExistingItem_ReturnsNoContent()
    {
        // Arrange
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.DeleteItemAsync("test-collection", "test-item", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteCollectionItem("test-collection", "test-item", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        var noContent = result as NoContentResult;
        noContent!.StatusCode.Should().Be((int)HttpStatusCode.NoContent);

        _mockAuditLogger.Verify(x => x.LogDataAccess(
            "testuser",
            "DELETE",
            "STAC_Item",
            "test-collection/test-item",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCollectionItem_WithNonExistingItem_ReturnsNotFound()
    {
        // Arrange
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.DeleteItemAsync("test-collection", "nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteCollectionItem("test-collection", "nonexistent", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCollection_WithETag_ReturnsETagHeader()
    {
        // Arrange
        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test description",
            ETag = "test-etag-123"
        };

        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        // Act
        var result = await _controller.GetCollection("test-collection", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _controller.Response.Headers.Should().ContainKey("ETag");
        _controller.Response.Headers["ETag"].ToString().Should().Contain("test-etag-123");
    }

    [Fact]
    public async Task GetCollectionItem_WithETag_ReturnsETagHeader()
    {
        // Arrange
        var item = new StacItemRecord
        {
            Id = "test-item",
            CollectionId = "test-collection",
            Properties = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ETag = "test-item-etag"
        };

        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetItemAsync("test-collection", "test-item", It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        // Act
        var result = await _controller.GetCollectionItem("test-collection", "test-item", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _controller.Response.Headers.Should().ContainKey("ETag");
        _controller.Response.Headers["ETag"].ToString().Should().Contain("test-item-etag");
    }

    [Fact]
    public async Task PostCollectionItem_WithExistingItem_ReturnsConflict()
    {
        // Arrange
        var itemJson = CreateValidItemJson("existing-item");
        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test"
        };
        var existingItem = new StacItemRecord
        {
            Id = "existing-item",
            CollectionId = "test-collection",
            Properties = new JsonObject()
        };

        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);
        _mockStore.Setup(x => x.GetItemAsync("test-collection", "existing-item", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        // Act
        var result = await _controller.PostCollectionItem("test-collection", itemJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    private static JsonObject CreateValidCollectionJson(string id)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["title"] = "Test Collection",
            ["description"] = "A test collection for unit tests",
            ["license"] = "MIT",
            ["extent"] = new JsonObject
            {
                ["spatial"] = new JsonObject
                {
                    ["bbox"] = new JsonArray { new JsonArray { -180, -90, 180, 90 } }
                },
                ["temporal"] = new JsonObject
                {
                    ["interval"] = new JsonArray { new JsonArray { "2024-01-01T00:00:00Z", null } }
                }
            }
        };
    }

    private static JsonObject CreateValidItemJson(string id)
    {
        return new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = id,
            ["geometry"] = new JsonObject
            {
                ["type"] = "Point",
                ["coordinates"] = new JsonArray { -122.5, 45.5 }
            },
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-01T00:00:00Z"
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject
                {
                    ["href"] = "https://example.com/data.tif",
                    ["type"] = "image/tiff"
                }
            }
        };
    }

    #region Malformed JSON Tests

    [Fact]
    public async Task PostCollection_WithNonStringId_ReturnsBadRequest()
    {
        // Arrange - id is a number instead of string
        var collectionJson = new JsonObject
        {
            ["id"] = 12345,
            ["title"] = "Test Collection",
            ["description"] = "Test",
            ["license"] = "MIT"
        };

        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollection(collectionJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        badRequest!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task PostCollection_WithBooleanId_ReturnsBadRequest()
    {
        // Arrange - id is a boolean instead of string
        var collectionJson = new JsonObject
        {
            ["id"] = true,
            ["title"] = "Test Collection",
            ["description"] = "Test"
        };

        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollection(collectionJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostCollection_WithObjectId_ReturnsBadRequest()
    {
        // Arrange - id is an object instead of string
        var collectionJson = new JsonObject
        {
            ["id"] = new JsonObject { ["value"] = "test" },
            ["title"] = "Test Collection",
            ["description"] = "Test"
        };

        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollection(collectionJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostCollection_WithNonStringKeywords_IgnoresMalformedValues()
    {
        // Arrange - keywords array contains non-string values
        var collectionJson = new JsonObject
        {
            ["id"] = "test-collection",
            ["title"] = "Test Collection",
            ["description"] = "Test",
            ["license"] = "MIT",
            ["keywords"] = new JsonArray { "valid", 123, true, "another-valid" }
        };

        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StacCollectionRecord?)null);
        _mockStore.Setup(x => x.UpsertCollectionAsync(It.IsAny<StacCollectionRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollection(collectionJson, CancellationToken.None);

        // Assert - should succeed but only include valid string keywords
        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task PostItem_WithNonStringId_ReturnsBadRequest()
    {
        // Arrange - id is a number instead of string
        var itemJson = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = 12345,
            ["geometry"] = new JsonObject
            {
                ["type"] = "Point",
                ["coordinates"] = new JsonArray { -122.5, 45.5 }
            },
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-01T00:00:00Z"
            }
        };

        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test"
        };

        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        // Act
        var result = await _controller.PostCollectionItem("test-collection", itemJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostItem_WithInvalidBboxTypes_ReturnsBadRequest()
    {
        // Arrange - bbox contains non-numeric values
        var itemJson = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject
            {
                ["type"] = "Point",
                ["coordinates"] = new JsonArray { -122.5, 45.5 }
            },
            ["bbox"] = new JsonArray { -180, -90, "invalid", 90 },
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-01T00:00:00Z"
            }
        };

        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test"
        };

        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);
        _mockStore.Setup(x => x.GetItemAsync("test-collection", "test-item", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StacItemRecord?)null);

        // Act
        var result = await _controller.PostCollectionItem("test-collection", itemJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        var value = badRequest!.Value as dynamic;
        value.Should().NotBeNull();
    }

    [Fact]
    public async Task PostItem_WithNonStringDatetime_SkipsDatetimeParsing()
    {
        // Arrange - datetime is a number instead of string
        var itemJson = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject
            {
                ["type"] = "Point",
                ["coordinates"] = new JsonArray { -122.5, 45.5 }
            },
            ["properties"] = new JsonObject
            {
                ["datetime"] = 12345 // Invalid type
            }
        };

        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test"
        };

        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);
        _mockStore.Setup(x => x.GetItemAsync("test-collection", "test-item", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StacItemRecord?)null);
        _mockStore.Setup(x => x.UpsertItemAsync(It.IsAny<StacItemRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PostCollectionItem("test-collection", itemJson, CancellationToken.None);

        // Assert - should succeed but datetime will be null
        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task PutCollection_WithNonStringTitle_IgnoresValue()
    {
        // Arrange - title is a number instead of string
        var collectionJson = new JsonObject
        {
            ["id"] = "test-collection",
            ["title"] = 12345,
            ["description"] = "Test description",
            ["license"] = "MIT"
        };

        _mockValidationService.Setup(x => x.ValidateCollection(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.UpsertCollectionAsync(It.IsAny<StacCollectionRecord>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PutCollection("test-collection", collectionJson, CancellationToken.None);

        // Assert - should succeed but title will be null
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PatchItem_WithInvalidBboxTypes_ReturnsBadRequest()
    {
        // Arrange
        var existingItem = new StacItemRecord
        {
            Id = "test-item",
            CollectionId = "test-collection",
            Properties = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" }
        };

        var patchJson = new JsonObject
        {
            ["bbox"] = new JsonArray { -180, true, 180, 90 } // Invalid: contains boolean
        };

        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetItemAsync("test-collection", "test-item", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _controller.PatchCollectionItem("test-collection", "test-item", patchJson, CancellationToken.None);
        });
    }

    [Fact]
    public async Task PutItem_WithIdMismatch_ReturnsBadRequest()
    {
        // Arrange
        var itemJson = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "item-a",
            ["geometry"] = new JsonObject
            {
                ["type"] = "Point",
                ["coordinates"] = new JsonArray { -122.5, 45.5 }
            },
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-01T00:00:00Z"
            }
        };

        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test"
        };

        _mockValidationService.Setup(x => x.ValidateItem(It.IsAny<JsonObject>()))
            .Returns(StacValidationResult.Success());
        _mockStore.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockStore.Setup(x => x.GetCollectionAsync("test-collection", It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        // Act
        var result = await _controller.PutCollectionItem("test-collection", "item-b", itemJson, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        var value = badRequest!.Value as dynamic;
        string errorMessage = value!.error;
        errorMessage.Should().Contain("must match the path parameter");
    }

    #endregion
}

using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Host.Stac;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

[Collection("HostTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
[Trait("Speed", "Fast")]
public sealed class StacValidationServiceTests
{
    private readonly StacValidationService _validator;

    public StacValidationServiceTests()
    {
        _validator = new StacValidationService(NullLogger<StacValidationService>.Instance);
    }

    [Fact]
    public void ValidateCollection_WithValidCollection_ReturnsSuccess()
    {
        // Arrange
        var collection = new JsonObject
        {
            ["id"] = "test-collection",
            ["description"] = "Test description",
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

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateCollection_WithMissingId_ReturnsError()
    {
        // Arrange
        var collection = new JsonObject
        {
            ["description"] = "Test description",
            ["license"] = "MIT"
        };

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "id");
    }

    [Fact]
    public void ValidateCollection_WithMissingDescription_ReturnsError()
    {
        // Arrange
        var collection = new JsonObject
        {
            ["id"] = "test-collection",
            ["license"] = "MIT"
        };

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "description");
    }

    [Fact]
    public void ValidateCollection_WithMissingLicense_ReturnsError()
    {
        // Arrange
        var collection = new JsonObject
        {
            ["id"] = "test-collection",
            ["description"] = "Test description"
        };

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "license");
    }

    [Fact]
    public void ValidateCollection_WithMissingExtent_ReturnsError()
    {
        // Arrange
        var collection = new JsonObject
        {
            ["id"] = "test-collection",
            ["description"] = "Test description",
            ["license"] = "MIT"
        };

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "extent");
    }

    [Fact]
    public void ValidateItem_WithValidItem_ReturnsSuccess()
    {
        // Arrange
        var item = new JsonObject
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

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateItem_WithMissingId_ReturnsError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject()
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "id");
    }

    [Fact]
    public void ValidateItem_WithInvalidType_ReturnsError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "InvalidType",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject()
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "type" && e.Message.Contains("Feature"));
    }

    [Fact]
    public void ValidateItem_WithMissingGeometry_ReturnsError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject()
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "geometry");
    }

    [Fact]
    public void ValidateItem_WithMissingProperties_ReturnsError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["assets"] = new JsonObject()
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "properties");
    }

    [Fact]
    public void ValidateItem_WithMissingAssets_ReturnsError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "assets");
    }

    [Fact]
    public void ValidateItem_WithNullGeometry_IsValid()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject()
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateItem_WithInvalidBbox_ReturnsError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject(),
            ["bbox"] = new JsonArray { 1, 2, 3 } // Invalid bbox with 3 elements
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "bbox");
    }

    [Fact]
    public void ValidateItem_WithValidBbox4Elements_IsValid()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject(),
            ["bbox"] = new JsonArray { -180, -90, 180, 90 }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateItem_WithValidBbox6Elements_IsValid()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject(),
            ["bbox"] = new JsonArray { -180, -90, 0, 180, 90, 1000 }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

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
public sealed class StacValidationEnhancedTests
{
    private readonly StacValidationService _validator;

    public StacValidationEnhancedTests()
    {
        _validator = new StacValidationService(NullLogger<StacValidationService>.Instance);
    }

    #region Collection Validation Tests

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
    public void ValidateCollection_WithMissingId_ReturnsDetailedError()
    {
        // Arrange
        var collection = new JsonObject
        {
            ["description"] = "Test description",
            ["license"] = "MIT",
            ["extent"] = new JsonObject
            {
                ["spatial"] = new JsonObject { ["bbox"] = new JsonArray { new JsonArray { -180, -90, 180, 90 } } },
                ["temporal"] = new JsonObject { ["interval"] = new JsonArray { new JsonArray { "2024-01-01T00:00:00Z", null } } }
            }
        };

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        var error = result.Errors[0];
        error.Field.Should().Be("id");
        error.Message.Should().Contain("Required field is missing or empty");
        error.ExpectedFormat.Should().NotBeNullOrEmpty();
        error.Example.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateCollection_WithTooLongId_ReturnsDetailedError()
    {
        // Arrange
        var longId = new string('a', 300);
        var collection = new JsonObject
        {
            ["id"] = longId,
            ["description"] = "Test description",
            ["license"] = "MIT",
            ["extent"] = new JsonObject
            {
                ["spatial"] = new JsonObject { ["bbox"] = new JsonArray { new JsonArray { -180, -90, 180, 90 } } },
                ["temporal"] = new JsonObject { ["interval"] = new JsonArray { new JsonArray { "2024-01-01T00:00:00Z", null } } }
            }
        };

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "id" && e.Message.Contains("maximum length"));
    }

    [Fact]
    public void ValidateCollection_WithInvalidType_ReturnsDetailedError()
    {
        // Arrange
        var collection = new JsonObject
        {
            ["id"] = "test-collection",
            ["type"] = "InvalidType",
            ["description"] = "Test description",
            ["license"] = "MIT",
            ["extent"] = new JsonObject
            {
                ["spatial"] = new JsonObject { ["bbox"] = new JsonArray { new JsonArray { -180, -90, 180, 90 } } },
                ["temporal"] = new JsonObject { ["interval"] = new JsonArray { new JsonArray { "2024-01-01T00:00:00Z", null } } }
            }
        };

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.Field == "type").Subject;
        error.Message.Should().Contain("Invalid type");
        error.ActualValue.Should().Be("InvalidType");
        error.ExpectedFormat.Should().Contain("Collection");
    }

    #endregion

    #region Item Validation Tests

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
    public void ValidateItem_WithMissingId_ReturnsDetailedError()
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
        var error = result.Errors.Should().ContainSingle(e => e.Field == "id").Subject;
        error.Message.Should().Contain("Required field is missing or empty");
        error.ExpectedFormat.Should().NotBeNullOrEmpty();
        error.Example.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Bbox Validation Tests

    [Theory]
    [InlineData(3)] // Too few coordinates
    [InlineData(5)] // Invalid count
    [InlineData(7)] // Too many coordinates
    public void ValidateItem_WithInvalidBboxCount_ReturnsDetailedError(int coordinateCount)
    {
        // Arrange
        var bboxArray = new JsonArray();
        for (int i = 0; i < coordinateCount; i++)
        {
            bboxArray.Add(i);
        }

        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject(),
            ["bbox"] = bboxArray
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.Field == "bbox").Subject;
        error.Message.Should().Contain($"expected 4 or 6, but received {coordinateCount}");
        error.ExpectedFormat.Should().NotBeNullOrEmpty();
        error.Example.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(-181, -90, 180, 90, "minX")] // minX out of range
    [InlineData(-180, -91, 180, 90, "minY")] // minY out of range
    [InlineData(-180, -90, 181, 90, "maxX")] // maxX out of range
    [InlineData(-180, -90, 180, 91, "maxY")] // maxY out of range
    public void ValidateItem_WithOutOfRangeBboxCoordinates_ReturnsDetailedError(double minX, double minY, double maxX, double maxY, string expectedFieldPart)
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject(),
            ["bbox"] = new JsonArray { minX, minY, maxX, maxY }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("out of valid range"));
    }

    [Theory]
    [InlineData(10, -90, -10, 90)] // minX > maxX
    [InlineData(-180, 10, 180, -10)] // minY > maxY
    public void ValidateItem_WithInvalidBboxMinMaxOrder_ReturnsDetailedError(double minX, double minY, double maxX, double maxY)
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject(),
            ["bbox"] = new JsonArray { minX, minY, maxX, maxY }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("less than or equal to"));
        var error = result.Errors.First(e => e.Message.Contains("less than or equal to"));
        error.ActualValue.Should().NotBeNullOrEmpty();
        error.ExpectedFormat.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region DateTime Validation Tests

    [Fact]
    public void ValidateItem_WithInvalidDatetimeFormat_ReturnsDetailedError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01" }, // Missing time component
            ["assets"] = new JsonObject()
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.Field == "properties.datetime").Subject;
        error.Message.Should().Contain("Invalid datetime format");
        error.ActualValue.Should().Be("2024-01-01");
        error.ExpectedFormat.Should().Contain("RFC 3339");
        error.Example.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateItem_WithNullDatetimeButMissingStartEnd_ReturnsDetailedError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = null },
            ["assets"] = new JsonObject()
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.Field == "properties.datetime").Subject;
        error.Message.Should().Contain("start_datetime");
        error.Message.Should().Contain("end_datetime");
        error.Example.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateItem_WithStartAfterEnd_ReturnsDetailedError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject
            {
                ["datetime"] = null,
                ["start_datetime"] = "2024-12-31T23:59:59Z",
                ["end_datetime"] = "2024-01-01T00:00:00Z"
            },
            ["assets"] = new JsonObject()
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.Field == "properties").Subject;
        error.Message.Should().Contain("start_datetime must be less than or equal to end_datetime");
        error.ActualValue.Should().Contain("start=");
        error.ActualValue.Should().Contain("end=");
    }

    [Theory]
    [InlineData("1899-01-01T00:00:00Z")] // Too far in the past
    [InlineData("2200-01-01T00:00:00Z")] // Too far in the future
    public void ValidateItem_WithDatetimeOutsideReasonableRange_ReturnsDetailedError(string datetime)
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = datetime },
            ["assets"] = new JsonObject()
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("outside reasonable range"));
    }

    #endregion

    #region Link Validation Tests

    [Fact]
    public void ValidateItem_WithLinkMissingHref_ReturnsDetailedError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject(),
            ["links"] = new JsonArray
            {
                new JsonObject { ["rel"] = "self" } // Missing href
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.Field == "links[0].href").Subject;
        error.Message.Should().Contain("Required field is missing or empty");
        error.ExpectedFormat.Should().Contain("URL");
        error.Example.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateItem_WithLinkMissingRel_ReturnsDetailedError()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject(),
            ["properties"] = new JsonObject { ["datetime"] = "2024-01-01T00:00:00Z" },
            ["assets"] = new JsonObject(),
            ["links"] = new JsonArray
            {
                new JsonObject { ["href"] = "https://example.com" } // Missing rel
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.Field == "links[0].rel").Subject;
        error.Message.Should().Contain("Required field is missing or empty");
        error.Example.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Extent Validation Tests

    [Fact]
    public void ValidateCollection_WithMissingSpatialExtent_ReturnsDetailedError()
    {
        // Arrange
        var collection = new JsonObject
        {
            ["id"] = "test-collection",
            ["description"] = "Test description",
            ["license"] = "MIT",
            ["extent"] = new JsonObject
            {
                ["temporal"] = new JsonObject { ["interval"] = new JsonArray { new JsonArray { "2024-01-01T00:00:00Z", null } } }
            }
        };

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle(e => e.Field == "extent.spatial").Subject;
        error.Message.Should().Contain("Required field is missing or not an object");
        error.ExpectedFormat.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateCollection_WithTemporalIntervalStartAfterEnd_ReturnsDetailedError()
    {
        // Arrange
        var collection = new JsonObject
        {
            ["id"] = "test-collection",
            ["description"] = "Test description",
            ["license"] = "MIT",
            ["extent"] = new JsonObject
            {
                ["spatial"] = new JsonObject { ["bbox"] = new JsonArray { new JsonArray { -180, -90, 180, 90 } } },
                ["temporal"] = new JsonObject
                {
                    ["interval"] = new JsonArray
                    {
                        new JsonArray { "2024-12-31T23:59:59Z", "2024-01-01T00:00:00Z" } // Start after end
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateCollection(collection);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field.Contains("extent.temporal.interval") && e.Message.Contains("less than or equal to"));
    }

    #endregion

    #region Error Formatting Tests

    [Fact]
    public void StacValidationError_ToString_FormatsAllFields()
    {
        // Arrange
        var error = new StacValidationError
        {
            Field = "bbox",
            Message = "Invalid bbox count",
            ActualValue = "[1, 2, 3]",
            ExpectedFormat = "4 or 6 coordinates",
            Example = "[-180, -90, 180, 90]"
        };

        // Act
        var formatted = error.ToString();

        // Assert
        formatted.Should().Contain("Field 'bbox'");
        formatted.Should().Contain("Invalid bbox count");
        formatted.Should().Contain("Actual value: '[1, 2, 3]'");
        formatted.Should().Contain("Expected format: 4 or 6 coordinates");
        formatted.Should().Contain("Example: [-180, -90, 180, 90]");
    }

    [Fact]
    public void StacValidationError_ToString_HandlesMinimalFields()
    {
        // Arrange
        var error = new StacValidationError
        {
            Field = "id",
            Message = "Required field is missing"
        };

        // Act
        var formatted = error.ToString();

        // Assert
        formatted.Should().Contain("Field 'id'");
        formatted.Should().Contain("Required field is missing");
        formatted.Should().NotContain("Actual value");
        formatted.Should().NotContain("Expected format");
        formatted.Should().NotContain("Example");
    }

    #endregion
}

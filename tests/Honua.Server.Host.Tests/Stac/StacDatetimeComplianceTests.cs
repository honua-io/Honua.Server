using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Host.Stac;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

/// <summary>
/// Tests for STAC 1.0+ datetime field compliance.
/// STAC 1.0 requires that Items have EITHER:
/// - A non-null "datetime" field, OR
/// - Both "start_datetime" and "end_datetime" fields (with "datetime" set to null)
/// </summary>
public sealed class StacDatetimeComplianceTests
{
    private readonly StacValidationService _validator;

    public StacDatetimeComplianceTests()
    {
        _validator = new StacValidationService(NullLogger<StacValidationService>.Instance);
    }

    [Fact]
    public void ValidateItem_WithValidDatetime_ShouldPass()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject
            {
                ["type"] = "Point",
                ["coordinates"] = new JsonArray { 0, 0 }
            },
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-01T00:00:00Z"
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject
                {
                    ["href"] = "https://example.com/data.tif"
                }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeTrue("item has a valid datetime field");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateItem_WithNullDatetimeAndBothStartEnd_ShouldPass()
    {
        // Arrange - This is the STAC 1.0 compliant way to represent time ranges
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject
            {
                ["type"] = "Point",
                ["coordinates"] = new JsonArray { 0, 0 }
            },
            ["properties"] = new JsonObject
            {
                ["datetime"] = null, // Explicitly null
                ["start_datetime"] = "2024-01-01T00:00:00Z",
                ["end_datetime"] = "2024-01-31T23:59:59Z"
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeTrue("null datetime with start/end is valid per STAC 1.0");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateItem_WithMissingDatetime_ShouldFail()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = new JsonObject
            {
                ["type"] = "Point",
                ["coordinates"] = new JsonArray { 0, 0 }
            },
            ["properties"] = new JsonObject
            {
                // No datetime, start_datetime, or end_datetime
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse("datetime is missing and no start/end provided");
        result.Errors.Should().ContainSingle(e => e.Field == "properties.datetime");
        result.Errors[0].Message.Should().Contain("datetime");
        result.Errors[0].Message.Should().Contain("start_datetime");
        result.Errors[0].Message.Should().Contain("end_datetime");
    }

    [Fact]
    public void ValidateItem_WithNullDatetimeButMissingStartDatetime_ShouldFail()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = null,
                ["end_datetime"] = "2024-01-31T23:59:59Z"
                // Missing start_datetime
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse("null datetime requires BOTH start and end");
        result.Errors.Should().Contain(e => e.Field == "properties.datetime");
    }

    [Fact]
    public void ValidateItem_WithNullDatetimeButMissingEndDatetime_ShouldFail()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = null,
                ["start_datetime"] = "2024-01-01T00:00:00Z"
                // Missing end_datetime
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse("null datetime requires BOTH start and end");
        result.Errors.Should().Contain(e => e.Field == "properties.datetime");
    }

    [Fact]
    public void ValidateItem_WithStartAfterEnd_ShouldFail()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = null,
                ["start_datetime"] = "2024-12-31T23:59:59Z",
                ["end_datetime"] = "2024-01-01T00:00:00Z" // End before start!
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse("start_datetime must be <= end_datetime");
        result.Errors.Should().Contain(e =>
            e.Field == "properties" &&
            e.Message.Contains("start_datetime") &&
            e.Message.Contains("end_datetime"));
    }

    [Fact]
    public void ValidateItem_WithInvalidDatetimeFormat_ShouldFail()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = "not-a-valid-datetime"
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse("datetime must be RFC 3339 format");
        result.Errors.Should().Contain(e =>
            e.Field == "properties.datetime" &&
            e.Message.Contains("Invalid datetime format"));
    }

    [Fact]
    public void ValidateItem_WithDatetimeAndStartEnd_ShouldAllowBoth()
    {
        // Arrange - STAC allows both datetime AND start/end to be present
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-15T12:00:00Z",
                ["start_datetime"] = "2024-01-01T00:00:00Z",
                ["end_datetime"] = "2024-01-31T23:59:59Z"
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeTrue("STAC allows both datetime and start/end together");
    }

    [Fact]
    public void ValidateItem_WithDatetimeAsNumber_ShouldFail()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = 1234567890 // Number instead of string
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse("datetime must be a string");
        result.Errors.Should().Contain(e =>
            e.Field == "properties.datetime" &&
            e.Message.Contains("must be a string"));
    }

    [Fact]
    public void ValidateItem_WithFutureDatetime_ShouldFail()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2999-12-31T23:59:59Z" // Far future
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse("datetime should not be too far in the future");
        result.Errors.Should().Contain(e =>
            e.Field == "properties.datetime" &&
            e.Message.Contains("outside reasonable range"));
    }

    [Fact]
    public void ValidateItem_WithVeryOldDatetime_ShouldFail()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = "1800-01-01T00:00:00Z" // Before 1900
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeFalse("datetime should not be before 1900");
        result.Errors.Should().Contain(e =>
            e.Field == "properties.datetime" &&
            e.Message.Contains("outside reasonable range"));
    }

    [Fact]
    public void ValidateItem_WithTimezone_ShouldPass()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-01T12:00:00+05:30" // With timezone offset
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeTrue("datetime with timezone offset is valid RFC 3339");
    }

    [Fact]
    public void ValidateItem_WithMilliseconds_ShouldPass()
    {
        // Arrange
        var item = new JsonObject
        {
            ["type"] = "Feature",
            ["id"] = "test-item",
            ["geometry"] = null,
            ["properties"] = new JsonObject
            {
                ["datetime"] = "2024-01-01T12:00:00.123Z" // With milliseconds
            },
            ["assets"] = new JsonObject
            {
                ["data"] = new JsonObject { ["href"] = "https://example.com/data.tif" }
            }
        };

        // Act
        var result = _validator.ValidateItem(item);

        // Assert
        result.IsValid.Should().BeTrue("datetime with milliseconds is valid RFC 3339");
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using FluentAssertions;
using Honua.Server.Enterprise.Sensors.Query;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Sensors.Query;

[Trait("Category", "Unit")]
[Trait("Feature", "SensorThings")]
[Trait("Component", "QueryParser")]
public class ExpandOptionsTests
{
    [Theory]
    [InlineData("Locations", 1)]
    [InlineData("Locations,Datastreams", 2)]
    [InlineData("Locations, Datastreams, HistoricalLocations", 3)]
    [InlineData("Thing,Sensor,ObservedProperty", 3)]
    public void Parse_WithCommaSeparatedProperties_ReturnsCorrectPropertyList(string expand, int expectedCount)
    {
        // Act
        var options = ExpandOptions.Parse(expand);

        // Assert
        options.Should().NotBeNull();
        options.Properties.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void Parse_WithSingleProperty_ReturnsCorrectProperty()
    {
        // Arrange
        const string expand = "Datastreams";

        // Act
        var options = ExpandOptions.Parse(expand);

        // Assert
        options.Properties.Should().HaveCount(1);
        options.Properties[0].Should().Be("Datastreams");
    }

    [Fact]
    public void Parse_WithWhitespace_TrimsProperties()
    {
        // Arrange
        const string expand = " Locations , Datastreams  ,  HistoricalLocations ";

        // Act
        var options = ExpandOptions.Parse(expand);

        // Assert
        options.Properties.Should().HaveCount(3);
        options.Properties[0].Should().Be("Locations");
        options.Properties[1].Should().Be("Datastreams");
        options.Properties[2].Should().Be("HistoricalLocations");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithNullOrEmptyString_ReturnsEmptyPropertyList(string? expand)
    {
        // Act
        var options = ExpandOptions.Parse(expand!);

        // Assert
        options.Should().NotBeNull();
        options.Properties.Should().BeEmpty();
    }

    [Fact]
    public void DefaultMaxDepth_IsTwo()
    {
        // Arrange
        const string expand = "Locations";

        // Act
        var options = ExpandOptions.Parse(expand);

        // Assert
        options.MaxDepth.Should().Be(2);
    }
}

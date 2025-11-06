using FluentAssertions;
using Honua.MapSDK.Models;
using Xunit;

namespace Honua.MapSDK.Tests.ServiceTests;

/// <summary>
/// Tests for filter definition models and expression generation
/// </summary>
public class FilterDefinitionTests
{
    #region SpatialFilter Tests

    [Fact]
    public void SpatialFilter_BoundingBox_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var filter = new SpatialFilter
        {
            SpatialType = SpatialFilterType.BoundingBox,
            BoundingBox = new[] { -122.5, 37.5, -122.0, 38.0 }
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        expression.Should().NotBeNull();
        var expr = expression as dynamic;
        expr.type.Should().Be("bbox");
        ((double[])expr.bbox).Should().BeEquivalentTo(new[] { -122.5, 37.5, -122.0, 38.0 });
    }

    [Fact]
    public void SpatialFilter_Circle_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var filter = new SpatialFilter
        {
            SpatialType = SpatialFilterType.Circle,
            Center = new[] { -122.4194, 37.7749 },
            Radius = 5000
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        expression.Should().NotBeNull();
        var expr = expression as dynamic;
        expr.type.Should().Be("circle");
        ((double[])expr.center).Should().BeEquivalentTo(new[] { -122.4194, 37.7749 });
        ((double)expr.radius).Should().Be(5000);
    }

    [Fact]
    public void SpatialFilter_Polygon_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var polygon = new List<double[]>
        {
            new[] { -122.5, 37.5 },
            new[] { -122.5, 38.0 },
            new[] { -122.0, 38.0 },
            new[] { -122.0, 37.5 },
            new[] { -122.5, 37.5 }
        };

        var filter = new SpatialFilter
        {
            SpatialType = SpatialFilterType.Polygon,
            Polygon = polygon
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        expression.Should().NotBeNull();
        var expr = expression as dynamic;
        expr.type.Should().Be("polygon");
        ((List<double[]>)expr.coordinates).Should().BeEquivalentTo(polygon);
    }

    [Fact]
    public void SpatialFilter_WithinDistance_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var filter = new SpatialFilter
        {
            SpatialType = SpatialFilterType.WithinDistance,
            Center = new[] { -122.4194, 37.7749 },
            Distance = 1000
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        expression.Should().NotBeNull();
        var expr = expression as dynamic;
        expr.type.Should().Be("distance");
        ((double[])expr.center).Should().BeEquivalentTo(new[] { -122.4194, 37.7749 });
        ((double)expr.distance).Should().Be(1000);
    }

    [Fact]
    public void SpatialFilter_ToString_ShouldReturnReadableDescription()
    {
        // Arrange & Act
        var bboxFilter = new SpatialFilter { SpatialType = SpatialFilterType.BoundingBox };
        var circleFilter = new SpatialFilter { SpatialType = SpatialFilterType.Circle, Radius = 5000 };
        var distanceFilter = new SpatialFilter { SpatialType = SpatialFilterType.WithinDistance, Distance = 1000 };

        // Assert
        bboxFilter.ToString().Should().Be("Within map extent");
        circleFilter.ToString().Should().Be("Within 5000m radius");
        distanceFilter.ToString().Should().Be("Within 1000m of point");
    }

    #endregion

    #region AttributeFilter Tests

    [Fact]
    public void AttributeFilter_Equals_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var filter = new AttributeFilter
        {
            Field = "name",
            Operator = AttributeOperator.Equals,
            Value = "San Francisco"
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        var expr = expression as object[];
        expr.Should().NotBeNull();
        expr![0].Should().Be("==");
        ((object[])expr[1])[0].Should().Be("get");
        ((object[])expr[1])[1].Should().Be("name");
        expr[2].Should().Be("San Francisco");
    }

    [Fact]
    public void AttributeFilter_GreaterThan_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var filter = new AttributeFilter
        {
            Field = "population",
            Operator = AttributeOperator.GreaterThan,
            Value = 1000000,
            FieldType = FieldType.Number
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        var expr = expression as object[];
        expr.Should().NotBeNull();
        expr![0].Should().Be(">");
        expr[2].Should().Be(1000000);
    }

    [Fact]
    public void AttributeFilter_In_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var filter = new AttributeFilter
        {
            Field = "state",
            Operator = AttributeOperator.In,
            Values = new List<object> { "California", "Oregon", "Washington" }
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        var expr = expression as object[];
        expr.Should().NotBeNull();
        expr![0].Should().Be("in");
        var values = expr[2] as object[];
        values.Should().NotBeNull();
        values!.Should().Contain("California");
        values.Should().Contain("Oregon");
        values.Should().Contain("Washington");
    }

    [Fact]
    public void AttributeFilter_IsNull_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var filter = new AttributeFilter
        {
            Field = "description",
            Operator = AttributeOperator.IsNull
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        var expr = expression as object[];
        expr.Should().NotBeNull();
        expr![0].Should().Be("==");
        expr[2].Should().BeNull();
    }

    [Fact]
    public void AttributeFilter_ToString_ShouldReturnReadableDescription()
    {
        // Arrange
        var equalsFilter = new AttributeFilter
        {
            Field = "name",
            Label = "City Name",
            Operator = AttributeOperator.Equals,
            Value = "San Francisco"
        };

        var inFilter = new AttributeFilter
        {
            Field = "state",
            Operator = AttributeOperator.In,
            Values = new List<object> { "CA", "OR", "WA", "NV", "AZ" }
        };

        var nullFilter = new AttributeFilter
        {
            Field = "description",
            Operator = AttributeOperator.IsNull
        };

        // Act & Assert
        equalsFilter.ToString().Should().Be("City Name = San Francisco");
        inFilter.ToString().Should().Contain("state in [CA, OR, WA (+2 more)]");
        nullFilter.ToString().Should().Be("description is null");
    }

    [Theory]
    [InlineData(AttributeOperator.NotEquals, "!=")]
    [InlineData(AttributeOperator.GreaterThan, ">")]
    [InlineData(AttributeOperator.LessThan, "<")]
    [InlineData(AttributeOperator.GreaterThanOrEqual, ">=")]
    [InlineData(AttributeOperator.LessThanOrEqual, "<=")]
    public void AttributeFilter_ComparisonOperators_ShouldGenerateCorrectExpression(
        AttributeOperator op,
        string expectedOp)
    {
        // Arrange
        var filter = new AttributeFilter
        {
            Field = "value",
            Operator = op,
            Value = 100
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        var expr = expression as object[];
        expr.Should().NotBeNull();
        expr![0].Should().Be(expectedOp);
    }

    #endregion

    #region TemporalFilter Tests

    [Fact]
    public void TemporalFilter_Before_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var filter = new TemporalFilter
        {
            DateField = "timestamp",
            TemporalType = TemporalFilterType.Before,
            StartDate = date
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        var expr = expression as object[];
        expr.Should().NotBeNull();
        expr![0].Should().Be("<");
        ((object[])expr[1])[1].Should().Be("timestamp");
        expr[2].Should().Be(date.ToString("o"));
    }

    [Fact]
    public void TemporalFilter_Between_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var filter = new TemporalFilter
        {
            DateField = "timestamp",
            TemporalType = TemporalFilterType.Between,
            StartDate = startDate,
            EndDate = endDate
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        var expr = expression as object[];
        expr.Should().NotBeNull();
        expr![0].Should().Be("all");
        expr.Should().HaveCount(3);
    }

    [Fact]
    public void TemporalFilter_LastNDays_ShouldCalculateCorrectDates()
    {
        // Arrange
        var filter = new TemporalFilter
        {
            DateField = "timestamp",
            TemporalType = TemporalFilterType.LastN,
            RelativeValue = 7,
            RelativeUnit = RelativeTimeUnit.Days
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        filter.EndDate.Should().NotBeNull();
        filter.StartDate.Should().NotBeNull();
        filter.EndDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        filter.StartDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(-7), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void TemporalFilter_LastNMonths_ShouldCalculateCorrectDates()
    {
        // Arrange
        var filter = new TemporalFilter
        {
            DateField = "timestamp",
            TemporalType = TemporalFilterType.LastN,
            RelativeValue = 3,
            RelativeUnit = RelativeTimeUnit.Months
        };

        // Act
        var expression = filter.ToExpression();

        // Assert
        filter.EndDate.Should().NotBeNull();
        filter.StartDate.Should().NotBeNull();
        filter.StartDate.Should().BeCloseTo(DateTime.UtcNow.AddMonths(-3), TimeSpan.FromDays(1));
    }

    [Fact]
    public void TemporalFilter_ToString_ShouldReturnReadableDescription()
    {
        // Arrange
        var beforeFilter = new TemporalFilter
        {
            TemporalType = TemporalFilterType.Before,
            StartDate = new DateTime(2024, 1, 1)
        };

        var betweenFilter = new TemporalFilter
        {
            TemporalType = TemporalFilterType.Between,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31)
        };

        var lastNFilter = new TemporalFilter
        {
            TemporalType = TemporalFilterType.LastN,
            RelativeValue = 30,
            RelativeUnit = RelativeTimeUnit.Days
        };

        // Act & Assert
        beforeFilter.ToString().Should().Be("Before 2024-01-01");
        betweenFilter.ToString().Should().Be("2024-01-01 to 2024-12-31");
        lastNFilter.ToString().Should().Be("Last 30 days");
    }

    #endregion

    #region Base FilterDefinition Tests

    [Fact]
    public void FilterDefinition_ShouldHaveUniqueId()
    {
        // Arrange & Act
        var filter1 = new SpatialFilter();
        var filter2 = new SpatialFilter();

        // Assert
        filter1.Id.Should().NotBe(filter2.Id);
        filter1.Id.Should().NotBeNullOrEmpty();
        filter2.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FilterDefinition_Type_ShouldMatchFilterClass()
    {
        // Arrange & Act
        var spatialFilter = new SpatialFilter();
        var attributeFilter = new AttributeFilter { Field = "test" };
        var temporalFilter = new TemporalFilter { DateField = "date" };

        // Assert
        spatialFilter.Type.Should().Be(FilterType.Spatial);
        attributeFilter.Type.Should().Be(FilterType.Attribute);
        temporalFilter.Type.Should().Be(FilterType.Temporal);
    }

    [Fact]
    public void FilterDefinition_IsActive_ShouldDefaultToFalse()
    {
        // Arrange & Act
        var filter = new AttributeFilter { Field = "test" };

        // Assert
        filter.IsActive.Should().BeFalse();
    }

    [Fact]
    public void FilterDefinition_Label_ShouldBeSettable()
    {
        // Arrange
        var filter = new AttributeFilter
        {
            Field = "population",
            Label = "City Population"
        };

        // Act & Assert
        filter.Label.Should().Be("City Population");
    }

    #endregion
}

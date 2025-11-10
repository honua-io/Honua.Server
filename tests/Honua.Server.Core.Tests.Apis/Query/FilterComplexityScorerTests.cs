// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Query.Filter;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Query;

public class FilterComplexityScorerTests
{
    private readonly FilterComplexityScorer _scorer;

    public FilterComplexityScorerTests()
    {
        _scorer = new FilterComplexityScorer();
    }

    [Fact]
    public void CalculateComplexity_WithSimpleEquality_ReturnsLowScore()
    {
        // Arrange
        var filter = new QueryFilter
        {
            Type = FilterType.Equality,
            Field = "name",
            Value = "test"
        };

        // Act
        var score = _scorer.CalculateComplexity(filter);

        // Assert
        score.Should().BeLessThan(10);
    }

    [Fact]
    public void CalculateComplexity_WithLogicalAnd_ReturnsHigherScore()
    {
        // Arrange
        var filter = new QueryFilter
        {
            Type = FilterType.And,
            Children = new[]
            {
                new QueryFilter { Type = FilterType.Equality, Field = "name", Value = "test" },
                new QueryFilter { Type = FilterType.GreaterThan, Field = "age", Value = 18 }
            }
        };

        // Act
        var score = _scorer.CalculateComplexity(filter);

        // Assert
        score.Should().BeGreaterThan(5);
    }

    [Fact]
    public void CalculateComplexity_WithNestedLogical_ReturnsHighScore()
    {
        // Arrange
        var filter = new QueryFilter
        {
            Type = FilterType.And,
            Children = new[]
            {
                new QueryFilter
                {
                    Type = FilterType.Or,
                    Children = new[]
                    {
                        new QueryFilter { Type = FilterType.Equality, Field = "status", Value = "active" },
                        new QueryFilter { Type = FilterType.Equality, Field = "status", Value = "pending" }
                    }
                },
                new QueryFilter { Type = FilterType.GreaterThan, Field = "priority", Value = 5 }
            }
        };

        // Act
        var score = _scorer.CalculateComplexity(filter);

        // Assert
        score.Should().BeGreaterThan(10);
    }

    [Fact]
    public void CalculateComplexity_WithSpatialFilter_ReturnsModerateScore()
    {
        // Arrange
        var filter = new QueryFilter
        {
            Type = FilterType.Spatial,
            SpatialOperation = "INTERSECTS",
            Field = "geometry",
            GeometryValue = "POINT(10 20)"
        };

        // Act
        var score = _scorer.CalculateComplexity(filter);

        // Assert
        score.Should().BeGreaterThan(5);
        score.Should().BeLessThan(50);
    }

    [Fact]
    public void IsComplexityAcceptable_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var filter = new QueryFilter
        {
            Type = FilterType.Equality,
            Field = "name",
            Value = "test"
        };
        var maxComplexity = 100;

        // Act
        var isAcceptable = _scorer.IsComplexityAcceptable(filter, maxComplexity);

        // Assert
        isAcceptable.Should().BeTrue();
    }

    [Fact]
    public void IsComplexityAcceptable_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var filter = CreateDeeplyNestedFilter(20); // Very complex filter
        var maxComplexity = 10;

        // Act
        var isAcceptable = _scorer.IsComplexityAcceptable(filter, maxComplexity);

        // Assert
        isAcceptable.Should().BeFalse();
    }

    private QueryFilter CreateDeeplyNestedFilter(int depth)
    {
        if (depth == 0)
        {
            return new QueryFilter { Type = FilterType.Equality, Field = "test", Value = "value" };
        }

        return new QueryFilter
        {
            Type = FilterType.And,
            Children = new[]
            {
                CreateDeeplyNestedFilter(depth - 1),
                CreateDeeplyNestedFilter(depth - 1)
            }
        };
    }
}

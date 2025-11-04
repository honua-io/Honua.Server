using Xunit;
using FluentAssertions;
using Honua.Cli.AI.Services.VectorSearch;

namespace Honua.Cli.AI.Tests.Services.VectorSearch;

[Trait("Category", "Unit")]
public sealed class PatternConfidenceTests
{
    [Fact]
    public void Calculate_WithHighQualityPattern_ReturnsHighConfidence()
    {
        // Arrange: Excellent match, high success rate, many deployments
        var result = new PatternSearchResult
        {
            Id = "test-1",
            PatternName = "High Quality Pattern",
            Score = 0.95,           // Excellent semantic match
            SuccessRate = 0.98,     // 98% success rate
            DeploymentCount = 50,   // 50 deployments
            CloudProvider = "aws"
        };

        // Act
        var confidence = PatternConfidence.Calculate(result);

        // Assert
        confidence.Level.Should().Be("High");
        confidence.Overall.Should().BeGreaterThan(0.8);
        confidence.VectorSimilarity.Should().Be(0.95);
        confidence.SuccessRate.Should().Be(0.98);
        confidence.DeploymentCount.Should().Be(50);
        confidence.Explanation.Should().Contain("High confidence");
    }

    [Fact]
    public void Calculate_WithMediumQualityPattern_ReturnsMediumConfidence()
    {
        // Arrange: Good match, decent success rate, moderate deployments
        var result = new PatternSearchResult
        {
            Id = "test-2",
            PatternName = "Medium Quality Pattern",
            Score = 0.75,           // Good semantic match
            SuccessRate = 0.85,     // 85% success rate
            DeploymentCount = 15,   // 15 deployments
            CloudProvider = "azure"
        };

        // Act
        var confidence = PatternConfidence.Calculate(result);

        // Assert
        confidence.Level.Should().Be("Medium");
        confidence.Overall.Should().BeInRange(0.6, 0.8);
        confidence.Explanation.Should().Contain("Medium confidence");
    }

    [Fact]
    public void Calculate_WithLowQualityPattern_ReturnsLowConfidence()
    {
        // Arrange: Weak match, low success rate, few deployments
        var result = new PatternSearchResult
        {
            Id = "test-3",
            PatternName = "Low Quality Pattern",
            Score = 0.45,           // Weak semantic match
            SuccessRate = 0.65,     // 65% success rate
            DeploymentCount = 3,    // Only 3 deployments
            CloudProvider = "gcp"
        };

        // Act
        var confidence = PatternConfidence.Calculate(result);

        // Assert
        confidence.Level.Should().Be("Low");
        confidence.Overall.Should().BeLessThan(0.6);
        confidence.Explanation.Should().Contain("Low confidence");
        confidence.Explanation.Should().Contain("manual review");
    }

    [Fact]
    public void GetConfidence_ExtensionMethod_Works()
    {
        // Arrange
        var result = new PatternSearchResult
        {
            Id = "test-4",
            PatternName = "Test Pattern",
            Score = 0.8,
            SuccessRate = 0.9,
            DeploymentCount = 20,
            CloudProvider = "aws"
        };

        // Act
        var confidence = result.GetConfidence();

        // Assert
        confidence.Should().NotBeNull();
        confidence.Level.Should().BeOneOf("High", "Medium");
    }

    [Fact]
    public void Calculate_WeightsFactorsCorrectly()
    {
        // Arrange: Test that the formula is correct
        // Formula: (similarity * 0.4) + (successRate * 0.4) + (min(deploymentCount/50, 1.0) * 0.2)
        var result = new PatternSearchResult
        {
            Id = "test-5",
            PatternName = "Formula Test",
            Score = 0.5,            // 0.5 * 0.4 = 0.2
            SuccessRate = 0.5,      // 0.5 * 0.4 = 0.2
            DeploymentCount = 25,   // (25/50) * 0.2 = 0.1
            CloudProvider = "aws"
        };

        // Act
        var confidence = PatternConfidence.Calculate(result);

        // Assert
        // Expected: 0.2 + 0.2 + 0.1 = 0.5
        confidence.Overall.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void Calculate_WithManyDeployments_SaturatesAt50()
    {
        // Arrange: Deployment count saturates at 50 (doesn't give extra credit for 100+)
        var result1 = new PatternSearchResult
        {
            Id = "test-6a",
            PatternName = "50 Deployments",
            Score = 0.8,
            SuccessRate = 0.9,
            DeploymentCount = 50,
            CloudProvider = "aws"
        };

        var result2 = new PatternSearchResult
        {
            Id = "test-6b",
            PatternName = "100 Deployments",
            Score = 0.8,
            SuccessRate = 0.9,
            DeploymentCount = 100,  // Should have same weight as 50
            CloudProvider = "aws"
        };

        // Act
        var confidence1 = PatternConfidence.Calculate(result1);
        var confidence2 = PatternConfidence.Calculate(result2);

        // Assert
        confidence1.Overall.Should().BeApproximately(confidence2.Overall, 0.001);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var result = new PatternSearchResult
        {
            Id = "test-7",
            PatternName = "ToString Test",
            Score = 0.9,
            SuccessRate = 0.95,
            DeploymentCount = 30,
            CloudProvider = "aws"
        };

        // Act
        var confidence = PatternConfidence.Calculate(result);
        var str = confidence.ToString();

        // Assert
        str.Should().Contain("High confidence");
        str.Should().Contain("%");
    }

    [Fact]
    public void Calculate_WithNullPattern_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => PatternConfidence.Calculate(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

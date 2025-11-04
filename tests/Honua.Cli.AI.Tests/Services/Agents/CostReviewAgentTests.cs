using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class CostReviewAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly CostReviewAgent _agent;

    public CostReviewAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _agent = new CostReviewAgent(_kernel, _mockLlmProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CostReviewAgent(null!, _mockLlmProvider.Object));
    }

    [Fact]
    public async Task ReviewAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var artifactType = "terraform";
        var content = "resource \"aws_instance\" \"example\" { instance_type = \"t2.micro\" }";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "[]",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ReviewAsync(artifactType, content, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<CostReviewResult>();
    }

    [Fact]
    public async Task ReviewAsync_WithCostOptimization_IncludesRecommendations()
    {
        // Arrange
        var artifactType = "terraform";
        var content = "resource \"aws_instance\" \"example\" { instance_type = \"m5.24xlarge\" }";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"[
                    {
                        ""severity"": ""High"",
                        ""category"": ""Instance Size"",
                        ""description"": ""Using expensive m5.24xlarge instance"",
                        ""recommendation"": ""Consider using smaller instance type"",
                        ""estimatedMonthlySavings"": 2000.0
                    }
                ]",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ReviewAsync(artifactType, content, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Issues.Should().NotBeEmpty();
    }
}

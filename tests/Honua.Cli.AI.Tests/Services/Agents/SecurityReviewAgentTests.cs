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
public class SecurityReviewAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly SecurityReviewAgent _agent;

    public SecurityReviewAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _agent = new SecurityReviewAgent(_kernel, _mockLlmProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SecurityReviewAgent(null!, _mockLlmProvider.Object));
    }

    [Fact]
    public async Task ReviewAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var artifactType = "terraform";
        var content = "resource \"aws_instance\" \"example\" {}";
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
        result.Should().BeOfType<SecurityReviewResult>();
    }

    [Fact]
    public async Task ReviewAsync_WithVulnerableConfiguration_IncludesFindings()
    {
        // Arrange
        var artifactType = "terraform";
        var content = "password = \"hardcoded123\"";
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
                        ""severity"": ""Critical"",
                        ""category"": ""Hardcoded Credentials"",
                        ""description"": ""Password is hardcoded in configuration"",
                        ""recommendation"": ""Use environment variables or secrets manager"",
                        ""cweId"": ""CWE-798""
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

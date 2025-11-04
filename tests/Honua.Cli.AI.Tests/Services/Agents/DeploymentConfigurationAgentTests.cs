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
public class DeploymentConfigurationAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly DeploymentConfigurationAgent _agent;

    public DeploymentConfigurationAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _agent = new DeploymentConfigurationAgent(_kernel, _mockLlmProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DeploymentConfigurationAgent(null!, _mockLlmProvider.Object));
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_DoesNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() => new DeploymentConfigurationAgent(_kernel, null));
        Assert.Null(exception);
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Deploy Honua to Azure AKS with PostgreSQL database";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""cloudProvider"": ""Azure"",
                    ""deploymentTarget"": ""AKS"",
                    ""databaseType"": ""PostgreSQL"",
                    ""regions"": [""eastus""],
                    ""environment"": ""production"",
                    ""summary"": ""Azure AKS deployment with PostgreSQL""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("DeploymentConfiguration");
        result.Action.Should().Be("ProcessDeploymentRequest");
        result.Message.Should().Contain("deployment configuration");
    }

    [Fact]
    public async Task ProcessAsync_WithDryRun_ReturnsGeneratedMessage()
    {
        // Arrange
        var request = "Deploy to AWS ECS with Redis cache";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""cloudProvider"": ""AWS"",
                    ""deploymentTarget"": ""ECS"",
                    ""cacheType"": ""Redis"",
                    ""regions"": [""us-east-1""],
                    ""summary"": ""AWS ECS deployment with Redis""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("dry-run");
    }

    [Fact]
    public async Task ProcessAsync_WithInvalidConfiguration_SucceedsWithDefaults()
    {
        // Arrange
        var request = "Deploy without specifying target";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""cloudProvider"": """",
                    ""deploymentTarget"": """",
                    ""summary"": ""Invalid deployment""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Agent provides defaults or fallback guidance when configuration is unclear
        result.Success.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WithLlmFailure_SucceedsWithDefaults()
    {
        // Arrange
        var request = "Deploy to GCP";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "Error occurred",
                Model = "test-model",
                Success = false,
                ErrorMessage = "LLM service unavailable"
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Agent falls back to default configuration when LLM fails
        result.Success.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WithMultiRegionDeployment_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Deploy to AWS in us-east-1 and eu-west-1 for high availability";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""cloudProvider"": ""AWS"",
                    ""deploymentTarget"": ""ECS"",
                    ""regions"": [""us-east-1"", ""eu-west-1""],
                    ""highAvailability"": true,
                    ""summary"": ""Multi-region AWS deployment""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }
}

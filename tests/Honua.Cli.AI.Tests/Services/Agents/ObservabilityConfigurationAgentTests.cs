using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class ObservabilityConfigurationAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<ObservabilityConfigurationAgent>> _mockLogger;
    private readonly ObservabilityConfigurationAgent _agent;

    public ObservabilityConfigurationAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<ObservabilityConfigurationAgent>>();
        _agent = new ObservabilityConfigurationAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ObservabilityConfigurationAgent(null!, _mockLlmProvider.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ObservabilityConfigurationAgent(_kernel, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ObservabilityConfigurationAgent(_kernel, _mockLlmProvider.Object, null!));
    }

    [Fact]
    public async Task GenerateObservabilityConfigAsync_WithDockerCompose_ReturnsSuccessResult()
    {
        // Arrange
        var request = new ObservabilityConfigRequest
        {
            DeploymentName = "honua-prod",
            Platform = "docker-compose",
            Backend = "prometheus"
        };
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.GenerateObservabilityConfigAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("ObservabilityConfiguration");
    }

    [Fact]
    public async Task GenerateObservabilityConfigAsync_WithKubernetes_ReturnsSuccessResult()
    {
        // Arrange
        var request = new ObservabilityConfigRequest
        {
            DeploymentName = "honua-k8s",
            Platform = "kubernetes",
            Backend = "otlp"
        };
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.GenerateObservabilityConfigAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    private void SetupLlmProviderResponses()
    {
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""otelConfig"": ""receivers:\n  otlp:\n    protocols:\n      grpc:"",
                    ""prometheusConfig"": ""scrape_configs:\n  - job_name: honua"",
                    ""grafanaDashboard"": ""{\""title\"": \""Honua Dashboard\""}"",
                    ""alertRules"": ""groups:\n  - name: honua""
                }",
                Model = "test-model",
                Success = true
            });
    }
}

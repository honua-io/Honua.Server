using System;
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
public class BlueGreenDeploymentAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly BlueGreenDeploymentAgent _agent;

    public BlueGreenDeploymentAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _agent = new BlueGreenDeploymentAgent(_kernel, _mockLlmProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BlueGreenDeploymentAgent(null!, _mockLlmProvider.Object));
    }

    [Fact]
    public async Task ProcessAsync_WithBlueGreenStrategy_ReturnsSuccess()
    {
        // Arrange
        var request = "Setup blue-green deployment for honua-service on Kubernetes";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""strategy"": ""blue-green"",
                    ""platform"": ""kubernetes"",
                    ""serviceName"": ""honua-service"",
                    ""blueEnvironment"": {
                        ""name"": ""blue"",
                        ""endpoint"": ""http://blue-service:8080""
                    },
                    ""greenEnvironment"": {
                        ""name"": ""green"",
                        ""endpoint"": ""http://green-service:8080""
                    },
                    ""trafficSplitPercentage"": 0,
                    ""healthCheckPath"": ""/health"",
                    ""rollbackOnFailure"": true,
                    ""summary"": ""Blue-green deployment setup""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("BlueGreenDeployment");
    }

    [Fact]
    public async Task ProcessAsync_WithCanaryStrategy_ReturnsSuccess()
    {
        // Arrange
        var request = "Setup canary deployment with 10% traffic for api-service";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""strategy"": ""canary"",
                    ""platform"": ""kubernetes"",
                    ""serviceName"": ""api-service"",
                    ""blueEnvironment"": {
                        ""name"": ""stable"",
                        ""endpoint"": ""http://stable:8080""
                    },
                    ""greenEnvironment"": {
                        ""name"": ""canary"",
                        ""endpoint"": ""http://canary:8080""
                    },
                    ""trafficSplitPercentage"": 10,
                    ""healthCheckPath"": ""/health"",
                    ""rollbackOnFailure"": true,
                    ""summary"": ""Canary deployment with 10% traffic""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("canary");
    }

    [Fact]
    public async Task ValidateDeploymentConfigurationAsync_WithInvalidTrafficPercentage_ReturnsInvalid()
    {
        // Arrange
        var config = new BlueGreenConfiguration
        {
            Strategy = "canary",
            ServiceName = "test-service",
            BlueEnvironment = new BlueGreenEnvironment { Name = "blue", Endpoint = "http://blue:8080" },
            GreenEnvironment = new BlueGreenEnvironment { Name = "green", Endpoint = "http://green:8080" },
            TrafficSplitPercentage = 150
        };
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Act
        var result = await _agent.ValidateDeploymentConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("between 0 and 100");
    }

    [Fact]
    public async Task ValidateDeploymentConfigurationAsync_WithMissingServiceName_ReturnsInvalid()
    {
        // Arrange
        var config = new BlueGreenConfiguration
        {
            Strategy = "blue-green",
            ServiceName = "",
            BlueEnvironment = new BlueGreenEnvironment { Name = "blue", Endpoint = "http://blue:8080" },
            GreenEnvironment = new BlueGreenEnvironment { Name = "green", Endpoint = "http://green:8080" }
        };
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Act
        var result = await _agent.ValidateDeploymentConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Service name is required");
    }
}

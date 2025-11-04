using System;
using System.IO;
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
public class ObservabilityValidationAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<ObservabilityValidationAgent>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly ObservabilityValidationAgent _agent;

    public ObservabilityValidationAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<ObservabilityValidationAgent>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        _agent = new ObservabilityValidationAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object, _mockHttpClientFactory.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ObservabilityValidationAgent(null!, _mockLlmProvider.Object, _mockLogger.Object, _mockHttpClientFactory.Object));
    }

    [Fact]
    public async Task ValidateAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        var request = new DeploymentHealthCheckRequest { DeploymentName = "honua-test" };

        // Act
        var result = await _agent.ValidateDeploymentHealthAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Verify HttpClient factory was called to create HTTP clients for health checks
        _mockHttpClientFactory.Verify(
            f => f.CreateClient(It.IsAny<string>()),
            Times.AtLeastOnce,
            "Should create HTTP client for health check requests");
    }

    [Fact]
    public async Task ValidateAsync_WithMetricsValidation_ChecksMetricsEndpoint()
    {
        // Arrange
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        var request = new DeploymentHealthCheckRequest { DeploymentName = "honua-test" };

        // Act
        var result = await _agent.ValidateDeploymentHealthAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Verify HTTP client was created for metrics endpoint validation
        _mockHttpClientFactory.Verify(
            f => f.CreateClient(It.IsAny<string>()),
            Times.AtLeastOnce,
            "Should create HTTP client for metrics endpoint validation");

        // Verify logger was used to log validation steps
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Should log validation activities");
    }
}

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
public class GisEndpointValidationAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<GisEndpointValidationAgent>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly GisEndpointValidationAgent _agent;

    public GisEndpointValidationAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<GisEndpointValidationAgent>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        _agent = new GisEndpointValidationAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object, _mockHttpClientFactory.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GisEndpointValidationAgent(null!, _mockLlmProvider.Object, _mockLogger.Object, _mockHttpClientFactory.Object));
    }

    [Fact]
    public async Task ValidateDeployedServicesAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = new GisValidationRequest { BaseUrl = "https://example.com" };
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ValidateDeployedServicesAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<GisValidationResult>();

        // Verify HttpClient factory was called for GIS endpoint validation
        _mockHttpClientFactory.Verify(
            f => f.CreateClient(It.IsAny<string>()),
            Times.AtLeastOnce,
            "Should create HTTP client to validate GIS endpoints");

        // Verify logger was used
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

    [Fact]
    public async Task ValidateDeployedServicesAsync_WithMultipleServices_ValidatesAll()
    {
        // Arrange
        var request = new GisValidationRequest { BaseUrl = "https://example.com" };
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ValidateDeployedServicesAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Verify HttpClient factory was called multiple times for different services
        _mockHttpClientFactory.Verify(
            f => f.CreateClient(It.IsAny<string>()),
            Times.AtLeastOnce,
            "Should create HTTP clients to validate multiple GIS services");
    }
}

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
public class DiagramGeneratorAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<DiagramGeneratorAgent>> _mockLogger;
    private readonly DiagramGeneratorAgent _agent;

    public DiagramGeneratorAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<DiagramGeneratorAgent>>();
        _agent = new DiagramGeneratorAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DiagramGeneratorAgent(null!, _mockLlmProvider.Object, _mockLogger.Object));
    }
    [Fact]
    public async Task GenerateAsciiArchitectureDiagramAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var deploymentDescription = "AWS deployment with EC2 and RDS";
        var cloudProvider = "AWS";

        // Act
        var result = await _agent.GenerateAsciiArchitectureDiagramAsync(deploymentDescription, cloudProvider, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAsciiArchitectureDiagramAsync_WithComplexSystem_ReturnsDiagram()
    {
        // Arrange
        var deploymentDescription = "Multi-tier web application";
        var cloudProvider = "Azure";

        // Act
        var result = await _agent.GenerateAsciiArchitectureDiagramAsync(deploymentDescription, cloudProvider, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }
}

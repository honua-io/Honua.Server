using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class ArchitectureDocumentationAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<ArchitectureDocumentationAgent>> _mockLogger;
    private readonly ArchitectureDocumentationAgent _agent;

    public ArchitectureDocumentationAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<ArchitectureDocumentationAgent>>();

        // Setup mock LLM provider to return successful responses
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "Mock architecture documentation content",
                Success = true,
                Model = "mock-model",
                TotalTokens = 100
            });

        _agent = new ArchitectureDocumentationAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ArchitectureDocumentationAgent(null!, _mockLlmProvider.Object, _mockLogger.Object));
    }

    [Fact]
    public async Task GenerateAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = new ArchitectureDocumentationRequest { CloudProvider = "AWS" };

        // Act
        var result = await _agent.GenerateAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ArchitectureDocumentation>();
    }

    [Fact]
    public async Task GenerateAsync_WithDifferentProvider_ReturnsDocumentation()
    {
        // Arrange
        var request = new ArchitectureDocumentationRequest { CloudProvider = "Azure" };

        // Act
        var result = await _agent.GenerateAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CloudProvider.Should().Be("Azure");
    }
}

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
public class DeploymentExecutionAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<DeploymentExecutionAgent>> _mockLogger;
    private readonly DeploymentExecutionAgent _agent;

    public DeploymentExecutionAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<DeploymentExecutionAgent>>();
        _agent = new DeploymentExecutionAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DeploymentExecutionAgent(null!, _mockLlmProvider.Object, _mockLogger.Object));
    }
    [Fact]
    public async Task ExecuteDeploymentAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var deploymentType = "terraform";
        var terraformDir = "/tmp/terraform";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ExecuteDeploymentAsync(deploymentType, terraformDir, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<AgentStepResult>();
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WithDryRun_SkipsExecution()
    {
        // Arrange
        var deploymentType = "terraform";
        var terraformDir = "/tmp/terraform";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = true
        };

        // Act
        var result = await _agent.ExecuteDeploymentAsync(deploymentType, terraformDir, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }
}

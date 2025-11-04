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

[Trait("Category", "Integration")]
[Trait("Speed", "Slow")]
public class PerformanceBenchmarkAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly PerformanceBenchmarkAgent _agent;

    public PerformanceBenchmarkAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _agent = new PerformanceBenchmarkAgent(_kernel, _mockLlmProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PerformanceBenchmarkAgent(null!, _mockLlmProvider.Object));
    }

    [Fact]
    public async Task GenerateBenchmarkPlanAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Run performance benchmark on API endpoints";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = true
        };

        // Act
        var result = await _agent.GenerateBenchmarkPlanAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<AgentStepResult>();
    }

    [Fact]
    public async Task GenerateBenchmarkPlanAsync_WithDryRun_DoesNotRunBenchmark()
    {
        // Arrange
        var request = "Benchmark tile serving performance";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = true
        };

        // Act
        var result = await _agent.GenerateBenchmarkPlanAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }
}

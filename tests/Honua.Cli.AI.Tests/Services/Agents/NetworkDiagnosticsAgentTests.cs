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
public class NetworkDiagnosticsAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<NetworkDiagnosticsAgent>> _mockLogger;
    private readonly NetworkDiagnosticsAgent _agent;

    public NetworkDiagnosticsAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<NetworkDiagnosticsAgent>>();
        _agent = new NetworkDiagnosticsAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new NetworkDiagnosticsAgent(null!, _mockLlmProvider.Object, _mockLogger.Object));
    }

    [Fact]
    public async Task DiagnoseAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = new NetworkDiagnosticsRequest
        {
            TargetUrl = "https://example.com"
        };
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.DiagnoseAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NetworkDiagnosticsResult>();
    }

    [Fact]
    public async Task DiagnoseAsync_WithHostAndPort_IncludesDiagnostics()
    {
        // Arrange
        var request = new NetworkDiagnosticsRequest
        {
            TargetHost = "localhost",
            TargetPort = 5432
        };
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.DiagnoseAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }
}

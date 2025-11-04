using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Microsoft.SemanticKernel;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class HonuaUpgradeAgentTests
{
    private readonly Kernel _kernel;
    private readonly HonuaUpgradeAgent _agent;

    public HonuaUpgradeAgentTests()
    {
        _kernel = new Kernel();
        _agent = new HonuaUpgradeAgent(_kernel);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HonuaUpgradeAgent(null!));
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Upgrade Honua from v1.0 to v2.0";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = true
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AgentName.Should().Be("HonuaUpgrade");
    }

    [Fact]
    public async Task ProcessAsync_WithDryRun_DoesNotPerformUpgrade()
    {
        // Arrange
        var request = "Upgrade to latest version";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = true
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("dry-run");
    }
}

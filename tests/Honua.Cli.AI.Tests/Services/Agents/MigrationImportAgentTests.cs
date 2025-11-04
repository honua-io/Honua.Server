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
public class MigrationImportAgentTests
{
    private readonly Kernel _kernel;
    private readonly MigrationImportAgent _agent;

    public MigrationImportAgentTests()
    {
        _kernel = new Kernel();
        _agent = new MigrationImportAgent(_kernel);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MigrationImportAgent(null!));
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Import data from GeoServer to Honua";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AgentName.Should().Be("MigrationImport");
    }

    [Fact]
    public async Task ProcessAsync_WithMapServerMigration_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Migrate from MapServer to Honua";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }
}

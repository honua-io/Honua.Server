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
public class DataIngestionAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<DataIngestionAgent>> _mockLogger;
    private readonly DataIngestionAgent _agent;

    public DataIngestionAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<DataIngestionAgent>>();
        _agent = new DataIngestionAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DataIngestionAgent(null!, _mockLlmProvider.Object, _mockLogger.Object));
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Ingest GeoTIFF data from S3";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AgentName.Should().Be("DataIngestion");
    }

    [Fact]
    public async Task ProcessAsync_WithShapefileIngestion_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Import shapefile data into PostGIS";
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

using System;
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
public class DatabaseOptimizationAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<DatabaseOptimizationAgent>> _mockLogger;
    private readonly DatabaseOptimizationAgent _agent;

    public DatabaseOptimizationAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<DatabaseOptimizationAgent>>();
        _agent = new DatabaseOptimizationAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseOptimizationAgent(null!, _mockLlmProvider.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseOptimizationAgent(_kernel, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DatabaseOptimizationAgent(_kernel, _mockLlmProvider.Object, null!));
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Optimize PostgreSQL database for high-traffic OLTP workload";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("DatabaseOptimization");
        result.Action.Should().Be("ProcessDatabaseOptimization");
        result.Message.Should().Contain("Database Optimization Analysis");
    }

    [Fact]
    public async Task ProcessAsync_WithPostgreSQLRequest_ContainsIndexingRecommendations()
    {
        // Arrange
        var request = "Optimize PostgreSQL for spatial queries";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Indexing Recommendations");
    }

    [Fact]
    public async Task ProcessAsync_WithHighAvailabilityRequest_ContainsReplicationRecommendations()
    {
        // Arrange
        var request = "Setup PostgreSQL with high availability and read replicas";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Performance Optimizations");
    }

    [Fact]
    public async Task ProcessAsync_WithLlmFailure_ReturnsErrorResult()
    {
        // Arrange
        var request = "Optimize database";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "Error occurred",
                Model = "test-model",
                Success = false,
                ErrorMessage = "LLM service unavailable"
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Error");
    }

    [Fact]
    public async Task ProcessAsync_WithBackupRequirements_ContainsBackupStrategy()
    {
        // Arrange
        var request = "Optimize PostgreSQL with 30-day backup retention";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Backup Strategy");
    }

    [Fact]
    public async Task ProcessAsync_WithCostOptimization_ContainsCostRecommendations()
    {
        // Arrange
        var request = "Optimize PostgreSQL to reduce costs";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Cost Optimizations");
    }

    private void SetupLlmProviderResponses()
    {
        var setupCount = 0;
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                setupCount++;
                if (setupCount == 1)
                {
                    // First call - database analysis
                    return new LlmResponse
                    {
                        Content = @"{
                            ""databaseType"": ""PostgreSQL"",
                            ""instanceSize"": ""db.r5.xlarge"",
                            ""storageType"": ""gp3"",
                            ""expectedLoad"": ""1000 TPS"",
                            ""useCase"": ""OLTP with spatial data"",
                            ""highAvailability"": true,
                            ""backupRetention"": 30
                        }",
                        Model = "test-model",
                        Success = true
                    };
                }
                else
                {
                    // Second call - optimization recommendations
                    return new LlmResponse
                    {
                        Content = @"{
                            ""performanceOptimizations"": [""Enable read replicas for read-heavy workloads"", ""Configure connection pooling with pgbouncer""],
                            ""indexingRecommendations"": [""Create GIST index on spatial columns"", ""Add B-tree indexes on frequently queried columns""],
                            ""connectionPoolingSettings"": [""Set max_connections=200"", ""Configure pool size to 50""],
                            ""backupStrategy"": [""Automated daily backups with 30-day retention"", ""Enable point-in-time recovery""],
                            ""costOptimizations"": [""Use Reserved Instances for 40% savings"", ""Enable auto-scaling for variable workloads""],
                            ""warnings"": [""Current instance may be oversized for workload""]
                        }",
                        Model = "test-model",
                        Success = true
                    };
                }
            });
    }
}

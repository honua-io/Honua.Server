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
public class DisasterRecoveryAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<DisasterRecoveryAgent>> _mockLogger;
    private readonly DisasterRecoveryAgent _agent;

    public DisasterRecoveryAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<DisasterRecoveryAgent>>();
        _agent = new DisasterRecoveryAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DisasterRecoveryAgent(null!, _mockLlmProvider.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DisasterRecoveryAgent(_kernel, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DisasterRecoveryAgent(_kernel, _mockLlmProvider.Object, null!));
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Create disaster recovery plan with RTO of 1 hour";
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
        result.AgentName.Should().Be("DisasterRecovery");
        result.Action.Should().Be("ProcessDisasterRecoveryPlan");
        result.Message.Should().Contain("Disaster Recovery Plan");
    }

    [Fact]
    public async Task ProcessAsync_WithMultiRegion_ContainsFailoverSteps()
    {
        // Arrange
        var request = "Setup multi-region DR with automatic failover";
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
        result.Message.Should().Contain("Failover Steps");
    }

    [Fact]
    public async Task ProcessAsync_WithBackupRequirements_ContainsBackupProcedures()
    {
        // Arrange
        var request = "Create DR plan with automated backups";
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
        result.Message.Should().Contain("Backup Procedures");
    }

    [Fact]
    public async Task ProcessAsync_WithLlmFailure_ReturnsSuccessWithDefaults()
    {
        // Arrange
        var request = "Create DR plan";
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
        // Agent falls back to default values when LLM fails, so it still succeeds
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Disaster Recovery Plan");
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
                    return new LlmResponse
                    {
                        Content = @"{
                            ""rto"": ""1 hour"",
                            ""rpo"": ""15 minutes"",
                            ""primaryRegion"": ""us-east-1"",
                            ""drRegions"": [""us-west-2""],
                            ""criticalServices"": [""API"", ""Database""]
                        }",
                        Model = "test-model",
                        Success = true
                    };
                }
                else
                {
                    return new LlmResponse
                    {
                        Content = @"{
                            ""strategyType"": ""Active-Passive"",
                            ""backupProcedures"": [""Automated daily backups"", ""Cross-region replication""],
                            ""failoverSteps"": [""Detect failure"", ""Promote secondary"", ""Update DNS""],
                            ""dataReplicationStrategy"": [""Async replication to DR region""],
                            ""testingPlan"": [""Quarterly DR drills""],
                            ""estimatedCost"": ""$500/month""
                        }",
                        Model = "test-model",
                        Success = true
                    };
                }
            });
    }
}

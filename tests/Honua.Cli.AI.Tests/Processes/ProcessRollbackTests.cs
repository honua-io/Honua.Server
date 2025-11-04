using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.State;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Honua.Cli.AI.Services.Processes.Steps.Upgrade;

namespace Honua.Cli.AI.Tests.Processes;

/// <summary>
/// Tests for process rollback functionality.
/// </summary>
[Collection("AITests")]
[Trait("Category", "Integration")]
public class ProcessRollbackTests
{
    [Fact]
    public async Task RollbackAsync_DeployInfrastructureStep_Success()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DeployInfrastructureStep>>();
        var step = new DeployInfrastructureStep(logger);

        var state = new DeploymentState
        {
            DeploymentId = "test-deployment-1",
            CloudProvider = "AWS",
            DeploymentName = "honua-test",
            CreatedResources = new()
            {
                "database_instance",
                "storage_bucket",
                "container_cluster"
            }
        };

        // Act
        var result = await step.RollbackAsync(state, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Contains("Destroyed infrastructure resources", result.Details);
    }

    [Fact]
    public async Task RollbackAsync_DeployInfrastructureStep_NoResources_Success()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DeployInfrastructureStep>>();
        var step = new DeployInfrastructureStep(logger);

        var state = new DeploymentState
        {
            DeploymentId = "test-deployment-2",
            CloudProvider = "AWS",
            DeploymentName = "honua-test",
            CreatedResources = new() // Empty list
        };

        // Act
        var result = await step.RollbackAsync(state, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Contains("No infrastructure resources", result.Details);
    }

    [Fact]
    public async Task RollbackAsync_DeployInfrastructureStep_InvalidStateType_Failure()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DeployInfrastructureStep>>();
        var step = new DeployInfrastructureStep(logger);

        var invalidState = new object(); // Wrong state type

        // Act
        var result = await step.RollbackAsync(invalidState, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid state type", result.Error);
    }

    [Fact]
    public async Task RollbackAsync_DeployHonuaApplicationStep_AWS_Success()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DeployHonuaApplicationStep>>();
        var step = new DeployHonuaApplicationStep(logger);

        var state = new DeploymentState
        {
            DeploymentId = "test-deployment-3",
            CloudProvider = "AWS",
            DeploymentName = "honua-test-app"
        };

        // Act
        var result = await step.RollbackAsync(state, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Contains("ECS", result.Details);
    }

    [Fact]
    public async Task RollbackAsync_DeployHonuaApplicationStep_Azure_Success()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DeployHonuaApplicationStep>>();
        var step = new DeployHonuaApplicationStep(logger);

        var state = new DeploymentState
        {
            DeploymentId = "test-deployment-4",
            CloudProvider = "Azure",
            DeploymentName = "honua-test-app"
        };

        // Act
        var result = await step.RollbackAsync(state, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Contains("AKS", result.Details);
    }

    [Fact]
    public async Task RollbackAsync_DeployHonuaApplicationStep_GCP_Success()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DeployHonuaApplicationStep>>();
        var step = new DeployHonuaApplicationStep(logger);

        var state = new DeploymentState
        {
            DeploymentId = "test-deployment-5",
            CloudProvider = "GCP",
            DeploymentName = "honua-test-app"
        };

        // Act
        var result = await step.RollbackAsync(state, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Contains("GKE", result.Details);
    }

    [Fact]
    public async Task RollbackAsync_CreateBlueEnvironmentStep_Success()
    {
        // Arrange
        var logger = Mock.Of<ILogger<CreateBlueEnvironmentStep>>();
        var step = new CreateBlueEnvironmentStep(logger);

        var state = new UpgradeState
        {
            UpgradeId = "test-upgrade-1",
            DeploymentName = "honua-production",
            BlueEnvironment = "honua-production-blue",
            MigrationsCompleted = true
        };

        // Act
        var result = await step.RollbackAsync(state, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Contains("Destroyed blue environment", result.Details);
    }

    [Fact]
    public async Task RollbackAsync_CreateBlueEnvironmentStep_NoBlueEnvironment_Success()
    {
        // Arrange
        var logger = Mock.Of<ILogger<CreateBlueEnvironmentStep>>();
        var step = new CreateBlueEnvironmentStep(logger);

        var state = new UpgradeState
        {
            UpgradeId = "test-upgrade-2",
            DeploymentName = "honua-production",
            BlueEnvironment = string.Empty // No blue environment
        };

        // Act
        var result = await step.RollbackAsync(state, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Contains("No blue environment", result.Details);
    }

    [Fact]
    public async Task RollbackAsync_SwitchTrafficStep_Success()
    {
        // Arrange
        var logger = Mock.Of<ILogger<SwitchTrafficStep>>();
        var step = new SwitchTrafficStep(logger);

        var state = new UpgradeState
        {
            UpgradeId = "test-upgrade-3",
            DeploymentName = "honua-production",
            TrafficPercentageOnBlue = 50 // Currently at 50%
        };

        // Act
        var result = await step.RollbackAsync(state, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Contains("Switched traffic back to green", result.Details);
        Assert.Contains("50", result.Details);
    }

    [Fact]
    public async Task RollbackAsync_SwitchTrafficStep_AlreadyOnGreen_Success()
    {
        // Arrange
        var logger = Mock.Of<ILogger<SwitchTrafficStep>>();
        var step = new SwitchTrafficStep(logger);

        var state = new UpgradeState
        {
            UpgradeId = "test-upgrade-4",
            DeploymentName = "honua-production",
            TrafficPercentageOnBlue = 0 // Already on green
        };

        // Act
        var result = await step.RollbackAsync(state, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Contains("already on green", result.Details);
    }

    [Fact]
    public async Task RollbackAsync_Cancellation_Failure()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DeployInfrastructureStep>>();
        var step = new DeployInfrastructureStep(logger);

        var state = new DeploymentState
        {
            DeploymentId = "test-deployment-6",
            CloudProvider = "AWS",
            CreatedResources = new() { "database_instance" }
        };

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var result = await step.RollbackAsync(state, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Contains("cancelled", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupportsRollback_DeploymentSteps_ReturnsTrue()
    {
        // Arrange
        var infraLogger = Mock.Of<ILogger<DeployInfrastructureStep>>();
        var appLogger = Mock.Of<ILogger<DeployHonuaApplicationStep>>();

        var infraStep = new DeployInfrastructureStep(infraLogger);
        var appStep = new DeployHonuaApplicationStep(appLogger);

        // Assert
        Assert.True(infraStep.SupportsRollback);
        Assert.True(appStep.SupportsRollback);
    }

    [Fact]
    public void SupportsRollback_UpgradeSteps_ReturnsTrue()
    {
        // Arrange
        var blueLogger = Mock.Of<ILogger<CreateBlueEnvironmentStep>>();
        var trafficLogger = Mock.Of<ILogger<SwitchTrafficStep>>();

        var blueStep = new CreateBlueEnvironmentStep(blueLogger);
        var trafficStep = new SwitchTrafficStep(trafficLogger);

        // Assert
        Assert.True(blueStep.SupportsRollback);
        Assert.True(trafficStep.SupportsRollback);
    }

    [Fact]
    public void RollbackDescription_AllSteps_NotEmpty()
    {
        // Arrange
        var infraLogger = Mock.Of<ILogger<DeployInfrastructureStep>>();
        var appLogger = Mock.Of<ILogger<DeployHonuaApplicationStep>>();
        var blueLogger = Mock.Of<ILogger<CreateBlueEnvironmentStep>>();
        var trafficLogger = Mock.Of<ILogger<SwitchTrafficStep>>();

        var steps = new IProcessStepRollback[]
        {
            new DeployInfrastructureStep(infraLogger),
            new DeployHonuaApplicationStep(appLogger),
            new CreateBlueEnvironmentStep(blueLogger),
            new SwitchTrafficStep(trafficLogger)
        };

        // Assert
        foreach (var step in steps)
        {
            Assert.False(string.IsNullOrWhiteSpace(step.RollbackDescription));
        }
    }

    [Fact]
    public async Task RollbackOrchestrator_ProcessNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ProcessRollbackOrchestrator>>();
        var stateStore = new Mock<IProcessStateStore>();
        var serviceProvider = Mock.Of<IServiceProvider>();

        stateStore
            .Setup(s => s.GetProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessInfo?)null);

        var orchestrator = new ProcessRollbackOrchestrator(logger, stateStore.Object, serviceProvider);

        // Act
        var result = await orchestrator.RollbackProcessAsync("nonexistent-process");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalSteps);
        Assert.Equal(0, result.SuccessfulRollbacks);
        Assert.Contains("not found", result.Steps[0].Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RollbackOrchestrator_InvalidState_ReturnsInvalidStateResult()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ProcessRollbackOrchestrator>>();
        var stateStore = new Mock<IProcessStateStore>();
        var serviceProvider = Mock.Of<IServiceProvider>();

        var processInfo = new ProcessInfo
        {
            ProcessId = "test-process",
            WorkflowType = "HonuaDeployment",
            Status = "Completed" // Not Failed or Running
        };

        stateStore
            .Setup(s => s.GetProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processInfo);

        var orchestrator = new ProcessRollbackOrchestrator(logger, stateStore.Object, serviceProvider);

        // Act
        var result = await orchestrator.RollbackProcessAsync("test-process");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalSteps);
        Assert.Contains("cannot be rolled back", result.Steps[0].Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}

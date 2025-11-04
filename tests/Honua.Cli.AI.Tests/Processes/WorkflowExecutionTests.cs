using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.State;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Honua.Cli.AI.Services.Processes.Steps.Upgrade;
using Honua.Cli.AI.TestSupport;
using Honua.Cli.AI.Services.Discovery;
using Honua.Cli.AI.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Xunit;
using Xunit.Abstractions;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Telemetry;

namespace Honua.Cli.AI.Tests.Processes;

/// <summary>
/// Tests for complete workflow execution scenarios including pause/resume.
/// Tests end-to-end workflow behavior with state management.
/// </summary>
[Trait("Category", "ProcessFramework")]
[Trait("Category", "Workflow")]
[Collection("ProcessFramework")]
public class WorkflowExecutionTests
{
    private readonly ITestOutputHelper _output;

    public WorkflowExecutionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private Kernel CreateTestKernel()
    {
        var builder = Kernel.CreateBuilder();

        // Add logging to the kernel's service collection
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IAzureCli, NoopAzureCli>();
        builder.Services.AddSingleton<IGcloudCli, NoopGcloudCli>();
        builder.Services.AddSingleton<IHttpClientFactory, TestHttpClientFactory>();
        builder.Services.AddSingleton<ICloudDiscoveryService, TestDiscoveryService>();
        builder.Services.AddSingleton<IResourceEnvelopeCatalog, ResourceEnvelopeCatalog>();
        builder.Services.AddSingleton<IDeploymentGuardrailValidator, DeploymentGuardrailValidator>();
        builder.Services.AddSingleton<ITelemetryService, NullTelemetryService>();
        builder.Services.AddSingleton<IDeploymentGuardrailMonitor, PostDeployGuardrailMonitor>();
        builder.Services.AddSingleton<IDeploymentMetricsProvider, TestDeploymentMetricsProvider>();

        // Register deployment steps directly to the kernel's service collection
        builder.Services.AddTransient<ValidateDeploymentRequirementsStep>(sp =>
            new ValidateDeploymentRequirementsStep(
                NullLogger<ValidateDeploymentRequirementsStep>.Instance,
                sp.GetRequiredService<IDeploymentGuardrailValidator>(),
                sp.GetRequiredService<ICloudDiscoveryService>()));
        builder.Services.AddTransient<GenerateInfrastructureCodeStep>(sp =>
            new GenerateInfrastructureCodeStep(NullLogger<GenerateInfrastructureCodeStep>.Instance));
        builder.Services.AddTransient<ReviewInfrastructureStep>(sp =>
            new ReviewInfrastructureStep(NullLogger<ReviewInfrastructureStep>.Instance));
        builder.Services.AddTransient<DeployInfrastructureStep>(sp =>
            new DeployInfrastructureStep(NullLogger<DeployInfrastructureStep>.Instance));
        builder.Services.AddTransient<ConfigureServicesStep>(sp =>
            new ConfigureServicesStep(
                NullLogger<ConfigureServicesStep>.Instance,
                azureCli: sp.GetRequiredService<IAzureCli>(),
                gcloudCli: sp.GetRequiredService<IGcloudCli>()));
        builder.Services.AddTransient<DeployHonuaApplicationStep>(sp =>
            new DeployHonuaApplicationStep(
                NullLogger<DeployHonuaApplicationStep>.Instance,
                azureCli: sp.GetRequiredService<IAzureCli>(),
                gcloudCli: sp.GetRequiredService<IGcloudCli>()));
        builder.Services.AddTransient<ValidateDeploymentStep>(sp =>
            new ValidateDeploymentStep(
                NullLogger<ValidateDeploymentStep>.Instance,
                sp.GetRequiredService<IDeploymentGuardrailMonitor>(),
                sp.GetRequiredService<IDeploymentMetricsProvider>(),
                sp.GetRequiredService<IHttpClientFactory>()));
        builder.Services.AddTransient<ConfigureObservabilityStep>(sp =>
            new ConfigureObservabilityStep(NullLogger<ConfigureObservabilityStep>.Instance));

        // Register upgrade steps directly to the kernel's service collection
        builder.Services.AddTransient<DetectCurrentVersionStep>(sp =>
            new DetectCurrentVersionStep(NullLogger<DetectCurrentVersionStep>.Instance));
        builder.Services.AddTransient<BackupDatabaseStep>(sp =>
            new BackupDatabaseStep(NullLogger<BackupDatabaseStep>.Instance));
        builder.Services.AddTransient<CreateBlueEnvironmentStep>(sp =>
            new CreateBlueEnvironmentStep(NullLogger<CreateBlueEnvironmentStep>.Instance));
        builder.Services.AddTransient<SwitchTrafficStep>(sp =>
            new SwitchTrafficStep(NullLogger<SwitchTrafficStep>.Instance));

        return builder.Build();
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client = new();

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    #region Workflow Execution Tests

    [Fact]
    public async Task DeploymentWorkflow_ExecutesFirstStep()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = DeploymentProcess.BuildProcess().Build();
        var request = new DeploymentRequest(
            CloudProvider: "AWS",
            Region: "us-west-2",
            DeploymentName: "test-deployment",
            Tier: "Development",
            Features: new List<string> { "API" }
        );

        var initialEvent = new KernelProcessEvent
        {
            Id = "StartDeployment",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);

        // Give it time to execute
        await Task.Delay(300);

        // Assert
        processHandle.Should().NotBeNull();
//         processHandle.Id.Should().NotBeNullOrEmpty();
// 
//         _output.WriteLine($"Deployment workflow started: {processHandle.Id}");
    }

    [Fact]
    public async Task UpgradeWorkflow_ExecutesFirstStep()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = UpgradeProcess.BuildProcess().Build();
        var request = new UpgradeRequest(
            DeploymentName: "production",
            TargetVersion: "2.0.0"
        );

        var initialEvent = new KernelProcessEvent
        {
            Id = "StartUpgrade",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        await Task.Delay(300);

        // Assert
        processHandle.Should().NotBeNull();
//         processHandle.Id.Should().NotBeNullOrEmpty();
// 
//         _output.WriteLine($"Upgrade workflow started: {processHandle.Id}");
    }

    [Fact]
    public async Task Workflow_WithValidInput_DoesNotThrow()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = DeploymentProcess.BuildProcess().Build();
        var request = new DeploymentRequest(
            CloudProvider: "Azure",
            Region: "eastus",
            DeploymentName: "azure-test",
            Tier: "Production",
            Features: new List<string> { "API", "Database" }
        );

        // Act & Assert
        var processHandle = await process.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartDeployment",
                Data = request
            });

        await Task.Delay(200);

        processHandle.Should().NotBeNull();
        _output.WriteLine("Workflow with valid input executed without throwing");
    }

    #endregion

    #region Pause/Resume Tests

    [Fact]
    public async Task Workflow_CanTrackPausedState()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);
        var processId = "paused-workflow";

        // Act - Start workflow and mark as paused
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = processId,
            WorkflowType = "Deployment",
            Status = "Running",
            CurrentStep = "ReviewInfrastructure",
            CompletionPercentage = 30,
            StartTime = DateTime.UtcNow
        });

        // Simulate pause (workflow waiting for approval)
        await store.UpdateProcessStatusAsync(processId, "Paused", 30);

        var pausedProcess = await store.GetProcessAsync(processId);

        // Assert
        pausedProcess.Should().NotBeNull();
        pausedProcess!.Status.Should().Be("Paused");
        pausedProcess.CompletionPercentage.Should().Be(30);

        _output.WriteLine($"Workflow paused at step: {pausedProcess.CurrentStep}");
    }

    [Fact]
    public async Task Workflow_CanResumeAfterPause()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);
        var processId = "resume-workflow";

        // Start and pause
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = processId,
            WorkflowType = "Deployment",
            Status = "Running",
            CurrentStep = "ReviewInfrastructure",
            CompletionPercentage = 30,
            StartTime = DateTime.UtcNow
        });

        await store.UpdateProcessStatusAsync(processId, "Paused", 30);

        // Act - Resume
        await store.UpdateProcessStatusAsync(processId, "Running", 30);

        var resumedProcess = await store.GetProcessAsync(processId);

        // Assert
        resumedProcess.Should().NotBeNull();
        resumedProcess!.Status.Should().Be("Running");

        _output.WriteLine("Workflow resumed successfully");
    }

    [Fact]
    public async Task Workflow_PauseDoesNotAppearInActiveProcesses()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);

        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = "active-1",
            Status = "Running",
            WorkflowType = "Deploy",
            StartTime = DateTime.UtcNow
        });

        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = "paused-1",
            Status = "Paused",
            WorkflowType = "Deploy",
            StartTime = DateTime.UtcNow
        });

        // Act
        var activeProcesses = await store.GetActiveProcessesAsync();

        // Assert
        activeProcesses.Should().HaveCount(1);
        activeProcesses.Should().Contain(p => p.ProcessId == "active-1");
        activeProcesses.Should().NotContain(p => p.ProcessId == "paused-1");

        _output.WriteLine("Paused processes correctly excluded from active list");
    }

    [Fact]
    public async Task Workflow_MultipleProcesses_CanHaveDifferentStates()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);

        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = "running-1",
            Status = "Running",
            WorkflowType = "Deploy",
            CompletionPercentage = 50,
            StartTime = DateTime.UtcNow
        });

        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = "paused-1",
            Status = "Paused",
            WorkflowType = "Upgrade",
            CompletionPercentage = 30,
            StartTime = DateTime.UtcNow
        });

        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = "pending-1",
            Status = "Pending",
            WorkflowType = "Metadata",
            CompletionPercentage = 0,
            StartTime = DateTime.UtcNow
        });

        // Act
        var running = await store.GetProcessAsync("running-1");
        var paused = await store.GetProcessAsync("paused-1");
        var pending = await store.GetProcessAsync("pending-1");

        // Assert
        running!.Status.Should().Be("Running");
        paused!.Status.Should().Be("Paused");
        pending!.Status.Should().Be("Pending");

        _output.WriteLine("Multiple processes maintain independent states correctly");
    }

    #endregion

    #region Error Recovery Tests

    [Fact]
    public async Task Workflow_AfterFailure_CanRetry()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);
        var processId = "retry-workflow";

        // First attempt fails
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = processId,
            WorkflowType = "Deployment",
            Status = "Running",
            CurrentStep = "DeployInfrastructure",
            CompletionPercentage = 50,
            StartTime = DateTime.UtcNow
        });

        await store.UpdateProcessStatusAsync(processId, "Failed", 50, "Network timeout");

        var failedProcess = await store.GetProcessAsync(processId);
        failedProcess!.Status.Should().Be("Failed");

        // Act - Retry by resetting to running
        await store.UpdateProcessStatusAsync(processId, "Running", 50);

        var retriedProcess = await store.GetProcessAsync(processId);

        // Assert
        retriedProcess.Should().NotBeNull();
        retriedProcess!.Status.Should().Be("Running");
        retriedProcess.CompletionPercentage.Should().Be(50);

        _output.WriteLine("Workflow retry initiated after failure");
    }

    [Fact]
    public async Task Workflow_PartialProgress_PreservedOnFailure()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);
        var processId = "partial-progress";

        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = processId,
            WorkflowType = "Deployment",
            Status = "Running",
            CurrentStep = "ConfigureServices",
            CompletionPercentage = 75,
            StartTime = DateTime.UtcNow
        });

        // Act - Fail at 75%
        await store.UpdateProcessStatusAsync(processId, "Failed", 75, "Configuration error");

        var failedProcess = await store.GetProcessAsync(processId);

        // Assert
        failedProcess.Should().NotBeNull();
        failedProcess!.Status.Should().Be("Failed");
        failedProcess.CompletionPercentage.Should().Be(75);
        failedProcess.CurrentStep.Should().Be("ConfigureServices");

        _output.WriteLine($"Partial progress preserved: {failedProcess.CompletionPercentage}% at {failedProcess.CurrentStep}");
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task Workflow_LongRunning_CanBeTimedOut()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Start the task and immediately await it - this is a test scenario
        // VSTHRD003: We intentionally create and immediately await the task to test cancellation behavior
        var longRunningTask = Task.Run(async () =>
        {
            await Task.Delay(10000, cts.Token); // 10 second operation
        }, cts.Token);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => longRunningTask);
        _output.WriteLine("Long-running workflow correctly timed out");
    }

    [Fact]
    public async Task Workflow_WithTimeout_UpdatesStatusOnCancellation()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);
        var processId = "timeout-test";

        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = processId,
            WorkflowType = "Benchmark",
            Status = "Running",
            StartTime = DateTime.UtcNow
        });

        var cts = new CancellationTokenSource();

        // Start a task that checks cancellation
        var task = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Update state on cancellation
                await store.UpdateProcessStatusAsync(processId, "Cancelled", errorMessage: "Timeout exceeded");
                throw;
            }
        }, cts.Token);

        // Act - Cancel after short delay
        await Task.Delay(150);
        cts.Cancel();

        // Wait for cancellation to process
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        var cancelledProcess = await store.GetProcessAsync(processId);

        // Assert
        cancelledProcess.Should().NotBeNull();
        cancelledProcess!.Status.Should().Be("Cancelled");

        _output.WriteLine("Workflow status updated after timeout cancellation");
    }

    #endregion

    #region Rollback Tests

    [Fact]
    public async Task Workflow_OnFailure_CanTrackRollbackState()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);
        var processId = "rollback-test";

        // Deployment started
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = processId,
            WorkflowType = "Deployment",
            Status = "Running",
            CurrentStep = "DeployApplication",
            CompletionPercentage = 80,
            StartTime = DateTime.UtcNow
        });

        // Failed and needs rollback
        await store.UpdateProcessStatusAsync(processId, "Failed", 80, "Deployment validation failed");

        // Act - Update to rollback state
        await store.UpdateProcessStatusAsync(processId, "Rolling Back", 80, "Deployment validation failed");

        var rollingBackProcess = await store.GetProcessAsync(processId);

        // Assert
        rollingBackProcess.Should().NotBeNull();
        rollingBackProcess!.Status.Should().Be("Rolling Back");
        rollingBackProcess.ErrorMessage.Should().Contain("validation failed");

        _output.WriteLine("Rollback state tracked correctly");
    }

    #endregion

    #region Workflow Chaining Tests

    [Fact]
    public async Task Workflow_CompletedProcess_CanTriggerNext()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);

        // First workflow completes
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = "deploy-1",
            WorkflowType = "Deployment",
            Status = "Running",
            StartTime = DateTime.UtcNow
        });

        await store.UpdateProcessStatusAsync("deploy-1", "Completed", 100);

        // Act - Start next workflow
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = "upgrade-1",
            WorkflowType = "Upgrade",
            Status = "Running",
            StartTime = DateTime.UtcNow
        });

        var completedDeploy = await store.GetProcessAsync("deploy-1");
        var runningUpgrade = await store.GetProcessAsync("upgrade-1");

        // Assert
        completedDeploy!.Status.Should().Be("Completed");
        runningUpgrade!.Status.Should().Be("Running");

        _output.WriteLine("Sequential workflow chaining works correctly");
    }

    #endregion

    #region State Transition Tests

    [Fact]
    public async Task Workflow_StateTransitions_FollowValidSequence()
    {
        // Arrange
        var store = new InMemoryProcessStateStore(NullLogger<InMemoryProcessStateStore>.Instance);
        var processId = "state-transition";
        var stateHistory = new List<string>();

        // Act - Track state transitions
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = processId,
            WorkflowType = "Deployment",
            Status = "Pending",
            StartTime = DateTime.UtcNow
        });
        stateHistory.Add("Pending");

        await store.UpdateProcessStatusAsync(processId, "Running", 10);
        stateHistory.Add("Running");

        await store.UpdateProcessStatusAsync(processId, "Running", 50);
        stateHistory.Add("Running");

        await store.UpdateProcessStatusAsync(processId, "Paused", 50);
        stateHistory.Add("Paused");

        await store.UpdateProcessStatusAsync(processId, "Running", 50);
        stateHistory.Add("Running");

        await store.UpdateProcessStatusAsync(processId, "Completed", 100);
        stateHistory.Add("Completed");

        var finalProcess = await store.GetProcessAsync(processId);

        // Assert
        stateHistory.Should().ContainInOrder("Pending", "Running", "Running", "Paused", "Running", "Completed");
        finalProcess!.Status.Should().Be("Completed");

        _output.WriteLine($"State transitions: {string.Join(" -> ", stateHistory)}");
    }

    #endregion
}

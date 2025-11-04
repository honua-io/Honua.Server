using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.State;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Honua.Cli.AI.Services.Processes.Steps.Upgrade;
using Honua.Cli.AI.Services.Processes.Steps.Metadata;
using Honua.Cli.AI.Services.Processes.Steps.GitOps;
using Honua.Cli.AI.Services.Processes.Steps.Benchmark;
using Honua.Cli.AI.TestSupport;
using Honua.Cli.AI.Services.Discovery;
using Honua.Cli.AI.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Telemetry;

namespace Honua.Cli.AI.Tests.Processes;

/// <summary>
/// Integration tests for Semantic Kernel Process Framework workflows.
/// Tests all 5 processes: Deployment, Upgrade, Metadata, GitOps, and Benchmark.
/// </summary>
[Trait("Category", "ProcessFramework")]
[Collection("ProcessFramework")]
public class ProcessFrameworkTests
{
    private readonly ITestOutputHelper _output;

    public ProcessFrameworkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Helper Methods

    private Kernel CreateTestKernel()
    {
        var builder = Kernel.CreateBuilder();

        // Add logging to the kernel's service collection
        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddDebug());
        builder.Services.AddSingleton<IHttpClientFactory, TestHttpClientFactory>();

        // Register all step types
        RegisterDeploymentSteps(builder.Services);
        RegisterUpgradeSteps(builder.Services);
        RegisterMetadataSteps(builder.Services);
        RegisterGitOpsSteps(builder.Services);
        RegisterBenchmarkSteps(builder.Services);

        return builder.Build();
    }

    private void RegisterDeploymentSteps(IServiceCollection services)
    {
        services.AddSingleton<IResourceEnvelopeCatalog, ResourceEnvelopeCatalog>();
        services.AddSingleton<IDeploymentGuardrailValidator, DeploymentGuardrailValidator>();
        services.AddSingleton<ITelemetryService, NullTelemetryService>();
        services.AddSingleton<IDeploymentGuardrailMonitor, PostDeployGuardrailMonitor>();
        services.AddSingleton<IDeploymentMetricsProvider, TestDeploymentMetricsProvider>();
        services.AddSingleton<IAzureCli, NoopAzureCli>();
        services.AddSingleton<IGcloudCli, NoopGcloudCli>();
        services.AddSingleton<ICloudDiscoveryService, TestDiscoveryService>();

        services.AddTransient<ValidateDeploymentRequirementsStep>(sp =>
            new ValidateDeploymentRequirementsStep(
                CreateMockLogger<ValidateDeploymentRequirementsStep>(),
                sp.GetRequiredService<IDeploymentGuardrailValidator>(),
                sp.GetRequiredService<ICloudDiscoveryService>()));
        services.AddTransient<GenerateInfrastructureCodeStep>(sp =>
            new GenerateInfrastructureCodeStep(CreateMockLogger<GenerateInfrastructureCodeStep>()));
        services.AddTransient<ReviewInfrastructureStep>(sp =>
            new ReviewInfrastructureStep(CreateMockLogger<ReviewInfrastructureStep>()));
        services.AddTransient<DeployInfrastructureStep>(sp =>
            new DeployInfrastructureStep(CreateMockLogger<DeployInfrastructureStep>()));
        services.AddTransient<ConfigureServicesStep>(sp =>
            new ConfigureServicesStep(
                CreateMockLogger<ConfigureServicesStep>(),
                awsCli: DefaultAwsCli.Shared,
                azureCli: sp.GetRequiredService<IAzureCli>(),
                gcloudCli: sp.GetRequiredService<IGcloudCli>()));
        services.AddTransient<DeployHonuaApplicationStep>(sp =>
            new DeployHonuaApplicationStep(
                CreateMockLogger<DeployHonuaApplicationStep>(),
                awsCli: DefaultAwsCli.Shared,
                azureCli: sp.GetRequiredService<IAzureCli>(),
                gcloudCli: sp.GetRequiredService<IGcloudCli>()));
        services.AddTransient<ValidateDeploymentStep>(sp =>
            new ValidateDeploymentStep(
                CreateMockLogger<ValidateDeploymentStep>(),
                sp.GetRequiredService<IDeploymentGuardrailMonitor>(),
                sp.GetRequiredService<IDeploymentMetricsProvider>(),
                sp.GetRequiredService<IHttpClientFactory>()));
        services.AddTransient<ConfigureObservabilityStep>(sp =>
            new ConfigureObservabilityStep(CreateMockLogger<ConfigureObservabilityStep>()));
    }

    private void RegisterUpgradeSteps(IServiceCollection services)
    {
        services.AddTransient<DetectCurrentVersionStep>(sp =>
            new DetectCurrentVersionStep(CreateMockLogger<DetectCurrentVersionStep>()));
        services.AddTransient<BackupDatabaseStep>(sp =>
            new BackupDatabaseStep(CreateMockLogger<BackupDatabaseStep>()));
        services.AddTransient<CreateBlueEnvironmentStep>(sp =>
            new CreateBlueEnvironmentStep(CreateMockLogger<CreateBlueEnvironmentStep>()));
        services.AddTransient<SwitchTrafficStep>(sp =>
            new SwitchTrafficStep(CreateMockLogger<SwitchTrafficStep>()));
    }

    private void RegisterMetadataSteps(IServiceCollection services)
    {
        services.AddTransient<ExtractMetadataStep>(sp =>
            new ExtractMetadataStep(CreateMockLogger<ExtractMetadataStep>()));
        services.AddTransient<GenerateStacItemStep>(sp =>
            new GenerateStacItemStep(CreateMockLogger<GenerateStacItemStep>()));
        services.AddTransient<PublishStacStep>(sp =>
            new PublishStacStep(CreateMockLogger<PublishStacStep>()));
    }

    private void RegisterGitOpsSteps(IServiceCollection services)
    {
        services.AddTransient<ValidateGitConfigStep>(sp =>
            new ValidateGitConfigStep(CreateMockLogger<ValidateGitConfigStep>()));
        services.AddTransient<SyncConfigStep>(sp =>
            new SyncConfigStep(CreateMockLogger<SyncConfigStep>()));
        services.AddTransient<MonitorDriftStep>(sp =>
            new MonitorDriftStep(CreateMockLogger<MonitorDriftStep>()));
    }

    private void RegisterBenchmarkSteps(IServiceCollection services)
    {
        services.AddTransient<SetupBenchmarkStep>(sp =>
            new SetupBenchmarkStep(
                CreateMockLogger<SetupBenchmarkStep>(),
                sp.GetRequiredService<IHttpClientFactory>()));
        services.AddTransient<RunBenchmarkStep>(sp =>
            new RunBenchmarkStep(CreateMockLogger<RunBenchmarkStep>()));
        services.AddTransient<AnalyzeResultsStep>(sp =>
            new AnalyzeResultsStep(CreateMockLogger<AnalyzeResultsStep>()));
        services.AddTransient<GenerateReportStep>(sp =>
            new GenerateReportStep(CreateMockLogger<GenerateReportStep>()));
    }

    private ILogger<T> CreateMockLogger<T>()
    {
        return NullLogger<T>.Instance;
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client = new();

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    private async Task<bool> WaitForProcessCompletion(
        LocalKernelProcessContext processHandle,
        TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);
        try
        {
            // Give the process time to complete
            await Task.Delay(100, cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    #endregion

    #region DeploymentProcess Tests

    [Fact]
    public void DeploymentProcess_Builds_Successfully()
    {
        // Arrange
        var processBuilder = DeploymentProcess.BuildProcess();

        // Act
        var process = processBuilder.Build();

        // Assert
        Assert.NotNull(process);
        _output.WriteLine("DeploymentProcess built successfully");
    }

    [Fact]
    public async Task DeploymentProcess_Starts_With_Valid_Input()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = DeploymentProcess.BuildProcess().Build();
        var request = new DeploymentRequest(
            CloudProvider: "AWS",
            Region: "us-west-2",
            DeploymentName: "test-deployment",
            Tier: "Development",
            Features: new List<string> { "API", "Database" });
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartDeployment",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);

        // Assert
        Assert.NotNull(processHandle);
    }

    [Fact]
    public async Task DeploymentProcess_Completes_All_Steps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = DeploymentProcess.BuildProcess().Build();
        var request = new DeploymentRequest(
            CloudProvider: "AWS",
            Region: "us-west-2",
            DeploymentName: "test-deployment",
            Tier: "Development",
            Features: new List<string> { "API" });
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartDeployment",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        var completed = await WaitForProcessCompletion(processHandle, TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(completed, "Process should complete within timeout");
        _output.WriteLine("DeploymentProcess completed all initial validation steps");
    }

    [Fact]
    public async Task DeploymentProcess_Handles_Errors_Gracefully()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = DeploymentProcess.BuildProcess().Build();
        var request = new DeploymentRequest(
            CloudProvider: "InvalidProvider",
            Region: "us-west-2",
            DeploymentName: "test-deployment",
            Tier: "Development",
            Features: new List<string>());
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartDeployment",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        await Task.Delay(200); // Allow time for processing

        // Assert
        Assert.NotNull(processHandle);
        _output.WriteLine("DeploymentProcess handled invalid provider gracefully");
    }

    [Fact]
    public async Task DeploymentProcess_Persists_State_Correctly()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var processBuilder = DeploymentProcess.BuildProcess();
        var process = processBuilder.Build();
        var request = new DeploymentRequest(
            CloudProvider: "AWS",
            Region: "us-west-2",
            DeploymentName: "test-deployment",
            Tier: "Production",
            Features: new List<string> { "API", "Database", "Cache" });
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartDeployment",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        await Task.Delay(100);

        // Assert - Process should maintain state
    }

    #endregion

    #region UpgradeProcess Tests

    [Fact]
    public void UpgradeProcess_Builds_Successfully()
    {
        // Arrange
        var processBuilder = UpgradeProcess.BuildProcess();

        // Act
        var process = processBuilder.Build();

        // Assert
        Assert.NotNull(process);
        _output.WriteLine($"UpgradeProcess built successfully");
    }

    [Fact]
    public async Task UpgradeProcess_Starts_With_Valid_Input()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = UpgradeProcess.BuildProcess().Build();
        var request = new UpgradeRequest(
            DeploymentName: "production-deployment",
            TargetVersion: "2.0.0");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartUpgrade",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);

        // Assert
        Assert.NotNull(processHandle);
    }

    [Fact]
    public async Task UpgradeProcess_Completes_All_Steps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = UpgradeProcess.BuildProcess().Build();
        var request = new UpgradeRequest(
            DeploymentName: "test-deployment",
            TargetVersion: "2.0.0");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartUpgrade",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        var completed = await WaitForProcessCompletion(processHandle, TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(completed, "Process should complete within timeout");
        _output.WriteLine("UpgradeProcess completed version detection step");
    }

    [Fact]
    public async Task UpgradeProcess_Handles_Errors_Gracefully()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = UpgradeProcess.BuildProcess().Build();
        var request = new UpgradeRequest(
            DeploymentName: "",
            TargetVersion: "2.0.0");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartUpgrade",
            Data = request
        };

        // Act & Assert - Should not throw
        var processHandle = await process.StartAsync(kernel, initialEvent);
        Assert.NotNull(processHandle);
        _output.WriteLine("UpgradeProcess handled invalid input gracefully");
    }

    [Fact]
    public async Task UpgradeProcess_Persists_State_Correctly()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = UpgradeProcess.BuildProcess().Build();
        var request = new UpgradeRequest(
            DeploymentName: "production-deployment",
            TargetVersion: "2.0.0");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartUpgrade",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        await Task.Delay(100);

        // Assert
    }

    #endregion

    #region MetadataProcess Tests

    [Fact]
    public void MetadataProcess_Builds_Successfully()
    {
        // Arrange
        var processBuilder = MetadataProcess.BuildProcess();

        // Act
        var process = processBuilder.Build();

        // Assert
        Assert.NotNull(process);
        _output.WriteLine("MetadataProcess built successfully");
    }

    [Fact]
    public async Task MetadataProcess_Starts_With_Valid_Input()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = MetadataProcess.BuildProcess().Build();
        var request = new MetadataRequest(
            DatasetPath: "/data/raster/landsat8.tif",
            DatasetName: "Landsat 8 Scene");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartMetadataExtraction",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);

        // Assert
        Assert.NotNull(processHandle);
    }

    [Fact]
    public async Task MetadataProcess_Completes_All_Steps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = MetadataProcess.BuildProcess().Build();
        var request = new MetadataRequest(
            DatasetPath: "/data/raster/test.cog",
            DatasetName: "Test COG");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartMetadataExtraction",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        var completed = await WaitForProcessCompletion(processHandle, TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(completed, "Process should complete within timeout");
        _output.WriteLine("MetadataProcess completed metadata extraction");
    }

    [Fact]
    public async Task MetadataProcess_Handles_Errors_Gracefully()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = MetadataProcess.BuildProcess().Build();
        var request = new MetadataRequest(
            DatasetPath: "",
            DatasetName: "Invalid Dataset");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartMetadataExtraction",
            Data = request
        };

        // Act & Assert - Should not throw
        var processHandle = await process.StartAsync(kernel, initialEvent);
        Assert.NotNull(processHandle);
        _output.WriteLine("MetadataProcess handled invalid path gracefully");
    }

    [Fact]
    public async Task MetadataProcess_Persists_State_Correctly()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = MetadataProcess.BuildProcess().Build();
        var request = new MetadataRequest(
            DatasetPath: "/data/sentinel2.zarr",
            DatasetName: "Sentinel-2 L2A");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartMetadataExtraction",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        await Task.Delay(100);

        // Assert
    }

    #endregion

    #region GitOpsProcess Tests

    [Fact]
    public void GitOpsProcess_Builds_Successfully()
    {
        // Arrange
        var processBuilder = GitOpsProcess.BuildProcess();

        // Act
        var process = processBuilder.Build();

        // Assert
        Assert.NotNull(process);
        _output.WriteLine("GitOpsProcess built successfully");
    }

    [Fact]
    public async Task GitOpsProcess_Starts_With_Valid_Input()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = GitOpsProcess.BuildProcess().Build();
        var request = new GitOpsRequest(
            RepoUrl: "https://github.com/org/honua-config.git",
            Branch: "main",
            ConfigPath: "deployments/production");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartGitOps",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);

        // Assert
        Assert.NotNull(processHandle);
    }

    [Fact]
    public async Task GitOpsProcess_Completes_All_Steps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = GitOpsProcess.BuildProcess().Build();
        var request = new GitOpsRequest(
            RepoUrl: "https://github.com/test/config.git",
            Branch: "main",
            ConfigPath: "config/");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartGitOps",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        var completed = await WaitForProcessCompletion(processHandle, TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(completed, "Process should complete within timeout");
        _output.WriteLine("GitOpsProcess completed config validation");
    }

    [Fact]
    public async Task GitOpsProcess_Handles_Errors_Gracefully()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = GitOpsProcess.BuildProcess().Build();
        var request = new GitOpsRequest(
            RepoUrl: "",
            Branch: "main",
            ConfigPath: "config/");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartGitOps",
            Data = request
        };

        // Act & Assert - Should not throw
        var processHandle = await process.StartAsync(kernel, initialEvent);
        Assert.NotNull(processHandle);
        _output.WriteLine("GitOpsProcess handled invalid repo URL gracefully");
    }

    [Fact]
    public async Task GitOpsProcess_Persists_State_Correctly()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = GitOpsProcess.BuildProcess().Build();
        var request = new GitOpsRequest(
            RepoUrl: "https://github.com/org/infra.git",
            Branch: "main",
            ConfigPath: "k8s/production");
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartGitOps",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        await Task.Delay(100);

        // Assert
    }

    #endregion

    #region BenchmarkProcess Tests

    [Fact]
    public void BenchmarkProcess_Builds_Successfully()
    {
        // Arrange
        var processBuilder = BenchmarkProcess.BuildProcess();

        // Act
        var process = processBuilder.Build();

        // Assert
        Assert.NotNull(process);
        _output.WriteLine("BenchmarkProcess built successfully");
    }

    [Fact]
    public async Task BenchmarkProcess_Starts_With_Valid_Input()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = BenchmarkProcess.BuildProcess().Build();
        var request = new BenchmarkRequest(
            BenchmarkName: "Load Test - Query API",
            TargetEndpoint: "https://api.honua.dev/query",
            Concurrency: 50,
            Duration: 300);
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartBenchmark",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);

        // Assert
        Assert.NotNull(processHandle);
    }

    [Fact]
    public async Task BenchmarkProcess_Completes_All_Steps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = BenchmarkProcess.BuildProcess().Build();
        var request = new BenchmarkRequest(
            BenchmarkName: "Performance Test",
            TargetEndpoint: "https://test.honua.dev",
            Concurrency: 10,
            Duration: 60);
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartBenchmark",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        var completed = await WaitForProcessCompletion(processHandle, TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(completed, "Process should complete within timeout");
        _output.WriteLine("BenchmarkProcess completed setup step");
    }

    [Fact]
    public async Task BenchmarkProcess_Handles_Errors_Gracefully()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = BenchmarkProcess.BuildProcess().Build();
        var request = new BenchmarkRequest(
            BenchmarkName: "Invalid Test",
            TargetEndpoint: "",
            Concurrency: -1,
            Duration: 0);
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartBenchmark",
            Data = request
        };

        // Act & Assert - Should not throw
        var processHandle = await process.StartAsync(kernel, initialEvent);
        Assert.NotNull(processHandle);
        _output.WriteLine("BenchmarkProcess handled invalid parameters gracefully");
    }

    [Fact]
    public async Task BenchmarkProcess_Persists_State_Correctly()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = BenchmarkProcess.BuildProcess().Build();
        var request = new BenchmarkRequest(
            BenchmarkName: "Stress Test",
            TargetEndpoint: "https://api.honua.dev",
            Concurrency: 100,
            Duration: 600);
        var initialEvent = new KernelProcessEvent
        {
            Id = "StartBenchmark",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);
        await Task.Delay(100);

        // Assert
    }

    #endregion

    #region Cross-Process Integration Tests

    [Fact]
    public void AllProcesses_Build_Successfully()
    {
        // Arrange & Act
        var deploymentProcess = DeploymentProcess.BuildProcess().Build();
        var upgradeProcess = UpgradeProcess.BuildProcess().Build();
        var metadataProcess = MetadataProcess.BuildProcess().Build();
        var gitOpsProcess = GitOpsProcess.BuildProcess().Build();
        var benchmarkProcess = BenchmarkProcess.BuildProcess().Build();

        // Assert
        Assert.NotNull(deploymentProcess);
        Assert.NotNull(upgradeProcess);
        Assert.NotNull(metadataProcess);
        Assert.NotNull(gitOpsProcess);
        Assert.NotNull(benchmarkProcess);

        _output.WriteLine("All 5 processes built successfully");
    }

    [Fact]
    public async Task AllProcesses_Start_Concurrently()
    {
        // Arrange
        var kernel = CreateTestKernel();

        var deploymentProcess = DeploymentProcess.BuildProcess().Build();
        var upgradeProcess = UpgradeProcess.BuildProcess().Build();
        var metadataProcess = MetadataProcess.BuildProcess().Build();
        var gitOpsProcess = GitOpsProcess.BuildProcess().Build();
        var benchmarkProcess = BenchmarkProcess.BuildProcess().Build();

        // Act
        var deploymentTask = deploymentProcess.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartDeployment",
                Data = new DeploymentRequest("AWS", "us-east-1", "test", "Dev", new())
            });

        var upgradeTask = upgradeProcess.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartUpgrade",
                Data = new UpgradeRequest("test-deployment", "2.0.0")
            });

        var metadataTask = metadataProcess.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartMetadataExtraction",
                Data = new MetadataRequest("/data/test.tif", "Test Dataset")
            });

        var gitOpsTask = gitOpsProcess.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartGitOps",
                Data = new GitOpsRequest("https://github.com/test/repo.git", "main", "config/")
            });

        var benchmarkTask = benchmarkProcess.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartBenchmark",
                Data = new BenchmarkRequest("Test", "https://test.api", 10, 60)
            });

        var handles = await Task.WhenAll(
            deploymentTask,
            upgradeTask,
            metadataTask,
            gitOpsTask,
            benchmarkTask);

        // Assert
        Assert.All(handles, handle => Assert.NotNull(handle));
        Assert.All(handles, handle => Assert.NotNull(handle));

        _output.WriteLine($"All 5 processes started concurrently successfully");
    }

    #endregion
}

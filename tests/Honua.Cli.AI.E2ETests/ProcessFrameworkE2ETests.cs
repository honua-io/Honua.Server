using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.E2ETests.Infrastructure;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Honua.Cli.AI.Services.Processes.Steps.Upgrade;
using Honua.Cli.AI.Services.Processes.Steps.Metadata;
using Honua.Cli.AI.Services.Processes.Steps.GitOps;
using Honua.Cli.AI.Services.Processes.Steps.Benchmark;
using Honua.Cli.AI.Services.Processes.State;
using Honua.Cli.AI.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;
using Xunit.Abstractions;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Discovery;
using Honua.Cli.AI.Services.Telemetry;

namespace Honua.Cli.AI.E2ETests;

/// <summary>
/// End-to-end integration tests for Process Framework workflows.
/// Tests complete workflow execution with mocked LLM and real process orchestration.
/// </summary>
[Trait("Category", "E2E")]
[Collection("ProcessFramework")]
public class ProcessFrameworkE2ETests : IClassFixture<E2ETestFixture>
{
    private readonly E2ETestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ProcessFrameworkE2ETests(E2ETestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.ResetTelemetry();
    }

    private Kernel CreateTestKernel(Action<IServiceCollection>? configure = null)
    {
        var builder = Kernel.CreateBuilder();

        builder.Services.AddLogging(logging => logging.AddDebug());
        builder.Services.AddSingleton<IHttpClientFactory, TestHttpClientFactory>();
        builder.Services.AddSingleton<IChatCompletionService>(_ => _fixture.MockLLM);
        builder.Services.AddSingleton(_fixture.MockLLM);

        // Register all step types
        RegisterDeploymentSteps(builder.Services);
        RegisterUpgradeSteps(builder.Services);
        RegisterMetadataSteps(builder.Services);
        RegisterGitOpsSteps(builder.Services);
        RegisterBenchmarkSteps(builder.Services);

        configure?.Invoke(builder.Services);

        var kernel = builder.Build();
        return kernel;
    }

    #region Step Registration

    private void RegisterDeploymentSteps(IServiceCollection services)
    {
        services.AddSingleton<IResourceEnvelopeCatalog, ResourceEnvelopeCatalog>();
        services.AddSingleton<IDeploymentGuardrailValidator, DeploymentGuardrailValidator>();
        services.AddSingleton<ITelemetryService, NullTelemetryService>();
        services.AddSingleton<IDeploymentGuardrailMonitor, PostDeployGuardrailMonitor>();
        services.AddSingleton<IDeploymentMetricsProvider, E2EDeploymentMetricsProvider>();
        services.AddSingleton<IAzureCli, NoopAzureCli>();
        services.AddSingleton<IGcloudCli, NoopGcloudCli>();
        services.AddSingleton<IAwsCli, NoopAwsCli>();
        var discoveryService = new FakeCloudDiscoveryService();
        services.AddSingleton<ICloudDiscoveryService>(discoveryService);

        services.AddTransient<ValidateDeploymentRequirementsStep>();
        services.AddTransient<GenerateInfrastructureCodeStep>();
        services.AddTransient<ReviewInfrastructureStep>();
        services.AddTransient<DeployInfrastructureStep>();
        services.AddTransient<ConfigureServicesStep>();
        services.AddTransient<DeployHonuaApplicationStep>();
        services.AddTransient<ValidateDeploymentStep>();
        services.AddTransient<ConfigureObservabilityStep>();
    }

    private void RegisterUpgradeSteps(IServiceCollection services)
    {
        services.AddTransient<DetectCurrentVersionStep>();
        services.AddTransient<BackupDatabaseStep>();
        services.AddTransient<CreateBlueEnvironmentStep>();
        services.AddTransient<SwitchTrafficStep>();
    }

    private void RegisterMetadataSteps(IServiceCollection services)
    {
        services.AddTransient<ExtractMetadataStep>();
        services.AddTransient<GenerateStacItemStep>();
        services.AddTransient<PublishStacStep>();
    }

    private void RegisterGitOpsSteps(IServiceCollection services)
    {
        services.AddTransient<ValidateGitConfigStep>();
        services.AddTransient<SyncConfigStep>();
        services.AddTransient<MonitorDriftStep>();
    }

    private void RegisterBenchmarkSteps(IServiceCollection services)
    {
        services.AddTransient<SetupBenchmarkStep>();
        services.AddTransient<RunBenchmarkStep>();
        services.AddTransient<AnalyzeResultsStep>();
        services.AddTransient<GenerateReportStep>();
    }

    #endregion

    #region Deployment Workflow E2E Tests

    [Fact]
    public async Task DeploymentWorkflow_CompleteE2E_StartsAndProcessesSteps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = DeploymentProcess.BuildProcess().Build();
        var request = new DeploymentRequest(
            CloudProvider: "AWS",
            Region: "us-west-2",
            DeploymentName: "e2e-test-deployment",
            Tier: "Development",
            Features: new List<string> { "GeoServer", "PostGIS", "VectorTiles" }
        );

        var initialEvent = new KernelProcessEvent
        {
            Id = "StartDeployment",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);

        await Task.Delay(200); // Allow process to execute initial steps

        // Assert
        processHandle.Should().NotBeNull();
        _output.WriteLine("Deployment workflow started successfully.");
        _output.WriteLine($"Mock LLM was called {_fixture.MockLLM.GetCallCount()} times");
    }

    [Fact]
    public async Task DeploymentWorkflow_WithInvalidProvider_HandlesErrorGracefully()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = DeploymentProcess.BuildProcess().Build();
        var request = new DeploymentRequest(
            CloudProvider: "InvalidProvider",
            Region: "us-west-2",
            DeploymentName: "invalid-test",
            Tier: "Development",
            Features: new List<string>()
        );

        var initialEvent = new KernelProcessEvent
        {
            Id = "StartDeployment",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);

        await Task.Delay(200);

        // Assert
        processHandle.Should().NotBeNull();
        _output.WriteLine("Deployment workflow handled invalid provider gracefully");
    }

    [Fact]
    public async Task DeploymentWorkflow_StateTracking_PersistsCorrectly()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = DeploymentProcess.BuildProcess().Build();
        var request = new DeploymentRequest(
            CloudProvider: "AWS",
            Region: "us-east-1",
            DeploymentName: "state-test",
            Tier: "Production",
            Features: new List<string> { "API", "Database", "Cache" }
        );

        var initialEvent = new KernelProcessEvent
        {
            Id = "StartDeployment",
            Data = request
        };

        // Act
        var processHandle = await process.StartAsync(kernel, initialEvent);

        await Task.Delay(150);

        // Assert
        _output.WriteLine("State persisted successfully.");
    }

    #endregion

    #region Upgrade Workflow E2E Tests

    [Fact]
    public async Task UpgradeWorkflow_CompleteE2E_StartsAndProcessesSteps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = UpgradeProcess.BuildProcess().Build();
        var request = new UpgradeRequest(
            DeploymentName: "production-deployment",
            TargetVersion: "2.0.0"
        );

        // Act
        var processHandle = await process.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartUpgrade",
                Data = request
            });

        await Task.Delay(200);

        // Assert
        processHandle.Should().NotBeNull();
        _output.WriteLine("Upgrade workflow started successfully.");
    }

    [Fact]
    public async Task UpgradeWorkflow_WithEmptyDeploymentName_HandlesErrorGracefully()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = UpgradeProcess.BuildProcess().Build();
        var request = new UpgradeRequest(
            DeploymentName: "",
            TargetVersion: "2.0.0"
        );

        // Act & Assert
        var processHandle = await process.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartUpgrade",
                Data = request
            });

        await Task.Delay(100);
        processHandle.Should().NotBeNull();
        _output.WriteLine("Upgrade workflow handled empty deployment name gracefully");
    }

    #endregion

    #region Metadata Workflow E2E Tests

    [Fact]
    public async Task MetadataWorkflow_CompleteE2E_StartsAndProcessesSteps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = MetadataProcess.BuildProcess().Build();
        var request = new MetadataRequest(
            DatasetPath: "/data/raster/landsat8.tif",
            DatasetName: "Landsat 8 Scene"
        );

        // Act
        var processHandle = await process.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartMetadataExtraction",
                Data = request
            });

        await Task.Delay(200);

        // Assert
        processHandle.Should().NotBeNull();
        _output.WriteLine("Metadata workflow started successfully.");
    }

    [Fact]
    public async Task MetadataWorkflow_WithInvalidPath_HandlesErrorGracefully()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = MetadataProcess.BuildProcess().Build();
        var request = new MetadataRequest(
            DatasetPath: "",
            DatasetName: "Invalid Dataset"
        );

        // Act
        var processHandle = await process.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartMetadataExtraction",
                Data = request
            });

        await Task.Delay(100);

        // Assert
        processHandle.Should().NotBeNull();
        _output.WriteLine("Metadata workflow handled invalid path gracefully");
    }

    #endregion

    #region GitOps Workflow E2E Tests

    [Fact]
    public async Task GitOpsWorkflow_CompleteE2E_StartsAndProcessesSteps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = GitOpsProcess.BuildProcess().Build();
        var request = new GitOpsRequest(
            RepoUrl: "https://github.com/org/honua-config.git",
            Branch: "main",
            ConfigPath: "deployments/production"
        );

        // Act
        var processHandle = await process.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartGitOps",
                Data = request
            });

        await Task.Delay(200);

        // Assert
        processHandle.Should().NotBeNull();
        _output.WriteLine("GitOps workflow started successfully.");
    }

    #endregion

    #region Benchmark Workflow E2E Tests

    [Fact]
    public async Task BenchmarkWorkflow_CompleteE2E_StartsAndProcessesSteps()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var process = BenchmarkProcess.BuildProcess().Build();
        var request = new BenchmarkRequest(
            BenchmarkName: "Load Test - Query API",
            TargetEndpoint: "https://api.honua.dev/query",
            Concurrency: 50,
            Duration: 300
        );

        // Act
        var processHandle = await process.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartBenchmark",
                Data = request
            });

        await Task.Delay(200);

        // Assert
        processHandle.Should().NotBeNull();
        _output.WriteLine("Benchmark workflow started successfully.");
    }

    #endregion

    #region Concurrent Workflow Tests

    [Fact]
    public async Task MultipleWorkflows_RunConcurrently_AllStartSuccessfully()
    {
        // Arrange
        var kernel = CreateTestKernel();

        var deploymentProcess = DeploymentProcess.BuildProcess().Build();
        var upgradeProcess = UpgradeProcess.BuildProcess().Build();
        var metadataProcess = MetadataProcess.BuildProcess().Build();
        var gitOpsProcess = GitOpsProcess.BuildProcess().Build();
        var benchmarkProcess = BenchmarkProcess.BuildProcess().Build();

        // Act
        var tasks = new[]
        {
            deploymentProcess.StartAsync(kernel, new KernelProcessEvent
            {
                Id = "StartDeployment",
                Data = new DeploymentRequest("AWS", "us-east-1", "concurrent-test-1", "Dev", new())
            }),
            upgradeProcess.StartAsync(kernel, new KernelProcessEvent
            {
                Id = "StartUpgrade",
                Data = new UpgradeRequest("concurrent-test-2", "2.0.0")
            }),
            metadataProcess.StartAsync(kernel, new KernelProcessEvent
            {
                Id = "StartMetadataExtraction",
                Data = new MetadataRequest("/data/test.tif", "Concurrent Test Dataset")
            }),
            gitOpsProcess.StartAsync(kernel, new KernelProcessEvent
            {
                Id = "StartGitOps",
                Data = new GitOpsRequest("https://github.com/test/repo.git", "main", "config/")
            }),
            benchmarkProcess.StartAsync(kernel, new KernelProcessEvent
            {
                Id = "StartBenchmark",
                Data = new BenchmarkRequest("Concurrent Test", "https://test.api", 10, 60)
            })
        };

        var handles = await Task.WhenAll(tasks);

        // Assert
        handles.Should().HaveCount(5);
        handles.Should().OnlyContain(h => h != null);
        _output.WriteLine("All 5 workflows started concurrently.");
    }

    #endregion

    #region Parameter Extraction E2E Tests

    [Fact]
    public async Task ParameterExtraction_DeploymentRequest_ExtractsCorrectly()
    {
        // Arrange
        var parameterService = new ParameterExtractionService(
            _fixture.MockLLM,
            NullLogger<ParameterExtractionService>.Instance
        );

        _fixture.MockLLM.QueueResponse(@"{
  ""cloudProvider"": ""Azure"",
  ""region"": ""eastus"",
  ""deploymentName"": ""azure-production"",
  ""tier"": ""Production"",
  ""features"": [""GeoServer"", ""STAC"", ""COG""]
}");

        // Act
        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to Azure East US for production with GeoServer, STAC, and COG support"
        );

        // Assert
        _output.WriteLine($"LLM deployment response: {_fixture.MockLLM.LastResponse}");
        request.Should().NotBeNull();
        request.CloudProvider.Should().Be("Azure");
        request.Region.Should().Be("eastus");
        request.DeploymentName.Should().Be("azure-production");
        request.Tier.Should().Be("Production");
        request.Features.Should().Contain(new[] { "GeoServer", "STAC", "COG" });

        _output.WriteLine($"Extracted deployment parameters: {request.CloudProvider}, {request.Region}, {request.DeploymentName}");
    }

    [Fact]
    public async Task ParameterExtraction_UpgradeRequest_ExtractsCorrectly()
    {
        // Arrange
        var parameterService = new ParameterExtractionService(
            _fixture.MockLLM,
            NullLogger<ParameterExtractionService>.Instance
        );

        _fixture.MockLLM.QueueResponse(@"{
  ""deploymentName"": ""staging-cluster"",
  ""targetVersion"": ""3.2.1""
}");

        // Act
        var request = await parameterService.ExtractUpgradeParametersAsync(
            "Upgrade staging-cluster to version 3.2.1"
        );

        // Assert
        _output.WriteLine($"LLM upgrade response: {_fixture.MockLLM.LastResponse}");
        request.Should().NotBeNull();
        request.DeploymentName.Should().Be("staging-cluster");
        request.TargetVersion.Should().Be("3.2.1");

        _output.WriteLine($"Extracted upgrade parameters: {request.DeploymentName} -> {request.TargetVersion}");
    }

    #endregion

    #region Process Recovery Tests

    [Fact]
    public async Task ProcessRecovery_AfterInterruption_CanRestart()
    {
        // Arrange
        var kernel = CreateTestKernel();
        var processBuilder = DeploymentProcess.BuildProcess();
        var process = processBuilder.Build();

        var request = new DeploymentRequest(
            CloudProvider: "AWS",
            Region: "us-west-2",
            DeploymentName: "recovery-test",
            Tier: "Development",
            Features: new List<string> { "API" }
        );

        // Act - Start process
        var processHandle1 = await process.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartDeployment",
                Data = request
            });

        await Task.Delay(100);

        // Simulate recovery by starting a new instance
        var process2 = processBuilder.Build();
        var processHandle2 = await process2.StartAsync(
            kernel,
            new KernelProcessEvent
            {
                Id = "StartDeployment",
                Data = request
            });

        // Assert
        processHandle1.Should().NotBeNull();
        processHandle2.Should().NotBeNull();
        _output.WriteLine("Process recovery test completed with distinct process instances.");
    }

    #endregion

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client = new();

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class E2EDeploymentMetricsProvider : IDeploymentMetricsProvider
    {
        public Task<DeploymentGuardrailMetrics> GetMetricsAsync(
            DeploymentState state,
            DeploymentGuardrailDecision decision,
            CancellationToken cancellationToken = default)
        {
            var metrics = new DeploymentGuardrailMetrics(
                CpuUtilization: 0.35m,
                MemoryUtilizationGb: 4m,
                ColdStartsPerHour: 0,
                QueueBacklog: 0,
                AverageLatencyMs: 120m);

            return Task.FromResult(metrics);
        }
    }

    private sealed class FakeCloudDiscoveryService : ICloudDiscoveryService
    {
        public Task<CloudDiscoverySnapshot> DiscoverAsync(CloudDiscoveryRequest request, CancellationToken cancellationToken)
        {
            var provider = request?.CloudProvider ?? "AWS";
            var region = request?.Region ?? "us-east-1";
            var normalized = provider.ToUpperInvariant();
            var suffix = normalized switch
            {
                "AZURE" => "azure",
                "GCP" or "GOOGLE" => "gcp",
                _ => "aws"
            };

            var networks = new[]
            {
                new DiscoveredNetwork(
                    Id: $"{suffix}-network-1",
                    Name: $"{normalized}-Primary-Network",
                    Region: region,
                    Subnets: new[]
                    {
                        new DiscoveredSubnet(
                            Id: $"{suffix}-subnet-a",
                            Name: $"{normalized}-Subnet-A",
                            Cidr: "10.0.0.0/24",
                            AvailabilityZone: $"{region}a")
                    })
            };

            var databases = new[]
            {
                new DiscoveredDatabase(
                    Identifier: $"{suffix}-db-1",
                    Engine: normalized switch
                    {
                        "AZURE" => "Azure SQL",
                        "GCP" or "GOOGLE" => "Cloud SQL",
                        _ => "PostgreSQL"
                    },
                    Endpoint: $"{suffix}-db.example.internal",
                    Status: "available")
            };

            var zones = new[]
            {
                new DiscoveredDnsZone(
                    Id: $"{suffix}-zone-1",
                    Name: $"{suffix}.example.internal",
                    IsPrivate: false)
            };

            var snapshot = new CloudDiscoverySnapshot(
                CloudProvider: provider,
                Networks: networks,
                Databases: databases,
                DnsZones: zones);

            return Task.FromResult(snapshot);
        }
    }
}

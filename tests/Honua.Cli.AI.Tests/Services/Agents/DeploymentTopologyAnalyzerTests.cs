using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Planning;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public sealed class DeploymentTopologyAnalyzerTests
{
    [Fact]
    public async Task AnalyzeFromPlanAsync_ShouldExtractTopologyFromPlan()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var plan = CreateTestPlan();

        // Act
        var topology = await analyzer.AnalyzeFromPlanAsync(plan, "aws", "us-east-1", CancellationToken.None);

        // Assert
        topology.Should().NotBeNull();
        topology.CloudProvider.Should().Be("aws");
        topology.Region.Should().Be("us-east-1");
    }

    [Fact]
    public async Task AnalyzeFromPlanAsync_WithDatabaseSteps_ShouldDetectDatabase()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var plan = CreatePlanWithDatabaseSteps();

        // Act
        var topology = await analyzer.AnalyzeFromPlanAsync(plan, "aws", "us-west-2", CancellationToken.None);

        // Assert
        topology.Database.Should().NotBeNull();
        topology.Database!.Engine.Should().Be("postgres");
        topology.Database.Version.Should().Be("15");
    }

    [Fact]
    public async Task AnalyzeFromPlanAsync_WithStorageSteps_ShouldDetectStorage()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var plan = CreatePlanWithStorageSteps();

        // Act
        var topology = await analyzer.AnalyzeFromPlanAsync(plan, "aws", "us-east-1", CancellationToken.None);

        // Assert
        topology.Storage.Should().NotBeNull();
        topology.Storage!.Type.Should().Be("s3");
    }

    [Fact]
    public async Task AnalyzeFromPlanAsync_WithProdPlan_ShouldDetectProdEnvironment()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var plan = CreateTestPlan(planId: "deploy-honua-prod");

        // Act
        var topology = await analyzer.AnalyzeFromPlanAsync(plan, "azure", "eastus", CancellationToken.None);

        // Assert
        topology.Environment.Should().Be("prod");
    }

    [Fact]
    public async Task AnalyzeFromPlanAsync_ShouldExtractFeatures()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var plan = CreatePlanWithFeatures();

        // Act
        var topology = await analyzer.AnalyzeFromPlanAsync(plan, "gcp", "us-central1", CancellationToken.None);

        // Assert
        topology.Features.Should().NotBeNull();
        topology.Features.Should().Contain("OGC WFS 2.0");
        topology.Features.Should().Contain("OGC WMS 1.3");
    }

    [Fact]
    public async Task AnalyzeFromConfigAsync_ShouldParseConfigContent()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var configContent = @"{
            ""database"": {
                ""provider"": ""postgresql"",
                ""connectionString"": ""Host=localhost;Database=honua""
            },
            ""storage"": {
                ""provider"": ""s3"",
                ""bucketName"": ""honua-tiles""
            },
            ""services"": {
                ""wfs"": true,
                ""wms"": true,
                ""wmts"": true
            }
        }";

        // Act
        var topology = await analyzer.AnalyzeFromConfigAsync(
            configContent,
            "aws",
            "us-east-1",
            "dev",
            CancellationToken.None);

        // Assert
        topology.Should().NotBeNull();
        topology.CloudProvider.Should().Be("aws");
        topology.Region.Should().Be("us-east-1");
        topology.Environment.Should().Be("dev");
        topology.Database.Should().NotBeNull();
        topology.Storage.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeFromConfigAsync_WithProdEnvironment_ShouldUseLargerResources()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var configContent = @"{""database"":{""provider"":""postgresql""}}";

        // Act
        var topology = await analyzer.AnalyzeFromConfigAsync(
            configContent,
            "aws",
            "us-east-1",
            "prod",
            CancellationToken.None);

        // Assert
        topology.Database.Should().NotBeNull();
        topology.Database!.InstanceSize.Should().Contain("xlarge");
        topology.Database.HighAvailability.Should().BeTrue();
        topology.Compute.Should().NotBeNull();
        topology.Compute!.InstanceCount.Should().BeGreaterThan(1);
        topology.Compute.AutoScaling.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeFromConfigAsync_WithDevEnvironment_ShouldUseSmallerResources()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var configContent = @"{""database"":{""provider"":""postgresql""}}";

        // Act
        var topology = await analyzer.AnalyzeFromConfigAsync(
            configContent,
            "aws",
            "us-east-1",
            "dev",
            CancellationToken.None);

        // Assert
        topology.Database.Should().NotBeNull();
        topology.Database!.InstanceSize.Should().Contain("micro");
        topology.Database.HighAvailability.Should().BeFalse();
        topology.Compute.Should().NotBeNull();
        topology.Compute!.InstanceCount.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeFromConfigAsync_ForAzure_ShouldUseBlobStorage()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var configContent = @"{""storage"":{""provider"":""azure-blob""}}";

        // Act
        var topology = await analyzer.AnalyzeFromConfigAsync(
            configContent,
            "azure",
            "eastus",
            "dev",
            CancellationToken.None);

        // Assert
        topology.Storage.Should().NotBeNull();
        topology.Storage!.Type.Should().Be("blob");
        topology.Monitoring!.Provider.Should().Be("application-insights");
    }

    [Fact]
    public async Task AnalyzeFromConfigAsync_ForGCP_ShouldUseGCSStorage()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var configContent = @"{""storage"":{""provider"":""gcs""}}";

        // Act
        var topology = await analyzer.AnalyzeFromConfigAsync(
            configContent,
            "gcp",
            "us-central1",
            "staging",
            CancellationToken.None);

        // Assert
        topology.Storage.Should().NotBeNull();
        topology.Storage!.Type.Should().Be("gcs");
        topology.Monitoring!.Provider.Should().Be("cloud-monitoring");
    }

    [Fact]
    public async Task AnalyzeFromPlanAsync_ShouldInferComputeConfig()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var plan = CreateTestPlan();

        // Act
        var topology = await analyzer.AnalyzeFromPlanAsync(plan, "aws", "us-east-1", CancellationToken.None);

        // Assert
        topology.Compute.Should().NotBeNull();
        topology.Compute!.Type.Should().Be("container");
    }

    [Fact]
    public async Task AnalyzeFromPlanAsync_ShouldInferNetworkingConfig()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var plan = CreateTestPlan();

        // Act
        var topology = await analyzer.AnalyzeFromPlanAsync(plan, "aws", "us-east-1", CancellationToken.None);

        // Assert
        topology.Networking.Should().NotBeNull();
        topology.Networking!.LoadBalancer.Should().BeTrue();
        topology.Networking.PublicAccess.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeFromPlanAsync_ShouldInferMonitoringConfig()
    {
        // Arrange
        var llmProvider = new MockLlmProvider();
        var analyzer = new DeploymentTopologyAnalyzer(llmProvider, NullLogger<DeploymentTopologyAnalyzer>.Instance);

        var plan = CreateTestPlan();

        // Act
        var topology = await analyzer.AnalyzeFromPlanAsync(plan, "aws", "us-east-1", CancellationToken.None);

        // Assert
        topology.Monitoring.Should().NotBeNull();
        topology.Monitoring!.Provider.Should().Be("cloudwatch");
        topology.Monitoring.EnableMetrics.Should().BeTrue();
        topology.Monitoring.EnableLogs.Should().BeTrue();
    }

    // Helper methods
    private static ExecutionPlan CreateTestPlan(string planId = "test-deployment")
    {
        return new ExecutionPlan
        {
            Id = planId,
            Title = "Deploy HonuaIO",
            Description = "Test deployment plan",
            Type = PlanType.Deployment,
            Steps = new List<PlanStep>
            {
                new()
                {
                    StepNumber = 1,
                    Description = "Create network",
                    Type = StepType.Custom,
                    Operation = "Create VPC"
                }
            },
            CredentialsRequired = new List<CredentialRequirement>(),
            Risk = new RiskAssessment
            {
                Level = RiskLevel.Low,
                RiskFactors = new List<string>(),
                Mitigations = new List<string>()
            }
        };
    }

    private static ExecutionPlan CreatePlanWithDatabaseSteps()
    {
        var plan = CreateTestPlan();
        plan.Steps.Add(new PlanStep
        {
            StepNumber = 2,
            Description = "Create PostgreSQL database",
            Type = StepType.Custom,
            Operation = "Provision RDS instance"
        });
        return plan;
    }

    private static ExecutionPlan CreatePlanWithStorageSteps()
    {
        var plan = CreateTestPlan();
        plan.Steps.Add(new PlanStep
        {
            StepNumber = 2,
            Description = "Create S3 bucket for tiles",
            Type = StepType.Custom,
            Operation = "Create storage bucket"
        });
        return plan;
    }

    private static ExecutionPlan CreatePlanWithFeatures()
    {
        return new ExecutionPlan
        {
            Id = "test-deployment",
            Title = "Deploy HonuaIO",
            Description = "Deploy HonuaIO with WFS, WMS, WMTS, and Vector Tiles support",
            Type = PlanType.Deployment,
            Steps = new List<PlanStep>
            {
                new()
                {
                    StepNumber = 1,
                    Description = "Create network",
                    Type = StepType.Custom,
                    Operation = "Create VPC"
                }
            },
            CredentialsRequired = new List<CredentialRequirement>(),
            Risk = new RiskAssessment
            {
                Level = RiskLevel.Low,
                RiskFactors = new List<string>(),
                Mitigations = new List<string>()
            }
        };
    }

    // Mock LLM Provider
    private sealed class MockLlmProvider : ILlmProvider
    {
        public string ProviderName => "mock";
        public string DefaultModel => "mock-model";

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<System.Collections.Generic.IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<string>>(new[] { "mock-model" });
        }

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmResponse
            {
                Content = "Mock analysis response",
                Model = DefaultModel,
                Success = true
            });
        }

        public async System.Collections.Generic.IAsyncEnumerable<LlmStreamChunk> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            yield return new LlmStreamChunk { Content = "Mock", IsFinal = true };
        }
    }
}

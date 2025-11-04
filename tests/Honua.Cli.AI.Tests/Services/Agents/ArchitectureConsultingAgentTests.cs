using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class ArchitectureConsultingAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly ArchitectureConsultingAgent _agent;

    public ArchitectureConsultingAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _agent = new ArchitectureConsultingAgent(_kernel, _mockLlmProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ArchitectureConsultingAgent(null!, _mockLlmProvider.Object));
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_DoesNotThrow()
    {
        // LlmProvider is optional - agent falls back to heuristic analysis
        var exception = Record.Exception(() => new ArchitectureConsultingAgent(_kernel, null!));
        exception.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeArchitectureAsync_WithSmallScaleRequirement_RecommendsDockerCompose()
    {
        // Arrange
        var request = "I need to deploy Honua for 10 users with minimal cost";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Setup requirements extraction (only LLM call)
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""workload"": { ""expectedUsers"": ""10"", ""dataVolume"": ""1GB"", ""trafficPattern"": ""steady"", ""geographicScope"": ""local"" },
                    ""budget"": { ""monthlyBudget"": ""$50"", ""costPriority"": ""low-cost"", ""managementPreference"": ""self-hosted"" },
                    ""team"": { ""devopsExperience"": ""beginner"", ""cloudProvider"": ""any"" }
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.AnalyzeArchitectureAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("ArchitectureConsulting");
        result.Message.Should().Contain("Recommended");
        result.Message.Should().Contain("Cost");

        // Verify specific technology recommendation for small scale
        result.Message.Should().Contain("Docker Compose",
            "Small scale deployments should recommend Docker Compose for simplicity and low cost");

        // Verify mock was called
        _mockLlmProvider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeArchitectureAsync_WithMediumScaleRequirement_RecommendsKubernetes()
    {
        // Arrange
        var request = "Deploy Honua for 5000 users with high availability";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Setup requirements extraction
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""workload"": { ""expectedUsers"": ""5000"", ""dataVolume"": ""500GB"", ""trafficPattern"": ""variable"", ""geographicScope"": ""regional"" },
                    ""budget"": { ""monthlyBudget"": ""$500-800"", ""costPriority"": ""balanced"", ""managementPreference"": ""managed"" },
                    ""team"": { ""devopsExperience"": ""intermediate"", ""cloudProvider"": ""any"" }
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.AnalyzeArchitectureAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Recommended");
        result.Message.Should().Contain("Cost");

        // Verify scalable technology recommendation for medium scale
        bool hasScalableTech = result.Message.Contains("Kubernetes") ||
                              result.Message.Contains("Cloud Run") ||
                              result.Message.Contains("ECS") ||
                              result.Message.Contains("AKS") ||
                              result.Message.Contains("EKS") ||
                              result.Message.Contains("GKE") ||
                              result.Message.Contains("Fargate") ||
                              result.Message.Contains("Container Apps");
        hasScalableTech.Should().BeTrue(
            "Medium to large scale deployments should recommend scalable container orchestration platforms");

        // Verify mock was called
        _mockLlmProvider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeArchitectureAsync_WithServerlessRequirement_RecommendsServerless()
    {
        // Arrange
        var request = "I want serverless deployment with pay-per-use pricing";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Setup requirements extraction
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""workload"": { ""expectedUsers"": ""variable"", ""dataVolume"": ""100GB"", ""trafficPattern"": ""intermittent"", ""geographicScope"": ""global"" },
                    ""budget"": { ""monthlyBudget"": ""$50-200"", ""costPriority"": ""pay-per-use"", ""managementPreference"": ""serverless"" },
                    ""team"": { ""devopsExperience"": ""beginner"", ""cloudProvider"": ""any"" }
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.AnalyzeArchitectureAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Recommended");
        result.Message.Should().Contain("Cost");

        // Verify serverless technology recommendations
        bool hasServerlessTech = result.Message.Contains("Serverless") ||
                                result.Message.Contains("Cloud Run") ||
                                result.Message.Contains("Fargate") ||
                                result.Message.Contains("Container Apps");
        hasServerlessTech.Should().BeTrue(
            "Serverless deployments should recommend serverless technologies like Cloud Run, Fargate, or Container Apps");

        // Verify mock was called
        _mockLlmProvider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeArchitectureAsync_WithCostOptimization_ProvidesComparison()
    {
        // Arrange
        var request = "What's the most cost-effective way to deploy Honua for 1000 users?";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Setup requirements extraction
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""workload"": { ""expectedUsers"": ""1000"", ""dataVolume"": ""100GB"", ""trafficPattern"": ""steady"", ""geographicScope"": ""regional"" },
                    ""budget"": { ""monthlyBudget"": ""minimize"", ""costPriority"": ""cost-effective"", ""managementPreference"": ""flexible"" },
                    ""team"": { ""devopsExperience"": ""intermediate"", ""cloudProvider"": ""any"" }
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.AnalyzeArchitectureAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Recommended");
        result.Message.Should().Contain("Alternative Options");
    }

    [Fact]
    public async Task AnalyzeArchitectureAsync_WithAwsPreference_RecommendsAwsServices()
    {
        // Arrange
        var request = "Deploy Honua on AWS with best practices";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Setup requirements extraction
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""Workload"": { ""ExpectedUsers"": ""1000-5000"", ""DataVolume"": ""500GB"", ""TrafficPattern"": ""steady"", ""GeographicScope"": ""regional"" },
                    ""Budget"": { ""MonthlyBudget"": ""$300-600"", ""CostPriority"": ""balanced"", ""ManagementPreference"": ""managed"" },
                    ""Team"": { ""DevOpsExperience"": ""intermediate"", ""CloudPreference"": ""aws"" }
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.AnalyzeArchitectureAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Recommended");
        // AWS services should be mentioned in the message
        bool hasAwsService = result.Message.Contains("EKS") || result.Message.Contains("ECS") ||
                            result.Message.Contains("Fargate") || result.Message.Contains("Aurora") ||
                            result.Message.Contains("ElastiCache");
        hasAwsService.Should().BeTrue($"Expected AWS services in message but got: {result.Message}");
    }

    [Fact]
    public async Task AnalyzeArchitectureAsync_WithAzurePreference_RecommendsAzureServices()
    {
        // Arrange
        var request = "Deploy Honua on Azure with best practices";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Setup requirements extraction
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""Workload"": { ""ExpectedUsers"": ""1000-5000"", ""DataVolume"": ""500GB"", ""TrafficPattern"": ""steady"", ""GeographicScope"": ""regional"" },
                    ""Budget"": { ""MonthlyBudget"": ""$300-600"", ""CostPriority"": ""balanced"", ""ManagementPreference"": ""managed"" },
                    ""Team"": { ""DevOpsExperience"": ""intermediate"", ""CloudPreference"": ""azure"" }
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.AnalyzeArchitectureAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Recommended");
        // Azure services should be mentioned in the message
        bool hasAzureService = result.Message.Contains("AKS") || result.Message.Contains("Azure Kubernetes") ||
                              result.Message.Contains("Container Instances") || result.Message.Contains("Azure Database") ||
                              result.Message.Contains("Azure Cache");
        hasAzureService.Should().BeTrue($"Expected Azure services in message but got: {result.Message}");
    }

    [Fact]
    public async Task AnalyzeArchitectureAsync_WithGcpPreference_RecommendsGcpServices()
    {
        // Arrange
        var request = "Deploy Honua on Google Cloud Platform";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Setup requirements extraction
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""workload"": { ""expectedUsers"": ""1000-5000"", ""dataVolume"": ""500GB"", ""trafficPattern"": ""steady"", ""geographicScope"": ""regional"" },
                    ""budget"": { ""monthlyBudget"": ""$300-600"", ""costPriority"": ""balanced"", ""managementPreference"": ""serverless"" },
                    ""team"": { ""devopsExperience"": ""intermediate"", ""cloudProvider"": ""gcp"" }
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.AnalyzeArchitectureAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Recommended");
        // GCP services should be mentioned
        (result.Message.Contains("GKE") || result.Message.Contains("Cloud Run") || result.Message.Contains("Cloud SQL")).Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeArchitectureAsync_WithLlmFailure_ReturnsFailureResult()
    {
        // Arrange
        var request = "Recommend architecture";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "LLM error",
                Model = "test-model",
                Success = false
            });

        // Act
        var result = await _agent.AnalyzeArchitectureAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(); // Agent falls back to heuristic extraction, so still succeeds
        result.Message.Should().Contain("Recommended");
    }

    [Fact]
    public async Task AnalyzeArchitectureAsync_WithEdgeCaseRequirement_ProvidesGuidance()
    {
        // Arrange
        var request = "I need to deploy for 100,000 users with global distribution";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Setup requirements extraction
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""workload"": { ""expectedUsers"": ""100000"", ""dataVolume"": ""10TB"", ""trafficPattern"": ""high-variable"", ""geographicScope"": ""global"" },
                    ""budget"": { ""monthlyBudget"": ""$5000-10000"", ""costPriority"": ""performance"", ""managementPreference"": ""managed"" },
                    ""team"": { ""devopsExperience"": ""expert"", ""cloudProvider"": ""multi-cloud"" }
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.AnalyzeArchitectureAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Recommended");
        result.Message.Should().Contain("Alternative Options"); // Should provide multiple options for complex requirements
    }
}

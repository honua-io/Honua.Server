using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class SemanticAgentCoordinatorTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<IntelligentAgentSelector> _mockAgentSelector;
    private readonly Mock<ILogger<SemanticAgentCoordinator>> _mockLogger;
    private readonly SemanticAgentCoordinator _coordinator;

    public SemanticAgentCoordinatorTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();

        // Create AgentCapabilityRegistry with required parameters
        var capabilityRegistry = new AgentCapabilityRegistry(
            Microsoft.Extensions.Options.Options.Create(new AgentCapabilityOptions()),
            Mock.Of<ILogger<AgentCapabilityRegistry>>());

        _mockAgentSelector = new Mock<IntelligentAgentSelector>(
            _mockLlmProvider.Object,
            capabilityRegistry,
            Mock.Of<ILogger<IntelligentAgentSelector>>());
        _mockLogger = new Mock<ILogger<SemanticAgentCoordinator>>();

        _coordinator = new SemanticAgentCoordinator(
            _mockLlmProvider.Object,
            _kernel,
            _mockAgentSelector.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SemanticAgentCoordinator(
                null!,
                _kernel,
                _mockAgentSelector.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SemanticAgentCoordinator(
                _mockLlmProvider.Object,
                null!,
                _mockAgentSelector.Object,
                _mockLogger.Object));
    }

    [Fact]
    public async Task ProcessRequestAsync_WithSpaDeploymentRequest_RouteToSpaDeploymentAgent()
    {
        // Arrange
        var request = "Help me deploy my React app with Honua";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Mock intent analysis to return SPA deployment
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""primaryIntent"": ""spa"",
                    ""requiredAgents"": [""SpaDeployment""],
                    ""requiresMultipleAgents"": false,
                    ""reasoning"": ""User wants to deploy React SPA with Honua""
                }",
                Success = true
            });

        // Mock SPA agent response
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => !r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""isSpaDeployment"": true,
                    ""framework"": ""react"",
                    ""frontendDomain"": ""app.example.com"",
                    ""apiDomain"": ""api.example.com"",
                    ""subdomainDeployment"": true,
                    ""apiGatewayRouting"": false,
                    ""scale"": ""medium"",
                    ""localDevOrigins"": [""http://localhost:3000""]
                }",
                Success = true
            });

        _mockAgentSelector
            .Setup(s => s.SelectBestAgentAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                "SpaDeployment",
                new AgentConfidence { Overall = 0.95, Level = "High" }));

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentsInvolved.Should().Contain("SpaDeployment");
        result.Response.Should().Contain("React");
    }

    [Fact]
    public async Task ProcessRequestAsync_WithDeploymentConfigRequest_RouteToDeploymentConfiguration()
    {
        // Arrange
        var request = "Generate Terraform for AWS ECS deployment";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""primaryIntent"": ""deployment"",
                    ""requiredAgents"": [""DeploymentConfiguration""],
                    ""requiresMultipleAgents"": false,
                    ""reasoning"": ""User wants to generate Terraform""
                }",
                Success = true
            });

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => !r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = "Terraform configuration generated successfully",
                Success = true
            });

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.AgentsInvolved.Should().Contain("DeploymentConfiguration");
    }

    [Fact]
    public async Task ProcessRequestAsync_WithArchitectureRequest_RouteToArchitectureConsulting()
    {
        // Arrange
        var request = "What's the best way to deploy Honua for 10000 users?";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""primaryIntent"": ""architecture"",
                    ""requiredAgents"": [""ArchitectureConsulting""],
                    ""requiresMultipleAgents"": false,
                    ""reasoning"": ""User needs architecture guidance""
                }",
                Success = true
            });

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => !r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = "For 10000 users, I recommend Kubernetes with auto-scaling",
                Success = true
            });

        _mockAgentSelector
            .Setup(s => s.SelectBestAgentAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                "ArchitectureConsulting",
                new AgentConfidence { Overall = 0.95, Level = "High" }));

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.AgentsInvolved.Should().Contain("ArchitectureConsulting");
        result.Response.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessRequestAsync_WithBenchmarkRequest_RouteToPerformanceBenchmark()
    {
        // Arrange
        var request = "Create a load testing plan for my Honua deployment";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""primaryIntent"": ""benchmark"",
                    ""requiredAgents"": [""PerformanceBenchmark""],
                    ""requiresMultipleAgents"": false,
                    ""reasoning"": ""User wants load testing strategy""
                }",
                Success = true
            });

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => !r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = "Load testing plan: Use k6 to test query endpoints with 100 concurrent users",
                Success = true
            });

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.AgentsInvolved.Should().Contain("PerformanceBenchmark");
    }

    [Fact]
    public async Task ProcessRequestAsync_WithMultiAgentRequest_OrchestratesMultipleAgents()
    {
        // Arrange
        var request = "Deploy Honua to AWS with security hardening";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""primaryIntent"": ""deployment"",
                    ""requiredAgents"": [""DeploymentConfiguration"", ""PerformanceOptimization""],
                    ""requiresMultipleAgents"": true,
                    ""reasoning"": ""User needs deployment with performance optimization""
                }",
                Success = true
            });

        _mockAgentSelector
            .Setup(s => s.SelectBestAgentAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                "DeploymentConfiguration",
                new AgentConfidence { Overall = 0.95, Level = "High" }));

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => !r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = "Agent completed successfully",
                Success = true
            });

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AgentsInvolved.Should().HaveCountGreaterThanOrEqualTo(2, "multiple agents should be involved");
        result.Steps.Should().HaveCountGreaterThanOrEqualTo(2, "multiple agent steps should be executed");
        result.Steps.Should().Contain(s => s.Success, "at least one agent should succeed");
    }

    [Fact]
    public async Task ProcessRequestAsync_WithIntentAnalysisFailure_ReturnsFallback()
    {
        // Arrange
        var request = "Do something with Honua";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = "LLM error",
                Success = false
            });

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => !r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = "Fallback response",
                Success = true
            });

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Should fallback to DeploymentConfiguration based on coordinator logic
        result.AgentsInvolved.Should().Contain("DeploymentConfiguration");
    }

    [Fact]
    public async Task ProcessRequestAsync_WithInvalidJson_HandlesGracefully()
    {
        // Arrange
        var request = "Generate deployment";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = "{ invalid json here",
                Success = true
            });

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => !r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = "Fallback response",
                Success = true
            });

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Should handle gracefully with fallback
    }

    [Fact]
    public async Task ProcessRequestAsync_WithBlueGreenDeploymentRequest_RouteToBlueGreenAgent()
    {
        // Arrange
        var request = "Setup blue-green deployment for my Honua service";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""primaryIntent"": ""deployment"",
                    ""requiredAgents"": [""BlueGreenDeployment""],
                    ""requiresMultipleAgents"": false,
                    ""reasoning"": ""User wants blue-green deployment setup""
                }",
                Success = true
            });

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => !r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""strategy"": ""blue-green"",
                    ""platform"": ""kubernetes"",
                    ""serviceName"": ""honua-service"",
                    ""blueEnvironment"": {
                        ""name"": ""blue"",
                        ""endpoint"": ""http://blue-service:8080""
                    },
                    ""greenEnvironment"": {
                        ""name"": ""green"",
                        ""endpoint"": ""http://green-service:8080""
                    },
                    ""trafficSplitPercentage"": 0,
                    ""healthCheckPath"": ""/health"",
                    ""rollbackOnFailure"": true,
                    ""summary"": ""Blue-green deployment configured""
                }",
                Success = true
            });

        _mockAgentSelector
            .Setup(s => s.SelectBestAgentAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                "BlueGreenDeployment",
                new AgentConfidence { Overall = 0.95, Level = "High" }));

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.AgentsInvolved.Should().Contain("BlueGreenDeployment");
    }

    [Fact]
    public async Task ProcessRequestAsync_WithNextStepsGeneration_ProvidesNextSteps()
    {
        // Arrange
        var request = "Deploy Honua with SPA frontend";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""primaryIntent"": ""spa"",
                    ""requiredAgents"": [""SpaDeployment""],
                    ""requiresMultipleAgents"": false
                }",
                Success = true
            });

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(
                It.Is<LlmRequest>(r => !r.UserPrompt.Contains("Analyze this user request")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{
                    ""isSpaDeployment"": true,
                    ""framework"": ""react"",
                    ""frontendDomain"": ""app.example.com"",
                    ""apiDomain"": ""api.example.com"",
                    ""subdomainDeployment"": true,
                    ""apiGatewayRouting"": false,
                    ""scale"": ""medium"",
                    ""localDevOrigins"": [""http://localhost:3000""]
                }",
                Success = true
            });

        _mockAgentSelector
            .Setup(s => s.SelectBestAgentAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                "DeploymentConfiguration",
                new AgentConfidence { Overall = 0.95, Level = "High" }));

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.NextSteps.Should().NotBeEmpty();
        result.NextSteps.Should().Contain(step => step.Contains("CORS"));
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsSessionHistory()
    {
        // Arrange
        var request = "Test request";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{""primaryIntent"": ""deployment"", ""requiredAgents"": [""DeploymentConfiguration""], ""requiresMultipleAgents"": false}",
                Success = true
            });

        await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Act
        var history = await _coordinator.GetHistoryAsync();

        // Assert
        history.Should().NotBeNull();
        history.SessionId.Should().NotBeEmpty();
        history.Interactions.Should().HaveCount(1);
        history.Interactions.First().UserRequest.Should().Be(request);
    }

    [Fact]
    public async Task ProcessRequestAsync_WithVerboseContext_IncludesDebugInfo()
    {
        // Arrange
        var request = "Generate deployment";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            Verbosity = VerbosityLevel.Debug
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Model = "test-model",
                Content = @"{""primaryIntent"": ""deployment"", ""requiredAgents"": [""DeploymentConfiguration""], ""requiresMultipleAgents"": false}",
                Success = true
            });

        // Act
        var result = await _coordinator.ProcessRequestAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Steps.Should().NotBeEmpty();
        result.Steps.First().Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }
}

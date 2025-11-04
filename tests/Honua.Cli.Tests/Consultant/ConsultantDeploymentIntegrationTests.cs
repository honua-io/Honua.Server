using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.Services.Consultant;
using Honua.Cli.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.Consultant;

/// <summary>
/// Tests for consultant integration with deployment commands.
/// Verifies that the consultant can orchestrate deployment workflows.
/// </summary>
[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class ConsultantDeploymentIntegrationTests : IDisposable
{
    private readonly TestConsole _console;
    private readonly TestEnvironment _environment;
    private readonly TemporaryDirectory _workspaceDir;
    private readonly MockLlmProvider _llmProvider;
    private readonly MockAgentCoordinator _agentCoordinator;

    public ConsultantDeploymentIntegrationTests()
    {
        _console = new TestConsole();
        _workspaceDir = new TemporaryDirectory();
        _environment = new TestEnvironment(_workspaceDir.Path);
        _llmProvider = new MockLlmProvider();
        _agentCoordinator = new MockAgentCoordinator();
    }

    public void Dispose()
    {
        _workspaceDir?.Dispose();
    }

    [Fact]
    public async Task Consultant_ShouldGenerateDeploymentPlan_WhenAskedToDeployHonua()
    {
        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();

        _llmProvider.ResponseOverride = @"{
            ""executiveSummary"": ""Deploy HonuaIO to AWS us-east-1 production environment"",
            ""confidence"": ""high"",
            ""reinforcedObservations"": [
                ""Production deployment requires proper infrastructure setup"",
                ""AWS us-east-1 region selected for deployment""
            ],
            ""plan"": [
                {
                    ""title"": ""Deploy HonuaIO infrastructure"",
                    ""skill"": ""DeploymentSkill"",
                    ""action"": ""DeployToAWS"",
                    ""category"": ""deployment"",
                    ""rationale"": ""Setup production environment in AWS"",
                    ""successCriteria"": ""Services running in us-east-1"",
                    ""risk"": ""medium"",
                    ""dependencies"": [],
                    ""inputs"": { ""region"": ""us-east-1"", ""environment"": ""production"" }
                }
            ]
        }";

        var planner = new SemanticConsultantPlanner(_llmProvider, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "Deploy HonuaIO to AWS us-east-1 for production",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: false,
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y"); // Approve plan

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        plan.Steps.Should().Contain(s =>
            s.Skill.Contains("Deploy", StringComparison.OrdinalIgnoreCase) ||
            s.Action.Contains("Deploy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Consultant_ShouldGenerateIAMPermissions_WhenAskedForDeploymentCredentials()
    {
        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();

        _llmProvider.ResponseOverride = @"{
            ""executiveSummary"": ""Generate least-privilege IAM permissions for HonuaIO deployment"",
            ""confidence"": ""high"",
            ""reinforcedObservations"": [
                ""Need IAM permissions for AWS deployment"",
                ""Least-privilege principle should be applied""
            ],
            ""plan"": [
                {
                    ""title"": ""Generate IAM policy for HonuaIO"",
                    ""skill"": ""SecuritySkill"",
                    ""action"": ""GenerateIAMPolicy"",
                    ""category"": ""security"",
                    ""rationale"": ""Create least-privilege IAM permissions"",
                    ""successCriteria"": ""IAM policy generated with minimal required permissions"",
                    ""risk"": ""low"",
                    ""dependencies"": [],
                    ""inputs"": { ""service"": ""HonuaIO"", ""provider"": ""AWS"" }
                }
            ]
        }";

        var planner = new SemanticConsultantPlanner(_llmProvider, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "Generate least-privilege IAM permissions for deploying HonuaIO to AWS",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: false,
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y"); // Approve

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        plan.Steps.Should().Contain(s =>
            s.Action.Contains("IAM", StringComparison.OrdinalIgnoreCase) ||
            (s.Rationale != null && s.Rationale.Contains("permission", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Consultant_ShouldValidateTopology_BeforeDeployment()
    {
        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();

        _llmProvider.ResponseOverride = @"{
            ""executiveSummary"": ""Validate HonuaIO deployment topology for correctness"",
            ""confidence"": ""high"",
            ""reinforcedObservations"": [
                ""Topology validation prevents deployment failures"",
                ""Check configuration before executing deployment""
            ],
            ""plan"": [
                {
                    ""title"": ""Validate deployment topology"",
                    ""skill"": ""ValidationSkill"",
                    ""action"": ""ValidateTopology"",
                    ""category"": ""validation"",
                    ""rationale"": ""Verify topology configuration is correct before deployment"",
                    ""successCriteria"": ""All topology checks pass"",
                    ""risk"": ""low"",
                    ""dependencies"": [],
                    ""inputs"": { ""topologyFile"": ""topology.json"" }
                }
            ]
        }";

        var planner = new SemanticConsultantPlanner(_llmProvider, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "Validate my HonuaIO deployment topology before executing",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: false,
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        plan.Steps.Should().Contain(s =>
            s.Action.Contains("validat", StringComparison.OrdinalIgnoreCase) ||
            (s.Rationale != null && s.Rationale.Contains("verify", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Consultant_ShouldRecommendHA_ForProductionDeployments()
    {
        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        _llmProvider.ResponseOverride = @"{
            ""executiveSummary"": ""Deploy production HonuaIO with high availability"",
            ""confidence"": ""high"",
            ""reinforcedObservations"": [
                ""Production environment requires high availability"",
                ""Multiple instances recommended for fault tolerance""
            ],
            ""plan"": [
                {
                    ""title"": ""Configure high availability database"",
                    ""skill"": ""DeploymentSkill"",
                    ""action"": ""ConfigureHADatabase"",
                    ""category"": ""deployment"",
                    ""rationale"": ""Production requires fault tolerance"",
                    ""successCriteria"": ""Database failover enabled"",
                    ""risk"": ""low"",
                    ""dependencies"": [],
                    ""inputs"": { ""environment"": ""prod"", ""instances"": 3 }
                }
            ]
        }";

        var planner = new SemanticConsultantPlanner(_llmProvider, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "Deploy HonuaIO to production",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: false,
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        plan.ExecutiveSummary.Should().Contain("high availability", "Consultant should recommend HA for production");
        plan.Steps.Should().Contain(s => s.Action.Contains("HA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Consultant_ShouldSuggestCostOptimization_ForDevDeployments()
    {
        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        _llmProvider.ResponseOverride = @"{
            ""executiveSummary"": ""Deploy cost-optimized development environment"",
            ""confidence"": ""high"",
            ""reinforcedObservations"": [
                ""Development environment - use smaller instances"",
                ""Single instance sufficient for dev workloads""
            ],
            ""plan"": [
                {
                    ""title"": ""Deploy with minimal resources"",
                    ""skill"": ""DeploymentSkill"",
                    ""action"": ""DeployMinimal"",
                    ""category"": ""deployment"",
                    ""rationale"": ""Cost optimization for development"",
                    ""successCriteria"": ""Environment running on t3.micro"",
                    ""risk"": ""low"",
                    ""dependencies"": [],
                    ""inputs"": { ""environment"": ""dev"", ""instanceType"": ""t3.micro"" }
                }
            ]
        }";

        var planner = new SemanticConsultantPlanner(_llmProvider, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "Deploy HonuaIO for development testing",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: false,
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        plan.ExecutiveSummary.Should().Contain("cost", "Consultant should mention cost optimization for dev");
    }

    [Fact]
    public async Task Consultant_WithMultiAgentMode_ShouldOrchestrateDeployment()
    {
        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();
        var planner = new MockPlanner(); // Won't be called in multi-agent mode

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "Deploy HonuaIO using multi-agent coordination",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: false,
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.MultiAgent);

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _console.Output.Should().Contain("Using multi-agent consultation mode");
        _agentCoordinator.ProcessRequestCalled.Should().BeTrue("Agent coordinator should be invoked");
    }

    // Helper classes
    private sealed class MockConsultantExecutor : IConsultantExecutor
    {
        public ConsultantPlan? LastPlan { get; private set; }

        public Task<ExecutionResult> ExecuteAsync(ConsultantPlan plan, CancellationToken cancellationToken)
        {
            LastPlan = plan;

            var results = plan.Steps
                .Select((step, index) => new StepExecutionResult(
                    StepIndex: index + 1,
                    Skill: step.Skill,
                    Action: step.Action,
                    Success: true,
                    Output: "Mock execution successful",
                    Error: null))
                .ToList();

            return Task.FromResult(new ExecutionResult(true, "Mock execution completed", results));
        }
    }

    private sealed class MockAgentCoordinator : IAgentCoordinator
    {
        public bool ProcessRequestCalled { get; private set; }

        public Task<AgentCoordinatorResult> ProcessRequestAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
        {
            ProcessRequestCalled = true;

            return Task.FromResult(new AgentCoordinatorResult
            {
                Success = true,
                Response = "Mock agent deployment completed",
                AgentsInvolved = new System.Collections.Generic.List<string> { "DeploymentAgent", "IAMGeneratorAgent" },
                Steps = new System.Collections.Generic.List<AgentStepResult>()
            });
        }

        public Task<AgentInteractionHistory> GetHistoryAsync()
        {
            return Task.FromResult(new AgentInteractionHistory
            {
                SessionId = Guid.NewGuid().ToString(),
                Interactions = new System.Collections.Generic.List<AgentInteraction>
                {
                    new()
                    {
                        UserRequest = "Deploy HonuaIO",
                        Response = "Deployment complete",
                        Success = true,
                        Timestamp = DateTime.UtcNow,
                        AgentsUsed = new System.Collections.Generic.List<string> { "DeploymentAgent" }
                    }
                }
            });
        }
    }

    private sealed class MockLlmProvider : ILlmProvider
    {
        public string? ResponseOverride { get; set; }

        public string ProviderName => "mock";
        public string DefaultModel => "mock-model";

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<System.Collections.Generic.IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<string>>(new[] { "mock-model" });

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            var content = ResponseOverride ?? @"{
                ""executiveSummary"": ""Deploy HonuaIO infrastructure"",
                ""confidence"": ""high"",
                ""reinforcedObservations"": [],
                ""plan"": [
                    {
                        ""title"": ""Deploy to cloud"",
                        ""skill"": ""DeploymentSkill"",
                        ""action"": ""Deploy"",
                        ""category"": ""deployment"",
                        ""rationale"": ""Setup infrastructure"",
                        ""successCriteria"": ""Services running"",
                        ""risk"": ""medium"",
                        ""dependencies"": [],
                        ""inputs"": {}
                    }
                ]
            }";

            return Task.FromResult(new LlmResponse
            {
                Content = content,
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

    private sealed class MockPatternStore : Honua.Cli.AI.Services.VectorSearch.IDeploymentPatternKnowledgeStore
    {
        public Task IndexApprovedPatternAsync(Honua.Cli.AI.Services.VectorSearch.DeploymentPattern pattern, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<System.Collections.Generic.List<Honua.Cli.AI.Services.VectorSearch.PatternSearchResult>> SearchPatternsAsync(
            Honua.Cli.AI.Services.VectorSearch.DeploymentRequirements requirements,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new System.Collections.Generic.List<Honua.Cli.AI.Services.VectorSearch.PatternSearchResult>());
        }
    }

    private sealed class MockPlanner : IConsultantPlanner
    {
        public Task<ConsultantPlan> CreatePlanAsync(ConsultantPlanningContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Planner should not be called in multi-agent mode");
        }
    }
}

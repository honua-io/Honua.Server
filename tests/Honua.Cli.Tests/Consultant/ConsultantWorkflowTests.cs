using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Cli.Services.Consultant;
using Honua.Cli.Services.Metadata;
using Honua.Cli.Tests.Support;
using Spectre.Console.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Xunit;

namespace Honua.Cli.Tests.Consultant;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class ConsultantWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldLogPlanAndRespectApproval_WhenConfirmed()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var console = new TestConsole();
        console.Input.PushTextWithEnter("y");

        var environment = new TestEnvironment(configDir.Path);
        var clock = new TestClock(new DateTimeOffset(2025, 9, 21, 12, 0, 0, TimeSpan.Zero));
        var workflow = CreateWorkflow(console, environment, clock);

        var request = new ConsultantRequest(
            Prompt: "connect my PostGIS datasource",
            DryRun: false,
            AutoApprove: false,
            SuppressLogging: false,
            WorkspacePath: workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Approved.Should().BeTrue();
        result.Executed.Should().BeTrue();

        var logPath = Path.Combine(environment.LogsRoot, "consultant-20250921.md");
        File.Exists(logPath).Should().BeTrue();
        var logContent = await File.ReadAllTextAsync(logPath);
        logContent.Should().Contain("Approved: true");
        logContent.Should().Contain("DataSourceSkill.ConnectPostgis");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnPlanDeclined_WhenOperatorRejects()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var console = new TestConsole();
        console.Input.PushTextWithEnter("n");

        var environment = new TestEnvironment(configDir.Path);
        var clock = new TestClock(new DateTimeOffset(2025, 9, 21, 12, 0, 0, TimeSpan.Zero));
        var workflow = CreateWorkflow(console, environment, clock);

        var request = new ConsultantRequest(
            Prompt: "connect my PostGIS datasource",
            DryRun: false,
            AutoApprove: false,
            SuppressLogging: false,
            WorkspacePath: workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Approved.Should().BeFalse();
        console.Output.Should().Contain("Plan approval declined");

        var logPath = Path.Combine(environment.LogsRoot, "consultant-20250921.md");
        File.Exists(logPath).Should().BeTrue();
        var logContent = await File.ReadAllTextAsync(logPath);
        logContent.Should().Contain("Approved: false");
    }

    [Fact]
    public async Task ExecuteAsync_WithSemanticPlanner_ShouldCompleteEndToEnd()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var console = new TestConsole();
        console.Input.PushTextWithEnter("y");

        var environment = new TestEnvironment(configDir.Path);
        var clock = new TestClock(new DateTimeOffset(2025, 9, 22, 8, 30, 0, TimeSpan.Zero));

        var llm = new SemanticStubLlmProvider();
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new SemanticStubPatternStore();
        var planner = new SemanticConsultantPlanner(llm, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);

        var contextBuilder = new ColorSafeContextBuilder();
        var formatter = new TableConsultantPlanFormatter(console);
        var logWriter = new SessionLogWriter(environment, clock);
        var executor = new CapturingConsultantExecutor();

        var request = new ConsultantRequest(
            Prompt: "Design a resilient Honua deployment",
            DryRun: false,
            AutoApprove: false,
            SuppressLogging: true,
            WorkspacePath: workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        var previewContext = await contextBuilder.BuildAsync(request, CancellationToken.None);
        previewContext.Observations.Select(o => o.Severity)
            .Should()
            .OnlyContain(severity => severity == "red" || severity == "yellow" || severity == "silver");

        var workflow = new ConsultantWorkflow(console, environment, contextBuilder, planner, formatter, logWriter, executor);

        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Executed.Should().BeTrue();
        llm.LastRequest.Should().NotBeNull();
        llm.LastRequest!.UserPrompt.Should().Contain("Response Format");

        executor.LastPlan.Should().NotBeNull();
        executor.LastPlan!.ExecutiveSummary.Should().Be("Deploy stub plan");
        executor.LastPlan.Steps.Should().Contain(step => step.Skill == "InfraSkill" && step.Action == "Provision");

        console.Output.Should().Contain("InfraSkill");
        console.Output.Should().Contain("Provision");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEscapeObservationMarkup()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var console = new TestConsole();

        var environment = new TestEnvironment(configDir.Path);
        var clock = new TestClock(new DateTimeOffset(2025, 11, 3, 14, 0, 0, TimeSpan.Zero));

        var planner = new BootstrapConsultantPlanner(clock);
        var contextBuilder = new EscapingContextBuilder();
        var formatter = new TableConsultantPlanFormatter(console);
        var logWriter = new SessionLogWriter(environment, clock);
        var executor = new StubConsultantExecutor();

        var workflow = new ConsultantWorkflow(console, environment, contextBuilder, planner, formatter, logWriter, executor);

        var request = new ConsultantRequest(
            Prompt: "Check markup",
            DryRun: true,
            AutoApprove: false,
            SuppressLogging: true,
            WorkspacePath: workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        console.Output.Should().Contain("[alert]Check metrics[/]");
        console.Output.Should().Contain("[severity]Validate backups[/]");
    }

    [Fact]
    public async Task ExecuteAsync_AutoMode_ShouldFallbackWhenCoordinatorFails()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var console = new TestConsole();
        console.Input.PushTextWithEnter("y");

        var environment = new TestEnvironment(configDir.Path);
        var clock = new TestClock(new DateTimeOffset(2025, 10, 1, 9, 0, 0, TimeSpan.Zero));

        var llm = new SemanticStubLlmProvider();
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new SemanticStubPatternStore();
        var planner = new SemanticConsultantPlanner(llm, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);

        var contextBuilder = new ColorSafeContextBuilder();
        var formatter = new TableConsultantPlanFormatter(console);
        var logWriter = new SessionLogWriter(environment, clock);
        var executor = new CapturingConsultantExecutor();
        var coordinator = new AlwaysFailsAgentCoordinator();

        var critics = new IAgentCritic[] { new PlanSafetyCritic() };

        var workflow = new ConsultantWorkflow(console, environment, contextBuilder, planner, formatter, logWriter, executor, coordinator, critics);

        var request = new ConsultantRequest(
            Prompt: "Deploy automatically",
            DryRun: false,
            AutoApprove: false,
            SuppressLogging: false,
            WorkspacePath: workspaceDir.Path,
            Mode: ConsultantExecutionMode.Auto);

        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        console.Output.Should().Contain("Falling back to plan-based consultant workflow");
        executor.LastPlan.Should().NotBeNull();
        coordinator.Requests.Should().Contain(request.Prompt);
        coordinator.LastContext.Should().NotBeNull();

        console.Output.Should().Contain("Critic warnings");
    }

    [Fact]
    public async Task ExecuteAsync_MultiAgentMode_ShouldReturnCoordinatorPlan()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var console = new TestConsole();

        var environment = new TestEnvironment(configDir.Path);
        var clock = new TestClock(new DateTimeOffset(2025, 10, 2, 11, 0, 0, TimeSpan.Zero));

        var contextBuilder = new ColorSafeContextBuilder();
        var formatter = new TableConsultantPlanFormatter(console);
        var logWriter = new SessionLogWriter(environment, clock);
        var executor = new CapturingConsultantExecutor();
        var coordinator = new AlwaysSucceedsAgentCoordinator();

        // Planner won't be invoked; stub to throw if it is
        var planner = new ThrowingPlanner();

        var critics = new IAgentCritic[] { new PlanSafetyCritic() };

        var workflow = new ConsultantWorkflow(console, environment, contextBuilder, planner, formatter, logWriter, executor, coordinator, critics);

        var request = new ConsultantRequest(
            Prompt: "Run multi-agent deployment",
            DryRun: false,
            AutoApprove: false,
            SuppressLogging: false,
            Verbose: true,
            WorkspacePath: workspaceDir.Path,
            Mode: ConsultantExecutionMode.MultiAgent);

        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Executed.Should().BeTrue();
        console.Output.Should().Contain("Using multi-agent consultation mode");
        console.Output.Should().Contain("Automation completed");
        console.Output.Should().Contain("Suggested next steps");
        console.Output.Should().Contain("Agent steps");
        executor.LastPlan.Should().BeNull();

        coordinator.Requests.Should().Contain(request.Prompt);
        coordinator.LastContext.Should().NotBeNull();

        var history = await coordinator.GetHistoryAsync();
        history.Interactions.Should().ContainSingle();
        history.Interactions[0].Success.Should().BeTrue();

        var logPath = Path.Combine(environment.LogsRoot, "consultant-20251002.md");
        File.Exists(logPath).Should().BeTrue();
        var logContent = await File.ReadAllTextAsync(logPath);
        logContent.Should().Contain("HistorySession");
        logContent.Should().Contain("automation");

        var transcriptDirectoryFiles = Directory.GetFiles(environment.LogsRoot, "consultant-*-multi-*.json");
        transcriptDirectoryFiles.Should().NotBeEmpty();
        var transcriptContent = await File.ReadAllTextAsync(transcriptDirectoryFiles[0]);
        transcriptContent.Should().Contain("automation");
        transcriptContent.Should().Contain("\"steps\"");
    }

    private static ConsultantWorkflow CreateWorkflow(TestConsole console, TestEnvironment environment, TestClock clock)
    {
        var planner = new BootstrapConsultantPlanner(clock);
        var contextBuilder = new StubConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(console);
        var logWriter = new SessionLogWriter(environment, clock);
        var executor = new StubConsultantExecutor();

        var critics = new IAgentCritic[] { new PlanSafetyCritic() };

        return new ConsultantWorkflow(console, environment, contextBuilder, planner, formatter, logWriter, executor, agentCritics: critics);
    }

    private sealed class StubConsultantExecutor : IConsultantExecutor
    {
        public Task<ExecutionResult> ExecuteAsync(ConsultantPlan plan, CancellationToken cancellationToken)
        {
            var results = plan.Steps
                .Select((step, index) => new StepExecutionResult(
                    StepIndex: index + 1,
                    Skill: step.Skill,
                    Action: step.Action,
                    Success: true,
                    Output: "stub",
                    Error: null))
                .ToList();

            return Task.FromResult(new ExecutionResult(true, "Stub executor completed plan", results));
        }
    }

    private sealed class CapturingConsultantExecutor : IConsultantExecutor
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
                    Output: "stub",
                    Error: null))
                .ToList();

            return Task.FromResult(new ExecutionResult(true, "Captured plan", results));
        }
    }

    private sealed class StubConsultantContextBuilder : IConsultantContextBuilder
    {
        public Task<ConsultantPlanningContext> BuildAsync(ConsultantRequest request, CancellationToken cancellationToken)
        {
            var workspace = new WorkspaceProfile(
                request.WorkspacePath,
                MetadataDetected: false,
                Metadata: null,
                Infrastructure: new InfrastructureInventory(
                    HasDockerCompose: false,
                    HasKubernetesManifests: false,
                    HasTerraform: false,
                    HasHelmCharts: false,
                    HasCiPipelines: false,
                    HasMonitoringConfig: false,
                    DeploymentArtifacts: Array.Empty<string>(),
                    PotentialCloudProviders: Array.Empty<string>()),
                Tags: Array.Empty<string>());

            var context = new ConsultantPlanningContext(
                request,
                workspace,
                Array.Empty<ConsultantObservation>(),
                DateTimeOffset.UtcNow);

            return Task.FromResult(context);
        }
    }

    private sealed class EscapingContextBuilder : IConsultantContextBuilder
    {
        public Task<ConsultantPlanningContext> BuildAsync(ConsultantRequest request, CancellationToken cancellationToken)
        {
            var workspace = new WorkspaceProfile(
                request.WorkspacePath,
                MetadataDetected: false,
                Metadata: null,
                Infrastructure: new InfrastructureInventory(
                    HasDockerCompose: false,
                    HasKubernetesManifests: false,
                    HasTerraform: false,
                    HasHelmCharts: false,
                    HasCiPipelines: false,
                    HasMonitoringConfig: false,
                    DeploymentArtifacts: Array.Empty<string>(),
                    PotentialCloudProviders: Array.Empty<string>()),
                Tags: Array.Empty<string>());

            var observations = new[]
            {
                new ConsultantObservation("obs-1", "high", "[alert]Check metrics[/]", "", "Investigate"),
                new ConsultantObservation("obs-2", "medium", "[severity]Validate backups[/]", "", "Verify snapshots")
            };

            var context = new ConsultantPlanningContext(
                request,
                workspace,
                observations,
                DateTimeOffset.UtcNow);

            return Task.FromResult(context);
        }
    }

    private sealed class SemanticStubLlmProvider : ILlmProvider
    {
        private const string ResponseJson = """
{
  "executiveSummary": "Deploy stub plan",
  "confidence": "high",
  "reinforcedObservations": [],
  "plan": [
    {
      "title": "Establish guardrails",
      "skill": "SafetySkill",
      "action": "SnapshotWorkspace",
      "category": "safety",
      "rationale": "Protect state before changes",
      "successCriteria": "Snapshot created",
      "risk": "low",
      "dependencies": [],
      "inputs": {}
    },
    {
      "title": "Provision infrastructure",
      "skill": "InfraSkill",
      "action": "Provision",
      "category": "deployment",
      "rationale": "Stand up core platform",
      "successCriteria": "Control plane reachable",
      "risk": "medium",
      "dependencies": ["Establish guardrails"],
      "inputs": { "blueprint": "azure-resilient" }
    }
  ]
}
""";

        public LlmRequest? LastRequest { get; private set; }

        public string ProviderName => "stub";

        public string DefaultModel => "stub-model";

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(new[] { DefaultModel });

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new LlmResponse
            {
                Content = ResponseJson,
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

    private sealed class SemanticStubPatternStore : IDeploymentPatternKnowledgeStore
    {
        public Task IndexApprovedPatternAsync(DeploymentPattern pattern, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<List<PatternSearchResult>> SearchPatternsAsync(DeploymentRequirements requirements, CancellationToken cancellationToken = default)
        {
            var result = new PatternSearchResult
            {
                Id = "azure-resilient",
                PatternName = "Azure Resilient",
                CloudProvider = "azure",
                SuccessRate = 0.94,
                DeploymentCount = 18,
                Content = "Blueprint for resilient Azure deployment",
                ConfigurationJson = "{\"blueprint\":\"azure-resilient\"}",
                Score = 0.88
            };

            return Task.FromResult(new List<PatternSearchResult> { result });
        }
    }

    private sealed class ColorSafeContextBuilder : IConsultantContextBuilder
    {
        private readonly ConsultantContextBuilder _inner = new();

        public async Task<ConsultantPlanningContext> BuildAsync(ConsultantRequest request, CancellationToken cancellationToken)
        {
            var context = await _inner.BuildAsync(request, cancellationToken).ConfigureAwait(false);

            if (context.Observations.Count == 0)
            {
                return context;
            }

            var remapped = context.Observations
                .Select(o => new ConsultantObservation(
                    o.Id,
                    MapSeverity(o.Severity),
                    o.Summary,
                    o.Detail,
                    o.Recommendation))
                .ToArray();

            return context with { Observations = remapped };
        }

        private static string MapSeverity(string severity)
        {
            return severity?.ToLowerInvariant() switch
            {
                "high" => "red",
                "medium" => "yellow",
                "low" => "silver",
                _ => "silver"
            };
        }
    }

    private sealed class ThrowingPlanner : IConsultantPlanner
    {
        public Task<ConsultantPlan> CreatePlanAsync(ConsultantPlanningContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Planner should not be called in multi-agent mode when coordinator succeeds.");
        }
    }
}

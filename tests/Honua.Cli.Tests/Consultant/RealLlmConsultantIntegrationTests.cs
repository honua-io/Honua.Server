using System;
using System.IO;
using System.Linq;
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
/// Real-world consultant integration tests using actual LLM (OpenAI/Claude).
///
/// WARNING: These tests make real API calls and will incur costs!
/// - OpenAI API costs: ~$0.002-0.02 per test (depending on model)
/// - Anthropic API costs: ~$0.001-0.015 per test
///
/// These tests only run when USE_REAL_LLM=true environment variable is set.
/// For cost-free testing, use OllamaConsultantIntegrationTests instead.
///
/// They validate that the consultant can handle complex, realistic scenarios.
/// </summary>
[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class RealLlmConsultantIntegrationTests : IDisposable
{
    private readonly TestConsole _console;
    private readonly TestEnvironment _environment;
    private readonly TemporaryDirectory _workspaceDir;
    private readonly ILlmProvider? _llmProvider;
    private readonly MockAgentCoordinator _agentCoordinator;
    private readonly bool _shouldSkip;

    public RealLlmConsultantIntegrationTests()
    {
        _console = new TestConsole();
        _workspaceDir = new TemporaryDirectory();
        _environment = new TestEnvironment(_workspaceDir.Path);
        _llmProvider = TestConfiguration.CreateLlmProvider();
        _agentCoordinator = new MockAgentCoordinator();

        // Skip tests if USE_REAL_LLM is not set or if API key is not available
        var useRealLlm = Environment.GetEnvironmentVariable("USE_REAL_LLM");
        _shouldSkip = !string.Equals(useRealLlm, "true", StringComparison.OrdinalIgnoreCase) ||
                      !TestConfiguration.HasRealLlmProvider;
    }

    public void Dispose()
    {
        _workspaceDir?.Dispose();
    }

    #region Deployment Scenarios

    [Fact]
    public async Task RealLlm_ShouldHandleComplexDeploymentRequest()
    {
        if (_shouldSkip)
        {
            // Skip silently - test only runs with real LLM
            return;
        }

        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        var planner = new SemanticConsultantPlanner(_llmProvider!, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "I need to deploy HonuaIO to AWS us-west-2 for a production environment. " +
                   "It needs to handle 1000 concurrent users with high availability. " +
                   "We have a PostGIS database with property boundaries and need caching for performance.",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: true, // Suppress to avoid Spectre.Console markup issues with real LLM
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue("Real LLM should successfully generate a deployment plan");
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        plan.ExecutiveSummary.Should().NotBeNullOrEmpty("Plan should have an executive summary");
        plan.Steps.Should().NotBeEmpty("Plan should contain deployment steps");

        // Verify the plan addresses key requirements
        var allStepText = string.Join(" ", plan.Steps.Select(s => $"{s.Skill} {s.Action} {s.Description} {s.Rationale}"));
        allStepText.Should().ContainAny(new[] { "deploy", "deployment", "infrastructure" },
            "Plan should mention deployment");
        allStepText.Should().ContainAny(new[] { "postgis", "database", "postgres" },
            "Plan should address database requirements");
    }

    [Fact]
    public async Task RealLlm_ShouldGenerateRealisticIAMPermissions()
    {
        if (_shouldSkip) return;

        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        var planner = new SemanticConsultantPlanner(_llmProvider!, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "Generate least-privilege IAM permissions for deploying HonuaIO to AWS. " +
                   "We need permissions for ECS, RDS PostgreSQL with PostGIS, S3 for raster storage, " +
                   "and CloudWatch for monitoring.",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: true, // Suppress to avoid Spectre.Console markup issues
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        var allText = string.Join(" ", plan.Steps.Select(s => $"{s.Action} {s.Description} {s.Rationale}"));

        // Verify IAM-related content
        allText.Should().ContainAny(new[] { "iam", "permission", "policy", "role", "access" },
            "Plan should address IAM permissions");
        allText.Should().ContainAny(new[] { "least", "privilege", "minimal", "principle" },
            "Plan should mention least-privilege security");
    }

    #endregion

    #region Performance Optimization Scenarios

    [Fact]
    public async Task RealLlm_ShouldProvidePerformanceOptimizationPlan()
    {
        if (_shouldSkip) return;

        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        var planner = new SemanticConsultantPlanner(_llmProvider!, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "My property boundaries layer is taking 5 seconds to render. " +
                   "It has 500,000 complex polygons. How can I optimize performance?",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: true, // Suppress to avoid Spectre.Console markup issues
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        var allText = string.Join(" ", plan.Steps.Select(s => $"{s.Action} {s.Description} {s.Rationale}"));

        // Verify performance optimization strategies
        allText.Should().ContainAny(new[] { "index", "cache", "optimize", "performance", "spatial" },
            "Plan should include performance optimization strategies");
    }

    [Fact]
    public async Task RealLlm_ShouldSuggestCachingForHighTraffic()
    {
        if (_shouldSkip) return;

        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        var planner = new SemanticConsultantPlanner(_llmProvider!, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "We're expecting 10,000 requests per minute to our basemap tiles. " +
                   "What caching strategy should we use?",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: true, // Suppress to avoid Spectre.Console markup issues
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        var allText = string.Join(" ", plan.Steps.Select(s => $"{s.Action} {s.Description} {s.Rationale}"));

        allText.Should().ContainAny(new[] { "cache", "caching", "cdn", "redis", "tile" },
            "Plan should suggest caching strategies for high traffic");
    }

    #endregion

    #region Migration Scenarios

    [Fact]
    public async Task RealLlm_ShouldHandleArcGISMigrationRequest()
    {
        if (_shouldSkip) return;

        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        var planner = new SemanticConsultantPlanner(_llmProvider!, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "We want to migrate from ArcGIS Server to HonuaIO. " +
                   "We have 50 feature layers and 20 map services. " +
                   "What's the migration strategy?",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: true, // Suppress to avoid Spectre.Console markup issues
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        var allText = string.Join(" ", plan.Steps.Select(s => $"{s.Action} {s.Description} {s.Rationale}"));

        allText.Should().ContainAny(new[] { "migrat", "arcgis", "layer", "service", "import" },
            "Plan should address ArcGIS migration");
    }

    #endregion

    #region Troubleshooting Scenarios

    [Fact]
    public async Task RealLlm_ShouldDiagnosePerformanceProblem()
    {
        if (_shouldSkip) return;

        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        var planner = new SemanticConsultantPlanner(_llmProvider!, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "My WMS requests are timing out. The database CPU is at 95%. " +
                   "The logs show 'Seq Scan on parcels' in slow queries. What's wrong?",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: true, // Suppress to avoid Spectre.Console markup issues
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        var allText = string.Join(" ", plan.Steps.Select(s => $"{s.Action} {s.Description} {s.Rationale}"));

        // Should diagnose missing index issue
        allText.Should().ContainAny(new[] { "index", "spatial", "gist", "sequential scan", "seq scan" },
            "Plan should identify missing index as the root cause");
    }

    [Fact]
    public async Task RealLlm_ShouldHandleSecurityConfigurationRequest()
    {
        if (_shouldSkip) return;

        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        var planner = new SemanticConsultantPlanner(_llmProvider!, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "Configure security for production deployment. " +
                   "We need HTTPS, CORS for trusted domains, API key authentication, " +
                   "and rate limiting to prevent abuse.",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: true, // Suppress to avoid Spectre.Console markup issues
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        var allText = string.Join(" ", plan.Steps.Select(s => $"{s.Action} {s.Description} {s.Rationale}"));

        allText.Should().ContainAny(new[] { "https", "tls", "ssl", "security", "cors", "auth", "rate" },
            "Plan should address security configuration requirements");
    }

    #endregion

    #region Data Management Scenarios

    [Fact]
    public async Task RealLlm_ShouldHandleDataImportRequest()
    {
        if (_shouldSkip) return;

        // Arrange
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var kernel = Kernel.CreateBuilder().Build();
        var patternStore = new MockPatternStore();
        var planner = new SemanticConsultantPlanner(_llmProvider!, clock, kernel, patternStore, NullLogger<SemanticConsultantPlanner>.Instance);
        var contextBuilder = new ConsultantContextBuilder();
        var formatter = new TableConsultantPlanFormatter(_console);
        var logWriter = new SessionLogWriter(_environment, clock);
        var executor = new MockConsultantExecutor();

        var workflow = new ConsultantWorkflow(_console, _environment, contextBuilder, planner, formatter, logWriter, executor, _agentCoordinator);

        var request = new ConsultantRequest(
            Prompt: "Import a 2GB Shapefile with city boundaries into PostGIS. " +
                   "It has 10,000 features with complex multipolygons. " +
                   "Set up proper indexing and optimize for web rendering.",
            DryRun: false,
            AutoApprove: true,
            SuppressLogging: true, // Suppress to avoid Spectre.Console markup issues
            WorkspacePath: _workspaceDir.Path,
            Mode: ConsultantExecutionMode.Plan);

        _console.Input.PushTextWithEnter("y");

        // Act
        var result = await workflow.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        executor.LastPlan.Should().NotBeNull();

        var plan = executor.LastPlan!;
        var allText = string.Join(" ", plan.Steps.Select(s => $"{s.Action} {s.Description} {s.Rationale}"));

        allText.Should().ContainAny(new[] { "import", "shapefile", "postgis", "index", "spatial" },
            "Plan should cover data import and optimization");
    }

    #endregion

    #region Helper Classes

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
        public Task<AgentCoordinatorResult> ProcessRequestAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentCoordinatorResult
            {
                Success = true,
                Response = "Mock agent response",
                AgentsInvolved = new System.Collections.Generic.List<string>(),
                Steps = new System.Collections.Generic.List<AgentStepResult>()
            });
        }

        public Task<AgentInteractionHistory> GetHistoryAsync()
        {
            return Task.FromResult(new AgentInteractionHistory
            {
                SessionId = Guid.NewGuid().ToString(),
                Interactions = new System.Collections.Generic.List<AgentInteraction>()
            });
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

    #endregion
}

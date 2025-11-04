using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Cli.Services;
using Honua.Cli.Services.Consultant;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Xunit;

namespace Honua.Cli.Tests.Consultant;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class SemanticConsultantPlannerTests
{
    [Fact]
    public async Task CreatePlanAsync_ShouldIncludeDeploymentPatternsInPrompt()
    {
        const string jsonResponse = "{\"executiveSummary\":\"Use stub plan\",\"confidence\":\"medium\",\"reinforcedObservations\":[{\"id\":\"obs1\",\"severity\":\"high\",\"recommendation\":\"Act\"}],\"plan\":[{\"title\":\"Stub\",\"skill\":\"Testing\",\"action\":\"Execute\",\"category\":\"discovery\",\"rationale\":\"Stub\",\"successCriteria\":\"Done\",\"risk\":\"low\",\"dependencies\":[\"Prep\"],\"inputs\":{}}]}";
        var llm = new StubLlmProvider(jsonResponse);
        var clock = new TestClock(new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero));
        var kernel = Kernel.CreateBuilder().Build();
        var knowledgeStore = new StubPatternStore();

        var planner = new SemanticConsultantPlanner(
            llm,
            clock,
            kernel,
            knowledgeStore,
            NullLogger<SemanticConsultantPlanner>.Instance);

        var request = new ConsultantRequest(
            Prompt: "Stand up production",
            DryRun: true,
            AutoApprove: false,
            SuppressLogging: true,
            WorkspacePath: "/workspace",
            Mode: ConsultantExecutionMode.Plan);

        var metadata = new MetadataProfile(
            Services: new[]
            {
                new ServiceProfile("svc", "ogc", Enabled: true, DataSourceId: "db", LayerIds: new[] { "layer" }, Protocols: new[] { "ogcapi-features" })
            },
            DataSources: new[] { new DataSourceProfile("db", "aurora", true, new[] { "svc" }) },
            RasterDatasets: Array.Empty<RasterDatasetProfile>());

        var workspace = new WorkspaceProfile(
            RootPath: "/workspace",
            MetadataDetected: true,
            Metadata: metadata,
            Infrastructure: new InfrastructureInventory(
                HasDockerCompose: true,
                HasKubernetesManifests: false,
                HasTerraform: true,
                HasHelmCharts: false,
                HasCiPipelines: true,
                HasMonitoringConfig: true,
                DeploymentArtifacts: new[] { "terraform" },
                PotentialCloudProviders: new[] { "aws" }),
            Tags: new[] { "aws", "prod" });

        var context = new ConsultantPlanningContext(
            request,
            workspace,
            Array.Empty<ConsultantObservation>(),
            clock.UtcNow);

        var plan = await planner.CreatePlanAsync(context, CancellationToken.None);

        llm.LastRequest.Should().NotBeNull();
        llm.LastRequest!.UserPrompt.Should().Contain("Relevant Deployment Patterns");
        llm.LastRequest.UserPrompt.Should().Contain("AWS Zero-Downtime");

        plan.ExecutiveSummary.Should().Be("Use stub plan");
        plan.Confidence.Should().Be("medium");
        plan.HighlightedObservations.Should().ContainSingle(o => o.Id == "obs1" && o.Recommendation == "Act");
        plan.Steps.Should().HaveCount(1);
        plan.Steps[0].Dependencies.Should().ContainSingle("Prep");
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldFallbackToLegacyArray_WhenEnvelopeMissing()
    {
        const string legacyPayload = "[{\"skill\":\"Diagnostics\",\"action\":\"AnalyzeWorkspace\",\"inputs\":{\"path\":\"/workspace\"}}," +
                                     "{\"skill\":\"Deployment\",\"action\":\"ProvisionInfrastructure\",\"inputs\":{}}]";
        var llm = new StubLlmProvider(legacyPayload);
        var clock = new TestClock(new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero));
        var kernel = Kernel.CreateBuilder().Build();
        var knowledgeStore = new StubPatternStore();

        var planner = new SemanticConsultantPlanner(
            llm,
            clock,
            kernel,
            knowledgeStore,
            NullLogger<SemanticConsultantPlanner>.Instance);

        var request = new ConsultantRequest(
            Prompt: "Stand up production",
            DryRun: true,
            AutoApprove: false,
            SuppressLogging: true,
            WorkspacePath: "/workspace",
            Mode: ConsultantExecutionMode.Plan);

        var workspace = new WorkspaceProfile(
            RootPath: "/workspace",
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
            clock.UtcNow);

        var plan = await planner.CreatePlanAsync(context, CancellationToken.None);

        plan.ExecutiveSummary.Should().BeNull();
        plan.Steps.Should().HaveCount(2);
        plan.Steps[0].Skill.Should().Be("Diagnostics");
        plan.Steps[0].Inputs.Should().ContainKey("path");
        plan.Steps[1].Action.Should().Be("ProvisionInfrastructure");
    }

    private sealed class StubPatternStore : IDeploymentPatternKnowledgeStore
    {
        public Task IndexApprovedPatternAsync(DeploymentPattern pattern, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<List<PatternSearchResult>> SearchPatternsAsync(DeploymentRequirements requirements, CancellationToken cancellationToken = default)
        {
            var result = new PatternSearchResult
            {
                Id = "aws-zero-downtime",
                PatternName = "AWS Zero-Downtime",
                CloudProvider = "aws",
                SuccessRate = 0.97,
                DeploymentCount = 24,
                Content = "Highly available AWS architecture",
                ConfigurationJson = "{\"service\":\"aurora\"}",
                Score = 0.92
            };

            return Task.FromResult(new List<PatternSearchResult> { result });
        }
    }

    private sealed class StubLlmProvider : ILlmProvider
    {
        private readonly string _response;

        public StubLlmProvider(string response)
        {
            _response = response;
        }

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
                Content = _response,
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

    private sealed class TestClock : ISystemClock
    {
        public TestClock(DateTimeOffset now) => UtcNow = now;

        public DateTimeOffset UtcNow { get; }
    }
}

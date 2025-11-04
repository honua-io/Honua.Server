using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Services.Consultant;
using Honua.Cli.Tests.Support;
using Xunit;

namespace Honua.Cli.Tests.Consultant;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class BootstrapConsultantPlannerTests
{
    [Fact]
    public async Task CreatePlanAsync_ShouldIncludeDatasourceStep_WhenPromptMentionsPostgis()
    {
        var planner = new BootstrapConsultantPlanner(new TestClock(DateTimeOffset.UtcNow));
        var request = new ConsultantRequest("Help me connect PostGIS", DryRun: false, AutoApprove: false, SuppressLogging: false, WorkspacePath: "/workspaces/honua", Mode: ConsultantExecutionMode.Plan);

        var plan = await planner.CreatePlanAsync(CreateContext(request), CancellationToken.None);

        plan.Steps.Should().Contain(step => step.Skill == "DataSourceSkill" && step.Action == "ConnectPostgis");
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldSwitchToPlanMode_WhenDryRunRequested()
    {
        var planner = new BootstrapConsultantPlanner(new TestClock(DateTimeOffset.UtcNow));
        var request = new ConsultantRequest("Prepare deployment", DryRun: true, AutoApprove: false, SuppressLogging: false, WorkspacePath: "/workspaces/honua", Mode: ConsultantExecutionMode.Plan);

        var plan = await planner.CreatePlanAsync(CreateContext(request), CancellationToken.None);
    
        plan.Steps.Last().Action.Should().Be("SelectDeploymentBlueprint");
    }

    private static ConsultantPlanningContext CreateContext(ConsultantRequest request)
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

        return new ConsultantPlanningContext(
            request,
            workspace,
            Array.Empty<ConsultantObservation>(),
            DateTimeOffset.UtcNow);
    }
}

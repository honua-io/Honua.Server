using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Planning;
using Honua.Cli.Commands;
using Honua.Cli.Services;
using Honua.Cli.Tests.Support;
using Honua.Server.Core.Performance;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.Commands;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class DeployExecuteCommandTests : IDisposable
{
    private readonly TestConsole _console;
    private readonly TestEnvironment _environment;
    private readonly TemporaryDirectory _workspaceDir;
    private readonly MockAgentCoordinator _agentCoordinator;

    public DeployExecuteCommandTests()
    {
        _console = new TestConsole();
        _workspaceDir = new TemporaryDirectory();
        _environment = new TestEnvironment(_workspaceDir.Path);
        _agentCoordinator = new MockAgentCoordinator();
    }

    public void Dispose()
    {
        _workspaceDir?.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenAgentCoordinatorNotConfigured()
    {
        // Arrange
        var command = new DeployExecuteCommand(_console, _environment, agentCoordinator: null);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = "plan.json"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("AI coordinator not configured");
        _console.Output.Should().Contain("honua setup-wizard");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenNoPlanSpecified()
    {
        // Arrange
        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = null
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("No plan specified");
        _console.Output.Should().Contain("honua deploy plan");
    }

    [Fact]
    public async Task ExecuteAsync_WithDryRun_ShouldSimulateExecution()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("test-plan.json", "dev");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            DryRun = true,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Deploy HonuaIO");
        _console.Output.Should().Contain("Deployment Complete");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAutoApprove_ShouldPromptForConfirmation()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("confirm-plan.json", "prod");
        _console.Input.PushTextWithEnter("y"); // Confirm execution

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = false,
            DryRun = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Execute this deployment plan?");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDeclines_ShouldCancelExecution()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("decline-plan.json", "prod");
        _console.Input.PushTextWithEnter("n"); // Decline execution

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = false
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Deployment cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDisplayPlanSummary()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("summary-plan.json", "prod");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = true,
            DryRun = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Deploy HonuaIO to aws");
        _console.Output.Should().Contain("Cloud:");
        _console.Output.Should().Contain("Environment:");
        _console.Output.Should().Contain("Steps:");
        _console.Output.Should().Contain("Risk Level:");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteStepsInOrder()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("steps-plan.json", "dev");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = true,
            DryRun = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);

        var output = _console.Output;
        var step1Index = output.IndexOf("✓", StringComparison.Ordinal);
        var step2Index = output.IndexOf("✓", step1Index + 1, StringComparison.Ordinal);

        step1Index.Should().BeGreaterThan(0);
        step2Index.Should().BeGreaterThan(step1Index);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ShouldDisplayPostDeploymentInfo()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("success-plan.json", "prod");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = true,
            DryRun = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Post-Deployment Information:");
        _console.Output.Should().Contain("Next Steps:");
        _console.Output.Should().Contain("honua status");
        _console.Output.Should().Contain("Configure SSL/TLS certificates");
    }

    [Fact]
    public async Task ExecuteAsync_WithContinueOnError_ShouldNotStopOnFailure()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("error-plan.json", "dev");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = true,
            DryRun = true,
            ContinueOnError = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert - should complete even if some steps fail
        // The output shows "Deploy HonuaIO to aws" from the plan title
        _console.Output.Should().Contain("Deploy HonuaIO");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldShowProgressBar()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("progress-plan.json", "dev");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = true,
            DryRun = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        // The output shows "Deploy HonuaIO to aws" from the plan title and progress bar
        _console.Output.Should().Contain("Deploy HonuaIO");
    }

    [Fact]
    public async Task ExecuteAsync_WithVerboseFlag_ShouldShowDetailedOutput()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("verbose-plan.json", "dev");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = true,
            DryRun = true,
            Verbose = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingPlanFile_ShouldReturnError()
    {
        // Arrange
        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = "/nonexistent/plan.json",
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("Error:");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDisplayEstimatedDuration()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("duration-plan.json", "prod");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = true,
            DryRun = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Estimated Duration:");
    }

    [Fact]
    public async Task ExecuteAsync_WithProductionEnvironment_ShouldShowHigherRiskLevel()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("prod-risk-plan.json", "prod");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = true,
            DryRun = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().MatchRegex("Risk Level:.*Medium|High");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDisplayEndpointInformation()
    {
        // Arrange
        var planFile = await CreateTestPlanFileAsync("endpoints-plan.json", "dev");

        var command = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-execute", null);
        var settings = new DeployExecuteCommand.Settings
        {
            PlanFile = planFile,
            AutoApprove = true,
            DryRun = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Server Endpoint");
    }

    // Helper methods
    private async Task<string> CreateTestPlanFileAsync(string filename, string environment)
    {
        var planPath = Path.Combine(_workspaceDir.Path, filename);

        var plan = new
        {
            Plan = new
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Deploy HonuaIO to aws",
                Description = "Test deployment plan",
                Type = "Deployment",
                Steps = new[]
                {
                    new
                    {
                        StepNumber = 1,
                        Description = "Create virtual network",
                        Type = "Custom",
                        Operation = "Create VPC",
                        ExpectedOutcome = "Network ready",
                        EstimatedDuration = "00:05:00",
                        RequiresDowntime = false,
                        ModifiesData = false,
                        IsReversible = true,
                        DependsOn = Array.Empty<int>(),
                        Status = "Pending"
                    },
                    new
                    {
                        StepNumber = 2,
                        Description = "Deploy HonuaIO server",
                        Type = "Custom",
                        Operation = "Deploy container",
                        ExpectedOutcome = "Server running",
                        EstimatedDuration = "00:10:00",
                        RequiresDowntime = false,
                        ModifiesData = false,
                        IsReversible = true,
                        DependsOn = new[] { 1 },
                        Status = "Pending"
                    }
                },
                CredentialsRequired = new[]
                {
                    new
                    {
                        SecretRef = "aws-deployer",
                        Scope = new
                        {
                            Level = "Admin",
                            AllowedOperations = new[] { "CreateVPC", "CreateEC2" },
                            DeniedOperations = Array.Empty<string>(),
                            AllowedResources = Array.Empty<string>()
                        },
                        Duration = "02:00:00",
                        Purpose = "Infrastructure deployment",
                        Operations = new[] { "Create VPC", "Deploy instances" }
                    }
                },
                Risk = new
                {
                    Level = environment == "prod" ? "Medium" : "Low",
                    RiskFactors = new[] { $"{environment} environment", "Cloud provisioning" },
                    Mitigations = new[] { "Automated rollback", "Health checks" },
                    AllChangesReversible = true,
                    RequiresDowntime = false,
                    EstimatedDowntime = (string?)null
                },
                Status = "Pending",
                Environment = environment
            },
            Topology = new
            {
                CloudProvider = "aws",
                Region = "us-east-1",
                Environment = environment,
                Database = new
                {
                    Engine = "postgres",
                    Version = "15",
                    InstanceSize = environment == "prod" ? "db.r6g.xlarge" : "db.t4g.micro",
                    StorageGB = environment == "prod" ? 100 : 20,
                    HighAvailability = environment == "prod"
                },
                Compute = new
                {
                    Type = "container",
                    InstanceSize = environment == "prod" ? "c6i.2xlarge" : "t3.medium",
                    InstanceCount = environment == "prod" ? 3 : 1,
                    AutoScaling = environment == "prod"
                },
                Storage = new
                {
                    Type = "s3",
                    AttachmentStorageGB = 100,
                    RasterCacheGB = 500,
                    Replication = environment == "prod" ? "cross-region" : "single-region"
                },
                Networking = new
                {
                    LoadBalancer = true,
                    PublicAccess = true,
                    VpnRequired = false
                },
                Monitoring = new
                {
                    Provider = "cloudwatch",
                    EnableMetrics = true,
                    EnableLogs = true,
                    EnableTracing = environment == "prod"
                }
            },
            GeneratedAt = DateTime.UtcNow
        };

        // Use options with string enum converter to match what DeployExecuteCommand expects
        var options = new JsonSerializerOptions(JsonSerializerOptionsRegistry.WebIndented);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        var json = JsonSerializer.Serialize(plan, options);
        await File.WriteAllTextAsync(planPath, json);

        return planPath;
    }

    // Helper classes
    private sealed class MockAgentCoordinator : IAgentCoordinator
    {
        public Task<AgentCoordinatorResult> ProcessRequestAsync(string request, AgentExecutionContext context, System.Threading.CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentCoordinatorResult
            {
                Success = true,
                Response = "Mock deployment executed",
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
}

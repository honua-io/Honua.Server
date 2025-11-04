using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Planning;
using Honua.Cli.Commands;
using Honua.Cli.Services;
using Honua.Cli.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.E2E;

/// <summary>
/// End-to-end tests for the complete deployment workflow:
/// plan → generate-iam → validate → execute
/// </summary>
[Collection("CliTests")]
[Trait("Category", "E2E")]
public sealed class DeploymentWorkflowE2ETests : IDisposable
{
    private readonly TestConsole _console;
    private readonly string _workspacePath;
    private readonly ILlmProvider _llmProvider;
    private readonly MockAgentCoordinator _agentCoordinator;
    private readonly TestEnvironment _environment;

    public DeploymentWorkflowE2ETests()
    {
        _console = new TestConsole();
        _workspacePath = Path.Combine(Path.GetTempPath(), $"honua-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspacePath);
        _llmProvider = TestConfiguration.CreateLlmProvider();
        _agentCoordinator = new MockAgentCoordinator();
        _environment = new TestEnvironment(_workspacePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspacePath))
        {
            try
            {
                Directory.Delete(_workspacePath, recursive: true);
            }
            catch { /* Best effort */ }
        }
    }

    [Fact]
    public async Task CompleteWorkflow_PlanGenerateIamValidateExecute_ShouldSucceed()
    {
        // STEP 1: Create deployment plan
        var planCommand = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var planSettings = new DeployPlanCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "dev",
            Output = Path.Combine(_workspacePath, "deployment-plan.json")
        };

        var planResult = await planCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            planSettings);

        planResult.Should().Be(0, "Plan generation should succeed");
        File.Exists(planSettings.Output).Should().BeTrue("Plan file should be created");

        // STEP 2: Generate IAM permissions from plan
        var iamCommand = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var iamSettings = new DeployGenerateIamCommand.Settings
        {
            FromPlan = planSettings.Output,
            Output = Path.Combine(_workspacePath, "iam-terraform"),
            AutoApprove = true
        };

        var iamResult = await iamCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            iamSettings);

        iamResult.Should().Be(0, "IAM generation should succeed");
        Directory.Exists(iamSettings.Output).Should().BeTrue("IAM terraform directory should be created");
        File.Exists(Path.Combine(iamSettings.Output, "main.tf")).Should().BeTrue();

        // STEP 3: Validate topology
        var validateCommand = new DeployValidateTopologyCommand(_console, _environment);

        // Extract topology from plan for validation
        var planJson = await File.ReadAllTextAsync(planSettings.Output!);
        var planData = JsonDocument.Parse(planJson);
        var topologyJson = planData.RootElement.GetProperty("Topology").GetRawText();
        var topologyPath = Path.Combine(_workspacePath, "topology.json");
        await File.WriteAllTextAsync(topologyPath, topologyJson);

        var validateSettings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyPath
        };

        var validateResult = await validateCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            validateSettings);

        validateResult.Should().Be(0, "Topology validation should succeed");

        // STEP 4: Execute deployment (dry run)
        var executeCommand = new DeployExecuteCommand(_console, _environment, _agentCoordinator);
        var executeSettings = new DeployExecuteCommand.Settings
        {
            PlanFile = planSettings.Output,
            DryRun = true,
            AutoApprove = true
        };

        var executeResult = await executeCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            executeSettings);

        executeResult.Should().Be(0, "Execution (dry run) should succeed");

        // Verify overall workflow output
        _console.Output.Should().Contain("Deploy HonuaIO");
        _console.Output.Should().Contain("IAM permissions generated successfully");
        _console.Output.Should().Contain("Topology is valid");
        _console.Output.Should().Contain("Deployment Complete");
    }

    [Fact]
    public async Task CompleteWorkflow_WithProductionEnvironment_ShouldUseHighAvailability()
    {
        // Create production deployment plan
        var planCommand = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var planSettings = new DeployPlanCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-west-2",
            Environment = "prod", // Production
            Output = Path.Combine(_workspacePath, "prod-plan.json")
        };

        var planResult = await planCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            planSettings);

        planResult.Should().Be(0);

        // Verify plan includes HA configuration
        var planContent = await File.ReadAllTextAsync(planSettings.Output!);
        var planDoc = JsonDocument.Parse(planContent);
        var topology = planDoc.RootElement.GetProperty("Topology");

        var database = topology.GetProperty("Database");
        database.GetProperty("HighAvailability").GetBoolean().Should().BeTrue();

        var compute = topology.GetProperty("Compute");
        compute.GetProperty("InstanceCount").GetInt32().Should().BeGreaterThan(1);
        compute.GetProperty("AutoScaling").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CompleteWorkflow_ForAzure_ShouldGenerateAzureResources()
    {
        // Create Azure deployment plan
        var planCommand = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var planSettings = new DeployPlanCommand.Settings
        {
            CloudProvider = "azure",
            Region = "eastus",
            Environment = "staging",
            Output = Path.Combine(_workspacePath, "azure-plan.json")
        };

        await planCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            planSettings);

        // Generate IAM (RBAC for Azure)
        var iamCommand = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var iamSettings = new DeployGenerateIamCommand.Settings
        {
            FromPlan = planSettings.Output,
            Output = Path.Combine(_workspacePath, "azure-rbac"),
            AutoApprove = true
        };

        var iamResult = await iamCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            iamSettings);

        iamResult.Should().Be(0);

        // Verify Azure-specific terraform is generated
        var terraformContent = await File.ReadAllTextAsync(Path.Combine(iamSettings.Output, "main.tf"));
        terraformContent.Should().Contain("azuread_service_principal");
        terraformContent.Should().Contain("azurerm_role_definition");
    }

    [Fact]
    public async Task CompleteWorkflow_WithConfigFile_ShouldAnalyzeAndDeploy()
    {
        // Create HonuaIO config file
        var configFile = Path.Combine(_workspacePath, "honua-config.json");
        await File.WriteAllTextAsync(configFile, @"{
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
                ""wmts"": true,
                ""stac"": true
            }
        }");

        // Generate plan from config
        var planCommand = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var planSettings = new DeployPlanCommand.Settings
        {
            ConfigFile = configFile,
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "dev",
            Output = Path.Combine(_workspacePath, "config-plan.json")
        };

        var planResult = await planCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            planSettings);

        planResult.Should().Be(0);
        _console.Output.Should().Contain("Loading configuration from");
        _console.Output.Should().Contain(Path.GetFileName(configFile));
    }

    [Fact]
    public async Task CompleteWorkflow_WithTopologyValidationErrors_ShouldFailGracefully()
    {
        // Create invalid topology (missing required fields)
        var invalidTopology = @"{
            ""CloudProvider"": ""invalid-provider"",
            ""Region"": """",
            ""Environment"": ""dev""
        }";

        var topologyPath = Path.Combine(_workspacePath, "invalid-topology.json");
        await File.WriteAllTextAsync(topologyPath, invalidTopology);

        // Attempt validation
        var validateCommand = new DeployValidateTopologyCommand(_console, _environment);
        var validateSettings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyPath
        };

        var result = await validateCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            validateSettings);

        result.Should().Be(1, "Validation should fail for invalid topology");
        _console.Output.Should().Contain("Invalid cloud provider");
        _console.Output.Should().Contain("Region is required");
    }

    [Fact]
    public async Task CompleteWorkflow_MultiCloudComparison_ShouldGenerateDifferentConfigs()
    {
        var providers = new[] { "aws", "azure", "gcp" };
        var generatedConfigs = new System.Collections.Generic.Dictionary<string, string>();

        foreach (var provider in providers)
        {
            // Generate plan for each provider
            var planCommand = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
            var planSettings = new DeployPlanCommand.Settings
            {
                CloudProvider = provider,
                Region = "us-east-1",
                Environment = "dev",
                Output = Path.Combine(_workspacePath, $"{provider}-plan.json")
            };

            await planCommand.ExecuteAsync(
                new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
                planSettings);

            // Generate IAM for each
            var iamCommand = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
            var iamSettings = new DeployGenerateIamCommand.Settings
            {
                FromPlan = planSettings.Output,
                Output = Path.Combine(_workspacePath, $"{provider}-iam"),
                AutoApprove = true
            };

            await iamCommand.ExecuteAsync(
                new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
                iamSettings);

            var terraformConfig = await File.ReadAllTextAsync(Path.Combine(iamSettings.Output, "main.tf"));
            generatedConfigs[provider] = terraformConfig;
        }

        // Verify provider metadata is persisted in Terraform header
        generatedConfigs["aws"].Should().Contain("# Provider: aws");
        generatedConfigs["azure"].Should().Contain("# Provider: azure");
        generatedConfigs["gcp"].Should().Contain("# Provider: gcp");

        // Configs should be different
        generatedConfigs["aws"].Should().NotBe(generatedConfigs["azure"]);
        generatedConfigs["azure"].Should().NotBe(generatedConfigs["gcp"]);
    }

    [Fact]
    public async Task CompleteWorkflow_ShouldTrackEstimatedCosts()
    {
        var planCommand = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var planSettings = new DeployPlanCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod",
            Output = Path.Combine(_workspacePath, "cost-plan.json")
        };

        await planCommand.ExecuteAsync(
            new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "test-command", null),
            planSettings);

        // Verify plan includes resource sizing information for cost estimation
        var planContent = await File.ReadAllTextAsync(planSettings.Output!);
        var planDoc = JsonDocument.Parse(planContent);
        var topology = planDoc.RootElement.GetProperty("Topology");

        // Database instance size should be present for cost calculation
        topology.GetProperty("Database").GetProperty("InstanceSize").GetString().Should().NotBeNullOrEmpty();

        // Compute instance details
        topology.GetProperty("Compute").GetProperty("InstanceSize").GetString().Should().NotBeNullOrEmpty();
        topology.GetProperty("Compute").GetProperty("InstanceCount").GetInt32().Should().BeGreaterThan(0);

        // Storage allocation
        topology.GetProperty("Storage").GetProperty("AttachmentStorageGB").GetInt32().Should().BeGreaterThan(0);
        topology.GetProperty("Storage").GetProperty("RasterCacheGB").GetInt32().Should().BeGreaterThan(0);
    }

    // Helper classes
    private sealed class MockAgentCoordinator : IAgentCoordinator
    {
        public Task<AgentCoordinatorResult> ProcessRequestAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentCoordinatorResult
            {
                Success = true,
                Response = "Mock agent completed successfully",
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

    // Test environment helper
    private sealed class TestEnvironment : IHonuaCliEnvironment
    {
        private readonly string _basePath;

        public TestEnvironment(string basePath)
        {
            _basePath = basePath;
        }

        public string ConfigRoot => Path.Combine(_basePath, "config");
        public string SnapshotsRoot => Path.Combine(_basePath, "snapshots");
        public string LogsRoot => Path.Combine(_basePath, "logs");

        public void EnsureInitialized()
        {
            Directory.CreateDirectory(ConfigRoot);
            Directory.CreateDirectory(SnapshotsRoot);
            Directory.CreateDirectory(LogsRoot);
        }

        public string ResolveWorkspacePath(string? workspace)
        {
            return string.IsNullOrWhiteSpace(workspace) ? _basePath : workspace;
        }
    }
}

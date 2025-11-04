using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.Commands;
using Honua.Cli.Services;
using Honua.Cli.Tests.Support;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.Commands;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class DeployGenerateIamCommandTests : IDisposable
{
    private readonly TestConsole _console;
    private readonly TestEnvironment _environment;
    private readonly TemporaryDirectory _workspaceDir;
    private readonly ILlmProvider _llmProvider;
    private readonly MockAgentCoordinator _agentCoordinator;

    public DeployGenerateIamCommandTests()
    {
        _console = new TestConsole();
        _workspaceDir = new TemporaryDirectory();
        _environment = new TestEnvironment(_workspaceDir.Path);
        _llmProvider = TestConfiguration.CreateLlmProvider();
        _agentCoordinator = new MockAgentCoordinator();
    }

    public void Dispose()
    {
        _workspaceDir?.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenLlmProviderNotConfigured()
    {
        // Arrange
        var command = new DeployGenerateIamCommand(_console, _environment, llmProvider: null, agentCoordinator: null);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("AI provider not configured");
        _console.Output.Should().Contain("honua setup-wizard");
    }

    [Fact]
    public async Task ExecuteAsync_WithAWS_ShouldGenerateTerraformFiles()
    {
        // Arrange
        var outputDir = Path.Combine(_workspaceDir.Path, "iam-output");
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod",
            Output = outputDir,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        Directory.Exists(outputDir).Should().BeTrue();
        File.Exists(Path.Combine(outputDir, "main.tf")).Should().BeTrue();
        File.Exists(Path.Combine(outputDir, "variables.tf")).Should().BeTrue();
        File.Exists(Path.Combine(outputDir, "outputs.tf")).Should().BeTrue();
        File.Exists(Path.Combine(outputDir, "README.md")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithAzure_ShouldGenerateRBACConfiguration()
    {
        // Arrange
        var outputDir = Path.Combine(_workspaceDir.Path, "azure-iam");
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            CloudProvider = "azure",
            Region = "eastus",
            Environment = "dev",
            Output = outputDir,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        Directory.Exists(outputDir).Should().BeTrue();
        _console.Output.Should().Contain("IAM permissions generated successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WithGCP_ShouldGenerateServiceAccountConfig()
    {
        // Arrange
        var outputDir = Path.Combine(_workspaceDir.Path, "gcp-iam");
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            CloudProvider = "gcp",
            Region = "us-central1",
            Environment = "staging",
            Output = outputDir,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        Directory.Exists(outputDir).Should().BeTrue();
    }



    [Fact]
    public async Task ExecuteAsync_WithFromPlan_ShouldLoadTopologyFromPlanFile()
    {
        // Arrange
        var planFile = Path.Combine(_workspaceDir.Path, "deployment-plan.json");
        await File.WriteAllTextAsync(planFile, @"{
            ""Plan"": {
                ""Id"": ""test-plan"",
                ""Title"": ""Deploy HonuaIO"",
                ""Description"": ""Test deployment"",
                ""Type"": ""Deployment"",
                ""Steps"": [],
                ""CredentialsRequired"": [],
                ""Risk"": {
                    ""Level"": ""Low"",
                    ""RiskFactors"": [],
                    ""Mitigations"": []
                }
            },
            ""Topology"": {
                ""CloudProvider"": ""aws"",
                ""Region"": ""us-west-2"",
                ""Environment"": ""prod""
            }
        }");

        var outputDir = Path.Combine(_workspaceDir.Path, "iam-from-plan");
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            FromPlan = planFile,
            Output = outputDir,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Loading deployment plan from");
        _console.Output.Should().Contain(Path.GetFileName(planFile));
    }

    [Fact]
    public async Task ExecuteAsync_WithTopologyFile_ShouldLoadDirectly()
    {
        // Arrange
        var topologyFile = Path.Combine(_workspaceDir.Path, "topology.json");
        await File.WriteAllTextAsync(topologyFile, @"{
            ""CloudProvider"": ""azure"",
            ""Region"": ""westus2"",
            ""Environment"": ""staging"",
            ""Database"": {
                ""Engine"": ""postgres"",
                ""Version"": ""15"",
                ""InstanceSize"": ""db.t4g.micro"",
                ""StorageGB"": 20,
                ""HighAvailability"": false
            }
        }");

        var outputDir = Path.Combine(_workspaceDir.Path, "iam-from-topology");
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            TopologyFile = topologyFile,
            Output = outputDir,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Loading topology from");
        _console.Output.Should().Contain(Path.GetFileName(topologyFile));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDisplayTopologySummary()
    {
        // Arrange
        var outputDir = Path.Combine(_workspaceDir.Path, "iam-summary");
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod",
            Output = outputDir,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Cloud Provider");
        _console.Output.Should().Contain("aws");
        _console.Output.Should().Contain("Region");
        _console.Output.Should().Contain("us-east-1");
        _console.Output.Should().Contain("Environment");
        _console.Output.Should().Contain("prod");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDisplayNextSteps()
    {
        // Arrange
        var outputDir = Path.Combine(_workspaceDir.Path, "iam-steps");
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "dev",
            Output = outputDir,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Next Steps:");
        _console.Output.Should().Contain("terraform init");
        _console.Output.Should().Contain("terraform plan");
        _console.Output.Should().Contain("terraform apply");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldWarnAboutCredentialSecurity()
    {
        // Arrange
        var outputDir = Path.Combine(_workspaceDir.Path, "iam-security");
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod",
            Output = outputDir,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Never commit credentials to version control");
        _console.Output.Should().Contain("Store credentials in a secure password manager");
        _console.Output.Should().Contain("Rotate access keys");
    }


    [Fact]
    public async Task ExecuteAsync_WithVerboseFlag_ShouldShowDetailedErrors()
    {
        // Arrange
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            TopologyFile = "/nonexistent/topology.json",
            Output = _workspaceDir.Path,
            Verbose = true,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("Error:");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateReadmeWithSecurityGuidelines()
    {
        // Arrange
        var outputDir = Path.Combine(_workspaceDir.Path, "iam-readme");
        var command = new DeployGenerateIamCommand(_console, _environment, _llmProvider, _agentCoordinator);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-generate-iam", null);
        var settings = new DeployGenerateIamCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod",
            Output = outputDir,
            AutoApprove = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);

        var readmePath = Path.Combine(outputDir, "README.md");
        File.Exists(readmePath).Should().BeTrue();

        var readmeContent = await File.ReadAllTextAsync(readmePath);
        readmeContent.Should().Contain("HonuaIO IAM Deployment Configuration");
        readmeContent.Should().Contain("Security Notes");
        readmeContent.Should().Contain("Never commit credentials");
        readmeContent.Should().Contain("Deployment");
    }

    // Helper classes
    private sealed class MockAgentCoordinator : IAgentCoordinator
    {
        public Task<AgentCoordinatorResult> ProcessRequestAsync(string request, AgentExecutionContext context, System.Threading.CancellationToken cancellationToken)
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

}

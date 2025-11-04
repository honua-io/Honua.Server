using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.Planning;
using Honua.Cli.Commands;
using Honua.Cli.Services;
using Honua.Cli.Tests.Support;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.Commands;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class DeployPlanCommandTests : IDisposable
{
    private readonly TestConsole _console;
    private readonly TestEnvironment _environment;
    private readonly TemporaryDirectory _workspaceDir;
    private readonly ILlmProvider _llmProvider;
    private readonly MockAgentCoordinator _agentCoordinator;

    public DeployPlanCommandTests()
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
    public async Task ExecuteAsync_ShouldReturnError_WhenAgentCoordinatorNotConfigured()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, agentCoordinator: null, llmProvider: null);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "dev"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("AI coordinator not configured");
        _console.Output.Should().Contain("honua setup-wizard");
    }


    [Fact]
    public async Task ExecuteAsync_WithCloudProvider_ShouldGenerateDeploymentPlan()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-west-2",
            Environment = "prod",
            Output = Path.Combine(_workspaceDir.Path, "test-plan.json")
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Deploy HonuaIO to aws");
        _console.Output.Should().Contain("Deployment Steps:");
        _console.Output.Should().Contain("Total Estimated Duration");
        _console.Output.Should().Contain("Risk Level:");

        File.Exists(settings.Output).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithProdEnvironment_ShouldIncludeHighAvailabilityConfig()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("db.r6g.xlarge"); // Production database size
        _console.Output.Should().Contain("3x"); // Multiple instances
    }

    [Fact]
    public async Task ExecuteAsync_WithDevEnvironment_ShouldUseSmallerResources()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "dev"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("db.t4g.micro"); // Dev database size
        _console.Output.Should().Contain("1x"); // Single instance
    }

    [Fact]
    public async Task ExecuteAsync_WithConfigFile_ShouldLoadTopologyFromConfig()
    {
        // Arrange
        var configFile = Path.Combine(_workspaceDir.Path, "honua-config.json");
        await File.WriteAllTextAsync(configFile, @"{
            ""database"": {
                ""provider"": ""postgresql"",
                ""connectionString"": ""Host=localhost;Database=honua""
            },
            ""storage"": {
                ""provider"": ""s3"",
                ""bucketName"": ""honua-tiles""
            }
        }");

        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            ConfigFile = configFile,
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Loading configuration from");
        _console.Output.Should().Contain(Path.GetFileName(configFile));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOutputPlanStepsInOrder()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);

        var output = _console.Output;
        var networkIndex = output.IndexOf("Create virtual network", StringComparison.OrdinalIgnoreCase);
        var databaseIndex = output.IndexOf("Create postgres database", StringComparison.OrdinalIgnoreCase);
        var computeIndex = output.IndexOf("Deploy HonuaIO server", StringComparison.OrdinalIgnoreCase);

        networkIndex.Should().BeGreaterThan(0);
        databaseIndex.Should().BeGreaterThan(networkIndex);
        computeIndex.Should().BeGreaterThan(databaseIndex);
    }

    [Fact]
    public async Task ExecuteAsync_WithVerboseFlag_ShouldShowDetailedOutput()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            CloudProvider = "azure",
            Region = "eastus",
            Environment = "staging",
            Verbose = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        // Note: Status spinner text not captured in TestConsole, check for actual output
        _console.Output.Should().Contain("Creating deployment plan");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSuggestNextSteps()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            CloudProvider = "gcp",
            Region = "us-central1",
            Environment = "dev",
            Output = "deployment-plan.json"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Next Steps:");
        _console.Output.Should().Contain("honua deploy generate-iam");
        _console.Output.Should().Contain("honua deploy execute");
    }

    [Fact]
    public async Task ExecuteAsync_WithKubernetesProvider_ShouldUseContainerType()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            CloudProvider = "kubernetes",
            Region = "us-east-1",
            Environment = "prod"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("container");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCalculateTotalDuration()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().MatchRegex(@"Total Estimated Duration:.*\d+\s+minutes");
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ShouldReturnErrorCode()
    {
        // Arrange
        var command = new DeployPlanCommand(_console, _environment, _agentCoordinator, _llmProvider);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-plan", null);
        var settings = new DeployPlanCommand.Settings
        {
            ConfigFile = "/nonexistent/path/config.json",
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "dev"
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("Error:");
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

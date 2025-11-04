using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class GitOpsConfigurationAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly GitOpsConfigurationAgent _agent;

    public GitOpsConfigurationAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _agent = new GitOpsConfigurationAgent(_kernel, _mockLlmProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GitOpsConfigurationAgent(null!, _mockLlmProvider.Object));
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_DoesNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() => new GitOpsConfigurationAgent(_kernel, null));
        Assert.Null(exception);
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var workspacePath = Path.Combine(Path.GetTempPath(), $"gitops-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(workspacePath);

        var request = "Setup GitOps with repository https://github.com/test/repo branch main for production";
        var context = new AgentExecutionContext
        {
            WorkspacePath = workspacePath,
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""RepositoryUrl"": ""https://github.com/test/repo"",
                    ""Branch"": ""main"",
                    ""Environments"": [""production""],
                    ""PollIntervalSeconds"": 30,
                    ""AuthenticationMethod"": ""ssh-key"",
                    ""ReconciliationStrategy"": ""automatic"",
                    ""RequiresSecrets"": true,
                    ""MetadataFiles"": [""metadata.yaml""],
                    ""DatasourceFiles"": [""datasources.json""],
                    ""Summary"": ""GitOps setup for production""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue($"Error: {result.Message}");
        result.AgentName.Should().Be("GitOpsConfiguration");
        result.Action.Should().Be("ProcessGitOpsRequest");
        result.Message.Should().Contain("https://github.com/test/repo");
    }

    [Fact]
    public async Task AnalyzeGitOpsRequirementsAsync_WithLlmProvider_UsesLlmForAnalysis()
    {
        // Arrange
        var request = "Setup GitOps with https://github.com/test/repo for production and staging";
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""RepositoryUrl"": ""https://github.com/test/repo"",
                    ""Branch"": ""main"",
                    ""Environments"": [""production"", ""staging""],
                    ""PollIntervalSeconds"": 30,
                    ""AuthenticationMethod"": ""ssh-key"",
                    ""ReconciliationStrategy"": ""automatic"",
                    ""RequiresSecrets"": true,
                    ""MetadataFiles"": [""metadata.yaml""],
                    ""DatasourceFiles"": [""datasources.json""],
                    ""Summary"": ""Multi-environment GitOps setup""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.AnalyzeGitOpsRequirementsAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RepositoryUrl.Should().Be("https://github.com/test/repo");
        result.Branch.Should().Be("main");
        result.Environments.Should().Contain("production");
        result.Environments.Should().Contain("staging");
        result.PollIntervalSeconds.Should().Be(30);
        result.AuthenticationMethod.Should().Be("ssh-key");
        result.ReconciliationStrategy.Should().Be("automatic");

        _mockLlmProvider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeGitOpsRequirementsAsync_WithoutLlmProvider_UsesFallbackParsing()
    {
        // Arrange
        var agentWithoutLlm = new GitOpsConfigurationAgent(_kernel, null);
        var request = "Setup GitOps with https://github.com/test/repo branch main for production environment";
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await agentWithoutLlm.AnalyzeGitOpsRequirementsAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RepositoryUrl.Should().Be("https://github.com/test/repo");
        result.Branch.Should().Be("main");
        result.Environments.Should().Contain("production");
    }

    [Fact]
    public async Task AnalyzeGitOpsRequirementsAsync_DetectsMultipleEnvironments()
    {
        // Arrange
        var agentWithoutLlm = new GitOpsConfigurationAgent(_kernel, null);
        var request = "Setup for production, staging, and development environments";
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await agentWithoutLlm.AnalyzeGitOpsRequirementsAsync(request, context, CancellationToken.None);

        // Assert
        result.Environments.Should().Contain("production");
        result.Environments.Should().Contain("staging");
        result.Environments.Should().Contain("development");
    }

    [Fact]
    public async Task AnalyzeGitOpsRequirementsAsync_DetectsManualReconciliation()
    {
        // Arrange
        var agentWithoutLlm = new GitOpsConfigurationAgent(_kernel, null);
        var request = "Setup GitOps with manual approval required for changes";
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await agentWithoutLlm.AnalyzeGitOpsRequirementsAsync(request, context, CancellationToken.None);

        // Assert
        result.ReconciliationStrategy.Should().Be("approval-required");
    }

    [Fact]
    public async Task GenerateGitOpsConfigurationAsync_CreatesValidConfiguration()
    {
        // Arrange
        var analysis = new GitOpsAnalysis
        {
            RepositoryUrl = "https://github.com/test/repo",
            Branch = "main",
            Environments = new List<string> { "production" },
            PollIntervalSeconds = 60,
            AuthenticationMethod = "https",
            ReconciliationStrategy = "manual",
            Summary = "Test configuration"
        };
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await _agent.GenerateGitOpsConfigurationAsync(analysis, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RepositoryUrl.Should().Be("https://github.com/test/repo");
        result.Branch.Should().Be("main");
        result.PollIntervalSeconds.Should().Be(60);
        result.AuthenticationMethod.Should().Be("https");
        result.Environments.Should().Contain("production");
        result.ReconciliationStrategy.Should().Be("manual");
    }

    [Fact]
    public async Task ValidateGitOpsConfigurationAsync_WithValidConfig_ReturnsValid()
    {
        // Arrange
        var config = new GitOpsConfiguration
        {
            RepositoryUrl = "https://github.com/test/repo",
            Branch = "main",
            Environments = new List<string> { "production" },
            PollIntervalSeconds = 30,
            AuthenticationMethod = "ssh-key",
            ReconciliationStrategy = "automatic"
        };
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await _agent.ValidateGitOpsConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateGitOpsConfigurationAsync_WithMissingRepositoryUrl_ReturnsInvalid()
    {
        // Arrange
        var config = new GitOpsConfiguration
        {
            RepositoryUrl = "",
            Branch = "main",
            Environments = new List<string> { "production" },
            PollIntervalSeconds = 30
        };
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await _agent.ValidateGitOpsConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Repository URL is required");
    }

    [Fact]
    public async Task ValidateGitOpsConfigurationAsync_WithMissingBranch_ReturnsInvalid()
    {
        // Arrange
        var config = new GitOpsConfiguration
        {
            RepositoryUrl = "https://github.com/test/repo",
            Branch = "",
            Environments = new List<string> { "production" },
            PollIntervalSeconds = 30
        };
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await _agent.ValidateGitOpsConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Branch name is required");
    }

    [Fact]
    public async Task ValidateGitOpsConfigurationAsync_WithNoEnvironments_ReturnsInvalid()
    {
        // Arrange
        var config = new GitOpsConfiguration
        {
            RepositoryUrl = "https://github.com/test/repo",
            Branch = "main",
            Environments = new List<string>(),
            PollIntervalSeconds = 30
        };
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await _agent.ValidateGitOpsConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("At least one environment must be specified");
    }

    [Fact]
    public async Task ValidateGitOpsConfigurationAsync_WithLowPollInterval_ReturnsInvalid()
    {
        // Arrange
        var config = new GitOpsConfiguration
        {
            RepositoryUrl = "https://github.com/test/repo",
            Branch = "main",
            Environments = new List<string> { "production" },
            PollIntervalSeconds = 3
        };
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await _agent.ValidateGitOpsConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Poll interval must be at least 5 seconds");
    }

    [Fact]
    public async Task ProcessAsync_WithValidationFailure_ReturnsFailureResult()
    {
        // Arrange
        var request = "Setup GitOps"; // Missing required info
        var context = new AgentExecutionContext
        {
            WorkspacePath = "/tmp/test",
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""RepositoryUrl"": """",
                    ""Branch"": ""main"",
                    ""Environments"": [],
                    ""PollIntervalSeconds"": 30
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("validation failed");
    }

    [Fact]
    public async Task ProcessAsync_InDryRunMode_IndicatesDryRun()
    {
        // Arrange
        var workspacePath = Path.Combine(Path.GetTempPath(), $"gitops-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(workspacePath);

        var request = "Setup GitOps with https://github.com/test/repo for production";
        var context = new AgentExecutionContext
        {
            WorkspacePath = workspacePath,
            DryRun = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""RepositoryUrl"": ""https://github.com/test/repo"",
                    ""Branch"": ""main"",
                    ""Environments"": [""production""],
                    ""PollIntervalSeconds"": 30,
                    ""AuthenticationMethod"": ""ssh-key"",
                    ""ReconciliationStrategy"": ""automatic"",
                    ""Summary"": ""Test setup""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue($"Error: {result.Message}");
        result.Message.Should().Contain("dry-run");
    }

    [Theory]
    [InlineData("https://github.com/test/repo", "https://github.com/test/repo")]
    [InlineData("git@github.com:test/repo.git", "git@github.com:test/repo.git")]
    [InlineData("ssh://git@github.com/test/repo.git", "ssh://git@github.com/test/repo.git")]
    public async Task AnalyzeGitOpsRequirementsAsync_DetectsDifferentUrlFormats(string url, string expectedUrl)
    {
        // Arrange
        var agentWithoutLlm = new GitOpsConfigurationAgent(_kernel, null);
        var request = $"Setup GitOps with {url}";
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await agentWithoutLlm.AnalyzeGitOpsRequirementsAsync(request, context, CancellationToken.None);

        // Assert
        result.RepositoryUrl.Should().Be(expectedUrl);
    }

    [Theory]
    [InlineData("prod", "production")]
    [InlineData("stage", "staging")]
    [InlineData("dev", "development")]
    public async Task AnalyzeGitOpsRequirementsAsync_NormalizesEnvironmentNames(string input, string expected)
    {
        // Arrange
        var agentWithoutLlm = new GitOpsConfigurationAgent(_kernel, null);
        var request = $"Setup for {input} environment";
        var context = new AgentExecutionContext { WorkspacePath = "/tmp/test" };

        // Act
        var result = await agentWithoutLlm.AnalyzeGitOpsRequirementsAsync(request, context, CancellationToken.None);

        // Assert
        result.Environments.Should().Contain(expected);
    }
}

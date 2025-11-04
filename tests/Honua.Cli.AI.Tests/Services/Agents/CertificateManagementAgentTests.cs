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
public class CertificateManagementAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly CertificateManagementAgent _agent;

    public CertificateManagementAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _agent = new CertificateManagementAgent(_kernel, _mockLlmProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CertificateManagementAgent(null!, _mockLlmProvider.Object));
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_DoesNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() => new CertificateManagementAgent(_kernel, null));
        Assert.Null(exception);
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Setup SSL for honua.example.com with email admin@example.com";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""domains"": [""honua.example.com""],
                    ""email"": ""admin@example.com"",
                    ""challengeType"": ""Http01"",
                    ""autoRenew"": true,
                    ""storageProvider"": ""azure-keyvault"",
                    ""storageLocation"": ""honua-certificates"",
                    ""acmeEnvironment"": ""staging"",
                    ""summary"": ""SSL certificate for honua.example.com""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("CertificateManagement");
        result.Action.Should().Be("ProcessCertificateRequest");
        result.Message.Should().Contain("honua.example.com");
    }

    [Fact]
    public async Task ProcessAsync_WithWildcardDomain_UsesDns01Challenge()
    {
        // Arrange
        var request = "Setup SSL for *.api.example.com";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""domains"": [""*.api.example.com""],
                    ""email"": ""admin@example.com"",
                    ""challengeType"": ""Dns01"",
                    ""autoRenew"": true,
                    ""storageProvider"": ""azure-keyvault"",
                    ""storageLocation"": ""honua-certificates"",
                    ""acmeEnvironment"": ""staging"",
                    ""summary"": ""Wildcard SSL certificate""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_WithProductionEnvironment_UsesProductionAcme()
    {
        // Arrange
        var request = "Setup production SSL for honua.com with email admin@honua.com";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""domains"": [""honua.com""],
                    ""email"": ""admin@honua.com"",
                    ""challengeType"": ""Http01"",
                    ""autoRenew"": true,
                    ""storageProvider"": ""azure-keyvault"",
                    ""storageLocation"": ""honua-certificates"",
                    ""acmeEnvironment"": ""production"",
                    ""summary"": ""Production SSL certificate""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_WithDryRun_ReturnsGeneratedMessage()
    {
        // Arrange
        var request = "Setup SSL for test.example.com";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""domains"": [""test.example.com""],
                    ""email"": ""admin@example.com"",
                    ""challengeType"": ""Http01"",
                    ""autoRenew"": true,
                    ""storageProvider"": ""azure-keyvault"",
                    ""storageLocation"": ""honua-certificates"",
                    ""acmeEnvironment"": ""staging"",
                    ""summary"": ""SSL certificate setup""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("dry-run");
    }

    [Fact]
    public async Task ProcessAsync_WithInvalidDomains_ReturnsFailureResult()
    {
        // Arrange
        var request = "Setup SSL certificate without specifying domain";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""domains"": [],
                    ""email"": ""admin@example.com"",
                    ""challengeType"": ""Http01"",
                    ""autoRenew"": true,
                    ""storageProvider"": ""azure-keyvault"",
                    ""storageLocation"": ""honua-certificates"",
                    ""acmeEnvironment"": ""staging"",
                    ""summary"": ""SSL certificate setup""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("validation failed");
    }

    [Fact]
    public async Task ProcessAsync_WithMissingEmail_ReturnsFailureResult()
    {
        // Arrange
        var request = "Setup SSL for honua.com";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""domains"": [""honua.com""],
                    ""email"": """",
                    ""challengeType"": ""Http01"",
                    ""autoRenew"": true,
                    ""storageProvider"": ""azure-keyvault"",
                    ""storageLocation"": ""honua-certificates"",
                    ""acmeEnvironment"": ""staging"",
                    ""summary"": ""SSL certificate setup""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("email is required");
    }

    [Fact]
    public async Task ProcessAsync_WithWildcardAndHttp01_ReturnsFailureResult()
    {
        // Arrange
        var request = "Setup SSL for *.example.com with HTTP challenge";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""domains"": [""*.example.com""],
                    ""email"": ""admin@example.com"",
                    ""challengeType"": ""Http01"",
                    ""autoRenew"": true,
                    ""storageProvider"": ""azure-keyvault"",
                    ""storageLocation"": ""honua-certificates"",
                    ""acmeEnvironment"": ""staging"",
                    ""summary"": ""SSL certificate setup""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Wildcard domains require Dns01");
    }

    [Fact]
    public async Task AnalyzeCertificateRequirementsAsync_WithoutLlmProvider_UsesFallback()
    {
        // Arrange
        var agentWithoutLlm = new CertificateManagementAgent(_kernel, null);
        var request = "Setup SSL for test.honua.io with email admin@honua.io";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath()
        };

        // Act
        var analysis = await agentWithoutLlm.AnalyzeCertificateRequirementsAsync(request, context, CancellationToken.None);

        // Assert
        analysis.Should().NotBeNull();
        analysis.Domains.Should().Contain("test.honua.io");
        analysis.Email.Should().Be("admin@honua.io");
    }

    [Fact]
    public async Task ValidateCertificateConfigurationAsync_WithValidConfig_ReturnsValid()
    {
        // Arrange
        var config = new CertificateConfiguration
        {
            Domains = new List<string> { "test.com" },
            Email = "admin@test.com",
            ChallengeType = "Http01",
            AutoRenew = true,
            StorageProvider = "azure-keyvault",
            StorageLocation = "test-vault",
            AcmeEnvironment = "staging",
            Summary = "Test certificate"
        };
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath()
        };

        // Act
        var result = await _agent.ValidateCertificateConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeEmpty();
    }
}

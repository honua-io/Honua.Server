using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class ComplianceAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<ComplianceAgent>> _mockLogger;
    private readonly ComplianceAgent _agent;

    public ComplianceAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<ComplianceAgent>>();
        _agent = new ComplianceAgent(_kernel, _mockLlmProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ComplianceAgent(null!, _mockLlmProvider.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLlmProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ComplianceAgent(_kernel, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ComplianceAgent(_kernel, _mockLlmProvider.Object, null!));
    }

    [Fact]
    public async Task ProcessAsync_WithSOC2Request_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Assess SOC2 compliance for our deployment";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("Compliance");
        result.Action.Should().Be("ProcessComplianceAssessment");
        result.Message.Should().Contain("Compliance Assessment Report");
    }

    [Fact]
    public async Task ProcessAsync_WithGDPRRequest_ContainsDataResidencyRecommendations()
    {
        // Arrange
        var request = "Ensure GDPR compliance for EU users with PII data";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Data Residency Recommendations");
    }

    [Fact]
    public async Task ProcessAsync_WithHIPAARequest_ContainsEncryptionRequirements()
    {
        // Arrange
        var request = "Assess HIPAA compliance for healthcare data";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Encryption Requirements");
    }

    [Fact]
    public async Task ProcessAsync_WithComplianceGaps_IncludesRemediationSteps()
    {
        // Arrange
        var request = "Check compliance for financial services";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Compliance Gaps");
        result.Message.Should().Contain("Next Steps");
    }

    [Fact]
    public async Task ProcessAsync_WithLlmFailure_ReturnsSuccessWithDefaults()
    {
        // Arrange
        var request = "Assess compliance";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = "Error occurred",
                Model = "test-model",
                Success = false,
                ErrorMessage = "LLM service unavailable"
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Agent falls back to default analysis when LLM fails, so it still succeeds
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Compliance Assessment Report");
    }

    [Fact]
    public async Task ProcessAsync_WithMultipleFrameworks_ContainsAllFrameworks()
    {
        // Arrange
        var request = "Assess SOC2, GDPR, and HIPAA compliance";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Compliance Frameworks");
    }

    [Fact]
    public async Task ProcessAsync_WithAuditRequirements_ContainsAuditSection()
    {
        // Arrange
        var request = "Prepare for SOC2 audit with logging requirements";
        var context = new AgentExecutionContext
        {
            WorkspacePath = System.IO.Path.GetTempPath(),
            DryRun = false
        };

        SetupLlmProviderResponses();

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Audit Requirements");
    }

    private void SetupLlmProviderResponses()
    {
        var setupCount = 0;
        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                setupCount++;
                if (setupCount == 1)
                {
                    // First call - compliance requirements analysis
                    return new LlmResponse
                    {
                        Content = @"{
                            ""frameworks"": [""SOC2"", ""GDPR""],
                            ""dataTypes"": [""PII"", ""Financial Data""],
                            ""geographicRegions"": [""EU"", ""US""],
                            ""industry"": ""Healthcare""
                        }",
                        Model = "test-model",
                        Success = true
                    };
                }
                else
                {
                    // Second call - compliance assessment
                    return new LlmResponse
                    {
                        Content = @"{
                            ""controlsInPlace"": [""TLS encryption in transit"", ""At-rest encryption with KMS""],
                            ""complianceGaps"": [
                                {
                                    ""control"": ""Access Logging"",
                                    ""description"": ""No centralized access logs"",
                                    ""riskLevel"": ""High"",
                                    ""remediation"": ""Enable CloudTrail/Activity Log""
                                }
                            ],
                            ""requiredPolicies"": [""Privacy Policy"", ""Data Retention Policy""],
                            ""auditRequirements"": [""90-day log retention"", ""Real-time security alerting""],
                            ""dataResidencyRecommendations"": [""Store EU data in EU regions only""],
                            ""encryptionRequirements"": [""AES-256 for data at rest"", ""TLS 1.2+ for transit""],
                            ""complianceScore"": 75,
                            ""nextSteps"": [""Enable centralized logging"", ""Document incident response plan""]
                        }",
                        Model = "test-model",
                        Success = true
                    };
                }
            });
    }
}

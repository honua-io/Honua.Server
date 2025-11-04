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
public class DnsConfigurationAgentTests
{
    private readonly Kernel _kernel;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly DnsConfigurationAgent _agent;

    public DnsConfigurationAgentTests()
    {
        _kernel = new Kernel();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _agent = new DnsConfigurationAgent(_kernel, _mockLlmProvider.Object);
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DnsConfigurationAgent(null!, _mockLlmProvider.Object));
    }

    [Fact]
    public async Task ProcessAsync_WithARecordRequest_ReturnsSuccess()
    {
        // Arrange
        var request = "Setup A record for honua.example.com pointing to 203.0.113.5";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""provider"": ""cloudflare"",
                    ""zone"": ""example.com"",
                    ""records"": [
                        {
                            ""type"": ""A"",
                            ""name"": ""honua"",
                            ""value"": ""203.0.113.5"",
                            ""ttl"": 3600
                        }
                    ],
                    ""proxyEnabled"": false,
                    ""summary"": ""A record configuration""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("DnsConfiguration");
    }

    [Fact]
    public async Task ProcessAsync_WithCNAMERecord_ReturnsSuccess()
    {
        // Arrange
        var request = "Create CNAME record www.example.com pointing to example.com";
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Content = @"{
                    ""provider"": ""cloudflare"",
                    ""zone"": ""example.com"",
                    ""records"": [
                        {
                            ""type"": ""CNAME"",
                            ""name"": ""www"",
                            ""value"": ""example.com"",
                            ""ttl"": 3600
                        }
                    ],
                    ""proxyEnabled"": false,
                    ""summary"": ""CNAME record configuration""
                }",
                Model = "test-model",
                Success = true
            });

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateDnsConfigurationAsync_WithEmptyRecords_ReturnsInvalid()
    {
        // Arrange
        var config = new DnsConfiguration
        {
            Provider = "cloudflare",
            Zone = "example.com",
            Records = new List<DnsRecord>()
        };
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Act
        var result = await _agent.ValidateDnsConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("At least one DNS record is required");
    }

    [Fact]
    public async Task ValidateDnsConfigurationAsync_WithInvalidRecordType_ReturnsInvalid()
    {
        // Arrange
        var config = new DnsConfiguration
        {
            Provider = "cloudflare",
            Zone = "example.com",
            Records = new List<DnsRecord>
            {
                new DnsRecord { Type = "INVALID", Name = "test", Value = "1.2.3.4" }
            }
        };
        var context = new AgentExecutionContext { WorkspacePath = System.IO.Path.GetTempPath() };

        // Act
        var result = await _agent.ValidateDnsConfigurationAsync(config, context, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid record type");
    }
}

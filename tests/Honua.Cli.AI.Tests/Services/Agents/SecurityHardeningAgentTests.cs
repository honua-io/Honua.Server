using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Microsoft.SemanticKernel;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class SecurityHardeningAgentTests
{
    private readonly Kernel _kernel;
    private readonly SecurityHardeningAgent _agent;

    public SecurityHardeningAgentTests()
    {
        _kernel = new Kernel();

        // Add mock Security plugin
        _kernel.Plugins.AddFromObject(new MockSecurityPlugin(), "Security");

        _agent = new SecurityHardeningAgent(_kernel);
    }

    private class MockSecurityPlugin
    {
        [KernelFunction, Description("Generates authentication configuration")]
        public Task<string> GenerateAuthConfigAsync(string authType, string provider)
        {
            return Task.FromResult("{\"type\": \"JWT\", \"provider\": \"Auth0\"}");
        }

        [KernelFunction, Description("Generates CORS configuration")]
        public Task<string> GenerateCorsConfigAsync(string allowedOrigins, string allowedMethods)
        {
            return Task.FromResult("{\"origins\": [\"*\"], \"methods\": [\"GET\", \"POST\"]}");
        }

        [KernelFunction, Description("Generates rate limiting configuration")]
        public Task<string> GenerateRateLimitingConfigAsync(string requestsPerMinute, string burst)
        {
            return Task.FromResult("{\"limit\": \"100/min\", \"burst\": \"20\"}");
        }

        [KernelFunction, Description("Generates SSL/TLS configuration")]
        public Task<string> GenerateSslConfigAsync(string minVersion, string cipherSuites)
        {
            return Task.FromResult("{\"minVersion\": \"TLS1.2\", \"ciphers\": [\"ECDHE-RSA-AES256-GCM-SHA384\"]}");
        }
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SecurityHardeningAgent(null!));
    }

    [Fact]
    public async Task ProcessAsync_WithAuthenticationRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Setup authentication with JWT tokens";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("SecurityHardening");
        result.Action.Should().Be("ProcessSecurityRequest");
        result.Message.Should().Contain("security");
    }

    [Fact]
    public async Task ProcessAsync_WithCORSRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Configure CORS for my API to allow cross-origin requests";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("security");
    }

    [Fact]
    public async Task ProcessAsync_WithRateLimitingRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Add rate limiting to prevent abuse";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("security");
    }

    [Fact]
    public async Task ProcessAsync_WithSSLRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "Enable HTTPS and TLS encryption";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("security");
    }

    [Fact]
    public async Task ProcessAsync_WithDryRun_ReturnsGeneratedMessage()
    {
        // Arrange
        var request = "Setup comprehensive security with auth, CORS, and rate limiting";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = true
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("dry-run");
    }

    [Fact]
    public async Task ProcessAsync_WithMultipleSecurityFeatures_CreatesConfigFile()
    {
        // Arrange
        var request = "Setup authentication, authorization, CORS, and SSL";
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempPath);

        var context = new AgentExecutionContext
        {
            WorkspacePath = tempPath,
            DryRun = false
        };

        try
        {
            // Act
            var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();

            var securityFile = Path.Combine(tempPath, "security.json");
            File.Exists(securityFile).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
        }
    }

    [Fact]
    public async Task ProcessAsync_WithException_ReturnsErrorResult()
    {
        // Arrange
        var request = "Setup security";
        var context = new AgentExecutionContext
        {
            WorkspacePath = "/invalid/path/that/does/not/exist/and/cannot/be/created",
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Error");
    }
}

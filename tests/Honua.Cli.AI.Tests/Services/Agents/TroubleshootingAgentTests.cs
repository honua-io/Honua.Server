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
public class TroubleshootingAgentTests
{
    private readonly Kernel _kernel;
    private readonly TroubleshootingAgent _agent;

    public TroubleshootingAgentTests()
    {
        _kernel = new Kernel();

        // Add mock Diagnostics plugin
        _kernel.Plugins.AddFromObject(new MockDiagnosticsPlugin(), "Diagnostics");

        _agent = new TroubleshootingAgent(_kernel);
    }

    private class MockDiagnosticsPlugin
    {
        [KernelFunction, Description("Diagnoses server issues")]
        public Task<string> DiagnoseServerIssue(string symptoms, string recentLogs)
        {
            if (symptoms.Contains("500 errors"))
                return Task.FromResult("Application errors due to unhandled exceptions");
            if (symptoms.Contains("timeout"))
                return Task.FromResult("Database connection pool exhausted");
            if (symptoms.Contains("slow"))
                return Task.FromResult("Missing database indexes causing full table scans");
            if (symptoms.Contains("connect"))
                return Task.FromResult("Network connectivity issue or firewall blocking traffic");

            return Task.FromResult("Unknown issue requiring further investigation");
        }

        [KernelFunction, Description("Analyzes logs for errors")]
        public Task<string> AnalyzeLogs(string logContent)
        {
            return Task.FromResult("Error patterns found in application logs");
        }
    }

    [Fact]
    public void Constructor_WithNullKernel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TroubleshootingAgent(null!));
    }

    [Fact]
    public async Task ProcessAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = "API returning 500 errors";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AgentName.Should().Be("Troubleshooting");
        result.Action.Should().Be("ProcessTroubleshootingRequest");
    }

    [Fact]
    public async Task ProcessAsync_WithDatabaseError_IncludesRootCause()
    {
        // Arrange
        var request = "Database connection timeout";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("Root cause");
    }

    [Fact]
    public async Task ProcessAsync_WithPerformanceIssue_IncludesRemediation()
    {
        // Arrange
        var request = "Slow query performance";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("Remediation");
    }

    [Fact]
    public async Task ProcessAsync_WithNetworkIssue_IncludesDiagnostics()
    {
        // Arrange
        var request = "Cannot connect to external API";
        var context = new AgentExecutionContext
        {
            WorkspacePath = Path.GetTempPath(),
            DryRun = false
        };

        // Act
        var result = await _agent.ProcessAsync(request, context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("Troubleshooting");
    }
}

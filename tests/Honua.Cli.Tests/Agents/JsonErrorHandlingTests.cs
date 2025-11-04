using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Honua.Cli.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Xunit;

namespace Honua.Cli.Tests.Agents;

/// <summary>
/// Tests verifying the JSON error handling bug fixes documented in AI_CONSULTANT_BUGS.md.
/// These tests ensure agents properly handle malformed JSON responses from LLMs and external tools.
/// </summary>
[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class JsonErrorHandlingTests
{
    private readonly Kernel _kernel;

    public JsonErrorHandlingTests()
    {
        _kernel = new Kernel();
    }

    #region CloudPermissionGeneratorAgent Tests (Bug #2)

    [Fact]
    public async Task CloudPermissionGeneratorAgent_ShouldHandleMalformedJsonResponse()
    {
        // Arrange - Create mock provider that returns malformed JSON
        var mockProvider = new MockLlmProvider();
        mockProvider.SetDefaultResponse("{ this is not valid json at all }"); // Malformed JSON

        var agent = new CloudPermissionGeneratorAgent(mockProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = new DeploymentTopology
        {
            CloudProvider = "aws",
            Region = "us-east-1",
            Environment = "prod"
        };

        var context = new AgentExecutionContext
        {
            WorkspacePath = "/tmp",
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert - Should fail gracefully, not crash with JsonException
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("deserialize"); // Verifies JSON error was caught
    }

    [Fact]
    public async Task CloudPermissionGeneratorAgent_ShouldHandleNullServicesList()
    {
        // Arrange - Return valid JSON but with null Services property
        var mockProvider = new MockLlmProvider();
        mockProvider.SetDefaultResponse(@"{
            ""Services"": null
        }");

        var agent = new CloudPermissionGeneratorAgent(mockProvider, NullLogger<CloudPermissionGeneratorAgent>.Instance);

        var topology = new DeploymentTopology
        {
            CloudProvider = "azure",
            Region = "eastus",
            Environment = "dev"
        };

        var context = new AgentExecutionContext
        {
            WorkspacePath = "/tmp",
            DryRun = false
        };

        // Act
        var result = await agent.GeneratePermissionsAsync(topology, context, CancellationToken.None);

        // Assert - Should fail gracefully with proper error message
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region DeploymentConfigurationAgent Tests (Bug #3)

    [Fact]
    public async Task DeploymentConfigurationAgent_ShouldHandleInvalidEnumValues()
    {
        // Arrange - Provider returns JSON with invalid enum value
        var mockProvider = new MockLlmProvider();

        var json = JsonSerializer.Serialize(new
        {
            DeploymentType = "InvalidTypeXYZ123", // This would crash with Enum.Parse
            TargetEnvironment = "production",
            InfrastructureNeeds = new
            {
                RequiresDatabase = true,
                RequiresCache = false,
                RequiresStorage = true,
                RequiresQueue = false
            }
        });

        mockProvider.SetDefaultResponse(json);

        var agent = new DeploymentConfigurationAgent(_kernel, mockProvider);

        var context = new AgentExecutionContext
        {
            WorkspacePath = "/tmp",
            DryRun = true
        };

        // Act
        var result = await agent.ProcessAsync("Deploy a web application", context, CancellationToken.None);

        // Assert - Should use fallback enum value (DockerCompose), not crash
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Should succeed with fallback
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeploymentConfigurationAgent_ShouldHandleMalformedJsonFromLlm()
    {
        // Arrange - Provider returns completely invalid JSON
        var mockProvider = new MockLlmProvider();
        mockProvider.SetDefaultResponse("This is not JSON at all!");

        var agent = new DeploymentConfigurationAgent(_kernel, mockProvider);

        var context = new AgentExecutionContext
        {
            WorkspacePath = "/tmp",
            DryRun = true
        };

        // Act
        var result = await agent.ProcessAsync("Deploy something", context, CancellationToken.None);

        // Assert - Should fail gracefully
        result.Should().NotBeNull();
        // Agent may still succeed by using heuristics, or may fail - either is acceptable
        // as long as it doesn't throw JsonException
    }

    #endregion
}

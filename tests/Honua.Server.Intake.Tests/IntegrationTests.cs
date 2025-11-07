// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Intake.Controllers;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace Honua.Server.Intake.Tests;

/// <summary>
/// Integration tests for the complete intake flow - from conversation start to build trigger.
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationTests
{
    [Fact]
    public async Task FullIntakeFlow_StartToTriggerBuild_CompletesSuccessfully()
    {
        // Arrange
        var mockHttpHandler = new MockHttpMessageHandler();
        var mockHttpFactory = new Mock<IHttpClientFactory>();
        mockHttpFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(mockHttpHandler.ToHttpClient());

        var conversationStore = new InMemoryConversationStore();
        var mockManifestGenerator = new Mock<IManifestGenerator>();
        var mockRegistryProvisioner = new Mock<IRegistryProvisioner>();
        var mockBuildDeliveryService = new Mock<IBuildDeliveryService>();

        var agentOptions = new IntakeAgentOptions
        {
            Provider = "openai",
            OpenAIApiKey = "test-key",
            OpenAIModel = "gpt-4-turbo-preview"
        };

        var intakeAgent = new IntakeAgent(
            mockHttpFactory.Object,
            conversationStore,
            Options.Create(agentOptions),
            Mock.Of<ILogger<IntakeAgent>>());

        var controller = new IntakeController(
            intakeAgent,
            mockManifestGenerator.Object,
            mockRegistryProvisioner.Object,
            mockBuildDeliveryService.Object,
            Mock.Of<ILogger<IntakeController>>());

        // Step 1: Start conversation
        var startResponse = await controller.StartConversation(
            new StartConversationRequest { CustomerId = "customer-integration-test" },
            CancellationToken.None);

        var conversationResponse = (startResponse.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value as ConversationResponse;
        conversationResponse.Should().NotBeNull();
        var conversationId = conversationResponse!.ConversationId;

        // Step 2: Send first message
        var openAIResponse1 = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = "Great! What cloud provider would you like to use?"
                    }
                }
            }
        };

        mockHttpHandler
            .When("https://api.openai.com/v1/chat/completions")
            .Respond("application/json", JsonSerializer.Serialize(openAIResponse1));

        var message1Response = await controller.SendMessage(
            new SendMessageRequest
            {
                ConversationId = conversationId,
                Message = "I need WFS and PostGIS support"
            },
            CancellationToken.None);

        var intakeResponse1 = (message1Response.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value as IntakeResponse;
        intakeResponse1.Should().NotBeNull();
        intakeResponse1!.IntakeComplete.Should().BeFalse();

        // Step 3: Complete the intake
        var functionArgs = new
        {
            protocols = new[] { "WFS", "WMS" },
            databases = new[] { "PostGIS" },
            cloudProvider = "aws",
            architecture = "linux-x64",
            tier = "pro",
            expectedLoad = new
            {
                concurrentUsers = 100,
                requestsPerSecond = 50.0,
                classification = "moderate"
            }
        };

        var openAIResponse2 = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = "Perfect! I have all the information I need.",
                        function_call = new
                        {
                            name = "complete_intake",
                            arguments = JsonSerializer.Serialize(functionArgs)
                        }
                    }
                }
            }
        };

        mockHttpHandler
            .When("https://api.openai.com/v1/chat/completions")
            .Respond("application/json", JsonSerializer.Serialize(openAIResponse2));

        var message2Response = await controller.SendMessage(
            new SendMessageRequest
            {
                ConversationId = conversationId,
                Message = "AWS, Pro tier, moderate load"
            },
            CancellationToken.None);

        var intakeResponse2 = (message2Response.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value as IntakeResponse;
        intakeResponse2.Should().NotBeNull();
        intakeResponse2!.IntakeComplete.Should().BeTrue();
        intakeResponse2.Requirements.Should().NotBeNull();

        // Step 4: Trigger build
        var manifest = new BuildManifest
        {
            Version = "1.0",
            Name = "honua-server-pro",
            Architecture = "linux-x64",
            Modules = new List<string> { "WFS", "WMS", "PostGIS" },
            Tier = "pro",
            GeneratedAt = DateTimeOffset.UtcNow
        };

        mockManifestGenerator
            .Setup(x => x.GenerateAsync(It.IsAny<BuildRequirements>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);

        var registryResult = new RegistryProvisioningResult
        {
            Success = true,
            RegistryType = RegistryType.AwsEcr,
            CustomerId = "customer-integration-test",
            Namespace = "honua/customer-integration-test",
            ProvisionedAt = DateTimeOffset.UtcNow
        };

        mockRegistryProvisioner
            .Setup(x => x.ProvisionAsync("customer-integration-test", RegistryType.AwsEcr, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registryResult);

        var buildResponse = await controller.TriggerBuild(
            new TriggerBuildRequest
            {
                ConversationId = conversationId,
                CustomerId = "customer-integration-test",
                BuildName = "my-honua-server"
            },
            CancellationToken.None);

        // Assert
        var triggerResponse = (buildResponse.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value as TriggerBuildResponse;
        triggerResponse.Should().NotBeNull();
        triggerResponse!.Success.Should().BeTrue();
        triggerResponse.BuildId.Should().NotBeNullOrEmpty();
        triggerResponse.Manifest.Should().NotBeNull();
        triggerResponse.Manifest!.Name.Should().Be("honua-server-pro");
        triggerResponse.RegistryResult.Should().NotBeNull();
        triggerResponse.RegistryResult!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task FullIntakeFlow_WithErrorHandling_RecoversGracefully()
    {
        // Arrange
        var mockHttpHandler = new MockHttpMessageHandler();
        var mockHttpFactory = new Mock<IHttpClientFactory>();
        mockHttpFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(mockHttpHandler.ToHttpClient());

        var conversationStore = new InMemoryConversationStore();
        var mockManifestGenerator = new Mock<IManifestGenerator>();
        var mockRegistryProvisioner = new Mock<IRegistryProvisioner>();
        var mockBuildDeliveryService = new Mock<IBuildDeliveryService>();

        var agentOptions = new IntakeAgentOptions
        {
            Provider = "openai",
            OpenAIApiKey = "test-key"
        };

        var intakeAgent = new IntakeAgent(
            mockHttpFactory.Object,
            conversationStore,
            Options.Create(agentOptions),
            Mock.Of<ILogger<IntakeAgent>>());

        var controller = new IntakeController(
            intakeAgent,
            mockManifestGenerator.Object,
            mockRegistryProvisioner.Object,
            mockBuildDeliveryService.Object,
            Mock.Of<ILogger<IntakeController>>());

        // Start conversation
        var startResponse = await controller.StartConversation(null, CancellationToken.None);
        var conversationResponse = (startResponse.Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value as ConversationResponse;
        var conversationId = conversationResponse!.ConversationId;

        // Try to send message to wrong conversation
        var invalidMessageResponse = await controller.SendMessage(
            new SendMessageRequest
            {
                ConversationId = "nonexistent",
                Message = "Test"
            },
            CancellationToken.None);

        // Assert - Should handle error gracefully
        invalidMessageResponse.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.NotFoundObjectResult>();

        // Verify original conversation is still accessible
        var getConversationResponse = await controller.GetConversation(conversationId, CancellationToken.None);
        getConversationResponse.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
    }

    [Fact]
    public async Task FullIntakeFlow_MultipleConversations_IsolatesCorrectly()
    {
        // Arrange
        var mockHttpHandler = new MockHttpMessageHandler();
        var mockHttpFactory = new Mock<IHttpClientFactory>();
        mockHttpFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(mockHttpHandler.ToHttpClient());

        var conversationStore = new InMemoryConversationStore();

        var agentOptions = new IntakeAgentOptions
        {
            Provider = "openai",
            OpenAIApiKey = "test-key"
        };

        var intakeAgent = new IntakeAgent(
            mockHttpFactory.Object,
            conversationStore,
            Options.Create(agentOptions),
            Mock.Of<ILogger<IntakeAgent>>());

        // Start two conversations
        var conv1Response = await intakeAgent.StartConversationAsync("customer-1");
        var conv2Response = await intakeAgent.StartConversationAsync("customer-2");

        conv1Response.ConversationId.Should().NotBe(conv2Response.ConversationId);
        conv1Response.CustomerId.Should().Be("customer-1");
        conv2Response.CustomerId.Should().Be("customer-2");

        // Verify conversations are isolated
        var retrieved1 = await intakeAgent.GetConversationAsync(conv1Response.ConversationId);
        var retrieved2 = await intakeAgent.GetConversationAsync(conv2Response.ConversationId);

        retrieved1!.CustomerId.Should().Be("customer-1");
        retrieved2!.CustomerId.Should().Be("customer-2");
    }
}

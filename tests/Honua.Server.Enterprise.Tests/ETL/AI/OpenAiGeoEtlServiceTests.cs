// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL.AI;

/// <summary>
/// Tests for OpenAI-based GeoETL AI service
/// </summary>
public class OpenAiGeoEtlServiceTests
{
    private readonly OpenAiConfiguration _testConfig = new()
    {
        ApiKey = "test-api-key",
        Model = "gpt-4",
        IsAzure = false
    };

    [Fact]
    public async Task GenerateWorkflowAsync_WithValidPrompt_ShouldReturnWorkflow()
    {
        // Arrange
        var mockResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = @"{
                            ""metadata"": {
                                ""name"": ""Test Workflow"",
                                ""description"": ""A test workflow"",
                                ""category"": ""Testing""
                            },
                            ""nodes"": [
                                {
                                    ""id"": ""source-1"",
                                    ""type"": ""data_source.file"",
                                    ""name"": ""Source"",
                                    ""parameters"": {}
                                }
                            ],
                            ""edges"": []
                        }"
                    }
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var service = new OpenAiGeoEtlService(httpClient, _testConfig, NullLogger<OpenAiGeoEtlService>.Instance);

        // Act
        var result = await service.GenerateWorkflowAsync(
            "Create a test workflow",
            Guid.NewGuid(),
            Guid.NewGuid());

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Workflow);
        Assert.Equal("Test Workflow", result.Workflow.Metadata.Name);
        Assert.Single(result.Workflow.Nodes);
    }

    [Fact]
    public async Task GenerateWorkflowAsync_WithEmptyPrompt_ShouldReturnError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var service = new OpenAiGeoEtlService(httpClient, _testConfig, NullLogger<OpenAiGeoEtlService>.Instance);

        // Act
        var result = await service.GenerateWorkflowAsync("", Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateWorkflowAsync_WithApiError_ShouldReturnError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.BadRequest, "API Error");
        var service = new OpenAiGeoEtlService(httpClient, _testConfig, NullLogger<OpenAiGeoEtlService>.Instance);

        // Act
        var result = await service.GenerateWorkflowAsync(
            "Test prompt",
            Guid.NewGuid(),
            Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("AI service error", result.ErrorMessage);
    }

    [Fact]
    public async Task IsAvailableAsync_WithValidConfig_ShouldReturnTrue()
    {
        // Arrange
        var mockResponse = new
        {
            choices = new[] { new { message = new { content = "pong" } } }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var service = new OpenAiGeoEtlService(httpClient, _testConfig, NullLogger<OpenAiGeoEtlService>.Instance);

        // Act
        var isAvailable = await service.IsAvailableAsync();

        // Assert
        Assert.True(isAvailable);
    }

    [Fact]
    public async Task IsAvailableAsync_WithNoApiKey_ShouldReturnFalse()
    {
        // Arrange
        var config = new OpenAiConfiguration { ApiKey = "" };
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var service = new OpenAiGeoEtlService(httpClient, config, NullLogger<OpenAiGeoEtlService>.Instance);

        // Act
        var isAvailable = await service.IsAvailableAsync();

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public async Task IsAvailableAsync_WithApiError_ShouldReturnFalse()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, "Unauthorized");
        var service = new OpenAiGeoEtlService(httpClient, _testConfig, NullLogger<OpenAiGeoEtlService>.Instance);

        // Act
        var isAvailable = await service.IsAvailableAsync();

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public void OpenAiConfiguration_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var config = new OpenAiConfiguration();

        // Assert
        Assert.Equal("gpt-4", config.Model);
        Assert.False(config.IsAzure);
        Assert.Equal("2024-02-15-preview", config.ApiVersion);
    }

    [Fact]
    public void OpenAiConfiguration_AzureSettings_ShouldBeConfigurable()
    {
        // Arrange & Act
        var config = new OpenAiConfiguration
        {
            ApiKey = "test-key",
            Model = "gpt-35-turbo",
            IsAzure = true,
            Endpoint = "https://test.openai.azure.com",
            ApiVersion = "2023-05-15"
        };

        // Assert
        Assert.True(config.IsAzure);
        Assert.Equal("https://test.openai.azure.com", config.Endpoint);
        Assert.Equal("2023-05-15", config.ApiVersion);
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseContent)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        return new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
    }
}

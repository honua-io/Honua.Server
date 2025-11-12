// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models;
using Honua.Server.Core.Maps.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Honua.Server.Core.Tests.Maps.AI;

public class OpenAiMapGenerationServiceTests
{
    private readonly Mock<ILogger<OpenAiMapGenerationService>> _loggerMock;
    private readonly MapAiConfiguration _config;

    public OpenAiMapGenerationServiceTests()
    {
        _loggerMock = new Mock<ILogger<OpenAiMapGenerationService>>();
        _config = new MapAiConfiguration
        {
            ApiKey = "test-api-key",
            Model = "gpt-4",
            IsAzure = false
        };
    }

    [Fact]
    public async Task GenerateMapAsync_WithValidPrompt_ReturnsSuccessResult()
    {
        // Arrange
        var mockResponse = new
        {
            id = "test-id",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = JsonSerializer.Serialize(new
                        {
                            name = "Test Map",
                            description = "A test map",
                            settings = new
                            {
                                style = "maplibre://honua/streets",
                                center = new[] { -122.4194, 37.7749 },
                                zoom = 12,
                                pitch = 0,
                                bearing = 0,
                                projection = "mercator"
                            },
                            layers = new[]
                            {
                                new
                                {
                                    name = "Test Layer",
                                    type = "Vector",
                                    source = "grpc://api.honua.io/test",
                                    visible = true,
                                    opacity = 1.0,
                                    style = new { }
                                }
                            },
                            controls = new[]
                            {
                                new
                                {
                                    type = "Navigation",
                                    position = "top-right",
                                    visible = true
                                }
                            }
                        })
                    },
                    finish_reason = "stop"
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var service = new OpenAiMapGenerationService(httpClient, _config, _loggerMock.Object);

        // Act
        var result = await service.GenerateMapAsync("Show me test data", "test-user");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.MapConfiguration);
        Assert.Equal("Test Map", result.MapConfiguration.Name);
        Assert.Single(result.MapConfiguration.Layers);
        Assert.Single(result.MapConfiguration.Controls);
    }

    [Fact]
    public async Task GenerateMapAsync_WithEmptyPrompt_ReturnsFailure()
    {
        // Arrange
        var httpClient = new HttpClient();
        var service = new OpenAiMapGenerationService(httpClient, _config, _loggerMock.Object);

        // Act
        var result = await service.GenerateMapAsync("", "test-user");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Prompt cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateMapAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, new { error = "Invalid API key" });
        var service = new OpenAiMapGenerationService(httpClient, _config, _loggerMock.Object);

        // Act
        var result = await service.GenerateMapAsync("Show me test data", "test-user");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("AI service error", result.ErrorMessage);
    }

    [Fact]
    public async Task ExplainMapAsync_WithValidMap_ReturnsExplanation()
    {
        // Arrange
        var mockResponse = new
        {
            id = "test-id",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "This map shows test data with interactive controls."
                    },
                    finish_reason = "stop"
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var service = new OpenAiMapGenerationService(httpClient, _config, _loggerMock.Object);

        var mapConfig = new MapConfiguration
        {
            Name = "Test Map",
            Settings = new MapSettings
            {
                Style = "maplibre://honua/streets",
                Center = new[] { -122.4194, 37.7749 },
                Zoom = 12
            }
        };

        // Act
        var result = await service.ExplainMapAsync(mapConfig);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Explanation);
    }

    [Fact]
    public async Task SuggestImprovementsAsync_WithValidMap_ReturnsSuggestions()
    {
        // Arrange
        var mockResponse = new
        {
            id = "test-id",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = JsonSerializer.Serialize(new
                        {
                            suggestions = new[]
                            {
                                new
                                {
                                    type = "performance",
                                    description = "Add clustering for better performance",
                                    priority = 4,
                                    implementation = "Use cluster layer type"
                                }
                            }
                        })
                    },
                    finish_reason = "stop"
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var service = new OpenAiMapGenerationService(httpClient, _config, _loggerMock.Object);

        var mapConfig = new MapConfiguration
        {
            Name = "Test Map",
            Settings = new MapSettings
            {
                Style = "maplibre://honua/streets",
                Center = new[] { -122.4194, 37.7749 },
                Zoom = 12
            }
        };

        // Act
        var result = await service.SuggestImprovementsAsync(mapConfig);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Suggestions);
    }

    [Fact]
    public async Task IsAvailableAsync_WithValidConfig_ReturnsTrue()
    {
        // Arrange
        var mockResponse = new
        {
            id = "test-id",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "pong" },
                    finish_reason = "stop"
                }
            }
        };

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, mockResponse);
        var service = new OpenAiMapGenerationService(httpClient, _config, _loggerMock.Object);

        // Act
        var result = await service.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WithInvalidApiKey_ReturnsFalse()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, new { error = "Invalid API key" });
        var service = new OpenAiMapGenerationService(httpClient, _config, _loggerMock.Object);

        // Act
        var result = await service.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WithEmptyApiKey_ReturnsFalse()
    {
        // Arrange
        var config = new MapAiConfiguration { ApiKey = "" };
        var httpClient = new HttpClient();
        var service = new OpenAiMapGenerationService(httpClient, config, _loggerMock.Object);

        // Act
        var result = await service.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MapAiConfiguration_DefaultValues_AreSet()
    {
        // Arrange & Act
        var config = new MapAiConfiguration();

        // Assert
        Assert.Equal(string.Empty, config.ApiKey);
        Assert.Equal("gpt-4", config.Model);
        Assert.False(config.IsAzure);
        Assert.Equal(string.Empty, config.Endpoint);
        Assert.Equal("2024-02-15-preview", config.ApiVersion);
    }

    [Fact]
    public void MapGenerationResult_Succeed_CreatesSuccessResult()
    {
        // Arrange
        var mapConfig = new MapConfiguration
        {
            Name = "Test Map",
            Settings = new MapSettings
            {
                Style = "maplibre://honua/streets",
                Center = new[] { -122.4194, 37.7749 },
                Zoom = 12
            }
        };

        // Act
        var result = MapGenerationResult.Succeed(mapConfig, "Test explanation", 0.9);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(mapConfig, result.MapConfiguration);
        Assert.Equal("Test explanation", result.Explanation);
        Assert.Equal(0.9, result.Confidence);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void MapGenerationResult_Fail_CreatesFailureResult()
    {
        // Arrange & Act
        var result = MapGenerationResult.Fail("Test error");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Test error", result.ErrorMessage);
        Assert.Null(result.MapConfiguration);
    }

    [Fact]
    public void MapGenerationPromptTemplates_GetExamplePrompts_ReturnsPrompts()
    {
        // Act
        var prompts = MapGenerationPromptTemplates.GetExamplePrompts();

        // Assert
        Assert.NotEmpty(prompts);
        Assert.All(prompts, prompt => Assert.False(string.IsNullOrWhiteSpace(prompt)));
    }

    [Fact]
    public void MapGenerationPromptTemplates_GetSystemPrompt_ReturnsPrompt()
    {
        // Act
        var prompt = MapGenerationPromptTemplates.GetSystemPrompt();

        // Assert
        Assert.NotEmpty(prompt);
        Assert.Contains("Honua", prompt);
        Assert.Contains("MapLibre", prompt);
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, object responseContent)
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
                Content = new StringContent(JsonSerializer.Serialize(responseContent))
            });

        return new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
    }
}

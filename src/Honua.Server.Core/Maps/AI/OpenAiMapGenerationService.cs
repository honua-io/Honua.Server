// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Maps.AI;

/// <summary>
/// OpenAI-based implementation of map generation AI service
/// Supports both OpenAI and Azure OpenAI endpoints
/// </summary>
public class OpenAiMapGenerationService : IMapGenerationAiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiMapGenerationService> _logger;
    private readonly MapAiConfiguration _config;

    public OpenAiMapGenerationService(
        HttpClient httpClient,
        MapAiConfiguration config,
        ILogger<OpenAiMapGenerationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        if (_config.IsAzure)
        {
            _httpClient.BaseAddress = new Uri(_config.Endpoint);
            _httpClient.DefaultRequestHeaders.Add("api-key", _config.ApiKey);
        }
        else
        {
            _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        }
    }

    public async Task<MapGenerationResult> GenerateMapAsync(
        string prompt,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return MapGenerationResult.Fail("Prompt cannot be empty");
        }

        try
        {
            _logger.LogInformation("Generating map from prompt: {Prompt}", prompt);

            // Build the messages for the chat completion
            var messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = MapGenerationPromptTemplates.GetSystemPrompt() + "\n\n" + MapGenerationPromptTemplates.GetFewShotExamples()
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = MapGenerationPromptTemplates.FormatUserPrompt(prompt)
                }
            };

            var request = new OpenAiChatRequest
            {
                Model = _config.Model,
                Messages = messages,
                Temperature = 0.3, // Lower temperature for more deterministic outputs
                MaxTokens = 2500,
                ResponseFormat = new { type = "json_object" }
            };

            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var endpoint = _config.IsAzure
                ? $"openai/deployments/{_config.Model}/chat/completions?api-version={_config.ApiVersion}"
                : "chat/completions";

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, error);
                return MapGenerationResult.Fail($"AI service error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                return MapGenerationResult.Fail("No response from AI service");
            }

            var generatedContent = chatResponse.Choices[0].Message.Content;

            _logger.LogDebug("Generated map JSON: {Json}", generatedContent);

            // Parse the map configuration
            var mapConfig = ParseMapConfigurationJson(generatedContent, userId);
            if (mapConfig == null)
            {
                return MapGenerationResult.Fail("Failed to parse AI-generated map configuration");
            }

            // Extract spatial operations from the configuration
            var spatialOperations = ExtractSpatialOperations(mapConfig);

            _logger.LogInformation(
                "Successfully generated map with {LayerCount} layers and {ControlCount} controls",
                mapConfig.Layers.Count,
                mapConfig.Controls.Count);

            return MapGenerationResult.Succeed(
                mapConfig,
                mapConfig.Description ?? "AI-generated map",
                0.85,
                spatialOperations);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response from AI");
            return MapGenerationResult.Fail($"Invalid JSON from AI: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling AI service");
            return MapGenerationResult.Fail($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating map");
            return MapGenerationResult.Fail($"Error: {ex.Message}");
        }
    }

    public async Task<MapExplanationResult> ExplainMapAsync(
        MapConfiguration mapConfiguration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Explaining map: {MapId}", mapConfiguration.Id);

            var mapJson = JsonSerializer.Serialize(new
            {
                mapConfiguration.Name,
                mapConfiguration.Description,
                mapConfiguration.Settings,
                mapConfiguration.Layers,
                mapConfiguration.Controls,
                mapConfiguration.Filters
            }, new JsonSerializerOptions { WriteIndented = true });

            var messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = "You are an expert at explaining map configurations. Provide clear, concise explanations of what each map shows and how users can interact with it."
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = $@"Explain the following map configuration in plain language. Include:
1. A brief overall description (2-3 sentences)
2. Key features and layers
3. How users can interact with the map

Map Configuration:
{mapJson}"
                }
            };

            var request = new OpenAiChatRequest
            {
                Model = _config.Model,
                Messages = messages,
                Temperature = 0.5,
                MaxTokens = 1000
            };

            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var endpoint = _config.IsAzure
                ? $"openai/deployments/{_config.Model}/chat/completions?api-version={_config.ApiVersion}"
                : "chat/completions";

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, error);
                return MapExplanationResult.Fail($"AI service error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                return MapExplanationResult.Fail("No response from AI service");
            }

            var explanation = chatResponse.Choices[0].Message.Content;

            // Extract key features
            var keyFeatures = mapConfiguration.Layers.Select(l => l.Name).ToList();

            return MapExplanationResult.Succeed(explanation, keyFeatures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining map");
            return MapExplanationResult.Fail($"Error: {ex.Message}");
        }
    }

    public async Task<MapSuggestionResult> SuggestImprovementsAsync(
        MapConfiguration mapConfiguration,
        string? userFeedback = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating suggestions for map: {MapId}", mapConfiguration.Id);

            var mapJson = JsonSerializer.Serialize(new
            {
                mapConfiguration.Name,
                mapConfiguration.Description,
                mapConfiguration.Settings,
                mapConfiguration.Layers,
                mapConfiguration.Controls
            }, new JsonSerializerOptions { WriteIndented = true });

            var feedbackSection = string.IsNullOrWhiteSpace(userFeedback)
                ? ""
                : $"\n\nUser Feedback: {userFeedback}";

            var messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = @"You are an expert map designer. Analyze map configurations and suggest improvements for usability, performance, and visual design.

Return your suggestions as JSON in this format:
{
  ""suggestions"": [
    {
      ""type"": ""layer|style|filter|performance|usability"",
      ""description"": ""Clear description of the suggestion"",
      ""priority"": 1-5,
      ""implementation"": ""Optional code snippet or configuration""
    }
  ]
}"
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = $@"Analyze this map configuration and suggest 3-5 improvements:{feedbackSection}

Map Configuration:
{mapJson}

Return ONLY valid JSON."
                }
            };

            var request = new OpenAiChatRequest
            {
                Model = _config.Model,
                Messages = messages,
                Temperature = 0.6,
                MaxTokens = 1500,
                ResponseFormat = new { type = "json_object" }
            };

            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var endpoint = _config.IsAzure
                ? $"openai/deployments/{_config.Model}/chat/completions?api-version={_config.ApiVersion}"
                : "chat/completions";

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, error);
                return MapSuggestionResult.Fail($"AI service error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                return MapSuggestionResult.Fail("No response from AI service");
            }

            var suggestionsJson = chatResponse.Choices[0].Message.Content;
            var suggestionsData = JsonSerializer.Deserialize<SuggestionsWrapper>(suggestionsJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (suggestionsData?.Suggestions == null)
            {
                return MapSuggestionResult.Fail("Failed to parse suggestions from AI");
            }

            return MapSuggestionResult.Succeed(suggestionsData.Suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating suggestions");
            return MapSuggestionResult.Fail($"Error: {ex.Message}");
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            return false;
        }

        try
        {
            // Simple ping to check if service is available
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "ping" }
            };

            var request = new OpenAiChatRequest
            {
                Model = _config.Model,
                Messages = messages,
                MaxTokens = 5
            };

            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var endpoint = _config.IsAzure
                ? $"openai/deployments/{_config.Model}/chat/completions?api-version={_config.ApiVersion}"
                : "chat/completions";

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private MapConfiguration? ParseMapConfigurationJson(string json, string userId)
    {
        try
        {
            // Clean up JSON if needed (remove markdown code blocks)
            json = json.Trim();
            if (json.StartsWith("```json"))
            {
                json = json.Substring(7);
            }
            if (json.StartsWith("```"))
            {
                json = json.Substring(3);
            }
            if (json.EndsWith("```"))
            {
                json = json.Substring(0, json.Length - 3);
            }
            json = json.Trim();

            var mapConfig = JsonSerializer.Deserialize<MapConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (mapConfig != null)
            {
                mapConfig.Id = Guid.NewGuid().ToString();
                mapConfig.CreatedBy = userId;
                mapConfig.CreatedAt = DateTime.UtcNow;
                mapConfig.UpdatedAt = DateTime.UtcNow;
            }

            return mapConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse map configuration JSON: {Json}", json);
            return null;
        }
    }

    private List<string> ExtractSpatialOperations(MapConfiguration mapConfig)
    {
        var operations = new List<string>();

        foreach (var layer in mapConfig.Layers)
        {
            if (layer.Source.Contains("spatial_filter="))
            {
                var filterPart = layer.Source.Split("spatial_filter=")[1].Split('&')[0];
                operations.Add($"Spatial filter on layer '{layer.Name}': {filterPart}");
            }

            if (layer.Source.Contains("buffer"))
            {
                operations.Add($"Buffer operation on layer '{layer.Name}'");
            }

            if (layer.Source.Contains("intersect"))
            {
                operations.Add($"Intersection operation on layer '{layer.Name}'");
            }
        }

        return operations;
    }
}

/// <summary>
/// Configuration for Map AI service
/// </summary>
public class MapAiConfiguration
{
    /// <summary>
    /// OpenAI API key or Azure OpenAI key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use (e.g., gpt-4, gpt-4-turbo, gpt-3.5-turbo)
    /// </summary>
    public string Model { get; set; } = "gpt-4";

    /// <summary>
    /// Whether using Azure OpenAI instead of OpenAI
    /// </summary>
    public bool IsAzure { get; set; } = false;

    /// <summary>
    /// Azure OpenAI endpoint (if using Azure)
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI API version (if using Azure)
    /// </summary>
    public string ApiVersion { get; set; } = "2024-02-15-preview";
}

// Internal DTOs for OpenAI API
internal class OpenAiChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("response_format")]
    public object? ResponseFormat { get; set; }
}

internal class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal class OpenAiChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public Usage? Usage { get; set; }
}

internal class Choice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class Usage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

internal class SuggestionsWrapper
{
    [JsonPropertyName("suggestions")]
    public List<MapSuggestion> Suggestions { get; set; } = new();
}

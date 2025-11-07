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
using Honua.Server.Enterprise.ETL.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.AI;

/// <summary>
/// OpenAI-based implementation of GeoETL AI service
/// Supports both OpenAI and Azure OpenAI endpoints
/// </summary>
public class OpenAiGeoEtlService : IGeoEtlAiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiGeoEtlService> _logger;
    private readonly OpenAiConfiguration _config;

    public OpenAiGeoEtlService(
        HttpClient httpClient,
        OpenAiConfiguration config,
        ILogger<OpenAiGeoEtlService> logger)
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

    public async Task<WorkflowGenerationResult> GenerateWorkflowAsync(
        string prompt,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return WorkflowGenerationResult.Fail("Prompt cannot be empty");
        }

        try
        {
            _logger.LogInformation("Generating workflow from prompt: {Prompt}", prompt);

            // Build the messages for the chat completion
            var messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = GeoEtlPromptTemplates.GetSystemPrompt() + "\n\n" + GeoEtlPromptTemplates.GetFewShotExamples()
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = GeoEtlPromptTemplates.FormatUserPrompt(prompt)
                }
            };

            var request = new OpenAiChatRequest
            {
                Model = _config.Model,
                Messages = messages,
                Temperature = 0.3, // Lower temperature for more deterministic outputs
                MaxTokens = 2000,
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
                return WorkflowGenerationResult.Fail($"AI service error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                return WorkflowGenerationResult.Fail("No response from AI service");
            }

            var generatedContent = chatResponse.Choices[0].Message.Content;

            _logger.LogDebug("Generated workflow JSON: {Json}", generatedContent);

            // Parse the workflow definition
            var workflowData = ParseWorkflowJson(generatedContent);
            if (workflowData == null)
            {
                return WorkflowGenerationResult.Fail("Failed to parse AI-generated workflow");
            }

            // Create WorkflowDefinition
            var workflow = new WorkflowDefinition
            {
                TenantId = tenantId,
                CreatedBy = userId,
                Metadata = workflowData.Metadata ?? new WorkflowMetadata { Name = "Generated Workflow" },
                Nodes = workflowData.Nodes ?? new List<WorkflowNode>(),
                Edges = workflowData.Edges ?? new List<WorkflowEdge>()
            };

            _logger.LogInformation(
                "Successfully generated workflow with {NodeCount} nodes and {EdgeCount} edges",
                workflow.Nodes.Count,
                workflow.Edges.Count);

            return WorkflowGenerationResult.Succeed(
                workflow,
                workflowData.Metadata?.Description ?? "AI-generated workflow",
                0.85);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response from AI");
            return WorkflowGenerationResult.Fail($"Invalid JSON from AI: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling AI service");
            return WorkflowGenerationResult.Fail($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating workflow");
            return WorkflowGenerationResult.Fail($"Error: {ex.Message}");
        }
    }

    public async Task<WorkflowExplanationResult> ExplainWorkflowAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Explaining workflow: {WorkflowId}", workflow.Id);

            var workflowJson = JsonSerializer.Serialize(new
            {
                workflow.Metadata,
                workflow.Nodes,
                workflow.Edges
            }, new JsonSerializerOptions { WriteIndented = true });

            var messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = "You are an expert at explaining GeoETL workflows. Provide clear, concise explanations of what each workflow does and how data flows through it."
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = $@"Explain the following GeoETL workflow in plain language. Include:
1. A brief overall description (1-2 sentences)
2. A step-by-step breakdown of what happens at each node

Workflow:
{workflowJson}"
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
                return WorkflowExplanationResult.Fail($"AI service error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (chatResponse?.Choices == null || chatResponse.Choices.Count == 0)
            {
                return WorkflowExplanationResult.Fail("No response from AI service");
            }

            var explanation = chatResponse.Choices[0].Message.Content;

            // Parse into steps (simple split by newlines for now)
            var steps = explanation
                .Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .ToList();

            return WorkflowExplanationResult.Succeed(explanation, steps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining workflow");
            return WorkflowExplanationResult.Fail($"Error: {ex.Message}");
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

    private WorkflowData? ParseWorkflowJson(string json)
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

            return JsonSerializer.Deserialize<WorkflowData>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse workflow JSON: {Json}", json);
            return null;
        }
    }
}

/// <summary>
/// Configuration for OpenAI service
/// </summary>
public class OpenAiConfiguration
{
    /// <summary>
    /// OpenAI API key or Azure OpenAI key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use (e.g., gpt-4, gpt-3.5-turbo)
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

internal class WorkflowData
{
    [JsonPropertyName("metadata")]
    public WorkflowMetadata? Metadata { get; set; }

    [JsonPropertyName("nodes")]
    public List<WorkflowNode>? Nodes { get; set; }

    [JsonPropertyName("edges")]
    public List<WorkflowEdge>? Edges { get; set; }
}

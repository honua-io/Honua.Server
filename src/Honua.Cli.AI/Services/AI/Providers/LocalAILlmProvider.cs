// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// LLM provider implementation for LocalAI.
/// LocalAI is an OpenAI-compatible local inference server for running LLMs locally.
/// </summary>
public sealed class LocalAILlmProvider : LlmProviderBase
{
    private readonly LocalAIOptions _options;
    private readonly HttpClient _httpClient;

    public override string ProviderName => "LocalAI";
    public override string DefaultModel => _options.DefaultModel;

    public LocalAILlmProvider(LlmProviderOptions options, IHttpClientFactory? httpClientFactory = null, ILogger<LocalAILlmProvider>? logger = null)
        : base(options, logger ?? NullLogger<LocalAILlmProvider>.Instance)
    {
        _options = options.LocalAI;

        if (_options.EndpointUrl.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("LocalAI endpoint URL is required. Set it in configuration (LlmProvider:LocalAI:EndpointUrl).");
        }

        if (httpClientFactory != null)
        {
            _httpClient = httpClientFactory.CreateClient("LocalAI");
            _httpClient.BaseAddress = new Uri(_options.EndpointUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }
        else
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_options.EndpointUrl),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
            };
        }
    }

    protected override async Task<LlmResponse> CompleteInternalAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var messages = new List<LocalAIChatMessage>();

        // Add system prompt if provided
        if (request.SystemPrompt.HasValue())
        {
            messages.Add(new LocalAIChatMessage
            {
                Role = "system",
                Content = request.SystemPrompt
            });
        }

        // Add conversation history if provided
        if (request.ConversationHistory is not null)
        {
            foreach (var msg in request.ConversationHistory)
            {
                messages.Add(new LocalAIChatMessage
                {
                    Role = msg.Role.ToLowerInvariant(),
                    Content = msg.Content
                });
            }
        }

        // Add current user prompt
        messages.Add(new LocalAIChatMessage
        {
            Role = "user",
            Content = request.UserPrompt
        });

        // Create OpenAI-compatible chat completion request
        var apiRequest = new LocalAIChatCompletionRequest
        {
            Model = _options.DefaultModel,
            Messages = messages,
            MaxTokens = request.MaxTokens,
            Temperature = request.Temperature ?? 0.7
        };

        // Call LocalAI API with optimized JSON serialization
        var response = await _httpClient.PostAsJsonAsync(
            "/v1/chat/completions",
            apiRequest,
            CliJsonOptions.Standard,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var sanitizedError = SecretSanitizer.SanitizeErrorMessage(errorContent);
            return new LlmResponse
            {
                Content = string.Empty,
                Model = DefaultModel,
                Success = false,
                ErrorMessage = $"LocalAI API error: {response.StatusCode} - {sanitizedError}"
            };
        }

        var completionResponse = await response.Content.ReadFromJsonAsync<LocalAIChatCompletionResponse>(
            CliJsonOptions.Standard,
            cancellationToken);

        if (completionResponse?.Choices is null || completionResponse.Choices.Count == 0)
        {
            return new LlmResponse
            {
                Content = string.Empty,
                Model = DefaultModel,
                Success = false,
                ErrorMessage = "No response received from LocalAI"
            };
        }

        return new LlmResponse
        {
            Content = completionResponse.Choices[0].Message.Content ?? string.Empty,
            Model = completionResponse.Model ?? DefaultModel,
            TotalTokens = completionResponse.Usage?.TotalTokens,
            Success = true
        };
    }

    public override async IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<LocalAIChatMessage>();

        // Add system prompt if provided
        if (request.SystemPrompt.HasValue())
        {
            messages.Add(new LocalAIChatMessage
            {
                Role = "system",
                Content = request.SystemPrompt
            });
        }

        // Add conversation history if provided
        if (request.ConversationHistory is not null)
        {
            foreach (var msg in request.ConversationHistory)
            {
                messages.Add(new LocalAIChatMessage
                {
                    Role = msg.Role.ToLowerInvariant(),
                    Content = msg.Content
                });
            }
        }

        // Add current user prompt
        messages.Add(new LocalAIChatMessage
        {
            Role = "user",
            Content = request.UserPrompt
        });

        // Create OpenAI-compatible chat completion request with streaming enabled
        var apiRequest = new LocalAIChatCompletionRequest
        {
            Model = _options.DefaultModel,
            Messages = messages,
            MaxTokens = request.MaxTokens,
            Temperature = request.Temperature ?? 0.7,
            Stream = true
        };

        // Create HTTP request for streaming with optimized JSON serialization
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(apiRequest, mediaType: null, CliJsonOptions.Standard)
        };

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            yield return new LlmStreamChunk
            {
                Content = $"Error: {response.StatusCode} - {errorContent}",
                IsFinal = true,
                TokenCount = null
            };
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line.IsNullOrWhiteSpace())
                continue;

            // SSE format: "data: {json}" or "data: [DONE]"
            if (!line.StartsWith("data: "))
                continue;

            var data = line.Substring(6).Trim();

            if (data == "[DONE]")
            {
                yield return new LlmStreamChunk
                {
                    Content = string.Empty,
                    IsFinal = true,
                    TokenCount = null
                };
                break;
            }

            // Try to parse the JSON stream response with optimized deserialization
            LocalAIStreamResponse? streamResponse = null;
            try
            {
                streamResponse = JsonSerializer.Deserialize<LocalAIStreamResponse>(data, CliJsonOptions.Standard);
            }
            catch (JsonException)
            {
                // Skip malformed JSON lines
                continue;
            }

            if (streamResponse?.Choices != null && streamResponse.Choices.Count > 0)
            {
                var choice = streamResponse.Choices[0];
                var content = choice.Delta?.Content;

                if (!content.IsNullOrEmpty())
                {
                    yield return new LlmStreamChunk
                    {
                        Content = content,
                        IsFinal = false,
                        TokenCount = null
                    };
                }

                // Check if streaming is complete
                if (choice.FinishReason != null)
                {
                    yield return new LlmStreamChunk
                    {
                        Content = string.Empty,
                        IsFinal = true,
                        TokenCount = streamResponse.Usage?.TotalTokens
                    };
                }
            }
        }
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<LocalAIModelsResponse>(
                "/v1/models",
                CliJsonOptions.Standard,
                cancellationToken);

            if (response?.Data is null)
            {
                return new[] { DefaultModel };
            }

            return response.Data.Select(m => m.Id).ToList();
        }
        catch
        {
            // Fallback to default model if listing fails
            return new[] { DefaultModel };
        }
    }

    // LocalAI API DTOs (OpenAI-compatible format)
    private sealed class LocalAIChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class LocalAIChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<LocalAIChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }

        [JsonPropertyName("top_p")]
        public double? TopP { get; set; }

        [JsonPropertyName("stream")]
        public bool? Stream { get; set; }
    }

    private sealed class LocalAIChatCompletionResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("created")]
        public long? Created { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<LocalAIChoice> Choices { get; set; } = new();

        [JsonPropertyName("usage")]
        public LocalAIUsage? Usage { get; set; }
    }

    private sealed class LocalAIChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public LocalAIChatMessage Message { get; set; } = new();

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class LocalAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }

    private sealed class LocalAIModelsResponse
    {
        [JsonPropertyName("data")]
        public List<LocalAIModel> Data { get; set; } = new();
    }

    private sealed class LocalAIModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("owned_by")]
        public string? OwnedBy { get; set; }
    }

    // Streaming-specific DTOs
    private sealed class LocalAIStreamResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("created")]
        public long? Created { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<LocalAIStreamChoice> Choices { get; set; } = new();

        [JsonPropertyName("usage")]
        public LocalAIUsage? Usage { get; set; }
    }

    private sealed class LocalAIStreamChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("delta")]
        public LocalAIStreamDelta? Delta { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class LocalAIStreamDelta
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Honua.Cli.AI.Services.Security;
using Honua.Cli.AI.Serialization;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// LLM provider implementation for Ollama local inference server.
/// Ollama is a lightweight local LLM runtime that provides an OpenAI-compatible API.
/// Supports models like llama3.2, mistral, codellama, phi3, and more.
/// </summary>
/// <remarks>
/// Ollama API documentation: https://github.com/ollama/ollama/blob/main/docs/api.md
/// Default endpoint: http://localhost:11434/api
///
/// Key features:
/// - Zero-cost local inference (no API keys required)
/// - Privacy-friendly (data never leaves your machine)
/// - Support for dozens of open-source models
/// - Automatic model downloading via 'ollama pull' command
/// - Streaming support for real-time responses
/// </remarks>
public sealed class OllamaLlmProvider : LlmProviderBase
{
    private readonly OllamaOptions _options;
    private readonly HttpClient _httpClient;

    public override string ProviderName => "Ollama";
    public override string DefaultModel => _options.DefaultModel;

    /// <summary>
    /// Creates a new Ollama LLM provider instance.
    /// </summary>
    /// <param name="options">Configuration options for LLM providers</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public OllamaLlmProvider(
        LlmProviderOptions options,
        ILogger<OllamaLlmProvider>? logger = null,
        IHttpClientFactory? httpClientFactory = null)
        : base(options, logger ?? NullLogger<OllamaLlmProvider>.Instance)
    {
        _options = options.Ollama ?? throw new ArgumentNullException(nameof(options.Ollama));

        // Validate endpoint URL
        if (_options.EndpointUrl.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException(
                "Ollama endpoint URL is required. Set it in configuration (LlmProvider:Ollama:EndpointUrl) or use default 'http://localhost:11434'.");
        }

        // Create HTTP client via factory if available, otherwise create directly
        if (httpClientFactory != null)
        {
            _httpClient = httpClientFactory.CreateClient("Ollama");
            _httpClient.BaseAddress = new Uri(_options.EndpointUrl.TrimEnd('/'));
            _httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }
        else
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_options.EndpointUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
            };
        }

        Logger.LogInformation(
            "Initialized Ollama LLM provider with endpoint {Endpoint} and model {Model}",
            _options.EndpointUrl,
            _options.DefaultModel);
    }

    /// <summary>
    /// Tests whether the Ollama server is available and responding.
    /// </summary>
    public override async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the /api/tags endpoint to check if Ollama is running
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning(
                    "Ollama health check failed with status code {StatusCode}",
                    response.StatusCode);
                return false;
            }

            var modelsResponse = await response.Content.ReadFromJsonAsync<OllamaModelsResponse>(
                CliJsonOptions.Standard,
                cancellationToken);

            if (modelsResponse?.Models is null || modelsResponse.Models.Count == 0)
            {
                Logger.LogWarning("Ollama is running but no models are available. Run 'ollama pull {Model}' to download a model.", _options.DefaultModel);
                return false;
            }

            // Check if the configured model is available
            var modelExists = modelsResponse.Models.Any(m =>
                m.Name != null && m.Name.StartsWith(_options.DefaultModel, StringComparison.OrdinalIgnoreCase));

            if (!modelExists)
            {
                Logger.LogWarning(
                    "Configured model '{Model}' is not available in Ollama. Available models: {AvailableModels}. Run 'ollama pull {Model}' to download it.",
                    _options.DefaultModel,
                    string.Join(", ", modelsResponse.Models.Select(m => m.Name)),
                    _options.DefaultModel);
                return false;
            }

            Logger.LogDebug("Ollama health check passed. Model {Model} is available.", _options.DefaultModel);
            return true;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogWarning(
                ex,
                "Ollama is not reachable at {Endpoint}. Make sure Ollama is running with 'ollama serve'.",
                _options.EndpointUrl);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error checking Ollama availability");
            return false;
        }
    }

    /// <summary>
    /// Sends a completion request to Ollama and returns the full response.
    /// </summary>
    protected override async Task<LlmResponse> CompleteInternalAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        // Build the prompt from system prompt + conversation history + user prompt
        var prompt = BuildPrompt(request);

        // Create Ollama generate request
        var ollamaRequest = new OllamaGenerateRequest
        {
            Model = _options.DefaultModel,
            Prompt = prompt,
            Stream = false,
            Options = new OllamaRequestOptions
            {
                Temperature = (float?)request.Temperature ?? 0.7f,
                NumPredict = request.MaxTokens ?? ProviderOptions.DefaultMaxTokens
            }
        };

        Logger.LogDebug(
            "Sending Ollama completion request with model {Model}, temperature {Temperature}, max_tokens {MaxTokens}",
            ollamaRequest.Model,
            ollamaRequest.Options.Temperature,
            ollamaRequest.Options.NumPredict);

        // Call Ollama API with optimized JSON serialization
        var response = await _httpClient.PostAsJsonAsync(
            "/api/generate",
            ollamaRequest,
            CliJsonOptions.Standard,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var sanitizedError = SecretSanitizer.SanitizeErrorMessage(errorContent);
            Logger.LogError(
                "Ollama API returned error status {StatusCode}: {Error}",
                response.StatusCode,
                sanitizedError);

            return new LlmResponse
            {
                Content = string.Empty,
                Model = DefaultModel,
                Success = false,
                ErrorMessage = $"Ollama API error: {response.StatusCode} - {sanitizedError}"
            };
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
            CliJsonOptions.Standard,
            cancellationToken);

        if (result is null)
        {
            return new LlmResponse
            {
                Content = string.Empty,
                Model = DefaultModel,
                Success = false,
                ErrorMessage = "No response received from Ollama"
            };
        }

        Logger.LogDebug(
            "Ollama completion successful. Tokens: prompt={PromptTokens}, completion={CompletionTokens}, total={TotalTokens}",
            result.PromptEvalCount,
            result.EvalCount,
            (result.PromptEvalCount ?? 0) + (result.EvalCount ?? 0));

        return new LlmResponse
        {
            Content = result.Response ?? string.Empty,
            Model = result.Model ?? DefaultModel,
            TotalTokens = (result.PromptEvalCount ?? 0) + (result.EvalCount ?? 0),
            Success = true
        };
    }

    /// <summary>
    /// Streams a completion response from Ollama with real-time token generation.
    /// </summary>
    public override async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? response = null;
        Stream? stream = null;
        StreamReader? reader = null;

        try
        {
            // Build the prompt from system prompt + conversation history + user prompt
            var prompt = BuildPrompt(request);

            // Create Ollama generate request with streaming enabled
            var ollamaRequest = new OllamaGenerateRequest
            {
                Model = _options.DefaultModel,
                Prompt = prompt,
                Stream = true,
                Options = new OllamaRequestOptions
                {
                    Temperature = (float?)request.Temperature ?? 0.7f,
                    NumPredict = request.MaxTokens ?? ProviderOptions.DefaultMaxTokens
                }
            };

            Logger.LogDebug(
                "Sending Ollama streaming request with model {Model}, temperature {Temperature}, max_tokens {MaxTokens}",
                ollamaRequest.Model,
                ollamaRequest.Options.Temperature,
                ollamaRequest.Options.NumPredict);

            // Create HTTP request for streaming with optimized JSON serialization
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
            {
                Content = JsonContent.Create(ollamaRequest, mediaType: null, CliJsonOptions.Standard)
            };

            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError(
                    "Ollama streaming API returned error status {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);

                yield return new LlmStreamChunk
                {
                    Content = $"Error: {response.StatusCode} - {errorContent}",
                    IsFinal = true,
                    TokenCount = null
                };
                yield break;
            }

            stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            reader = new StreamReader(stream);

            var totalTokens = 0;

            // Read streaming response line by line (each line is a JSON object)
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line.IsNullOrWhiteSpace())
                    continue;

                // Parse JSON stream response with optimized deserialization
                OllamaStreamResponse? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaStreamResponse>(line, CliJsonOptions.Standard);
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning(ex, "Failed to parse Ollama stream response: {Line}", line);
                    continue;
                }

                if (chunk is null)
                    continue;

                // Yield content chunk if available
                if (!chunk.Response.IsNullOrEmpty())
                {
                    yield return new LlmStreamChunk
                    {
                        Content = chunk.Response,
                        IsFinal = false,
                        TokenCount = null
                    };
                }

                // Check if streaming is complete
                if (chunk.Done)
                {
                    totalTokens = (chunk.PromptEvalCount ?? 0) + (chunk.EvalCount ?? 0);

                    Logger.LogDebug(
                        "Ollama streaming completed. Tokens: prompt={PromptTokens}, completion={CompletionTokens}, total={TotalTokens}",
                        chunk.PromptEvalCount,
                        chunk.EvalCount,
                        totalTokens);

                    yield return new LlmStreamChunk
                    {
                        Content = string.Empty,
                        IsFinal = true,
                        TokenCount = totalTokens
                    };
                    break;
                }
            }
        }
        finally
        {
            // Ensure proper cleanup of resources
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
        }
    }

    /// <summary>
    /// Lists all models available in the Ollama installation.
    /// </summary>
    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning(
                    "Failed to list Ollama models: {StatusCode}",
                    response.StatusCode);
                return new[] { DefaultModel };
            }

            var modelsResponse = await response.Content.ReadFromJsonAsync<OllamaModelsResponse>(
                CliJsonOptions.Standard,
                cancellationToken);

            if (modelsResponse?.Models is null || modelsResponse.Models.Count == 0)
            {
                Logger.LogWarning("No models found in Ollama. Run 'ollama pull {Model}' to download models.", DefaultModel);
                return Array.Empty<string>();
            }

            var modelNames = modelsResponse.Models
                .Where(m => m.Name.HasValue())
                .Select(m => m.Name!)
                .ToList();

            Logger.LogDebug("Found {Count} Ollama models: {Models}", modelNames.Count, string.Join(", ", modelNames));

            return modelNames;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error listing Ollama models");
            return new[] { DefaultModel };
        }
    }

    /// <summary>
    /// Builds a prompt from the request components.
    /// Combines system prompt, conversation history, and user prompt into a single string.
    /// </summary>
    private static string BuildPrompt(LlmRequest request)
    {
        var parts = new List<string>();

        // Add system prompt if provided
        if (request.SystemPrompt.HasValue())
        {
            parts.Add($"System: {request.SystemPrompt}");
        }

        // Add conversation history if provided
        if (request.ConversationHistory is not null && request.ConversationHistory.Count > 0)
        {
            foreach (var msg in request.ConversationHistory)
            {
                var role = msg.Role switch
                {
                    "system" => "System",
                    "user" => "User",
                    "assistant" => "Assistant",
                    _ => msg.Role
                };

                parts.Add($"{role}: {msg.Content}");
            }
        }

        // Add current user prompt
        parts.Add($"User: {request.UserPrompt}");

        // Add assistant prefix to prompt the model to respond
        parts.Add("Assistant:");

        return string.Join("\n\n", parts);
    }

    #region Ollama API DTOs

    /// <summary>
    /// Request to Ollama /api/generate endpoint.
    /// </summary>
    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaRequestOptions? Options { get; set; }
    }

    /// <summary>
    /// Ollama model configuration options for API requests.
    /// </summary>
    private sealed class OllamaRequestOptions
    {
        [JsonPropertyName("temperature")]
        public float? Temperature { get; set; }

        [JsonPropertyName("top_p")]
        public float? TopP { get; set; }

        [JsonPropertyName("num_predict")]
        public int? NumPredict { get; set; }
    }

    /// <summary>
    /// Response from Ollama /api/generate endpoint (non-streaming).
    /// </summary>
    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }
    }

    /// <summary>
    /// Streaming response chunk from Ollama /api/generate endpoint.
    /// </summary>
    private sealed class OllamaStreamResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }
    }

    /// <summary>
    /// Response from Ollama /api/tags endpoint (model list).
    /// </summary>
    private sealed class OllamaModelsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel> Models { get; set; } = new();
    }

    /// <summary>
    /// Model information from Ollama.
    /// </summary>
    private sealed class OllamaModel
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("modified_at")]
        public string? ModifiedAt { get; set; }
    }

    #endregion
}

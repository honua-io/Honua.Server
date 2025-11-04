// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// Embedding provider implementation for LocalAI.
/// LocalAI is an OpenAI-compatible local inference server for generating embeddings locally.
/// </summary>
public sealed class LocalAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly LocalAIOptions _options;
    private readonly HttpClient _httpClient;

    public string ProviderName => "LocalAI";
    public string DefaultModel => _options.DefaultEmbeddingModel;
    public int Dimensions { get; }

    public LocalAIEmbeddingProvider(LlmProviderOptions options, int dimensions = 1536, IHttpClientFactory? httpClientFactory = null)
    {
        _options = options.LocalAI;
        Dimensions = dimensions;

        if (_options.EndpointUrl.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("LocalAI endpoint URL is required. Set it in configuration (LlmProvider:LocalAI:EndpointUrl).");
        }

        if (httpClientFactory != null)
        {
            _httpClient = httpClientFactory.CreateClient("LocalAI-Embedding");
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

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test with a minimal request
            var response = await GetEmbeddingAsync("test", cancellationToken);
            return response.Success;
        }
        catch
        {
            return false;
        }
    }

    public async Task<EmbeddingResponse> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            if (text.IsNullOrWhiteSpace())
            {
                throw new ArgumentException("Text cannot be null or whitespace", nameof(text));
            }

            var request = new LocalAIEmbeddingRequest
            {
                Model = _options.DefaultEmbeddingModel,
                Input = text
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/v1/embeddings",
                request,
                CliJsonOptions.Standard,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new EmbeddingResponse
                {
                    Embedding = Array.Empty<float>(),
                    Model = DefaultModel,
                    Success = false,
                    ErrorMessage = $"LocalAI API error: {response.StatusCode} - {errorContent}"
                };
            }

            var embeddingResponse = await response.Content.ReadFromJsonAsync<LocalAIEmbeddingResponse>(
                CliJsonOptions.Standard,
                cancellationToken);

            if (embeddingResponse?.Data is null || embeddingResponse.Data.Count == 0)
            {
                return new EmbeddingResponse
                {
                    Embedding = Array.Empty<float>(),
                    Model = DefaultModel,
                    Success = false,
                    ErrorMessage = "No response received from LocalAI"
                };
            }

            return new EmbeddingResponse
            {
                Embedding = embeddingResponse.Data[0].Embedding.ToArray(),
                Model = embeddingResponse.Model ?? DefaultModel,
                TotalTokens = embeddingResponse.Usage?.TotalTokens,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = DefaultModel,
                Success = false,
                ErrorMessage = $"LocalAI embedding error: {ex.Message}"
            };
        }
    }

    public async Task<IReadOnlyList<EmbeddingResponse>> GetEmbeddingBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (texts is null || texts.Count == 0)
            {
                throw new ArgumentException("Texts cannot be null or empty", nameof(texts));
            }

            var request = new LocalAIEmbeddingRequest
            {
                Model = _options.DefaultEmbeddingModel,
                Input = texts.ToList()
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/v1/embeddings",
                request,
                CliJsonOptions.Standard,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return texts.Select(_ => new EmbeddingResponse
                {
                    Embedding = Array.Empty<float>(),
                    Model = DefaultModel,
                    Success = false,
                    ErrorMessage = $"LocalAI API error: {response.StatusCode} - {errorContent}"
                }).ToList();
            }

            var embeddingResponse = await response.Content.ReadFromJsonAsync<LocalAIEmbeddingResponse>(
                CliJsonOptions.Standard,
                cancellationToken);

            if (embeddingResponse?.Data is null)
            {
                return texts.Select(_ => new EmbeddingResponse
                {
                    Embedding = Array.Empty<float>(),
                    Model = DefaultModel,
                    Success = false,
                    ErrorMessage = "No response received from LocalAI"
                }).ToList();
            }

            var results = new List<EmbeddingResponse>();
            foreach (var data in embeddingResponse.Data.OrderBy(d => d.Index))
            {
                results.Add(new EmbeddingResponse
                {
                    Embedding = data.Embedding.ToArray(),
                    Model = embeddingResponse.Model ?? DefaultModel,
                    TotalTokens = null,
                    Success = true
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            return texts.Select(_ => new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = DefaultModel,
                Success = false,
                ErrorMessage = $"LocalAI batch embedding error: {ex.Message}"
            }).ToList();
        }
    }

    // LocalAI API DTOs (OpenAI-compatible format)
    private sealed class LocalAIEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public object Input { get; set; } = string.Empty;
    }

    private sealed class LocalAIEmbeddingResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("data")]
        public List<LocalAIEmbeddingData> Data { get; set; } = new();

        [JsonPropertyName("usage")]
        public LocalAIUsage? Usage { get; set; }
    }

    private sealed class LocalAIEmbeddingData
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public List<float> Embedding { get; set; } = new();
    }

    private sealed class LocalAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }
}

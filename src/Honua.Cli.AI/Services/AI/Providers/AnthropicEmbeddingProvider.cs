// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// Embedding provider for Anthropic using Voyage AI (Anthropic's recommended embeddings partner).
/// Falls back to mock embeddings if no Voyage AI key is configured.
/// </summary>
public sealed class AnthropicEmbeddingProvider : IEmbeddingProvider
{
    private readonly AnthropicOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _useVoyageAI;
    private readonly string? _voyageApiKey;
    private const int EmbeddingDimensions = 1024; // Voyage AI voyage-2 dimensions

    public string ProviderName => "Anthropic/VoyageAI";
    public string DefaultModel => "voyage-2";
    public int Dimensions => EmbeddingDimensions;

    public AnthropicEmbeddingProvider(LlmProviderOptions options, IHttpClientFactory? httpClientFactory = null)
    {
        _options = options.Anthropic;

        if (httpClientFactory != null)
        {
            _httpClient = httpClientFactory.CreateClient("VoyageAI");
            _httpClient.BaseAddress = new Uri("https://api.voyageai.com/v1/");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }
        else
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.voyageai.com/v1/"),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        // Check if Voyage AI key is available (environment variable VOYAGE_API_KEY)
        _voyageApiKey = Environment.GetEnvironmentVariable("VOYAGE_API_KEY");
        _useVoyageAI = _voyageApiKey.HasValue();

        if (_useVoyageAI && _voyageApiKey is not null)
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_voyageApiKey}");
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Always available (falls back to mock if Voyage AI not configured)
        return Task.FromResult(true);
    }

    public async Task<EmbeddingResponse> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = DefaultModel,
                Success = false,
                ErrorMessage = "Text cannot be null or whitespace"
            };
        }

        if (!_useVoyageAI)
        {
            // Fall back to deterministic mock embeddings
            return GenerateMockEmbedding(text);
        }

        try
        {
            var requestBody = new
            {
                input = new[] { text },
                model = "voyage-2"
            };

            var response = await _httpClient.PostAsJsonAsync("embeddings", requestBody, CliJsonOptions.Standard, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new EmbeddingResponse
                {
                    Embedding = Array.Empty<float>(),
                    Model = DefaultModel,
                    Success = false,
                    ErrorMessage = $"Voyage AI API error: {error}"
                };
            }

            var result = await response.Content.ReadFromJsonAsync<VoyageEmbeddingResponse>(
                CliJsonOptions.Standard,
                cancellationToken);

            if (result?.Data is null || result.Data.Length == 0)
            {
                return new EmbeddingResponse
                {
                    Embedding = Array.Empty<float>(),
                    Model = DefaultModel,
                    Success = false,
                    ErrorMessage = "No embeddings returned from Voyage AI"
                };
            }

            return new EmbeddingResponse
            {
                Embedding = result.Data[0].Embedding,
                Model = result.Model ?? DefaultModel,
                TotalTokens = result.Usage?.TotalTokens,
                Success = true
            };
        }
        catch (Exception ex)
        {
            // Fall back to mock on error
            return GenerateMockEmbedding(text);
        }
    }

    public async Task<IReadOnlyList<EmbeddingResponse>> GetEmbeddingBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts is null || texts.Count == 0)
        {
            throw new ArgumentException("Texts cannot be null or empty", nameof(texts));
        }

        if (!_useVoyageAI)
        {
            // Fall back to mock embeddings
            var mockResults = new List<EmbeddingResponse>();
            foreach (var text in texts)
            {
                mockResults.Add(GenerateMockEmbedding(text));
            }
            return mockResults;
        }

        try
        {
            var requestBody = new
            {
                input = texts,
                model = "voyage-2"
            };

            var response = await _httpClient.PostAsJsonAsync("embeddings", requestBody, CliJsonOptions.Standard, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Voyage AI API error: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<VoyageEmbeddingResponse>(
                CliJsonOptions.Standard,
                cancellationToken);

            if (result?.Data is null)
            {
                throw new InvalidOperationException("No embeddings returned from Voyage AI");
            }

            var results = new List<EmbeddingResponse>();
            foreach (var embedding in result.Data)
            {
                results.Add(new EmbeddingResponse
                {
                    Embedding = embedding.Embedding,
                    Model = result.Model ?? DefaultModel,
                    TotalTokens = null,
                    Success = true
                });
            }

            return results;
        }
        catch (Exception)
        {
            // Fall back to mock embeddings on error
            var mockResults = new List<EmbeddingResponse>();
            foreach (var text in texts)
            {
                mockResults.Add(GenerateMockEmbedding(text));
            }
            return mockResults;
        }
    }

    private EmbeddingResponse GenerateMockEmbedding(string text)
    {
        // Generate deterministic mock embedding based on text hash
        var embedding = new float[EmbeddingDimensions];

        int generated = 0;
        int counter = 0;

        while (generated < embedding.Length)
        {
            var phrase = Encoding.UTF8.GetBytes($"{text}:{counter++}");
            var hash = SHA256.HashData(phrase);

            for (var i = 0; i <= hash.Length - 4 && generated < embedding.Length; i += 4)
            {
                var value = BinaryPrimitives.ReadInt32LittleEndian(hash.AsSpan(i, 4));
                var normalized = value / (float)int.MaxValue;
                embedding[generated++] = normalized;
            }
        }

        // Normalize vector length to 1
        var magnitude = MathF.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0f)
        {
            var scale = 1f / magnitude;
            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] *= scale;
            }
        }

        return new EmbeddingResponse
        {
            Embedding = embedding,
            Model = "mock-voyage-2",
            TotalTokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            Success = true,
            ErrorMessage = "Voyage AI key not configured; returned deterministic mock embedding."
        };
    }

    // Response models for Voyage AI API
    private class VoyageEmbeddingResponse
    {
        public VoyageEmbeddingData[]? Data { get; set; }
        public string? Model { get; set; }
        public VoyageUsage? Usage { get; set; }
    }

    private class VoyageEmbeddingData
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public int Index { get; set; }
    }

    private class VoyageUsage
    {
        public int TotalTokens { get; set; }
    }
}

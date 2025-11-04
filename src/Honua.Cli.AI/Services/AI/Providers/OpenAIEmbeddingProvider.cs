// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Embeddings;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// Embedding provider implementation for OpenAI.
/// Generates vector embeddings for semantic search and RAG.
/// </summary>
public sealed class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly OpenAIOptions _options;
    private readonly EmbeddingClient _embeddingClient;

    private const string DefaultEmbeddingModel = "text-embedding-3-small";

    public string ProviderName => "OpenAI";
    public string DefaultModel => DefaultEmbeddingModel;
    public int Dimensions => 1536; // text-embedding-3-small has 1536 dimensions

    public OpenAIEmbeddingProvider(LlmProviderOptions options)
    {
        _options = options.OpenAI;

        if (_options.ApiKey.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("OpenAI API key is required. Set it in configuration (LlmProvider:OpenAI:ApiKey) or environment variable OPENAI_API_KEY.");
        }

        // Create OpenAI client with API key
        var client = new OpenAIClient(_options.ApiKey);

        _embeddingClient = client.GetEmbeddingClient(DefaultEmbeddingModel);
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

            var response = await _embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);

            if (response?.Value is null)
            {
                return new EmbeddingResponse
                {
                    Embedding = Array.Empty<float>(),
                    Model = DefaultModel,
                    Success = false,
                    ErrorMessage = "No response received from OpenAI"
                };
            }

            return new EmbeddingResponse
            {
                Embedding = response.Value.ToFloats().ToArray(),
                Model = DefaultModel,
                TotalTokens = null, // SDK doesn't expose usage for single embedding
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
                ErrorMessage = $"OpenAI embedding error: {ex.Message}"
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

            var response = await _embeddingClient.GenerateEmbeddingsAsync(texts, cancellationToken: cancellationToken);

            if (response?.Value is null)
            {
                return texts.Select(_ => new EmbeddingResponse
                {
                    Embedding = Array.Empty<float>(),
                    Model = DefaultModel,
                    Success = false,
                    ErrorMessage = "No response received from OpenAI"
                }).ToList();
            }

            var embeddings = response.Value.ToList();
            var results = new List<EmbeddingResponse>();

            for (int i = 0; i < embeddings.Count; i++)
            {
                results.Add(new EmbeddingResponse
                {
                    Embedding = embeddings[i].ToFloats().ToArray(),
                    Model = DefaultModel,
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
                ErrorMessage = $"OpenAI batch embedding error: {ex.Message}"
            }).ToList();
        }
    }
}

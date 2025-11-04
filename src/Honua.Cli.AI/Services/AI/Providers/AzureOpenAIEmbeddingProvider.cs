// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI.Embeddings;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// Embedding provider implementation for Azure OpenAI Service.
/// Generates vector embeddings for semantic search and RAG.
/// </summary>
public sealed class AzureOpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly AzureOpenAIOptions _options;
    private readonly EmbeddingClient _embeddingClient;

    public string ProviderName => "AzureOpenAI";
    public string DefaultModel => _options.DefaultEmbeddingModel;
    public int Dimensions => 3072; // text-embedding-3-large has 3072 dimensions

    public AzureOpenAIEmbeddingProvider(LlmProviderOptions options)
    {
        _options = options.Azure;

        if (_options.EndpointUrl.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Azure OpenAI endpoint URL is required. Set it in configuration (LlmProvider:Azure:EndpointUrl).");
        }

        if (_options.ApiKey.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Azure OpenAI API key is required. Set it in configuration (LlmProvider:Azure:ApiKey) or use Managed Identity.");
        }

        if (_options.EmbeddingDeploymentName.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Azure OpenAI embedding deployment name is required. Set it in configuration (LlmProvider:Azure:EmbeddingDeploymentName).");
        }

        // Create Azure OpenAI client with API key
        var client = new AzureOpenAIClient(
            new Uri(_options.EndpointUrl),
            new global::Azure.AzureKeyCredential(_options.ApiKey));

        _embeddingClient = client.GetEmbeddingClient(_options.EmbeddingDeploymentName);
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
                    ErrorMessage = "No response received from Azure OpenAI"
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
                ErrorMessage = $"Azure OpenAI embedding error: {ex.Message}"
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
                    ErrorMessage = "No response received from Azure OpenAI"
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
                    TotalTokens = null, // SDK doesn't expose usage for embeddings
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
                ErrorMessage = $"Azure OpenAI batch embedding error: {ex.Message}"
            }).ToList();
        }
    }
}

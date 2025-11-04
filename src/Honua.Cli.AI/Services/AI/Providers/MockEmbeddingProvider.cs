// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// Mock embedding provider for testing and when no API keys are available.
/// Returns random embeddings with consistent dimensionality.
/// </summary>
public sealed class MockEmbeddingProvider : IEmbeddingProvider
{
    private readonly Random _random = new Random(42); // Seed for reproducibility
    private const int EmbeddingDimensions = 1536; // Standard dimension for mock embeddings

    public string ProviderName => "Mock";
    public string DefaultModel => "mock-embedding-v1";
    public int Dimensions => EmbeddingDimensions;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<EmbeddingResponse> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return Task.FromResult(new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = DefaultModel,
                Success = false,
                ErrorMessage = "Text cannot be null or whitespace"
            });
        }

        // Generate a deterministic random embedding based on text hash
        var hash = text.GetHashCode();
        var random = new Random(hash);

        var embedding = new float[EmbeddingDimensions];
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range: -1.0 to 1.0
        }

        // Normalize the vector
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            embedding[i] /= (float)magnitude;
        }

        return Task.FromResult(new EmbeddingResponse
        {
            Embedding = embedding,
            Model = DefaultModel,
            TotalTokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            Success = true
        });
    }

    public async Task<IReadOnlyList<EmbeddingResponse>> GetEmbeddingBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var results = new List<EmbeddingResponse>();

        foreach (var text in texts)
        {
            results.Add(await GetEmbeddingAsync(text, cancellationToken));
        }

        return results;
    }
}

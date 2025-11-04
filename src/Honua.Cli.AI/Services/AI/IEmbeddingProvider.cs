// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.AI;

/// <summary>
/// Represents a response from an embedding provider.
/// </summary>
public sealed record EmbeddingResponse
{
    /// <summary>
    /// The generated embedding vector.
    /// </summary>
    public required float[] Embedding { get; init; }

    /// <summary>
    /// The model that generated the embedding.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Total tokens used in the request.
    /// </summary>
    public int? TotalTokens { get; init; }

    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Abstraction for embedding providers (OpenAI, Azure OpenAI, local models).
/// Used for vector search and semantic similarity.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// The name of the provider (e.g., "Azure OpenAI", "OpenAI").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// The default model to use for embeddings.
    /// </summary>
    string DefaultModel { get; }

    /// <summary>
    /// The dimensionality of the embedding vectors produced by this provider.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Tests whether the provider is available and configured correctly.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embedding vector for a single text input.
    /// </summary>
    Task<EmbeddingResponse> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embedding vectors for multiple text inputs (batch operation).
    /// More efficient than calling GetEmbeddingAsync multiple times.
    /// </summary>
    Task<IReadOnlyList<EmbeddingResponse>> GetEmbeddingBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}

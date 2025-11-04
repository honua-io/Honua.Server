// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.AI;

/// <summary>
/// Represents a request to an LLM provider.
/// </summary>
public sealed record LlmRequest
{
    /// <summary>
    /// The system prompt (instructions for the model).
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// The user prompt or question.
    /// </summary>
    public required string UserPrompt { get; init; }

    /// <summary>
    /// Optional conversation history (for multi-turn conversations).
    /// </summary>
    public IReadOnlyList<LlmMessage>? ConversationHistory { get; init; }

    /// <summary>
    /// Maximum tokens to generate in the response.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Temperature for response generation (0.0 = deterministic, 1.0 = creative).
    /// </summary>
    public double? Temperature { get; init; }
}

/// <summary>
/// Represents a message in a conversation.
/// </summary>
public sealed record LlmMessage
{
    /// <summary>
    /// The role of the message sender (system, user, assistant).
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// The content of the message.
    /// </summary>
    public required string Content { get; init; }
}

/// <summary>
/// Represents a response from an LLM provider.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>
    /// The generated content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The model that generated the response.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Total tokens used in the request/response.
    /// </summary>
    public int? TotalTokens { get; init; }

    /// <summary>
    /// Whether the response was successful.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents a streaming chunk from an LLM provider.
/// </summary>
public sealed record LlmStreamChunk
{
    /// <summary>
    /// The content chunk being streamed.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Whether this is the final chunk.
    /// </summary>
    public bool IsFinal { get; init; }

    /// <summary>
    /// Token count for this chunk (if available).
    /// </summary>
    public int? TokenCount { get; init; }
}

/// <summary>
/// Abstraction for LLM providers (OpenAI, Anthropic, Ollama, Azure).
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// The name of the provider (e.g., "OpenAI", "Anthropic", "Ollama").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// The default model to use for this provider.
    /// </summary>
    string DefaultModel { get; }

    /// <summary>
    /// Tests whether the provider is available and configured correctly.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a completion request to the LLM provider.
    /// </summary>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a completion response from the LLM provider.
    /// </summary>
    IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available models from this provider (if supported).
    /// </summary>
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default);
}

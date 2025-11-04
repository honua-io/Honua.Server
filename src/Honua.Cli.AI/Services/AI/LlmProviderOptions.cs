// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;

namespace Honua.Cli.AI.Services.AI;

/// <summary>
/// Configuration options for LLM providers.
/// </summary>
public sealed class LlmProviderOptions
{
    /// <summary>
    /// The primary LLM provider to use (OpenAI, Anthropic, Ollama, Azure).
    /// </summary>
    [Required]
    public string Provider { get; set; } = "OpenAI";

    /// <summary>
    /// The fallback provider if the primary fails.
    /// </summary>
    public string? FallbackProvider { get; set; }

    /// <summary>
    /// Enable smart multi-provider routing. When true and multiple API keys are available,
    /// routes requests to the best provider based on task characteristics.
    /// </summary>
    public bool EnableSmartRouting { get; set; } = true;

    /// <summary>
    /// Model to use (defaults to provider's default if not specified).
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Default temperature for all requests (can be overridden per request).
    /// </summary>
    public double DefaultTemperature { get; set; } = 0.2;

    /// <summary>
    /// Default max tokens for responses (can be overridden per request).
    /// </summary>
    public int DefaultMaxTokens { get; set; } = 4000;

    /// <summary>
    /// Timeout in seconds for LLM requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Number of retries on transient failures.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Maximum retry delay in seconds for rate limit responses (default: 60 seconds).
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 60;

    /// <summary>
    /// OpenAI-specific configuration.
    /// </summary>
    public OpenAIOptions OpenAI { get; set; } = new();

    /// <summary>
    /// Anthropic-specific configuration.
    /// </summary>
    public AnthropicOptions Anthropic { get; set; } = new();

    /// <summary>
    /// Ollama-specific configuration.
    /// </summary>
    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>
    /// Azure OpenAI-specific configuration.
    /// </summary>
    public AzureOpenAIOptions Azure { get; set; } = new();

    /// <summary>
    /// LocalAI-specific configuration.
    /// </summary>
    public LocalAIOptions LocalAI { get; set; } = new();
}

/// <summary>
/// OpenAI-specific configuration.
/// </summary>
public sealed class OpenAIOptions
{
    /// <summary>
    /// OpenAI API key (can be stored in secrets manager).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Default model for OpenAI (e.g., gpt-4o, gpt-4o-mini).
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-4o";

    /// <summary>
    /// Organization ID (optional).
    /// </summary>
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Anthropic-specific configuration.
/// </summary>
public sealed class AnthropicOptions
{
    /// <summary>
    /// Anthropic API key (can be stored in secrets manager).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Default model for Anthropic (e.g., claude-3-5-sonnet-20241022).
    /// </summary>
    public string DefaultModel { get; set; } = "claude-3-5-sonnet-20241022";
}

/// <summary>
/// Ollama-specific configuration.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>
    /// Ollama endpoint URL (default: http://localhost:11434).
    /// </summary>
    public string EndpointUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Default model for Ollama (e.g., llama3.1, mistral).
    /// </summary>
    public string DefaultModel { get; set; } = "llama3.1";
}

/// <summary>
/// Azure OpenAI-specific configuration.
/// </summary>
public sealed class AzureOpenAIOptions
{
    /// <summary>
    /// Azure OpenAI endpoint URL.
    /// </summary>
    public string? EndpointUrl { get; set; }

    /// <summary>
    /// Azure OpenAI API key (can be stored in secrets manager).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Azure deployment name for chat completions.
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Azure deployment name for embeddings.
    /// </summary>
    public string? EmbeddingDeploymentName { get; set; }

    /// <summary>
    /// Default model for Azure OpenAI chat.
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-4o";

    /// <summary>
    /// Default model for Azure OpenAI embeddings.
    /// </summary>
    public string DefaultEmbeddingModel { get; set; } = "text-embedding-3-large";
}

/// <summary>
/// LocalAI-specific configuration.
/// </summary>
public sealed class LocalAIOptions
{
    /// <summary>
    /// LocalAI endpoint URL (default: http://localhost:8080).
    /// </summary>
    public string EndpointUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// Default model for LocalAI (e.g., ggml-gpt4all-j).
    /// </summary>
    public string DefaultModel { get; set; } = "ggml-gpt4all-j";

    /// <summary>
    /// Default embedding model for LocalAI.
    /// </summary>
    public string DefaultEmbeddingModel { get; set; } = "all-MiniLM-L6-v2";
}

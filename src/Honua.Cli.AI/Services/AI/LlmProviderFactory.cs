// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Cli.AI.Services.AI.Providers;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Options;

namespace Honua.Cli.AI.Services.AI;

/// <summary>
/// Factory for creating LLM provider instances.
/// </summary>
public interface ILlmProviderFactory
{
    /// <summary>
    /// Creates the primary LLM provider based on configuration.
    /// </summary>
    ILlmProvider CreatePrimary();

    /// <summary>
    /// Creates the fallback LLM provider if configured.
    /// </summary>
    ILlmProvider? CreateFallback();

    /// <summary>
    /// Creates a specific provider by name.
    /// </summary>
    ILlmProvider CreateProvider(string providerName);

    /// <summary>
    /// Gets list of available providers based on configured API keys.
    /// </summary>
    string[] GetAvailableProviders();

    /// <summary>
    /// Gets a provider by name if available.
    /// </summary>
    ILlmProvider? GetProvider(string providerName);
}

/// <summary>
/// Default implementation of ILlmProviderFactory.
/// </summary>
public sealed class LlmProviderFactory : ProviderFactoryBase<ILlmProvider>, ILlmProviderFactory
{
    private readonly LlmProviderOptions _options;

    public LlmProviderFactory(IOptions<LlmProviderOptions> options)
    {
        _options = options.Value;

        // Register all providers with their aliases
        RegisterProvider("openai", () => new OpenAILlmProvider(_options));
        RegisterProvider("azure", () => new AzureOpenAILlmProvider(_options), "azureopenai");
        RegisterProvider("anthropic", () => new AnthropicLlmProvider(_options), "claude");
        RegisterProvider("ollama", () => new OllamaLlmProvider(_options));
        RegisterProvider("localai", () => new LocalAILlmProvider(_options));
        RegisterProviderInstance("mock", new MockLlmProvider());
    }

    public ILlmProvider CreatePrimary()
    {
        return CreateProvider(_options.Provider);
    }

    public ILlmProvider? CreateFallback()
    {
        if (string.IsNullOrWhiteSpace(_options.FallbackProvider))
        {
            return null;
        }

        return CreateProvider(_options.FallbackProvider);
    }

    public new string[] GetAvailableProviders()
    {
        var available = new List<string>();

        // Check which providers have API keys configured
        if (!string.IsNullOrWhiteSpace(_options.OpenAI.ApiKey))
        {
            available.Add("openai");
        }

        if (!string.IsNullOrWhiteSpace(_options.Anthropic.ApiKey))
        {
            available.Add("anthropic");
        }

        if (!string.IsNullOrWhiteSpace(_options.Azure.ApiKey) &&
            !string.IsNullOrWhiteSpace(_options.Azure.EndpointUrl))
        {
            available.Add("azure");
        }

        // Ollama and LocalAI are available if endpoint is configured (no API key required)
        if (!string.IsNullOrWhiteSpace(_options.Ollama.EndpointUrl))
        {
            available.Add("ollama");
        }

        if (!string.IsNullOrWhiteSpace(_options.LocalAI.EndpointUrl))
        {
            available.Add("localai");
        }

        // Mock is always available
        available.Add("mock");

        return available.ToArray();
    }

    public ILlmProvider? GetProvider(string providerName)
    {
        var normalized = NormalizeProviderName(providerName);

        // Check if this provider is available
        var available = GetAvailableProviders();
        if (!available.Contains(normalized) && normalized != "mock")
        {
            return null;
        }

        return TryCreateProvider(providerName);
    }
}

/// <summary>
/// LLM provider with automatic fallback.
/// </summary>
public sealed class FallbackLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _primary;
    private readonly ILlmProvider? _fallback;

    public string ProviderName => $"{_primary.ProviderName} (with fallback: {_fallback?.ProviderName ?? "none"})";
    public string DefaultModel => _primary.DefaultModel;

    public FallbackLlmProvider(ILlmProvider primary, ILlmProvider? fallback = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var primaryAvailable = await _primary.IsAvailableAsync(cancellationToken);
        if (primaryAvailable)
        {
            return true;
        }

        if (_fallback is not null)
        {
            return await _fallback.IsAvailableAsync(cancellationToken);
        }

        return false;
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        // Try primary provider first
        var response = await _primary.CompleteAsync(request, cancellationToken);

        if (response.Success)
        {
            return response;
        }

        // If primary fails and fallback is available, try fallback
        if (_fallback is not null)
        {
            var fallbackAvailable = await _fallback.IsAvailableAsync(cancellationToken);
            if (fallbackAvailable)
            {
                return await _fallback.CompleteAsync(request, cancellationToken);
            }
        }

        // Return the failed response from primary
        return response;
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Try primary provider first
        await foreach (var chunk in _primary.StreamAsync(request, cancellationToken))
        {
            yield return chunk;
            if (chunk.IsFinal)
            {
                yield break;
            }
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        // Return models from primary provider
        return await _primary.ListModelsAsync(cancellationToken);
    }
}

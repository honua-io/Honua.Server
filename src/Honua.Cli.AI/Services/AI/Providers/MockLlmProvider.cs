// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// Mock LLM provider for testing (no external API calls).
/// </summary>
public sealed class MockLlmProvider : LlmProviderBase
{
    private readonly Dictionary<string, string> _responses = new();
    private readonly Dictionary<string, Func<string, string>> _responseGenerators = new();
    private string _defaultResponse = "This is a mock response from the test LLM provider.";
    private bool _isAvailable = true;

    public override string ProviderName => "Mock";
    public override string DefaultModel => "mock-model-v1";

    public MockLlmProvider(LlmProviderOptions? options = null, ILogger<MockLlmProvider>? logger = null)
        : base(options ?? new LlmProviderOptions(), logger ?? NullLogger<MockLlmProvider>.Instance)
    {
    }

    /// <summary>
    /// Sets up a response for a specific pattern.
    /// </summary>
    public void SetupResponse(string pattern, string response)
    {
        _responses[pattern.ToLowerInvariant()] = response;
    }

    /// <summary>
    /// Sets up a response generator function for a specific pattern.
    /// </summary>
    public void SetupResponseGenerator(string pattern, Func<string, string> generator)
    {
        _responseGenerators[pattern.ToLowerInvariant()] = generator;
    }

    /// <summary>
    /// Sets the default response when no pattern matches.
    /// </summary>
    public void SetDefaultResponse(string response)
    {
        _defaultResponse = response;
    }

    /// <summary>
    /// Sets whether the provider is available (for testing failure scenarios).
    /// </summary>
    public void SetAvailability(bool available)
    {
        _isAvailable = available;
    }

    public override Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_isAvailable);
    }

    protected override Task<LlmResponse> CompleteInternalAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        if (!_isAvailable)
        {
            return Task.FromResult(new LlmResponse
            {
                Content = string.Empty,
                Model = DefaultModel,
                Success = false,
                ErrorMessage = "Mock provider is unavailable"
            });
        }

        var prompt = request.UserPrompt.ToLowerInvariant();

        // Check for exact matches first
        foreach (var (pattern, response) in _responses)
        {
            if (prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new LlmResponse
                {
                    Content = response,
                    Model = DefaultModel,
                    TotalTokens = (response.Length + request.UserPrompt.Length) / 4, // Rough token estimate
                    Success = true
                });
            }
        }

        // Check for response generators
        foreach (var (pattern, generator) in _responseGenerators)
        {
            if (prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                var response = generator(request.UserPrompt);
                return Task.FromResult(new LlmResponse
                {
                    Content = response,
                    Model = DefaultModel,
                    TotalTokens = (response.Length + request.UserPrompt.Length) / 4,
                    Success = true
                });
            }
        }

        // Return default response
        return Task.FromResult(new LlmResponse
        {
            Content = _defaultResponse,
            Model = DefaultModel,
            TotalTokens = (_defaultResponse.Length + request.UserPrompt.Length) / 4,
            Success = true
        });
    }

    public override async IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For mock, just return the complete response as a single chunk
        var response = await CompleteAsync(request, cancellationToken);

        yield return new LlmStreamChunk
        {
            Content = response.Content,
            IsFinal = true,
            TokenCount = response.TotalTokens
        };
    }

    public override Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(new[]
        {
            "mock-model-v1",
            "mock-model-fast",
            "mock-model-accurate"
        });
    }

    /// <summary>
    /// Clears all configured responses and generators.
    /// </summary>
    public void Reset()
    {
        _responses.Clear();
        _responseGenerators.Clear();
        _defaultResponse = "This is a mock response from the test LLM provider.";
        _isAvailable = true;
    }
}

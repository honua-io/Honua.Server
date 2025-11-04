// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// Abstract base class for LLM providers that handles common retry logic and error handling.
/// </summary>
/// <remarks>
/// This base class provides:
/// - Availability checking with test requests
/// - Retry logic for transient failures via LlmRateLimitRetryPolicy
/// - Consistent error handling and logging
/// - Template method pattern for provider-specific implementations
///
/// Derived classes must implement provider-specific details like
/// API client initialization, message formatting, and response parsing.
///
/// Design principles:
/// - Uses Template Method pattern for common flow
/// - Keeps abstract methods focused on provider-specific concerns
/// - Does not force providers to share state or configuration
/// - Uses protected access for extensibility
/// - Avoids creating dependencies between different providers
/// </remarks>
public abstract class LlmProviderBase : ILlmProvider
{
    /// <summary>
    /// Logger instance for derived classes to use.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Provider options containing retry and timeout settings.
    /// </summary>
    protected readonly LlmProviderOptions ProviderOptions;

    /// <summary>
    /// Gets the name of this LLM provider (e.g., "OpenAI", "Anthropic").
    /// </summary>
    public abstract string ProviderName { get; }

    /// <summary>
    /// Gets the default model identifier for this provider.
    /// </summary>
    public abstract string DefaultModel { get; }

    /// <summary>
    /// Initializes a new instance of the LlmProviderBase class.
    /// </summary>
    /// <param name="providerOptions">Configuration options for all providers</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    protected LlmProviderBase(LlmProviderOptions providerOptions, ILogger logger)
    {
        ProviderOptions = providerOptions ?? throw new ArgumentNullException(nameof(providerOptions));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks if the provider is available by attempting a small test request.
    /// </summary>
    /// <remarks>
    /// Default implementation sends a minimal test prompt. Override if your provider
    /// has a dedicated health check endpoint or different availability semantics.
    /// </remarks>
    public virtual async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Test with a minimal request
            var testRequest = new LlmRequest
            {
                UserPrompt = "Hello",
                MaxTokens = 5
            };

            var response = await CompleteAsync(testRequest, cancellationToken).ConfigureAwait(false);
            return response.Success;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "{Provider} availability check failed", ProviderName);
            return false;
        }
    }

    /// <summary>
    /// Completes an LLM request with automatic retry logic for transient failures.
    /// </summary>
    /// <remarks>
    /// This method wraps the provider-specific implementation with retry logic
    /// and error handling. Rate limit exceptions (HTTP 429) are automatically
    /// retried with exponential backoff.
    /// </remarks>
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await LlmRateLimitRetryPolicy.ExecuteWithRetryAsync(
                ct => CompleteInternalAsync(request, ct),
                maxRetries: ProviderOptions.MaxRetries,
                maxRetryDelaySeconds: ProviderOptions.MaxRetryDelaySeconds,
                logger: Logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var errorMessage = $"{ProviderName} request failed: {ex.Message}";
            Logger.LogError(ex, "{Provider} completion failed", ProviderName);
            return new LlmResponse
            {
                Content = string.Empty,
                Model = DefaultModel,
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Provider-specific implementation of completion logic.
    /// </summary>
    /// <remarks>
    /// Implement this method to handle:
    /// - Message formatting for your provider's API
    /// - API client calls
    /// - Response parsing and mapping to LlmResponse
    ///
    /// This method is called within the retry policy, so transient failures
    /// will be automatically retried. Do not add your own retry logic here.
    ///
    /// Let exceptions bubble up - they will be caught and handled by the
    /// base CompleteAsync method with proper logging and error sanitization.
    /// </remarks>
    /// <param name="request">The LLM request to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The LLM response</returns>
    protected abstract Task<LlmResponse> CompleteInternalAsync(
        LlmRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Streams a completion response from the LLM provider.
    /// </summary>
    /// <remarks>
    /// Derived classes must implement this method. Note that streaming does not
    /// support retry logic due to C# yield return limitations with try-catch.
    /// Rate limit handling is only supported in the CompleteAsync method.
    /// </remarks>
    public abstract IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available models from this provider (if supported).
    /// </summary>
    /// <remarks>
    /// Derived classes must implement this method. If the provider does not
    /// support model listing, return a list containing only the DefaultModel.
    /// </remarks>
    public abstract Task<IReadOnlyList<string>> ListModelsAsync(
        CancellationToken cancellationToken = default);
}

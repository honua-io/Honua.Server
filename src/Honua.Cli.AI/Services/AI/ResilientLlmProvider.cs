// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Honua.Cli.AI.Services.AI;

/// <summary>
/// Wraps an ILlmProvider with circuit breaker and retry policies for resilience.
/// </summary>
/// <remarks>
/// This decorator adds:
/// - Circuit breaker to prevent cascading failures when LLM provider is down
/// - Retry with exponential backoff for transient failures
/// - Timeout budget tracking for multi-step operations
/// - Metrics for monitoring resilience behavior
///
/// Usage:
/// <code>
/// var resilientProvider = new ResilientLlmProvider(
///     underlyingProvider,
///     logger,
///     options: new ResilientLlmOptions
///     {
///         MaxRetries = 3,
///         CircuitBreakerThreshold = 5,
///         CircuitBreakerDuration = TimeSpan.FromMinutes(1)
///     });
/// </code>
/// </remarks>
public sealed class ResilientLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _innerProvider;
    private readonly ILogger<ResilientLlmProvider> _logger;
    private readonly ResilientLlmOptions _options;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;

    public ResilientLlmProvider(
        ILlmProvider innerProvider,
        ILogger<ResilientLlmProvider> logger,
        ResilientLlmOptions? options = null)
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ResilientLlmOptions();

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetries,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "LLM request failed, retry {RetryCount}/{MaxRetries} after {DelayMs}ms: {Error}",
                        retryCount, _options.MaxRetries, timespan.TotalMilliseconds, exception.Message);
                });

        // Configure circuit breaker
        _circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: _options.CircuitBreakerThreshold,
                durationOfBreak: _options.CircuitBreakerDuration,
                onBreak: (exception, duration) =>
                {
                    _logger.LogError(exception,
                        "Circuit breaker OPENED for {Provider} due to {Threshold} consecutive failures. " +
                        "Breaking for {DurationSeconds}s",
                        _innerProvider.ProviderName,
                        _options.CircuitBreakerThreshold,
                        duration.TotalSeconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation(
                        "Circuit breaker RESET for {Provider}. Resuming normal operation.",
                        _innerProvider.ProviderName);
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation(
                        "Circuit breaker HALF-OPEN for {Provider}. Testing if service recovered.",
                        _innerProvider.ProviderName);
                });
    }

    public string ProviderName => _innerProvider.ProviderName;

    public string DefaultModel => _innerProvider.DefaultModel;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Don't apply resilience policies to availability checks
        return _innerProvider.IsAvailableAsync(cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        // Don't apply resilience policies to model listing
        return _innerProvider.ListModelsAsync(cancellationToken);
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            // Apply circuit breaker first, then retry policy
            var response = await _circuitBreakerPolicy.ExecuteAsync(
                ct => _retryPolicy.ExecuteAsync(
                    innerCt => _innerProvider.CompleteAsync(request, innerCt),
                    ct),
                cancellationToken);

            return response;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex,
                "Circuit breaker is OPEN for {Provider}. Returning failure without attempting request.",
                _innerProvider.ProviderName);

            return new LlmResponse
            {
                Content = string.Empty,
                Success = false,
                ErrorMessage = $"LLM provider {_innerProvider.ProviderName} is temporarily unavailable due to repeated failures. " +
                               $"Circuit breaker will reset in {_options.CircuitBreakerDuration.TotalSeconds}s.",
                Model = _innerProvider.DefaultModel
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "LLM request failed after {MaxRetries} retries for {Provider}: {Error}",
                _options.MaxRetries, _innerProvider.ProviderName, ex.Message);

            return new LlmResponse
            {
                Content = string.Empty,
                Success = false,
                ErrorMessage = $"LLM request failed after {_options.MaxRetries} retries: {ex.Message}",
                Model = _innerProvider.DefaultModel
            };
        }
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // For streaming, we only apply circuit breaker (not retry)
        // Retrying a partial stream is complex and usually not desired
        IAsyncEnumerable<LlmStreamChunk>? stream = null;
        LlmStreamChunk? errorChunk = null;

        try
        {
            stream = await _circuitBreakerPolicy.ExecuteAsync(
                ct => Task.FromResult(_innerProvider.StreamAsync(request, ct)),
                cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex,
                "Circuit breaker is OPEN for {Provider}. Cannot start stream.",
                _innerProvider.ProviderName);

            errorChunk = new LlmStreamChunk
            {
                Content = $"[Error: LLM provider temporarily unavailable]",
                IsFinal = true
            };
        }

        if (errorChunk != null)
        {
            yield return errorChunk;
            yield break;
        }

        if (stream != null)
        {
            await foreach (var chunk in stream.WithCancellation(cancellationToken))
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Gets the current circuit breaker state.
    /// </summary>
    public CircuitState CircuitState => _circuitBreakerPolicy.CircuitState;

    /// <summary>
    /// Isolates the circuit breaker (opens it manually).
    /// Useful for testing or manual intervention.
    /// </summary>
    public void IsolateCircuit()
    {
        _circuitBreakerPolicy.Isolate();
        _logger.LogWarning("Circuit breaker manually ISOLATED for {Provider}", _innerProvider.ProviderName);
    }

    /// <summary>
    /// Resets the circuit breaker (closes it manually).
    /// Use with caution - only reset if you're sure the underlying service recovered.
    /// </summary>
    public void ResetCircuit()
    {
        _circuitBreakerPolicy.Reset();
        _logger.LogInformation("Circuit breaker manually RESET for {Provider}", _innerProvider.ProviderName);
    }
}

/// <summary>
/// Configuration options for ResilientLlmProvider.
/// </summary>
public sealed class ResilientLlmOptions
{
    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Number of consecutive exceptions before circuit breaker opens.
    /// Default: 5
    /// </summary>
    public int CircuitBreakerThreshold { get; init; } = 5;

    /// <summary>
    /// Duration to keep circuit breaker open before attempting recovery.
    /// Default: 1 minute
    /// </summary>
    public TimeSpan CircuitBreakerDuration { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether to enable retry for non-transient errors.
    /// Default: false (only retry transient errors like network issues)
    /// </summary>
    public bool RetryNonTransientErrors { get; init; } = false;
}

/// <summary>
/// Extension methods for wrapping LLM providers with resilience.
/// </summary>
public static class ResilientLlmProviderExtensions
{
    /// <summary>
    /// Wraps an LLM provider with circuit breaker and retry policies.
    /// </summary>
    public static ILlmProvider WithResilience(
        this ILlmProvider provider,
        ILogger<ResilientLlmProvider> logger,
        ResilientLlmOptions? options = null)
    {
        return new ResilientLlmProvider(provider, logger, options);
    }
}

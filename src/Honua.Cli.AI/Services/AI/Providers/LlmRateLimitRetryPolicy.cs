// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Resilience;

namespace Honua.Cli.AI.Services.AI.Providers;

/// <summary>
/// Provides retry policies for handling rate limit responses from LLM providers.
/// This class now delegates to ResiliencePolicies.CreateLlmRetryPolicy for consistency.
/// </summary>
internal static class LlmRateLimitRetryPolicy
{
    /// <summary>
    /// Creates a Polly retry pipeline for handling HTTP 429 rate limit responses.
    /// Delegates to ResiliencePolicies.CreateLlmRetryPolicy for consistent retry behavior.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 5 for LLM calls)</param>
    /// <param name="maxRetryDelaySeconds">Maximum retry delay in seconds (default: 60)</param>
    /// <param name="logger">Optional logger for rate limit events</param>
    /// <returns>A configured resilience pipeline</returns>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpRetryPipeline(
        int maxRetries = 5,
        int maxRetryDelaySeconds = 60,
        ILogger? logger = null)
    {
        return ResiliencePolicies.CreateLlmRetryPolicy(
            maxRetries: maxRetries,
            maxDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
            timeout: null, // No timeout in original implementation
            logger: logger);
    }

    /// <summary>
    /// Creates a Polly retry pipeline for handling general exceptions with rate limit detection.
    /// Delegates to ResiliencePolicies.CreateRetryPolicy with custom retry logic.
    /// </summary>
    public static ResiliencePipeline CreateExceptionRetryPipeline(
        int maxRetries = 5,
        int maxRetryDelaySeconds = 60,
        ILogger? logger = null)
    {
        // Use the general retry policy with custom exception filter
        return ResiliencePolicies.CreateRetryPolicy(
            maxRetries: maxRetries,
            initialDelay: TimeSpan.FromSeconds(1),
            logger: logger,
            shouldRetry: ex =>
                ex is HttpRequestException httpEx && httpEx.StatusCode == HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Executes an action with retry logic for rate limiting.
    /// Useful for SDK calls that don't return HttpResponseMessage.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> action,
        int maxRetries = 5,
        int maxRetryDelaySeconds = 60,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var pipeline = ResiliencePolicies.CreateRetryPolicy(
            maxRetries: maxRetries,
            initialDelay: TimeSpan.FromSeconds(1),
            logger: logger,
            shouldRetry: ex =>
                (ex is HttpRequestException httpEx && httpEx.StatusCode == HttpStatusCode.TooManyRequests) ||
                IsRateLimitException(ex));

        return await pipeline.ExecuteAsync(
            async ct => await action(ct),
            cancellationToken);
    }

    /// <summary>
    /// Checks if an exception is related to rate limiting.
    /// </summary>
    private static bool IsRateLimitException(Exception ex)
    {
        // Check for common rate limit error messages
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("rate limit") ||
               message.Contains("429") ||
               message.Contains("too many requests") ||
               message.Contains("quota exceeded");
    }
}

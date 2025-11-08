// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.ETL.Resilience;

/// <summary>
/// Defines retry policy for workflow execution
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts (default: 3)
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Backoff strategy for retries
    /// </summary>
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;

    /// <summary>
    /// Initial delay in seconds before first retry (default: 5)
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Maximum delay between retries in seconds (default: 300 = 5 minutes)
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 300;

    /// <summary>
    /// Add random jitter to prevent thundering herd
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Maximum jitter percentage (0.0 to 1.0)
    /// </summary>
    public double JitterFactor { get; set; } = 0.2;

    /// <summary>
    /// Error categories that should trigger a retry
    /// </summary>
    public HashSet<ErrorCategory> RetryableErrors { get; set; } = new()
    {
        ErrorCategory.Transient,
        ErrorCategory.Resource,
        ErrorCategory.External
    };

    /// <summary>
    /// Number of consecutive failures before circuit breaker opens
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker timeout in seconds (time before half-open state)
    /// </summary>
    public int CircuitBreakerTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to retry the entire workflow or just the failed node
    /// </summary>
    public RetryScope Scope { get; set; } = RetryScope.Node;

    /// <summary>
    /// Create default retry policy
    /// </summary>
    public static RetryPolicy Default => new();

    /// <summary>
    /// Create retry policy for transient errors (more aggressive retries)
    /// </summary>
    public static RetryPolicy ForTransientErrors => new()
    {
        MaxAttempts = 5,
        InitialDelaySeconds = 2,
        BackoffStrategy = BackoffStrategy.Exponential,
        RetryableErrors = new HashSet<ErrorCategory> { ErrorCategory.Transient }
    };

    /// <summary>
    /// Create retry policy for external service calls
    /// </summary>
    public static RetryPolicy ForExternalServices => new()
    {
        MaxAttempts = 3,
        InitialDelaySeconds = 10,
        BackoffStrategy = BackoffStrategy.Exponential,
        RetryableErrors = new HashSet<ErrorCategory> { ErrorCategory.External, ErrorCategory.Transient }
    };

    /// <summary>
    /// No retry policy
    /// </summary>
    public static RetryPolicy NoRetry => new()
    {
        MaxAttempts = 0
    };

    /// <summary>
    /// Calculate delay for a specific retry attempt
    /// </summary>
    public TimeSpan GetDelay(int attemptNumber)
    {
        if (attemptNumber <= 0) return TimeSpan.Zero;

        double delaySeconds = BackoffStrategy switch
        {
            BackoffStrategy.None => 0,
            BackoffStrategy.Constant => InitialDelaySeconds,
            BackoffStrategy.Linear => InitialDelaySeconds * attemptNumber,
            BackoffStrategy.Exponential => InitialDelaySeconds * Math.Pow(2, attemptNumber - 1),
            _ => InitialDelaySeconds
        };

        // Apply maximum delay cap
        delaySeconds = Math.Min(delaySeconds, MaxDelaySeconds);

        // Apply jitter if enabled
        if (UseJitter && delaySeconds > 0)
        {
            var jitterAmount = delaySeconds * JitterFactor;
            var random = new Random();
            var jitter = (random.NextDouble() * 2 - 1) * jitterAmount; // Random between -jitterAmount and +jitterAmount
            delaySeconds = Math.Max(0, delaySeconds + jitter);
        }

        return TimeSpan.FromSeconds(delaySeconds);
    }

    /// <summary>
    /// Determine if an error should be retried
    /// </summary>
    public bool ShouldRetry(ErrorCategory errorCategory, int currentAttempt)
    {
        if (currentAttempt >= MaxAttempts) return false;
        return RetryableErrors.Contains(errorCategory);
    }
}

/// <summary>
/// Backoff strategy for retries
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// No delay between retries
    /// </summary>
    None,

    /// <summary>
    /// Constant delay between retries
    /// </summary>
    Constant,

    /// <summary>
    /// Linear increase in delay (delay * attempt)
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential increase in delay (delay * 2^attempt)
    /// </summary>
    Exponential
}

/// <summary>
/// Retry scope - what to retry
/// </summary>
public enum RetryScope
{
    /// <summary>
    /// Retry only the failed node
    /// </summary>
    Node,

    /// <summary>
    /// Retry the entire workflow from beginning
    /// </summary>
    Workflow
}

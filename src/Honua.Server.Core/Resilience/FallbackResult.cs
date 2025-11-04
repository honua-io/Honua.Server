// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Represents the result of an operation with fallback support.
/// Indicates whether the result came from the primary source or a fallback.
/// </summary>
public sealed class FallbackResult<T>
{
    public T Value { get; }
    public bool IsFromFallback { get; }
    public FallbackReason? FallbackReason { get; }
    public Exception? Exception { get; }

    private FallbackResult(T value, bool isFromFallback, FallbackReason? reason, Exception? exception)
    {
        Value = value;
        IsFromFallback = isFromFallback;
        FallbackReason = reason;
        Exception = exception;
    }

    /// <summary>
    /// Creates a successful result from the primary source.
    /// </summary>
    public static FallbackResult<T> Success(T value)
    {
        return new FallbackResult<T>(value, false, null, null);
    }

    /// <summary>
    /// Creates a result from a fallback source.
    /// </summary>
    public static FallbackResult<T> Fallback(T value, FallbackReason reason, Exception? exception = null)
    {
        return new FallbackResult<T>(value, true, reason, exception);
    }

    /// <summary>
    /// Creates a failed result with no fallback available.
    /// </summary>
    public static FallbackResult<T> Failed(Exception exception, T defaultValue)
    {
        return new FallbackResult<T>(defaultValue, true, Resilience.FallbackReason.NoFallbackAvailable, exception);
    }
}

/// <summary>
/// Reasons why a fallback was used.
/// </summary>
public enum FallbackReason
{
    /// <summary>
    /// Primary service was unavailable.
    /// </summary>
    ServiceUnavailable,

    /// <summary>
    /// Primary service timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Circuit breaker was open.
    /// </summary>
    CircuitBreakerOpen,

    /// <summary>
    /// Service was throttled.
    /// </summary>
    Throttled,

    /// <summary>
    /// Using stale cache data.
    /// </summary>
    StaleCache,

    /// <summary>
    /// No fallback was available.
    /// </summary>
    NoFallbackAvailable,

    /// <summary>
    /// Other transient error.
    /// </summary>
    TransientError
}

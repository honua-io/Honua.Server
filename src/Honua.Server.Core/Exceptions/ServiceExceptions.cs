// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Exceptions;

/// <summary>
/// Base exception for service-level errors.
/// </summary>
public class ServiceException : HonuaException
{
    public ServiceException(string message) : base(message)
    {
    }

    public ServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when an external service is unavailable.
/// This is a transient error that should trigger fallback mechanisms.
/// </summary>
public sealed class ServiceUnavailableException : ServiceException, ITransientException
{
    public string ServiceName { get; }
    public bool IsTransient => true;

    public ServiceUnavailableException(string serviceName, string message)
        : base($"Service '{serviceName}' is unavailable: {message}")
    {
        ServiceName = serviceName;
    }

    public ServiceUnavailableException(string serviceName, string message, Exception innerException)
        : base($"Service '{serviceName}' is unavailable: {message}", innerException)
    {
        ServiceName = serviceName;
    }
}

/// <summary>
/// Exception thrown when a circuit breaker is open.
/// This prevents cascading failures by failing fast.
/// </summary>
public sealed class CircuitBreakerOpenException : ServiceException, ITransientException
{
    public string ServiceName { get; }
    public TimeSpan BreakDuration { get; }
    public bool IsTransient => true;

    public CircuitBreakerOpenException(string serviceName, TimeSpan breakDuration)
        : base($"Circuit breaker for '{serviceName}' is open. Service temporarily unavailable for {breakDuration.TotalSeconds}s.")
    {
        ServiceName = serviceName;
        BreakDuration = breakDuration;
    }
}

/// <summary>
/// Exception thrown when a service times out.
/// </summary>
public sealed class ServiceTimeoutException : ServiceException, ITransientException
{
    public string ServiceName { get; }
    public TimeSpan Timeout { get; }
    public bool IsTransient => true;

    public ServiceTimeoutException(string serviceName, TimeSpan timeout)
        : base($"Service '{serviceName}' timed out after {timeout.TotalSeconds}s.")
    {
        ServiceName = serviceName;
        Timeout = timeout;
    }

    public ServiceTimeoutException(string serviceName, TimeSpan timeout, Exception innerException)
        : base($"Service '{serviceName}' timed out after {timeout.TotalSeconds}s.", innerException)
    {
        ServiceName = serviceName;
        Timeout = timeout;
    }
}

/// <summary>
/// Exception thrown when a service is throttled due to rate limiting.
/// </summary>
public sealed class ServiceThrottledException : ServiceException, ITransientException
{
    public string ServiceName { get; }
    public TimeSpan? RetryAfter { get; }
    public bool IsTransient => true;

    public ServiceThrottledException(string serviceName, TimeSpan? retryAfter = null)
        : base($"Service '{serviceName}' is throttled. {(retryAfter.HasValue ? $"Retry after {retryAfter.Value.TotalSeconds}s." : "")}")
    {
        ServiceName = serviceName;
        RetryAfter = retryAfter;
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Metrics for resilience patterns (cache circuit breakers, database retries, background service failures).
/// Provides observability into resilience behavior for monitoring and alerting.
/// </summary>
public sealed class ResilienceMetrics
{
    private readonly Meter _meter;

    // Cache circuit breaker metrics
    private readonly Counter<long> _cacheCircuitBreakerOpenCount;
    private readonly Counter<long> _cacheCircuitBreakerClosedCount;
    private readonly Counter<long> _cacheCircuitBreakerHalfOpenCount;
    private readonly Counter<long> _cacheOperationSuccessCount;
    private readonly Counter<long> _cacheOperationFailureCount;
    private readonly Histogram<double> _cacheOperationDuration;

    // Database retry metrics
    private readonly Counter<long> _databaseRetryAttemptCount;
    private readonly Counter<long> _databaseRetrySuccessCount;
    private readonly Counter<long> _databaseRetryExhaustedCount;
    private readonly Histogram<double> _databaseOperationDuration;
    private readonly Counter<long> _databaseTransientErrorCount;

    // Background service metrics
    private readonly Counter<long> _backgroundServiceRetryCount;
    private readonly Counter<long> _backgroundServiceFailureCount;
    private readonly Counter<long> _backgroundServiceSuccessCount;
    private readonly Histogram<double> _backgroundServiceRetryDelay;

    public ResilienceMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create("Honua.Server.Resilience", "1.0.0");

        // Cache circuit breaker metrics
        _cacheCircuitBreakerOpenCount = _meter.CreateCounter<long>(
            "cache_circuit_breaker_open_total",
            "events",
            "Total number of times cache circuit breaker opened");

        _cacheCircuitBreakerClosedCount = _meter.CreateCounter<long>(
            "cache_circuit_breaker_closed_total",
            "events",
            "Total number of times cache circuit breaker closed");

        _cacheCircuitBreakerHalfOpenCount = _meter.CreateCounter<long>(
            "cache_circuit_breaker_halfopen_total",
            "events",
            "Total number of times cache circuit breaker entered half-open state");

        _cacheOperationSuccessCount = _meter.CreateCounter<long>(
            "cache_operation_success_total",
            "operations",
            "Total number of successful cache operations");

        _cacheOperationFailureCount = _meter.CreateCounter<long>(
            "cache_operation_failure_total",
            "operations",
            "Total number of failed cache operations");

        _cacheOperationDuration = _meter.CreateHistogram<double>(
            "cache_operation_duration_seconds",
            "seconds",
            "Duration of cache operations in seconds");

        // Database retry metrics
        _databaseRetryAttemptCount = _meter.CreateCounter<long>(
            "database_retry_attempt_total",
            "attempts",
            "Total number of database retry attempts");

        _databaseRetrySuccessCount = _meter.CreateCounter<long>(
            "database_retry_success_total",
            "successes",
            "Total number of successful database retries");

        _databaseRetryExhaustedCount = _meter.CreateCounter<long>(
            "database_retry_exhausted_total",
            "exhaustions",
            "Total number of times database retries were exhausted");

        _databaseOperationDuration = _meter.CreateHistogram<double>(
            "database_operation_duration_seconds",
            "seconds",
            "Duration of database operations in seconds");

        _databaseTransientErrorCount = _meter.CreateCounter<long>(
            "database_transient_error_total",
            "errors",
            "Total number of transient database errors");

        // Background service metrics
        _backgroundServiceRetryCount = _meter.CreateCounter<long>(
            "background_service_retry_total",
            "retries",
            "Total number of background service retries");

        _backgroundServiceFailureCount = _meter.CreateCounter<long>(
            "background_service_failure_total",
            "failures",
            "Total number of background service failures");

        _backgroundServiceSuccessCount = _meter.CreateCounter<long>(
            "background_service_success_total",
            "successes",
            "Total number of successful background service operations");

        _backgroundServiceRetryDelay = _meter.CreateHistogram<double>(
            "background_service_retry_delay_seconds",
            "seconds",
            "Delay before background service retry in seconds");
    }

    #region Cache Circuit Breaker Metrics

    public void RecordCacheCircuitBreakerOpened(string cacheName, string reason)
    {
        _cacheCircuitBreakerOpenCount.Add(1, new TagList
        {
            { "cache_name", cacheName },
            { "reason", reason }
        });
    }

    public void RecordCacheCircuitBreakerClosed(string cacheName)
    {
        _cacheCircuitBreakerClosedCount.Add(1, new TagList
        {
            { "cache_name", cacheName }
        });
    }

    public void RecordCacheCircuitBreakerHalfOpened(string cacheName)
    {
        _cacheCircuitBreakerHalfOpenCount.Add(1, new TagList
        {
            { "cache_name", cacheName }
        });
    }

    public void RecordCacheOperationSuccess(string cacheName, string operation, double durationSeconds)
    {
        var tags = new TagList
        {
            { "cache_name", cacheName },
            { "operation", operation }
        };

        _cacheOperationSuccessCount.Add(1, tags);
        _cacheOperationDuration.Record(durationSeconds, tags);
    }

    public void RecordCacheOperationFailure(string cacheName, string operation, string errorType, double durationSeconds)
    {
        var tags = new TagList
        {
            { "cache_name", cacheName },
            { "operation", operation },
            { "error_type", errorType }
        };

        _cacheOperationFailureCount.Add(1, tags);
        _cacheOperationDuration.Record(durationSeconds, tags);
    }

    #endregion

    #region Database Retry Metrics

    public void RecordDatabaseRetryAttempt(string provider, string operation, int attemptNumber)
    {
        _databaseRetryAttemptCount.Add(1, new TagList
        {
            { "provider", provider },
            { "operation", operation },
            { "attempt_number", attemptNumber }
        });
    }

    public void RecordDatabaseRetrySuccess(string provider, string operation, int totalAttempts)
    {
        _databaseRetrySuccessCount.Add(1, new TagList
        {
            { "provider", provider },
            { "operation", operation },
            { "total_attempts", totalAttempts }
        });
    }

    public void RecordDatabaseRetryExhausted(string provider, string operation, int maxAttempts)
    {
        _databaseRetryExhaustedCount.Add(1, new TagList
        {
            { "provider", provider },
            { "operation", operation },
            { "max_attempts", maxAttempts }
        });
    }

    public void RecordDatabaseOperation(string provider, string operation, double durationSeconds, bool success)
    {
        var tags = new TagList
        {
            { "provider", provider },
            { "operation", operation },
            { "success", success }
        };

        _databaseOperationDuration.Record(durationSeconds, tags);
    }

    public void RecordDatabaseTransientError(string provider, string errorType)
    {
        _databaseTransientErrorCount.Add(1, new TagList
        {
            { "provider", provider },
            { "error_type", errorType }
        });
    }

    #endregion

    #region Background Service Metrics

    public void RecordBackgroundServiceRetry(string serviceName, string operation, int attemptNumber, double delaySeconds)
    {
        var tags = new TagList
        {
            { "service_name", serviceName },
            { "operation", operation },
            { "attempt_number", attemptNumber }
        };

        _backgroundServiceRetryCount.Add(1, tags);
        _backgroundServiceRetryDelay.Record(delaySeconds, tags);
    }

    public void RecordBackgroundServiceFailure(string serviceName, string operation, string errorType)
    {
        _backgroundServiceFailureCount.Add(1, new TagList
        {
            { "service_name", serviceName },
            { "operation", operation },
            { "error_type", errorType }
        });
    }

    public void RecordBackgroundServiceSuccess(string serviceName, string operation)
    {
        _backgroundServiceSuccessCount.Add(1, new TagList
        {
            { "service_name", serviceName },
            { "operation", operation }
        });
    }

    #endregion
}

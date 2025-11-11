// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates cache invalidation configuration options.
/// </summary>
public sealed class CacheInvalidationOptionsValidator : IValidateOptions<CacheInvalidationOptions>
{
    public ValidateOptionsResult Validate(string? name, CacheInvalidationOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("CacheInvalidationOptions cannot be null");
        }

        var failures = new List<string>();

        // Validate retry count
        if (options.RetryCount < 0)
        {
            failures.Add($"CacheInvalidation RetryCount cannot be negative. Current: {options.RetryCount}. Set 'CacheInvalidation:RetryCount' to 0 (no retries) or positive value.");
        }
        else if (options.RetryCount > 10)
        {
            failures.Add($"CacheInvalidation RetryCount ({options.RetryCount}) exceeds recommended maximum of 10. Excessive retries can cause cascading failures. Reduce 'CacheInvalidation:RetryCount'.");
        }

        // Validate retry delay
        if (options.RetryDelayMs <= 0)
        {
            failures.Add($"CacheInvalidation RetryDelayMs must be positive. Current: {options.RetryDelayMs}. Set 'CacheInvalidation:RetryDelayMs' to a value greater than 0. Example: 100");
        }
        else if (options.RetryDelayMs > 10000)
        {
            failures.Add($"CacheInvalidation RetryDelayMs ({options.RetryDelayMs}) exceeds recommended maximum of 10000ms (10 seconds). Long initial delays can impact user experience. Reduce 'CacheInvalidation:RetryDelayMs'.");
        }

        // Validate max retry delay
        if (options.MaxRetryDelayMs <= 0)
        {
            failures.Add($"CacheInvalidation MaxRetryDelayMs must be positive. Current: {options.MaxRetryDelayMs}. Set 'CacheInvalidation:MaxRetryDelayMs' to a value greater than 0.");
        }
        else if (options.MaxRetryDelayMs < options.RetryDelayMs)
        {
            failures.Add($"CacheInvalidation MaxRetryDelayMs ({options.MaxRetryDelayMs}ms) must be greater than or equal to RetryDelayMs ({options.RetryDelayMs}ms). Adjust 'CacheInvalidation:MaxRetryDelayMs' or 'CacheInvalidation:RetryDelayMs'.");
        }
        else if (options.MaxRetryDelayMs > 60000)
        {
            failures.Add($"CacheInvalidation MaxRetryDelayMs ({options.MaxRetryDelayMs}) exceeds recommended maximum of 60000ms (60 seconds). Long delays can cause timeouts. Reduce 'CacheInvalidation:MaxRetryDelayMs'.");
        }

        // Validate health check sample size
        if (options.HealthCheckSampleSize <= 0)
        {
            failures.Add($"CacheInvalidation HealthCheckSampleSize must be positive. Current: {options.HealthCheckSampleSize}. Set 'CacheInvalidation:HealthCheckSampleSize' to a value greater than 0.");
        }
        else if (options.HealthCheckSampleSize > 10000)
        {
            failures.Add($"CacheInvalidation HealthCheckSampleSize ({options.HealthCheckSampleSize}) exceeds recommended maximum of 10000. Large samples can impact health check performance. Reduce 'CacheInvalidation:HealthCheckSampleSize'.");
        }

        // Validate drift percentage
        if (options.MaxDriftPercentage < 0)
        {
            failures.Add($"CacheInvalidation MaxDriftPercentage cannot be negative. Current: {options.MaxDriftPercentage}. Set 'CacheInvalidation:MaxDriftPercentage' to 0 or positive value.");
        }
        else if (options.MaxDriftPercentage > 100)
        {
            failures.Add($"CacheInvalidation MaxDriftPercentage ({options.MaxDriftPercentage}) exceeds 100%. Set 'CacheInvalidation:MaxDriftPercentage' to a value between 0 and 100.");
        }

        // Validate short TTL
        if (options.ShortTtl <= TimeSpan.Zero)
        {
            failures.Add($"CacheInvalidation ShortTtl must be positive. Current: {options.ShortTtl}. Set 'CacheInvalidation:ShortTtl' to a positive TimeSpan. Example: '00:00:30' (30 seconds)");
        }
        else if (options.ShortTtl > TimeSpan.FromHours(1))
        {
            failures.Add($"CacheInvalidation ShortTtl ({options.ShortTtl}) exceeds recommended maximum of 1 hour. Short TTL should be brief to minimize stale data window. Reduce 'CacheInvalidation:ShortTtl'.");
        }

        // Validate operation timeout
        if (options.OperationTimeout <= TimeSpan.Zero)
        {
            failures.Add($"CacheInvalidation OperationTimeout must be positive. Current: {options.OperationTimeout}. Set 'CacheInvalidation:OperationTimeout' to a positive TimeSpan. Example: '00:00:10' (10 seconds)");
        }
        else if (options.OperationTimeout > TimeSpan.FromMinutes(5))
        {
            failures.Add($"CacheInvalidation OperationTimeout ({options.OperationTimeout}) exceeds recommended maximum of 5 minutes. Long timeouts can cause resource exhaustion. Reduce 'CacheInvalidation:OperationTimeout'.");
        }

        // Validate strategy
        if (!Enum.IsDefined(typeof(CacheInvalidationStrategy), options.Strategy))
        {
            failures.Add($"CacheInvalidation Strategy '{options.Strategy}' is invalid. Valid values: Strict, Eventual, ShortTTL. Set 'CacheInvalidation:Strategy'.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

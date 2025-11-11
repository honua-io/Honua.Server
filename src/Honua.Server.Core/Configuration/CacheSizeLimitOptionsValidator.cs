// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Caching;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates cache size limit configuration options using IValidateOptions pattern.
/// </summary>
public sealed class CacheSizeLimitOptionsValidator : IValidateOptions<CacheSizeLimitOptions>
{
    public ValidateOptionsResult Validate(string? name, CacheSizeLimitOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("CacheSizeLimitOptions cannot be null");
        }

        var failures = new List<string>();

        // Validate max total size
        if (options.MaxTotalSizeMB < 0)
        {
            failures.Add($"Caching MaxTotalSizeMB cannot be negative. Current: {options.MaxTotalSizeMB}. Set 'honua:caching:MaxTotalSizeMB' to 0 (unlimited) or positive value.");
        }
        else if (options.MaxTotalSizeMB > 10000)
        {
            failures.Add($"Caching MaxTotalSizeMB ({options.MaxTotalSizeMB}MB) exceeds maximum of 10000MB (10GB). Excessive cache size can cause memory exhaustion. Reduce 'honua:caching:MaxTotalSizeMB'.");
        }
        else if (options.MaxTotalSizeMB == 0)
        {
            // Warning: Unlimited cache is not recommended in production
            // This is not a hard failure, but we'll log it as info
        }

        // Validate max total entries
        if (options.MaxTotalEntries < 0)
        {
            failures.Add($"Caching MaxTotalEntries cannot be negative. Current: {options.MaxTotalEntries}. Set 'honua:caching:MaxTotalEntries' to 0 (unlimited) or positive value.");
        }
        else if (options.MaxTotalEntries > 1000000)
        {
            failures.Add($"Caching MaxTotalEntries ({options.MaxTotalEntries}) exceeds maximum of 1,000,000. Excessive entries can cause performance degradation. Reduce 'honua:caching:MaxTotalEntries'.");
        }

        // Validate expiration scan frequency
        if (options.ExpirationScanFrequencyMinutes <= 0)
        {
            failures.Add($"Caching ExpirationScanFrequencyMinutes must be positive. Current: {options.ExpirationScanFrequencyMinutes}. Set 'honua:caching:ExpirationScanFrequencyMinutes' to a value greater than 0.");
        }
        else if (options.ExpirationScanFrequencyMinutes < 0.5)
        {
            failures.Add($"Caching ExpirationScanFrequencyMinutes ({options.ExpirationScanFrequencyMinutes}) is below minimum of 0.5 minutes (30 seconds). Frequent scans can impact performance. Increase 'honua:caching:ExpirationScanFrequencyMinutes'.");
        }
        else if (options.ExpirationScanFrequencyMinutes > 60)
        {
            failures.Add($"Caching ExpirationScanFrequencyMinutes ({options.ExpirationScanFrequencyMinutes}) exceeds maximum of 60 minutes. Infrequent scans can delay eviction. Reduce 'honua:caching:ExpirationScanFrequencyMinutes'.");
        }

        // Validate compaction percentage
        if (options.CompactionPercentage <= 0 || options.CompactionPercentage > 1.0)
        {
            failures.Add($"Caching CompactionPercentage must be between 0.1 and 0.5. Current: {options.CompactionPercentage}. Set 'honua:caching:CompactionPercentage' to a value between 0.1 (10%) and 0.5 (50%). Example: 0.25");
        }
        else if (options.CompactionPercentage < 0.1)
        {
            failures.Add($"Caching CompactionPercentage ({options.CompactionPercentage}) is below recommended minimum of 0.1 (10%). Low compaction may not free enough space. Increase 'honua:caching:CompactionPercentage'.");
        }
        else if (options.CompactionPercentage > 0.5)
        {
            failures.Add($"Caching CompactionPercentage ({options.CompactionPercentage}) exceeds recommended maximum of 0.5 (50%). High compaction can cause cache thrashing. Reduce 'honua:caching:CompactionPercentage'.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

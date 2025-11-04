// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Honua.Cli.AI.HealthChecks;

/// <summary>
/// Health check wrapper that reports on the configured process state store.
/// Uses Redis metrics when available and reports healthy when the in-memory fallback is active.
/// </summary>
public sealed class ProcessStateStoreHealthCheck : IHealthCheck
{
    private readonly IProcessStateStore _store;

    public ProcessStateStoreHealthCheck(IProcessStateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_store is RedisProcessStateStore redisStore)
        {
            return await redisStore.CheckHealthAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return HealthCheckResult.Healthy("Redis process state store not configured; using in-memory implementation.");
    }
}

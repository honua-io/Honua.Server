// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.HealthChecks;

/// <summary>
/// Health check for Azure OpenAI embedding provider connectivity.
/// </summary>
public sealed class AzureEmbeddingHealthCheck : IHealthCheck
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<AzureEmbeddingHealthCheck> _logger;

    public AzureEmbeddingHealthCheck(
        IEmbeddingProvider embeddingProvider,
        ILogger<AzureEmbeddingHealthCheck> logger)
    {
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _embeddingProvider.IsAvailableAsync(cancellationToken);

            if (isAvailable)
            {
                _logger.LogDebug("Azure embedding health check passed");
                return HealthCheckResult.Healthy(
                    "Azure embedding service is available",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["provider"] = _embeddingProvider.ProviderName,
                        ["model"] = _embeddingProvider.DefaultModel,
                        ["dimensions"] = _embeddingProvider.Dimensions
                    });
            }

            _logger.LogWarning("Azure embedding health check failed - service unavailable");
            return HealthCheckResult.Degraded(
                "Azure embedding service is configured but not responding");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure embedding health check failed");
            return HealthCheckResult.Unhealthy(
                "Azure embedding health check failed",
                ex);
        }
    }
}

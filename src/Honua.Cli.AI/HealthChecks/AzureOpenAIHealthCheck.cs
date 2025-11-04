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
/// Health check for Azure OpenAI LLM provider connectivity.
/// </summary>
public sealed class AzureOpenAIHealthCheck : IHealthCheck
{
    private readonly ILlmProviderFactory _factory;
    private readonly ILogger<AzureOpenAIHealthCheck> _logger;

    public AzureOpenAIHealthCheck(
        ILlmProviderFactory factory,
        ILogger<AzureOpenAIHealthCheck> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to create Azure provider
            var provider = _factory.CreateProvider("azure");

            // Check if it's available
            var isAvailable = await provider.IsAvailableAsync(cancellationToken);

            if (isAvailable)
            {
                _logger.LogDebug("Azure OpenAI health check passed");
                return HealthCheckResult.Healthy(
                    "Azure OpenAI is available",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["provider"] = provider.ProviderName,
                        ["model"] = provider.DefaultModel
                    });
            }

            _logger.LogWarning("Azure OpenAI health check failed - service unavailable");
            return HealthCheckResult.Degraded(
                "Azure OpenAI is configured but not responding",
                data: new System.Collections.Generic.Dictionary<string, object>
                {
                    ["provider"] = provider.ProviderName
                });
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI provider not configured");
            return HealthCheckResult.Unhealthy(
                "Azure OpenAI provider is not configured",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI health check failed");
            return HealthCheckResult.Unhealthy(
                "Azure OpenAI health check failed",
                ex);
        }
    }
}

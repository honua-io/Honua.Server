// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Attempts to pull live metrics from deployment observability endpoints to inform guardrail evaluation.
/// Falls back to heuristic values when endpoints are unavailable.
/// </summary>
public sealed class HttpDeploymentMetricsProvider : IDeploymentMetricsProvider
{
    private static readonly Regex PrometheusSample = new(@"^\s*(?<name>[a-zA-Z_:][a-zA-Z0-9_:]*)\s+(?<value>[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?)", RegexOptions.Compiled);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpDeploymentMetricsProvider> _logger;

    public HttpDeploymentMetricsProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpDeploymentMetricsProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeploymentGuardrailMetrics> GetMetricsAsync(
        DeploymentState state,
        DeploymentGuardrailDecision decision,
        CancellationToken cancellationToken = default)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (decision is null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        if (state.InfrastructureOutputs != null &&
            state.InfrastructureOutputs.TryGetValue("metrics_endpoint", out var metricsEndpoint) &&
            metricsEndpoint.HasValue())
        {
            if (!TryValidateEndpoint(metricsEndpoint, out var metricsUri))
            {
                _logger.LogWarning("Rejected metrics endpoint {Endpoint}: endpoint is not permitted.", metricsEndpoint);
            }
            else
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("GuardrailMetrics");
                    using var response = await client.GetAsync(metricsUri, cancellationToken).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        var parsed = ParsePrometheusPayload(payload);

                        if (parsed != null)
                        {
                            return parsed;
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Metrics endpoint {Endpoint} returned status {StatusCode}",
                            metricsUri,
                            response.StatusCode);
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex,
                        "Failed to retrieve guardrail metrics from {Endpoint}, falling back to heuristic values",
                        metricsUri);
                }
            }
        }

        return BuildHeuristicMetrics(state, decision);
    }

    private static DeploymentGuardrailMetrics BuildHeuristicMetrics(
        DeploymentState state,
        DeploymentGuardrailDecision decision)
    {
        var envelope = decision.Envelope;
        var cpuLoad = envelope.MinVCpu * 0.6m;
        var memoryLoad = envelope.MinMemoryGb * 0.55m;
        var coldStarts = envelope.MinProvisionedConcurrency.HasValue ? 1 : 0;
        var backlog = 0;

        if (state.ConcurrentUsers is { } concurrency && concurrency > 0)
        {
            var saturationRatio = (decimal)concurrency / Math.Max(1, envelope.MinInstances * 250);
            if (saturationRatio > 1m)
            {
                cpuLoad = Math.Min(envelope.MinVCpu, envelope.MinVCpu * saturationRatio);
                memoryLoad = Math.Min(envelope.MinMemoryGb, envelope.MinMemoryGb * saturationRatio);
                backlog = (int)Math.Ceiling((saturationRatio - 1m) * envelope.MinInstances * 50);
            }
        }

        return new DeploymentGuardrailMetrics(cpuLoad, memoryLoad, coldStarts, backlog);
    }

    private static DeploymentGuardrailMetrics? ParsePrometheusPayload(string payload)
    {
        if (payload.IsNullOrWhiteSpace())
        {
            return null;
        }

        decimal? cpu = null;
        decimal? memory = null;
        int? coldStarts = null;
        int? backlog = null;

        foreach (var line in payload.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var match = PrometheusSample.Match(trimmed);
            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups["name"].Value;
            var valueText = match.Groups["value"].Value;

            if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                continue;
            }

            switch (name)
            {
                case "honua_cpu_utilization_cores":
                case "container_cpu_usage_cores":
                    cpu = (decimal)numeric;
                    break;
                case "honua_memory_utilization_gib":
                case "container_memory_usage_gib":
                    memory = (decimal)numeric;
                    break;
                case "honua_cold_starts_per_hour":
                    coldStarts = (int)Math.Round(numeric);
                    break;
                case "honua_queue_backlog":
                    backlog = (int)Math.Round(numeric);
                    break;
            }
        }

        if (cpu is null && memory is null && coldStarts is null && backlog is null)
        {
            return null;
        }

        return new DeploymentGuardrailMetrics(
            cpu ?? 0,
            memory ?? 0,
            coldStarts ?? 0,
            backlog ?? 0);
    }

    private static bool TryValidateEndpoint(string endpoint, out Uri uri)
    {
        uri = null!;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parsed.IsLoopback)
        {
            return false;
        }

        if (IPAddress.TryParse(parsed.Host, out var address))
        {
            if (IsPrivateAddress(address))
            {
                return false;
            }
        }
        else
        {
            if (!HasPublicHostSegment(parsed.Host))
            {
                return false;
            }
        }

        uri = parsed;
        return true;
    }

    private static bool HasPublicHostSegment(string host)
    {
        if (host.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Require a dot to avoid single-label hostnames that typically map to internal infrastructure.
        return host.Contains('.', StringComparison.Ordinal);
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var octets = address.GetAddressBytes();
            return octets[0] switch
            {
                10 => true,
                172 when octets[1] >= 16 && octets[1] <= 31 => true,
                192 when octets[1] == 168 => true,
                169 when octets[1] == 254 => true,
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal ||
                   address.IsIPv6Multicast ||
                   address.IsIPv6SiteLocal ||
                   address.Equals(IPAddress.IPv6Loopback);
        }

        return false;
    }
}

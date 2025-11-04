// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Telemetry;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Guardrails;

public interface IDeploymentGuardrailMonitor
{
    Task EvaluateAsync(
        DeploymentGuardrailDecision decision,
        DeploymentGuardrailMetrics metrics,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compares live metrics with the resource envelope and surfaces optimization or saturation warnings.
/// </summary>
public sealed class PostDeployGuardrailMonitor : IDeploymentGuardrailMonitor
{
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<PostDeployGuardrailMonitor> _logger;

    public PostDeployGuardrailMonitor(
        ITelemetryService telemetry,
        ILogger<PostDeployGuardrailMonitor> logger)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EvaluateAsync(
        DeploymentGuardrailDecision decision,
        DeploymentGuardrailMetrics metrics,
        CancellationToken cancellationToken = default)
    {
        if (decision is null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        if (metrics is null)
        {
            throw new ArgumentNullException(nameof(metrics));
        }

        var envelope = decision.Envelope;
        var saturationDetected = false;

        if (metrics.CpuUtilization >= envelope.MinVCpu * 0.8m)
        {
            saturationDetected = true;
            _logger.LogWarning(
                "CPU utilization {Cpu} nearing guardrail for envelope {EnvelopeId} (min {MinCpu} vCPU).",
                metrics.CpuUtilization, envelope.Id, envelope.MinVCpu);
        }

        if (metrics.MemoryUtilizationGb >= envelope.MinMemoryGb * 0.8m)
        {
            saturationDetected = true;
            _logger.LogWarning(
                "Memory utilization {Memory} nearing guardrail for envelope {EnvelopeId} (min {MinMemory} GiB).",
                metrics.MemoryUtilizationGb, envelope.Id, envelope.MinMemoryGb);
        }

        if (envelope.MinProvisionedConcurrency is { } minConcurrency &&
            metrics.ColdStartsPerHour > 0 &&
            metrics.ColdStartsPerHour > Math.Max(5, minConcurrency / 2))
        {
            saturationDetected = true;
            _logger.LogWarning(
                "Cold starts ({ColdStarts}/hr) breaching guardrail for envelope {EnvelopeId}.",
                metrics.ColdStartsPerHour, envelope.Id);
        }

        if (metrics.QueueBacklog > envelope.MinInstances * 2)
        {
            saturationDetected = true;
            _logger.LogWarning(
                "Queue backlog ({Backlog}) suggests under-provisioned capacity for envelope {EnvelopeId}.",
                metrics.QueueBacklog, envelope.Id);
        }

        if (!saturationDetected && metrics.CpuUtilization <= envelope.MinVCpu * 0.2m &&
            metrics.QueueBacklog == 0)
        {
            _logger.LogInformation(
                "Workload profile {Profile} running well below guardrail {EnvelopeId}; consider downsizing.",
                decision.WorkloadProfile, envelope.Id);

            if (_telemetry.IsEnabled)
            {
                await _telemetry.TrackFeatureAsync(
                    "guardrail.optimization",
                    new()
                    {
                        ["envelopeId"] = envelope.Id,
                        ["workloadProfile"] = decision.WorkloadProfile,
                        ["signal"] = "underutilized"
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (!saturationDetected)
        {
            return;
        }

        if (_telemetry.IsEnabled)
        {
            await _telemetry.TrackFeatureAsync(
                "guardrail.alert",
                new()
                {
                    ["envelopeId"] = envelope.Id,
                    ["workloadProfile"] = decision.WorkloadProfile,
                    ["cpu"] = metrics.CpuUtilization.ToString(),
                    ["memory"] = metrics.MemoryUtilizationGb.ToString(),
                    ["coldStarts"] = metrics.ColdStartsPerHour.ToString(),
                    ["queueBacklog"] = metrics.QueueBacklog.ToString()
                },
                cancellationToken).ConfigureAwait(false);
        }
    }
}

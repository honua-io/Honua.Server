// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Minimal telemetry payload used to evaluate post-deployment guardrails.
/// Derived from observability pipeline once the deployment is live.
/// </summary>
public sealed record DeploymentGuardrailMetrics(
    decimal CpuUtilization,
    decimal MemoryUtilizationGb,
    int ColdStartsPerHour,
    int QueueBacklog,
    decimal? AverageLatencyMs = null);

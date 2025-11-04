// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes.State;

namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Retrieves live deployment metrics used to evaluate guardrail envelopes.
/// </summary>
public interface IDeploymentMetricsProvider
{
    Task<DeploymentGuardrailMetrics> GetMetricsAsync(
        DeploymentState state,
        DeploymentGuardrailDecision decision,
        CancellationToken cancellationToken = default);
}

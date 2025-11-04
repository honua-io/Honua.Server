using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Processes.State;

namespace Honua.Cli.AI.Tests.TestInfrastructure;

internal sealed class TestDeploymentMetricsProvider : IDeploymentMetricsProvider
{
    public Task<DeploymentGuardrailMetrics> GetMetricsAsync(
        DeploymentState state,
        DeploymentGuardrailDecision decision,
        CancellationToken cancellationToken = default)
    {
        var envelope = decision.Envelope;

        var metrics = new DeploymentGuardrailMetrics(
            CpuUtilization: envelope.MinVCpu * 0.5m,
            MemoryUtilizationGb: envelope.MinMemoryGb * 0.5m,
            ColdStartsPerHour: 0,
            QueueBacklog: 0);

        return Task.FromResult(metrics);
    }
}

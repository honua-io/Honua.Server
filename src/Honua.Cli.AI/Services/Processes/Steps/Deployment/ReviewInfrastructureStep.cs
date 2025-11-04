// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DeploymentState = Honua.Cli.AI.Services.Processes.State.DeploymentState;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Presents infrastructure code to user for review and approval.
/// </summary>
public class ReviewInfrastructureStep : KernelProcessStep<DeploymentState>
{
    private readonly ILogger<ReviewInfrastructureStep> _logger;
    private DeploymentState _state = new();

    public ReviewInfrastructureStep(ILogger<ReviewInfrastructureStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        _state = state.State ?? new DeploymentState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ReviewInfrastructure")]
    public async Task ReviewInfrastructureAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Presenting infrastructure for review: {DeploymentId}", _state.DeploymentId);

        _state.Status = "AwaitingApproval";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // In a real implementation, this would present the Terraform code to the user
                    // and wait for their approval via a UI or CLI prompt
                    // For now, we emit an event that requires external approval

                    var guardrailSummary = _state.GuardrailDecision is null
                        ? null
                        : new
                        {
                            envelopeId = _state.GuardrailDecision.Envelope.Id,
                            provider = _state.GuardrailDecision.Envelope.CloudProvider,
                            profile = _state.GuardrailDecision.WorkloadProfile,
                            minVCpu = _state.GuardrailDecision.Envelope.MinVCpu,
                            minMemoryGb = _state.GuardrailDecision.Envelope.MinMemoryGb,
                            minInstances = _state.GuardrailDecision.Envelope.MinInstances,
                            requestedSizing = _state.GuardrailDecision.RequestedSizing,
                            justification = _state.GuardrailDecision.Justification,
                            usesOverride = _state.GuardrailDecision.UsesOverride,
                            timestampUtc = _state.GuardrailDecision.TimestampUtc
                        };

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "ApprovalRequired",
                        Data = new
                        {
                            _state.DeploymentId,
                            _state.InfrastructureCode,
                            _state.EstimatedMonthlyCost,
                            _state.CloudProvider,
                            _state.Region,
                            Guardrail = guardrailSummary,
                            GuardrailHistory = _state.GuardrailHistory,
                            Summary = BuildReviewSummary()
                        }
                    });
                },
                _logger,
                "ReviewInfrastructure");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to present infrastructure for review after retries for {DeploymentId}", _state.DeploymentId);
            _state.Status = "ReviewFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ReviewFailed",
                Data = new { _state.DeploymentId, Error = ex.Message }
            });
        }
    }

    [KernelFunction("ProcessApproval")]
    public async Task ProcessApprovalAsync(
        KernelProcessStepContext context,
        bool approved)
    {
        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    _state.UserApproved = approved;

                    if (approved)
                    {
                        _logger.LogInformation("Infrastructure approved for deployment {DeploymentId}", _state.DeploymentId);
                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "InfrastructureApproved",
                            Data = _state
                        });
                    }
                    else
                    {
                        _logger.LogWarning("Infrastructure rejected for deployment {DeploymentId}", _state.DeploymentId);
                        _state.Status = "Rejected";
                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "InfrastructureRejected",
                            Data = _state
                        });
                    }
                },
                _logger,
                "ProcessApproval");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process approval after retries for {DeploymentId}", _state.DeploymentId);
            _state.Status = "ApprovalProcessingFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ApprovalProcessingFailed",
                Data = new { _state.DeploymentId, Error = ex.Message }
            });
        }
    }

    private object BuildReviewSummary()
    {
        var existing = _state.ExistingInfrastructure ?? ExistingInfrastructurePreference.Default;

        return new
        {
            DeploymentId = _state.DeploymentId,
            CloudProvider = _state.CloudProvider,
            Region = _state.Region,
            Tier = _state.Tier,
            Features = _state.Features,
            EstimatedMonthlyCost = _state.EstimatedMonthlyCost,
            ReusePreferences = new
            {
                existing.ReuseNetwork,
                existing.ReuseDatabase,
                existing.ReuseDns,
                NetworkId = _state.InfrastructureOutputs?.GetValueOrDefault("existing_network_id") ?? existing.ExistingNetworkId,
                DatabaseId = _state.InfrastructureOutputs?.GetValueOrDefault("existing_database_id") ?? existing.ExistingDatabaseId,
                DnsZoneId = _state.InfrastructureOutputs?.GetValueOrDefault("existing_dns_zone_id") ?? existing.ExistingDnsZoneId,
                existing.NetworkNotes,
                existing.DatabaseNotes,
                existing.DnsNotes
            },
            Discovery = _state.DiscoverySnapshot is null ? null : new
            {
                Networks = _state.DiscoverySnapshot.Networks.Count,
                Databases = _state.DiscoverySnapshot.Databases.Count,
                DnsZones = _state.DiscoverySnapshot.DnsZones.Count
            }
        };
    }
}

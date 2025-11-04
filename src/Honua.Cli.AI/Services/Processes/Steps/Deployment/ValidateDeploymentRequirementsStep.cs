// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Discovery;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DeploymentState = Honua.Cli.AI.Services.Processes.State.DeploymentState;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Validates deployment requirements including cloud provider, region, and credentials.
/// </summary>
public class ValidateDeploymentRequirementsStep : KernelProcessStep<DeploymentState>
{
    private readonly ILogger<ValidateDeploymentRequirementsStep> _logger;
    private readonly IDeploymentGuardrailValidator _guardrailValidator;
    private static readonly ICloudDiscoveryService NullDiscoveryService = new NullCloudDiscoveryService();
    private readonly ICloudDiscoveryService _discoveryService;
    private DeploymentState _state = new();

    public ValidateDeploymentRequirementsStep(
        ILogger<ValidateDeploymentRequirementsStep> logger,
        IDeploymentGuardrailValidator guardrailValidator,
        ICloudDiscoveryService? discoveryService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _guardrailValidator = guardrailValidator ?? throw new ArgumentNullException(nameof(guardrailValidator));
        _discoveryService = discoveryService ?? NullDiscoveryService;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        _state = state.State ?? new DeploymentState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ValidateRequirements")]
    public async Task ValidateRequirementsAsync(
        KernelProcessStepContext context,
        DeploymentRequest request)
    {
        _logger.LogInformation("Validating deployment for {CloudProvider} in {Region}",
            request.CloudProvider, request.Region);

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Update state
                    _state.DeploymentId = Guid.NewGuid().ToString();
                    _state.CloudProvider = request.CloudProvider;
                    _state.Region = request.Region;
                    _state.DeploymentName = request.DeploymentName;
                    _state.Tier = request.Tier;
                    _state.Features = new List<string>(request.Features);
                    _state.StartTime = DateTime.UtcNow;
                    _state.Status = "Validating";
                    _state.ConcurrentUsers = request.ConcurrentUsers;
                    _state.DataVolumeGb = request.DataVolumeGb;

                    // Validation logic
                    var supportedProviders = new[] { "AWS", "Azure", "GCP" };
                    if (!supportedProviders.Contains(request.CloudProvider, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.LogError("Unsupported cloud provider: {Provider}", request.CloudProvider);
                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "RequirementsInvalid",
                            Data = new { Error = $"Unsupported provider: {request.CloudProvider}" }
                        });
                        return;
                    }

                    // Apply resource guardrail envelope
                    var guardrailResult = _guardrailValidator.Validate(
                        request.CloudProvider,
                        request.WorkloadProfile,
                        request.Sizing,
                        request.GuardrailJustification);

                    if (!guardrailResult.IsValid)
                    {
                        _logger.LogError(
                            "Guardrail validation failed for deployment {DeploymentId}: {Violations}",
                            _state.DeploymentId,
                            string.Join("; ", guardrailResult.Decision.Violations.Select(v => $"{v.Field}: {v.Message}")));

                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "RequirementsInvalid",
                            Data = new
                            {
                                Error = "Guardrail validation failed",
                                guardrailResult.Decision.Violations
                            }
                        });
                        return;
                    }

                    var decision = guardrailResult.Decision;
                    _state.WorkloadProfile = decision.WorkloadProfile;
                    _state.GuardrailDecision = decision;
                    _state.GuardrailHistory ??= new List<GuardrailAuditEntry>();
                    _state.GuardrailHistory.Add(new GuardrailAuditEntry
                    {
                        EnvelopeId = decision.Envelope.Id,
                        CloudProvider = decision.Envelope.CloudProvider,
                        WorkloadProfile = decision.WorkloadProfile,
                        UsesOverride = decision.UsesOverride,
                        RequestedSizing = decision.RequestedSizing,
                        Violations = decision.Violations,
                        Justification = decision.Justification,
                        TimestampUtc = decision.TimestampUtc
                    });

                    _logger.LogInformation(
                        "Applied guardrail envelope {EnvelopeId} for workload profile {Profile}",
                        decision.Envelope.Id,
                        decision.WorkloadProfile);
                    _logger.LogInformation(
                        "Guardrail summary: vCPU >= {MinVCpu}, Memory >= {MinMemory} GiB, Instances >= {MinInstances}. Override={UsesOverride} Justification={Justification}",
                        decision.Envelope.MinVCpu,
                        decision.Envelope.MinMemoryGb,
                        decision.Envelope.MinInstances,
                        decision.UsesOverride,
                        decision.Justification ?? "n/a");

                    var existingPreference = request.ExistingInfrastructure ?? ExistingInfrastructurePreference.Default;
                    _state.ExistingInfrastructure = existingPreference;
                    _logger.LogInformation(
                        "Existing infrastructure preferences: reuseNetwork={ReuseNetwork}, reuseDatabase={ReuseDatabase}, reuseDns={ReuseDns}",
                        existingPreference.ReuseNetwork,
                        existingPreference.ReuseDatabase,
                        existingPreference.ReuseDns);

                    if (!request.SkipDiscovery)
                    {
                        try
                        {
                            var discovery = await _discoveryService.DiscoverAsync(
                                new CloudDiscoveryRequest(request.CloudProvider, request.Region),
                                CancellationToken.None).ConfigureAwait(false);

                            _state.DiscoverySnapshot = discovery;
                            _logger.LogInformation(
                                "Discovery summary for {Provider}: {Networks} networks, {Databases} databases, {Zones} DNS zones",
                                discovery.CloudProvider,
                                discovery.Networks.Count,
                                discovery.Databases.Count,
                                discovery.DnsZones.Count);

                            ApplyExistingInfrastructurePreferences(existingPreference, discovery);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Cloud discovery failed; continuing with default deployment plan");
                            _state.DiscoverySnapshot = null;
                            ApplyExistingInfrastructurePreferences(existingPreference, null);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Discovery skipped per request for deployment {DeploymentId}", _state.DeploymentId);
                        _state.DiscoverySnapshot = null;
                        ApplyExistingInfrastructurePreferences(existingPreference, null);
                    }

                    _logger.LogInformation("Requirements valid for deployment {DeploymentId}",
                        _state.DeploymentId);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "RequirementsValid",
                        Data = _state
                    });
                },
                _logger,
                "ValidateDeploymentRequirements");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate deployment requirements after retries");
            _state.Status = "ValidationFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "RequirementsInvalid",
                Data = new { Error = ex.Message }
            });
        }
    }

    private void ApplyExistingInfrastructurePreferences(ExistingInfrastructurePreference preference, CloudDiscoverySnapshot? snapshot)
    {
        if (_state.InfrastructureOutputs is null)
        {
            return;
        }

        _state.InfrastructureOutputs["reuse_network"] = preference.ReuseNetwork.ToString().ToLowerInvariant();
        _state.InfrastructureOutputs["reuse_database"] = preference.ReuseDatabase.ToString().ToLowerInvariant();
        _state.InfrastructureOutputs["reuse_dns"] = preference.ReuseDns.ToString().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(preference.NetworkNotes))
        {
            _state.InfrastructureOutputs["network_notes"] = preference.NetworkNotes!;
        }

        if (!string.IsNullOrWhiteSpace(preference.DatabaseNotes))
        {
            _state.InfrastructureOutputs["database_notes"] = preference.DatabaseNotes!;
        }

        if (!string.IsNullOrWhiteSpace(preference.DnsNotes))
        {
            _state.InfrastructureOutputs["dns_notes"] = preference.DnsNotes!;
        }

        if (snapshot is null)
        {
            return;
        }

        if (preference.ReuseNetwork && !string.IsNullOrWhiteSpace(preference.ExistingNetworkId))
        {
            var match = TryMatchNetwork(preference.ExistingNetworkId, snapshot);
            if (match is not null)
            {
                _logger.LogInformation("Matched existing network {NetworkId} ({Name}) for reuse", match.Id, match.Name);
                _state.InfrastructureOutputs["existing_network_id"] = match.Id;
                _state.InfrastructureOutputs["existing_network_name"] = match.Name;
                if (match.Subnets.Count > 0)
                {
                    _state.InfrastructureOutputs["existing_subnet_ids"] = string.Join(',', match.Subnets.Select(s => s.Id));
                    _state.InfrastructureOutputs["existing_subnet_cidrs"] = string.Join(',', match.Subnets.Select(s => s.Cidr));
                }
            }
            else
            {
                _logger.LogWarning("Requested existing network '{NetworkId}' not found in discovery snapshot", preference.ExistingNetworkId);
            }
        }

        if (preference.ReuseDatabase && !string.IsNullOrWhiteSpace(preference.ExistingDatabaseId))
        {
            var match = TryMatchDatabase(preference.ExistingDatabaseId, snapshot);
            if (match is not null)
            {
                _logger.LogInformation("Matched existing database {DatabaseId} ({Engine}) for reuse", match.Identifier, match.Engine);
                _state.InfrastructureOutputs["existing_database_id"] = match.Identifier;
                _state.InfrastructureOutputs["existing_database_engine"] = match.Engine ?? string.Empty;
                _state.InfrastructureOutputs["existing_database_status"] = match.Status ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(match.Endpoint))
                {
                    _state.InfrastructureOutputs["database_endpoint"] = match.Endpoint!;
                }
            }
            else
            {
                _logger.LogWarning("Requested existing database '{DatabaseId}' not found in discovery snapshot", preference.ExistingDatabaseId);
            }
        }

        if (preference.ReuseDns && !string.IsNullOrWhiteSpace(preference.ExistingDnsZoneId))
        {
            var match = TryMatchDnsZone(preference.ExistingDnsZoneId, snapshot);
            if (match is not null)
            {
                _logger.LogInformation("Matched existing DNS zone {ZoneId} ({Name}) for reuse", match.Id, match.Name);
                _state.InfrastructureOutputs["existing_dns_zone_id"] = match.Id;
                _state.InfrastructureOutputs["existing_dns_zone_name"] = match.Name;
                _state.InfrastructureOutputs["existing_dns_zone_private"] = match.IsPrivate.ToString().ToLowerInvariant();
            }
            else
            {
                _logger.LogWarning("Requested existing DNS zone '{ZoneId}' not found in discovery snapshot", preference.ExistingDnsZoneId);
            }
        }
    }

    private static DiscoveredNetwork? TryMatchNetwork(string identifierOrName, CloudDiscoverySnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(identifierOrName))
        {
            return null;
        }

        return snapshot.Networks.FirstOrDefault(n =>
            string.Equals(n.Id, identifierOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Name, identifierOrName, StringComparison.OrdinalIgnoreCase));
    }

    private static DiscoveredDatabase? TryMatchDatabase(string identifierOrName, CloudDiscoverySnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(identifierOrName))
        {
            return null;
        }

        return snapshot.Databases.FirstOrDefault(d =>
            string.Equals(d.Identifier, identifierOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.Endpoint, identifierOrName, StringComparison.OrdinalIgnoreCase));
    }

    private static DiscoveredDnsZone? TryMatchDnsZone(string identifierOrName, CloudDiscoverySnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(identifierOrName))
        {
            return null;
        }

        return snapshot.DnsZones.FirstOrDefault(z =>
            string.Equals(z.Id, identifierOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(z.Name?.TrimEnd('.'), identifierOrName.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
    }

    private sealed class NullCloudDiscoveryService : ICloudDiscoveryService
    {
        public Task<CloudDiscoverySnapshot> DiscoverAsync(CloudDiscoveryRequest request, CancellationToken cancellationToken)
        {
            var provider = request?.CloudProvider ?? "Unknown";

            var snapshot = new CloudDiscoverySnapshot(
                CloudProvider: provider,
                Networks: Array.Empty<DiscoveredNetwork>(),
                Databases: Array.Empty<DiscoveredDatabase>(),
                DnsZones: Array.Empty<DiscoveredDnsZone>());

            return Task.FromResult(snapshot);
        }
    }
}

/// <summary>
/// Request object for deployment validation.
/// </summary>
public record DeploymentRequest(
    string CloudProvider,
    string Region,
    string DeploymentName,
    string Tier,
    List<string> Features,
    string? WorkloadProfile = null,
    int? ConcurrentUsers = null,
    int? DataVolumeGb = null,
    DeploymentSizingRequest? Sizing = null,
    string? GuardrailJustification = null,
    ExistingInfrastructurePreference? ExistingInfrastructure = null,
    bool SkipDiscovery = false);

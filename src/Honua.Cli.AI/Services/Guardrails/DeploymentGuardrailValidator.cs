// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Guardrails;

public interface IDeploymentGuardrailValidator
{
    GuardrailValidationResult Validate(
        string cloudProvider,
        string? workloadProfile,
        DeploymentSizingRequest? requestedSizing,
        string? justification = null);
}

/// <summary>
/// Validates deployment sizing inputs against the guardrail catalog.
/// </summary>
public sealed class DeploymentGuardrailValidator : IDeploymentGuardrailValidator
{
    private readonly IResourceEnvelopeCatalog _catalog;

    public DeploymentGuardrailValidator(IResourceEnvelopeCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public GuardrailValidationResult Validate(
        string cloudProvider,
        string? workloadProfile,
        DeploymentSizingRequest? requestedSizing,
        string? justification = null)
    {
        if (cloudProvider.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Cloud provider is required.", nameof(cloudProvider));
        }

        var normalizedProvider = cloudProvider.Trim();
        var profile = workloadProfile.IsNullOrWhiteSpace()
            ? _catalog.GetDefaultWorkloadProfile(normalizedProvider)
            : workloadProfile.Trim();

        var envelope = _catalog.Resolve(normalizedProvider, profile);
        var violations = new List<GuardrailViolation>();

        if (requestedSizing?.RequestedVCpu is { } vCpu && vCpu < envelope.MinVCpu)
        {
            violations.Add(new GuardrailViolation(
                nameof(requestedSizing.RequestedVCpu),
                $"Requested {vCpu} vCPU but envelope '{envelope.Id}' requires >= {envelope.MinVCpu} vCPU."));
        }

        if (requestedSizing?.RequestedMemoryGb is { } memory && memory < envelope.MinMemoryGb)
        {
            violations.Add(new GuardrailViolation(
                nameof(requestedSizing.RequestedMemoryGb),
                $"Requested {memory} GiB but envelope '{envelope.Id}' requires >= {envelope.MinMemoryGb} GiB."));
        }

        if (requestedSizing?.RequestedEphemeralStorageGb is { } storage && storage < envelope.MinEphemeralGb)
        {
            violations.Add(new GuardrailViolation(
                nameof(requestedSizing.RequestedEphemeralStorageGb),
                $"Requested {storage} GiB ephemeral storage but envelope '{envelope.Id}' requires >= {envelope.MinEphemeralGb} GiB."));
        }

        if (requestedSizing?.RequestedMinInstances is { } instances && instances < envelope.MinInstances)
        {
            violations.Add(new GuardrailViolation(
                nameof(requestedSizing.RequestedMinInstances),
                $"Requested {instances} instances but envelope '{envelope.Id}' requires >= {envelope.MinInstances} instances."));
        }

        if (requestedSizing?.RequestedProvisionedConcurrency is { } concurrency &&
            envelope.MinProvisionedConcurrency is { } minProvisionedConcurrency &&
            concurrency < minProvisionedConcurrency)
        {
            violations.Add(new GuardrailViolation(
                nameof(requestedSizing.RequestedProvisionedConcurrency),
                $"Requested {concurrency} provisioned concurrency but envelope '{envelope.Id}' requires >= {minProvisionedConcurrency}."));
        }

        var usesOverride = requestedSizing != null;

        var decision = new DeploymentGuardrailDecision
        {
            WorkloadProfile = profile,
            Envelope = envelope,
            RequestedSizing = requestedSizing,
            Violations = violations,
            UsesOverride = usesOverride,
            Justification = justification
        };

        return new GuardrailValidationResult(violations.Count == 0, decision);
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Describes the minimum viable resource envelope for a workload profile on a specific platform.
/// </summary>
public sealed class ResourceEnvelope
{
    public ResourceEnvelope(
        string id,
        string cloudProvider,
        string platform,
        string workloadProfile,
        decimal minVCpu,
        decimal minMemoryGb,
        decimal minEphemeralGb,
        int minInstances,
        int? minProvisionedConcurrency = null,
        int? maxColdStartsPerHour = null,
        IReadOnlyList<string>? requiredAttachments = null,
        string? notes = null)
    {
        Id = id.IsNullOrWhiteSpace()
            ? throw new ArgumentException("Envelope id is required", nameof(id))
            : id;

        CloudProvider = cloudProvider.IsNullOrWhiteSpace()
            ? throw new ArgumentException("Cloud provider is required", nameof(cloudProvider))
            : cloudProvider;

        Platform = platform.IsNullOrWhiteSpace()
            ? throw new ArgumentException("Platform is required", nameof(platform))
            : platform;

        WorkloadProfile = workloadProfile.IsNullOrWhiteSpace()
            ? throw new ArgumentException("Workload profile is required", nameof(workloadProfile))
            : workloadProfile;

        if (minVCpu <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minVCpu), minVCpu, "Minimum vCPU must be positive.");
        }

        if (minMemoryGb <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minMemoryGb), minMemoryGb, "Minimum memory must be positive.");
        }

        if (minInstances < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minInstances), minInstances, "Minimum instances must be at least 1.");
        }

        if (minEphemeralGb < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minEphemeralGb), minEphemeralGb, "Ephemeral storage cannot be negative.");
        }

        if (minProvisionedConcurrency is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minProvisionedConcurrency), minProvisionedConcurrency, "Provisioned concurrency cannot be negative.");
        }

        Id = id;
        CloudProvider = cloudProvider;
        Platform = platform;
        WorkloadProfile = workloadProfile;
        MinVCpu = minVCpu;
        MinMemoryGb = minMemoryGb;
        MinEphemeralGb = minEphemeralGb;
        MinInstances = minInstances;
        MinProvisionedConcurrency = minProvisionedConcurrency;
        MaxColdStartsPerHour = maxColdStartsPerHour;
        RequiredAttachments = requiredAttachments ?? Array.Empty<string>();
        Notes = notes;
    }

    public string Id { get; }
    public string CloudProvider { get; }
    public string Platform { get; }
    public string WorkloadProfile { get; }
    public decimal MinVCpu { get; }
    public decimal MinMemoryGb { get; }
    public decimal MinEphemeralGb { get; }
    public int MinInstances { get; }
    public int? MinProvisionedConcurrency { get; }
    public int? MaxColdStartsPerHour { get; }
    public IReadOnlyList<string> RequiredAttachments { get; }
    public string? Notes { get; }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Hard-coded minimum guardrail envelopes for the initial MVP.
/// These values should be refined with production telemetry and ADR sign-off.
/// </summary>
public sealed class ResourceEnvelopeCatalog : IResourceEnvelopeCatalog
{
    private static readonly IReadOnlyDictionary<string, string> DefaultProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AWS"] = "api-standard",
        ["Azure"] = "ai-orchestration",
        ["GCP"] = "api-standard"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, ResourceEnvelope>> Catalog =
        new Dictionary<string, IReadOnlyDictionary<string, ResourceEnvelope>>(StringComparer.OrdinalIgnoreCase)
        {
            ["AWS"] = new Dictionary<string, ResourceEnvelope>(StringComparer.OrdinalIgnoreCase)
            {
                ["api-small"] = new ResourceEnvelope(
                    id: "aws-ecs-api-small",
                    cloudProvider: "AWS",
                    platform: "ECS_FARGATE",
                    workloadProfile: "api-small",
                    minVCpu: 1.0m,
                    minMemoryGb: 2.0m,
                    minEphemeralGb: 2.0m,
                    minInstances: 2,
                    notes: "Baseline for low-traffic APIs; enforces ALB slow-start and 2 GiB ephemeral volume."),

                ["api-standard"] = new ResourceEnvelope(
                    id: "aws-ecs-api-standard",
                    cloudProvider: "AWS",
                    platform: "ECS_FARGATE",
                    workloadProfile: "api-standard",
                    minVCpu: 2.0m,
                    minMemoryGb: 4.0m,
                    minEphemeralGb: 4.0m,
                    minInstances: 3,
                    notes: "Default profile for Honua APIs in production; assumes TLS offload and burst traffic handling."),

                ["raster-batch"] = new ResourceEnvelope(
                    id: "aws-ecs-raster-batch",
                    cloudProvider: "AWS",
                    platform: "ECS_FARGATE",
                    workloadProfile: "raster-batch",
                    minVCpu: 4.0m,
                    minMemoryGb: 8.0m,
                    minEphemeralGb: 50.0m,
                    minInstances: 2,
                    notes: "For heavy raster processing queues; requires EFS scratch space."),
            },
            ["Azure"] = new Dictionary<string, ResourceEnvelope>(StringComparer.OrdinalIgnoreCase)
            {
                ["ai-orchestration"] = new ResourceEnvelope(
                    id: "azure-functions-ai-orchestration",
                    cloudProvider: "Azure",
                    platform: "AZURE_FUNCTIONS_PREMIUM",
                    workloadProfile: "ai-orchestration",
                    minVCpu: 1.75m,
                    minMemoryGb: 3.5m,
                    minEphemeralGb: 3.5m,
                    minInstances: 1,
                    minProvisionedConcurrency: 5,
                    maxColdStartsPerHour: 5,
                    notes: "Premium EP1 plan with provisioned concurrency to mitigate cold starts."),

                ["analytics-heavy"] = new ResourceEnvelope(
                    id: "azure-functions-analytics-heavy",
                    cloudProvider: "Azure",
                    platform: "AZURE_FUNCTIONS_PREMIUM",
                    workloadProfile: "analytics-heavy",
                    minVCpu: 4.0m,
                    minMemoryGb: 10.0m,
                    minEphemeralGb: 10.0m,
                    minInstances: 1,
                    minProvisionedConcurrency: 10,
                    maxColdStartsPerHour: 2,
                    notes: "EP3 plan for high-memory geospatial analytics pipelines.")
            },
            ["GCP"] = new Dictionary<string, ResourceEnvelope>(StringComparer.OrdinalIgnoreCase)
            {
                ["api-standard"] = new ResourceEnvelope(
                    id: "gcp-cloudrun-api-standard",
                    cloudProvider: "GCP",
                    platform: "CLOUD_RUN",
                    workloadProfile: "api-standard",
                    minVCpu: 2.0m,
                    minMemoryGb: 4.0m,
                    minEphemeralGb: 2.0m,
                    minInstances: 2,
                    notes: "Cloud Run baseline for production APIs; ensures two revisions warm."),

                ["raster-batch"] = new ResourceEnvelope(
                    id: "gcp-cloudrun-raster-batch",
                    cloudProvider: "GCP",
                    platform: "CLOUD_RUN",
                    workloadProfile: "raster-batch",
                    minVCpu: 4.0m,
                    minMemoryGb: 8.0m,
                    minEphemeralGb: 20.0m,
                    minInstances: 1,
                    notes: "High-memory service for raster processing; consider Cloud Storage scratch space.")
            }
        };

    public ResourceEnvelope Resolve(string cloudProvider, string workloadProfile)
    {
        if (!Catalog.TryGetValue(cloudProvider, out var profiles))
        {
            throw new ArgumentException($"No guardrail catalog registered for provider '{cloudProvider}'.", nameof(cloudProvider));
        }

        if (!profiles.TryGetValue(workloadProfile, out var envelope))
        {
            throw new ArgumentException($"No guardrail envelope registered for profile '{workloadProfile}' on provider '{cloudProvider}'.", nameof(workloadProfile));
        }

        return envelope;
    }

    public string GetDefaultWorkloadProfile(string cloudProvider)
    {
        if (DefaultProfiles.TryGetValue(cloudProvider, out var profile))
        {
            return profile;
        }

        throw new ArgumentException($"No default workload profile configured for provider '{cloudProvider}'.", nameof(cloudProvider));
    }

    public IReadOnlyCollection<string> ListProfiles(string cloudProvider)
    {
        if (Catalog.TryGetValue(cloudProvider, out var profiles))
        {
            return profiles.Keys.ToList();
        }

        return Array.Empty<string>();
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Optional sizing overrides supplied by the user or requirement extraction.
/// Any values below the guardrail floor will trigger a validation failure.
/// </summary>
public sealed record DeploymentSizingRequest(
    decimal? RequestedVCpu = null,
    decimal? RequestedMemoryGb = null,
    decimal? RequestedEphemeralStorageGb = null,
    int? RequestedMinInstances = null,
    int? RequestedProvisionedConcurrency = null);

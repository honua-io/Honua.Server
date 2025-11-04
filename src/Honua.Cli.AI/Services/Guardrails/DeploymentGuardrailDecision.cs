// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Captures the guardrail decision that was applied to a deployment request.
/// Stored in process state for auditability and feedback.
/// </summary>
public sealed class DeploymentGuardrailDecision
{
    public required string WorkloadProfile { get; init; }
    public required ResourceEnvelope Envelope { get; init; }
    public DeploymentSizingRequest? RequestedSizing { get; init; }
    public IReadOnlyCollection<GuardrailViolation> Violations { get; init; } = Array.Empty<GuardrailViolation>();
    public bool UsesOverride { get; init; }
    public string? Justification { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}

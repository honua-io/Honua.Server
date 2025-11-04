// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Lightweight audit record capturing a guardrail decision applied during deployment planning.
/// </summary>
public sealed class GuardrailAuditEntry
{
    public required string EnvelopeId { get; init; }
    public required string CloudProvider { get; init; }
    public required string WorkloadProfile { get; init; }
    public bool UsesOverride { get; init; }
    public DeploymentSizingRequest? RequestedSizing { get; init; }
    public IReadOnlyCollection<GuardrailViolation> Violations { get; init; } = Array.Empty<GuardrailViolation>();
    public string? Justification { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}

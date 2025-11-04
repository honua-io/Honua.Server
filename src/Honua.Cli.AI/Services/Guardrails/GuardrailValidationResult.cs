// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.Guardrails;

/// <summary>
/// Result from validating a deployment request against guardrails.
/// </summary>
public sealed class GuardrailValidationResult
{
    public GuardrailValidationResult(bool isValid, DeploymentGuardrailDecision decision)
    {
        IsValid = isValid;
        Decision = decision;
    }

    public bool IsValid { get; }
    public DeploymentGuardrailDecision Decision { get; }
}

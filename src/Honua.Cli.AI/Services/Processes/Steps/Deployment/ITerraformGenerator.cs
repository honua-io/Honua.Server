// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Cli.AI.Services.Guardrails;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Interface for cloud-specific Terraform code generators.
/// Implements Strategy pattern for multi-cloud infrastructure generation.
/// </summary>
public interface ITerraformGenerator
{
    /// <summary>
    /// Gets the cloud provider name (e.g., "aws", "azure", "gcp").
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Generates main Terraform configuration (main.tf) for the cloud provider.
    /// </summary>
    /// <param name="envelope">Guardrail-approved resource envelope</param>
    /// <param name="deploymentName">Sanitized deployment name</param>
    /// <returns>Terraform HCL code as string</returns>
    string GenerateMainTerraform(ResourceEnvelope envelope, string deploymentName);

    /// <summary>
    /// Generates Terraform variables definition (variables.tf) for the cloud provider.
    /// </summary>
    /// <returns>Terraform variables HCL code</returns>
    string GenerateVariablesTerraform();

    /// <summary>
    /// Generates Terraform variable values (terraform.tfvars) with secure defaults.
    /// </summary>
    /// <param name="databasePassword">Securely generated database password</param>
    /// <returns>Terraform tfvars content</returns>
    string GenerateTfVars(string databasePassword);

    /// <summary>
    /// Estimates monthly cost in USD for the given resource envelope.
    /// </summary>
    /// <param name="tier">Service tier (Development, Production, Enterprise)</param>
    /// <param name="envelope">Resource envelope with compute/storage requirements</param>
    /// <returns>Estimated monthly cost in USD</returns>
    decimal EstimateMonthlyCost(string tier, ResourceEnvelope envelope);
}

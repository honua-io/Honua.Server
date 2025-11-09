// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DeploymentState = Honua.Cli.AI.Services.Processes.State.DeploymentState;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Generates Terraform infrastructure code for the target cloud provider.
/// This is the main partial class containing core orchestration logic.
/// </summary>
public partial class GenerateInfrastructureCodeStep : KernelProcessStep<DeploymentState>, IProcessStepTimeout
{
    private const int DatabasePasswordLength = 32;
    private readonly ILogger<GenerateInfrastructureCodeStep> _logger;
    private DeploymentState _state = new();

    /// <summary>
    /// Code generation should be fast, but allow time for LLM-based generation if needed.
    /// Default timeout: 5 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);

    public GenerateInfrastructureCodeStep(ILogger<GenerateInfrastructureCodeStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        _state = state.State ?? new DeploymentState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("GenerateInfrastructure")]
    public async Task GenerateInfrastructureAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Generating infrastructure code for {Provider} deployment {DeploymentId}",
            _state.CloudProvider, _state.DeploymentId);

        _state.Status = "GeneratingInfrastructure";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    var envelope = _state.GuardrailDecision?.Envelope
                        ?? throw new InvalidOperationException("Guardrail decision missing; run validation before code generation.");

                    _logger.LogInformation(
                        "Applying guardrail envelope {EnvelopeId} ({Profile})",
                        envelope.Id,
                        envelope.WorkloadProfile);

                    // Generate Terraform code based on cloud provider
                    var terraformCode = _state.CloudProvider.ToLower() switch
                    {
                        "aws" => GenerateAwsTerraform(envelope),
                        "azure" => GenerateAzureTerraform(envelope),
                        "gcp" => GenerateGcpTerraform(envelope),
                        _ => throw new InvalidOperationException($"Unsupported provider: {_state.CloudProvider}")
                    };

                    _state.InfrastructureCode = terraformCode;
                    _state.EstimatedMonthlyCost = CalculateEstimatedCost(_state.CloudProvider, _state.Tier);

                    // Write Terraform files to secure temporary directory with restricted permissions
                    var workspacePath = CreateSecureTempDirectory(_state.DeploymentId);
                    _state.TerraformWorkspacePath = workspacePath;

                    try
                    {
                        // Write main.tf
                        var mainTfPath = Path.Combine(workspacePath, "main.tf");
                        await File.WriteAllTextAsync(mainTfPath, terraformCode);
                        _logger.LogInformation("Wrote main.tf to {Path}", mainTfPath);

                        // Write variables.tf with required variables
                        var variablesTf = GenerateVariablesTf(_state.CloudProvider);
                        var variablesTfPath = Path.Combine(workspacePath, "variables.tf");
                        await File.WriteAllTextAsync(variablesTfPath, variablesTf);
                        _logger.LogInformation("Wrote variables.tf to {Path}", variablesTfPath);

                        // Generate secure passwords and configuration
                        var tfvars = GenerateTfVars(_state.CloudProvider);
                        if (!string.IsNullOrEmpty(tfvars))
                        {
                            var tfvarsPath = Path.Combine(workspacePath, "terraform.tfvars");
                            await File.WriteAllTextAsync(tfvarsPath, tfvars);
                            // Set restrictive permissions on tfvars file containing sensitive data
                            SetRestrictiveFilePermissions(tfvarsPath);
                            _logger.LogInformation("Wrote terraform.tfvars to {Path} with secure permissions", tfvarsPath);
                        }
                    }
                    catch
                    {
                        // Clean up secure temp directory on failure
                        CleanupSecureTempDirectory(workspacePath);
                        throw;
                    }

                    _logger.LogInformation("Generated infrastructure code for deployment {DeploymentId}. Estimated cost: ${Cost}/month",
                        _state.DeploymentId, _state.EstimatedMonthlyCost);
                    _logger.LogInformation("Terraform workspace: {WorkspacePath}", workspacePath);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "InfrastructureGenerated",
                        Data = _state
                    });
                },
                _logger,
                "GenerateInfrastructureCode");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate infrastructure code after retries for {DeploymentId}", _state.DeploymentId);
            _state.Status = "InfrastructureGenerationFailed";

            // Clean up workspace on failure
            if (!string.IsNullOrEmpty(_state.TerraformWorkspacePath) && Directory.Exists(_state.TerraformWorkspacePath))
            {
                CleanupSecureTempDirectory(_state.TerraformWorkspacePath);
            }

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "InfrastructureGenerationFailed",
                Data = new { _state.DeploymentId, Error = ex.Message }
            });
        }
    }

    private decimal CalculateEstimatedCost(string provider, string tier)
    {
        // Simple cost estimation (placeholder logic)
        return (provider.ToLower(), tier.ToLower()) switch
        {
            ("aws", "development") => 45.00m,  // Increased due to NAT Gateway
            ("aws", "staging") => 200.00m,
            ("aws", "production") => 1000.00m,
            ("azure", "development") => 35.00m,
            ("azure", "staging") => 190.00m,
            ("azure", "production") => 950.00m,
            ("gcp", "development") => 32.00m,
            ("gcp", "staging") => 180.00m,
            ("gcp", "production") => 900.00m,
            _ => 0.00m
        };
    }
}

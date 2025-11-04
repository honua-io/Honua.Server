// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Process builder for Honua deployment workflow.
/// Orchestrates 8 steps: validate → generate → review → deploy infra → configure → deploy app → validate → observability.
/// </summary>
public static class DeploymentProcess
{
    public static ProcessBuilder BuildProcess()
    {
        var builder = new ProcessBuilder("HonuaDeployment");

        // Add all 8 steps
        var validateStep = builder.AddStepFromType<ValidateDeploymentRequirementsStep>();
        var generateStep = builder.AddStepFromType<GenerateInfrastructureCodeStep>();
        var reviewStep = builder.AddStepFromType<ReviewInfrastructureStep>();
        var deployInfraStep = builder.AddStepFromType<DeployInfrastructureStep>();
        var configureServicesStep = builder.AddStepFromType<ConfigureServicesStep>();
        var deployAppStep = builder.AddStepFromType<DeployHonuaApplicationStep>();
        var validateDeploymentStep = builder.AddStepFromType<ValidateDeploymentStep>();
        var observabilityStep = builder.AddStepFromType<ConfigureObservabilityStep>();

        // Wire event routing

        // Start: external event → validate
        builder
            .OnInputEvent("StartDeployment")
            .SendEventTo(new ProcessFunctionTargetBuilder(validateStep, "ValidateRequirements"));

        // Validate → Generate
        validateStep
            .OnEvent("RequirementsValid")
            .SendEventTo(new ProcessFunctionTargetBuilder(generateStep, "GenerateInfrastructure"));

        // Generate → Review
        generateStep
            .OnEvent("InfrastructureGenerated")
            .SendEventTo(new ProcessFunctionTargetBuilder(reviewStep, "ReviewInfrastructure"));

        // Review → approval required (wait for external input)
        reviewStep
            .OnEvent("ApprovalRequired")
            .StopProcess(); // Pause process until user provides approval

        // External approval event → review step processes approval
        builder
            .OnInputEvent("UserApproval")
            .SendEventTo(new ProcessFunctionTargetBuilder(reviewStep, "ProcessApproval"));

        // Approved → Deploy infrastructure
        reviewStep
            .OnEvent("InfrastructureApproved")
            .SendEventTo(new ProcessFunctionTargetBuilder(deployInfraStep, "DeployInfrastructure"));

        // Deploy infra → Configure services
        deployInfraStep
            .OnEvent("InfrastructureDeployed")
            .SendEventTo(new ProcessFunctionTargetBuilder(configureServicesStep, "ConfigureServices"));

        // Configure services → Deploy application
        configureServicesStep
            .OnEvent("ServicesConfigured")
            .SendEventTo(new ProcessFunctionTargetBuilder(deployAppStep, "DeployApplication"));

        // Deploy app → Validate deployment
        deployAppStep
            .OnEvent("ApplicationDeployed")
            .SendEventTo(new ProcessFunctionTargetBuilder(validateDeploymentStep, "ValidateDeployment"));

        // Validate → Observability (final step)
        validateDeploymentStep
            .OnEvent("DeploymentValidated")
            .SendEventTo(new ProcessFunctionTargetBuilder(observabilityStep, "ConfigureObservability"));

        // Error handling: any failure triggers rollback
        validateStep
            .OnEvent("RequirementsInvalid")
            .StopProcess();

        deployInfraStep
            .OnEvent("DeploymentFailed")
            .StopProcess(); // Could add rollback step here

        configureServicesStep
            .OnEvent("ConfigurationFailed")
            .StopProcess();

        deployAppStep
            .OnEvent("ApplicationDeploymentFailed")
            .StopProcess();

        validateDeploymentStep
            .OnEvent("ValidationFailed")
            .StopProcess();

        observabilityStep
            .OnEvent("ObservabilityFailed")
            .StopProcess();

        return builder;
    }
}

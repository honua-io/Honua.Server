// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Process builder for TLS/SSL certificate renewal workflow.
/// Orchestrates 9 steps: scan → validate DNS → request → validate domain → issue → deploy → validate → update → notify.
/// Automates Let's Encrypt, ACM, and ZeroSSL certificate lifecycle management.
/// </summary>
public static class CertificateRenewalProcess
{
    public static ProcessBuilder BuildProcess()
    {
        var builder = new ProcessBuilder("CertificateRenewal");

        // Add all 9 steps
        var scanStep = builder.AddStepFromType<ScanCertificatesStep>();
        var checkExpirationStep = builder.AddStepFromType<CheckExpirationStep>();
        var requestStep = builder.AddStepFromType<RequestRenewalStep>();
        var validateDomainStep = builder.AddStepFromType<ValidateDomainStep>();
        var issueStep = builder.AddStepFromType<IssueCertificateStep>();
        var deployStep = builder.AddStepFromType<DeployCertificateStep>();
        var validateDeploymentStep = builder.AddStepFromType<ValidateCertificateDeploymentStep>();
        var updateMonitoringStep = builder.AddStepFromType<UpdateMonitoringStep>();
        var notifyStep = builder.AddStepFromType<NotifyCompletionStep>();

        // Wire event routing

        // Start: external event → scan
        builder
            .OnInputEvent("StartCertificateRenewal")
            .SendEventTo(new ProcessFunctionTargetBuilder(scanStep, "ScanCertificates"));

        // Scan → Check expiration (validate DNS control)
        scanStep
            .OnEvent("ExpiringCertificatesFound")
            .SendEventTo(new ProcessFunctionTargetBuilder(checkExpirationStep, "ValidateDNSControl"));

        // No certificates expiring → Notify (short-circuit)
        scanStep
            .OnEvent("NoCertificatesExpiring")
            .SendEventTo(new ProcessFunctionTargetBuilder(notifyStep, "NotifySuccess"));

        // DNS validation → Request certificate
        checkExpirationStep
            .OnEvent("DNSControlValidated")
            .SendEventTo(new ProcessFunctionTargetBuilder(requestStep, "RequestCertificate"));

        // Request → Complete DNS challenge (validate domain)
        requestStep
            .OnEvent("CertificateRequested")
            .SendEventTo(new ProcessFunctionTargetBuilder(validateDomainStep, "CompleteDNSChallenge"));

        // DNS challenge complete → Obtain certificate
        validateDomainStep
            .OnEvent("DNSChallengeComplete")
            .SendEventTo(new ProcessFunctionTargetBuilder(issueStep, "ObtainCertificate"));

        // Certificate obtained → Deploy certificate
        issueStep
            .OnEvent("CertificateObtained")
            .SendEventTo(new ProcessFunctionTargetBuilder(deployStep, "DeployCertificate"));

        // Deploy → Validate deployment
        deployStep
            .OnEvent("CertificateDeployed")
            .SendEventTo(new ProcessFunctionTargetBuilder(validateDeploymentStep, "ValidateDeployment"));

        // Validation passed → Update monitoring
        validateDeploymentStep
            .OnEvent("ValidationPassed")
            .SendEventTo(new ProcessFunctionTargetBuilder(updateMonitoringStep, "UpdateMonitoring"));

        // Monitoring updated → Notify completion
        updateMonitoringStep
            .OnEvent("MonitoringUpdated")
            .SendEventTo(new ProcessFunctionTargetBuilder(notifyStep, "NotifySuccess"));

        // Error handling: failures stop the process
        checkExpirationStep
            .OnEvent("DNSControlFailed")
            .StopProcess();

        deployStep
            .OnEvent("DeploymentFailed")
            .StopProcess();

        validateDeploymentStep
            .OnEvent("ValidationFailed")
            .StopProcess();

        return builder;
    }
}

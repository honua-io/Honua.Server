// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Certificates;
using CertificateRenewalState = Honua.Cli.AI.Services.Processes.State.CertificateRenewalState;

namespace Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

/// <summary>
/// Validates DNS control for ACME DNS-01 challenge.
/// Checks that the DNS provider API is accessible and we can create/delete TXT records.
/// </summary>
public class CheckExpirationStep : KernelProcessStep<CertificateRenewalState>
{
    private readonly ILogger<CheckExpirationStep> _logger;
    private readonly IDnsControlValidator? _dnsValidator;
    private CertificateRenewalState _state = new();

    public CheckExpirationStep(
        ILogger<CheckExpirationStep> logger,
        IDnsControlValidator? dnsValidator = null)
    {
        _logger = logger;
        _dnsValidator = dnsValidator;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<CertificateRenewalState> state)
    {
        _state = state.State ?? new CertificateRenewalState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ValidateDNSControl")]
    public async Task ValidateDNSControlAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Validating DNS control for domains: {Domains}",
            string.Join(", ", _state.DomainNames));

        _state.Status = "Validating DNS";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    var allDomainsValid = true;

                    // If no validator is configured, fail the step
                    if (_dnsValidator == null)
                    {
                        _logger.LogError(
                            "No DNS validator configured. DNS control validation is required for ACME DNS-01 challenges. " +
                            "Configure a DNS provider (Cloudflare, Azure DNS, or Route53) to enable validation.");

                        _state.Status = "DNS Validator Not Configured";
                        _state.ErrorMessage = "No DNS validator configured. Configure a DNS provider to enable DNS control validation.";

                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "DNSControlFailed",
                            Data = _state
                        });
                        return;
                    }

                    foreach (var domain in _state.DomainNames)
                    {
                        _logger.LogInformation("Checking DNS control for domain: {Domain}", domain);

                        try
                        {
                            var validationResult = await _dnsValidator.ValidateDnsControlAsync(domain);

                            if (!validationResult.HasControl)
                            {
                                _logger.LogError(
                                    "Failed to validate DNS control for domain: {Domain}. Reason: {Reason}",
                                    domain, validationResult.FailureReason);
                                allDomainsValid = false;
                                _state.FailedDomains.Add(domain);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "DNS control validated for domain: {Domain}. Provider: {Provider}",
                                    domain, validationResult.ProviderName);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Exception while validating DNS control for domain: {Domain}", domain);
                            allDomainsValid = false;
                            _state.FailedDomains.Add(domain);
                        }
                    }

                    if (!allDomainsValid)
                    {
                        _state.Status = "DNS Control Failed";
                        _state.ErrorMessage = $"Failed to validate DNS control for domains: {string.Join(", ", _state.FailedDomains)}";

                        await context.EmitEventAsync(new KernelProcessEvent
                        {
                            Id = "DNSControlFailed",
                            Data = _state
                        });
                        return;
                    }

                    _logger.LogInformation("DNS control validated for all domains");

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "DNSControlValidated",
                        Data = _state
                    });
                },
                _logger,
                "ValidateDNSControl");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate DNS control after retries");
            _state.Status = "DNS Validation Failed";
            _state.ErrorMessage = ex.Message;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DNSControlFailed",
                Data = _state
            });
        }
    }
}

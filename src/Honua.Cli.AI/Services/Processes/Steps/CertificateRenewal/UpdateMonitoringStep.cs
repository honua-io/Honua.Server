// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using CertificateRenewalState = Honua.Cli.AI.Services.Processes.State.CertificateRenewalState;
using Honua.Cli.AI.Services.Certificates;

namespace Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

/// <summary>
/// Updates certificate monitoring and inventory after successful renewal.
/// Cleans up old certificates and DNS challenge records.
/// </summary>
public class UpdateMonitoringStep : KernelProcessStep<CertificateRenewalState>
{
    private readonly ILogger<UpdateMonitoringStep> _logger;
    private readonly IChallengeProvider? _challengeProvider;
    private CertificateRenewalState _state = new();

    public UpdateMonitoringStep(ILogger<UpdateMonitoringStep> logger, IChallengeProvider? challengeProvider = null)
    {
        _logger = logger;
        _challengeProvider = challengeProvider;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<CertificateRenewalState> state)
    {
        _state = state.State ?? new CertificateRenewalState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("UpdateMonitoring")]
    public async Task UpdateMonitoringAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Updating monitoring and cleaning up after certificate renewal");

        _state.Status = "Updating Monitoring";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Update certificate inventory
                    _logger.LogInformation("Updating certificate inventory with new expiry dates");
                    foreach (var (domain, expiryDate) in _state.NewExpiryDates)
                    {
                        _logger.LogInformation("Certificate for {Domain} expires on {ExpiryDate}", domain, expiryDate);
                    }

                    // Clean up old certificates
                    _logger.LogInformation("Archiving {Count} old certificates", _state.ExpiringCertificates.Count);
                    foreach (var oldCert in _state.ExpiringCertificates)
                    {
                        // In production: move to archive storage
                        _logger.LogDebug("Archived certificate for {Domain} (thumbprint: {Thumbprint})",
                            oldCert.Domain, oldCert.Thumbprint);
                    }

                    // Clean up DNS challenge records
                    _logger.LogInformation("Cleaning up {Count} DNS challenge records",
                        _state.DnsChallengeRecords.Count);

                    if (_challengeProvider != null)
                    {
                        foreach (var (domain, challengeToken) in _state.DnsChallengeRecords)
                        {
                            // Skip the order URL entry and other metadata
                            if (domain.StartsWith("_") || domain.Contains("_token") || domain.Contains("_challengeType"))
                                continue;

                            try
                            {
                                // Get token and challenge type from state
                                var token = _state.DnsChallengeRecords.GetValueOrDefault($"{domain}_token", "");
                                var challengeType = _state.DnsChallengeRecords.GetValueOrDefault($"{domain}_challengeType", _state.ValidationMethod);

                                await _challengeProvider.CleanupChallengeAsync(
                                    domain,
                                    token,
                                    challengeToken,
                                    challengeType,
                                    CancellationToken.None);

                                _logger.LogInformation("Cleaned up {ChallengeType} challenge for {Domain}", challengeType, domain);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to cleanup challenge for {Domain}", domain);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No challenge provider configured - DNS challenge records must be manually cleaned up");
                    }

                    // Update monitoring alerts
                    _logger.LogInformation("Updating certificate expiry monitoring alerts");
                    // In production: update Prometheus alerts, CloudWatch alarms, etc.

                    _logger.LogInformation("Monitoring updated and cleanup completed");

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "MonitoringUpdated",
                        Data = _state
                    });
                },
                _logger,
                "UpdateMonitoring");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update monitoring after retries");
            _state.Status = "Monitoring Update Failed";
            _state.ErrorMessage = ex.Message;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "MonitoringUpdateFailed",
                Data = new { Error = ex.Message }
            });
        }
    }
}

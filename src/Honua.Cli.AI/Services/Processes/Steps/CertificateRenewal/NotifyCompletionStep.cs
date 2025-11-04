// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using CertificateRenewalState = Honua.Cli.AI.Services.Processes.State.CertificateRenewalState;

namespace Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;

/// <summary>
/// Sends completion notification with renewal summary.
/// Reports new expiry dates, deployed targets, and any issues encountered.
/// </summary>
public class NotifyCompletionStep : KernelProcessStep<CertificateRenewalState>
{
    private readonly ILogger<NotifyCompletionStep> _logger;
    private CertificateRenewalState _state = new();

    public NotifyCompletionStep(ILogger<NotifyCompletionStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<CertificateRenewalState> state)
    {
        _state = state.State ?? new CertificateRenewalState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("NotifySuccess")]
    public async Task NotifySuccessAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Certificate renewal completed successfully");

        _state.Status = "Complete";
        _state.RenewalCompleteTime = DateTime.UtcNow;

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    var duration = _state.RenewalCompleteTime.Value - _state.RenewalStartTime;

                    // Build notification message
                    var message = $@"Certificate Renewal Completed Successfully

Renewal ID: {_state.RenewalId}
Provider: {_state.CertificateProvider}
Duration: {duration.TotalMinutes:F1} minutes

Renewed Certificates ({_state.RenewedDomains.Count}):
{string.Join("\n", _state.RenewedDomains.Select(d => $"  - {d}"))}

New Expiry Dates:
{string.Join("\n", _state.NewExpiryDates.Select(kvp => $"  - {kvp.Key}: {kvp.Value:yyyy-MM-dd}"))}

Deployed Targets ({_state.UpdatedTargets.Count}):
{string.Join("\n", _state.UpdatedTargets.Select(t => $"  - {t.TargetType}: {t.TargetName}"))}

Next renewal recommended: {DateTime.UtcNow.AddDays(60):yyyy-MM-dd} (60 days)";

                    _logger.LogInformation(message);

                    // In production: send to Slack, email, etc.
                    // await _notificationService.SendNotificationAsync(message);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "ProcessComplete",
                        Data = _state
                    });
                },
                _logger,
                "NotifyCompletion");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send completion notification after retries");
            // Don't fail the entire process just because notification failed
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "NotificationFailed",
                Data = new { Error = ex.Message }
            });
        }
    }
}

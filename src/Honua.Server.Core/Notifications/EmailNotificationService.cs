// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Net;
using System.Net.Mail;
using System.Text;
using Honua.Server.Core.Deployment;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Notifications;

/// <summary>
/// Email-based notification service that sends deployment notifications via SMTP.
/// Uses HTML templates for professional-looking emails.
/// Implements resilient error handling - notification failures never block deployments.
/// Uses ResiliencePolicies.CreateRetryPolicy for consistent retry behavior.
/// </summary>
public class EmailNotificationService : INotificationService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly ResiliencePipeline _retryPolicy;
    private readonly string? _templatesDirectory;

    /// <summary>
    /// Initialize EmailNotificationService with configuration
    /// </summary>
    public EmailNotificationService(
        IOptions<NotificationOptions> options,
        ILogger<EmailNotificationService> logger)
    {
        Guard.NotNull(options);
        Guard.NotNull(logger);

        _options = options.Value.Email;
        _logger = logger;
        _templatesDirectory = options.Value.TemplatesDirectory;

        // Configure retry policy using centralized builder
        var retryOptions = options.Value.Retry;
        _retryPolicy = ResiliencePolicies.CreateRetryPolicy(
            maxRetries: retryOptions.MaxRetries,
            initialDelay: TimeSpan.FromMilliseconds(retryOptions.InitialDelayMs),
            logger: logger,
            shouldRetry: ex => ex is SmtpException || ex is InvalidOperationException);

        if (!_options.Enabled)
        {
            _logger.LogInformation("Email notifications are disabled");
        }
        else if (!ValidateConfiguration())
        {
            _logger.LogWarning("Email configuration is incomplete - notifications will not be sent");
        }
        else
        {
            _logger.LogInformation("Email notifications enabled with SMTP server {SmtpServer}:{SmtpPort}",
                _options.SmtpServer, _options.SmtpPort);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyDeploymentCreatedAsync(
        Deployment.Deployment deployment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSendNotification())
            return;

        try
        {
            var recipients = GetRecipients(deployment.Environment);
            var subject = $"[Honua GitOps] Deployment Created - {deployment.Environment}";
            var body = BuildDeploymentCreatedEmail(deployment, plan);

            await SendEmailAsync(recipients, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent email notification for deployment created: {DeploymentId}",
                deployment.Id);
        }
        catch (Exception ex)
        {
            LogNotificationError("deployment created", deployment.Id, ex);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyApprovalRequiredAsync(
        Deployment.Deployment deployment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSendNotification())
            return;

        try
        {
            var recipients = GetRecipients(deployment.Environment);
            var subject = $"[Honua GitOps] APPROVAL REQUIRED - {deployment.Environment}";
            var body = BuildApprovalRequiredEmail(deployment, plan);

            await SendEmailAsync(recipients, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent email notification for approval required: {DeploymentId}",
                deployment.Id);
        }
        catch (Exception ex)
        {
            LogNotificationError("approval required", deployment.Id, ex);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyApprovedAsync(
        Deployment.Deployment deployment,
        string approver,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSendNotification())
            return;

        try
        {
            var recipients = GetRecipients(deployment.Environment);
            var subject = $"[Honua GitOps] Deployment Approved - {deployment.Environment}";
            var body = BuildApprovedEmail(deployment, approver);

            await SendEmailAsync(recipients, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent email notification for deployment approved: {DeploymentId}",
                deployment.Id);
        }
        catch (Exception ex)
        {
            LogNotificationError("deployment approved", deployment.Id, ex);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyRejectedAsync(
        Deployment.Deployment deployment,
        string rejecter,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSendNotification())
            return;

        try
        {
            var recipients = GetRecipients(deployment.Environment);
            var subject = $"[Honua GitOps] Deployment Rejected - {deployment.Environment}";
            var body = BuildRejectedEmail(deployment, rejecter, reason);

            await SendEmailAsync(recipients, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent email notification for deployment rejected: {DeploymentId}",
                deployment.Id);
        }
        catch (Exception ex)
        {
            LogNotificationError("deployment rejected", deployment.Id, ex);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyDeploymentStartedAsync(
        Deployment.Deployment deployment,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSendNotification())
            return;

        try
        {
            var recipients = GetRecipients(deployment.Environment);
            var subject = $"[Honua GitOps] Deployment Started - {deployment.Environment}";
            var body = BuildDeploymentStartedEmail(deployment);

            await SendEmailAsync(recipients, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent email notification for deployment started: {DeploymentId}",
                deployment.Id);
        }
        catch (Exception ex)
        {
            LogNotificationError("deployment started", deployment.Id, ex);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyDeploymentCompletedAsync(
        Deployment.Deployment deployment,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSendNotification())
            return;

        try
        {
            var recipients = GetRecipients(deployment.Environment);
            var subject = $"[Honua GitOps] Deployment Completed - {deployment.Environment}";
            var body = BuildDeploymentCompletedEmail(deployment);

            await SendEmailAsync(recipients, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent email notification for deployment completed: {DeploymentId}",
                deployment.Id);
        }
        catch (Exception ex)
        {
            LogNotificationError("deployment completed", deployment.Id, ex);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyDeploymentFailedAsync(
        Deployment.Deployment deployment,
        string error,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSendNotification())
            return;

        try
        {
            var recipients = GetRecipients(deployment.Environment);
            var subject = $"[Honua GitOps] DEPLOYMENT FAILED - {deployment.Environment}";
            var body = BuildDeploymentFailedEmail(deployment, error);

            await SendEmailAsync(recipients, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent email notification for deployment failed: {DeploymentId}",
                deployment.Id);
        }
        catch (Exception ex)
        {
            LogNotificationError("deployment failed", deployment.Id, ex);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyRollbackAsync(
        Deployment.Deployment deployment,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSendNotification())
            return;

        try
        {
            var recipients = GetRecipients(deployment.Environment);
            var subject = $"[Honua GitOps] Rollback Initiated - {deployment.Environment}";
            var body = BuildRollbackEmail(deployment);

            await SendEmailAsync(recipients, subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent email notification for rollback: {DeploymentId}",
                deployment.Id);
        }
        catch (Exception ex)
        {
            LogNotificationError("rollback", deployment.Id, ex);
        }
    }

    // Private helper methods

    private bool ShouldSendNotification()
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Email notifications are disabled, skipping notification");
            return false;
        }

        if (!ValidateConfiguration())
        {
            _logger.LogWarning("Email configuration is incomplete, skipping notification");
            return false;
        }

        return true;
    }

    private bool ValidateConfiguration()
    {
        return !string.IsNullOrWhiteSpace(_options.SmtpServer) &&
               !string.IsNullOrWhiteSpace(_options.FromAddress) &&
               (_options.ToAddresses.Count > 0 || _options.PerEnvironment.Count > 0);
    }

    private List<string> GetRecipients(string environment)
    {
        var recipients = new List<string>();

        // Add environment-specific recipients
        if (_options.PerEnvironment.TryGetValue(environment.ToLowerInvariant(), out var envRecipients))
        {
            recipients.AddRange(envRecipients);
        }

        // Add default recipients if no environment-specific ones
        if (recipients.Count == 0)
        {
            recipients.AddRange(_options.ToAddresses);
        }

        return recipients;
    }

    private async Task SendEmailAsync(
        List<string> recipients,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (recipients.Count == 0)
        {
            _logger.LogWarning("No email recipients configured, skipping email notification");
            return;
        }

        await _retryPolicy.ExecuteAsync(async ct =>
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_options.FromAddress!, _options.FromName);
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            foreach (var recipient in recipients)
            {
                message.To.Add(recipient);
            }

            using var client = new SmtpClient(_options.SmtpServer, _options.SmtpPort)
            {
                EnableSsl = _options.UseSsl,
                Timeout = _options.TimeoutSeconds * 1000
            };

            if (!string.IsNullOrWhiteSpace(_options.SmtpUsername) &&
                !string.IsNullOrWhiteSpace(_options.SmtpPassword))
            {
                client.Credentials = new NetworkCredential(
                    _options.SmtpUsername,
                    _options.SmtpPassword);
            }

            await client.SendMailAsync(message, ct);
        }, cancellationToken);
    }

    private void LogNotificationError(string notificationType, string deploymentId, Exception ex)
    {
        _logger.LogWarning(
            ex,
            "Failed to send email notification for {NotificationType} (deployment: {DeploymentId}). " +
            "Deployment will continue normally.",
            notificationType,
            deploymentId);
    }

    // Email template builders

    private string BuildDeploymentCreatedEmail(Deployment.Deployment deployment, DeploymentPlan plan)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return BuildEmailTemplate(
            "Deployment Created",
            "#439FE0",
            $@"
                <h2 style='color: #439FE0; margin-top: 0;'>Deployment Created</h2>
                <p>A new deployment has been created and is starting.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Environment</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.Environment}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Commit</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'><code>{commitShort}</code></td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Branch</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.Branch}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Initiated By</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.InitiatedBy}</td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Risk Level</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{plan.RiskLevel}</td>
                    </tr>
                </table>

                <h3>Changes</h3>
                <ul>
                    <li><strong>{plan.Added.Count}</strong> resources added</li>
                    <li><strong>{plan.Modified.Count}</strong> resources modified</li>
                    <li><strong>{plan.Removed.Count}</strong> resources removed</li>
                    <li><strong>{plan.Migrations.Count}</strong> database migration(s)</li>
                </ul>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Deployment ID: <code>{deployment.Id}</code>
                </p>
            ");
    }

    private string BuildApprovalRequiredEmail(Deployment.Deployment deployment, DeploymentPlan plan)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;
        var reasons = GetApprovalReasons(plan);

        return BuildEmailTemplate(
            "Approval Required",
            "#FFA500",
            $@"
                <h2 style='color: #FFA500; margin-top: 0;'>Approval Required</h2>
                <p><strong>A deployment requires your approval before proceeding.</strong></p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Environment</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.Environment}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Commit</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'><code>{commitShort}</code></td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Risk Level</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{plan.RiskLevel}</td>
                    </tr>
                </table>

                <h3>Reason for Approval</h3>
                <ul>
                    {reasons}
                </ul>

                <h3>Changes</h3>
                <ul>
                    <li><strong>{plan.Added.Count}</strong> resources added</li>
                    <li><strong>{plan.Modified.Count}</strong> resources modified</li>
                    <li><strong>{plan.Removed.Count}</strong> resources removed</li>
                    <li><strong>{plan.Migrations.Count}</strong> database migration(s)</li>
                </ul>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Deployment ID: <code>{deployment.Id}</code>
                </p>
            ");
    }

    private string BuildApprovedEmail(Deployment.Deployment deployment, string approver)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return BuildEmailTemplate(
            "Deployment Approved",
            "#36A64F",
            $@"
                <h2 style='color: #36A64F; margin-top: 0;'>Deployment Approved</h2>
                <p>The deployment has been approved and will proceed.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Environment</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.Environment}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Commit</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'><code>{commitShort}</code></td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Approved By</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{approver}</td>
                    </tr>
                </table>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Deployment ID: <code>{deployment.Id}</code>
                </p>
            ");
    }

    private string BuildRejectedEmail(Deployment.Deployment deployment, string rejecter, string reason)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return BuildEmailTemplate(
            "Deployment Rejected",
            "#FF0000",
            $@"
                <h2 style='color: #FF0000; margin-top: 0;'>Deployment Rejected</h2>
                <p>The deployment has been rejected and will not proceed.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Environment</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.Environment}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Commit</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'><code>{commitShort}</code></td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Rejected By</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{rejecter}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Reason</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{reason}</td>
                    </tr>
                </table>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Deployment ID: <code>{deployment.Id}</code>
                </p>
            ");
    }

    private string BuildDeploymentStartedEmail(Deployment.Deployment deployment)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return BuildEmailTemplate(
            "Deployment Started",
            "#439FE0",
            $@"
                <h2 style='color: #439FE0; margin-top: 0;'>Deployment Started</h2>
                <p>The deployment is now applying changes.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Environment</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.Environment}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Commit</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'><code>{commitShort}</code></td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Started At</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.StartedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Initiated By</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.InitiatedBy}</td>
                    </tr>
                </table>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Deployment ID: <code>{deployment.Id}</code>
                </p>
            ");
    }

    private string BuildDeploymentCompletedEmail(Deployment.Deployment deployment)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;
        var duration = deployment.Duration?.ToString(@"mm\:ss") ?? "N/A";

        return BuildEmailTemplate(
            "Deployment Completed",
            "#36A64F",
            $@"
                <h2 style='color: #36A64F; margin-top: 0;'>Deployment Completed Successfully</h2>
                <p>The deployment has completed successfully.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Environment</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.Environment}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Commit</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'><code>{commitShort}</code></td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Duration</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{duration}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Completed At</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
                    </tr>
                </table>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Deployment ID: <code>{deployment.Id}</code>
                </p>
            ");
    }

    private string BuildDeploymentFailedEmail(Deployment.Deployment deployment, string error)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;
        var duration = deployment.Duration?.ToString(@"mm\:ss") ?? "N/A";

        return BuildEmailTemplate(
            "Deployment Failed",
            "#FF0000",
            $@"
                <h2 style='color: #FF0000; margin-top: 0;'>Deployment Failed</h2>
                <p><strong>The deployment has failed.</strong></p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Environment</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.Environment}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Commit</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'><code>{commitShort}</code></td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Duration</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{duration}</td>
                    </tr>
                </table>

                <h3>Error Details</h3>
                <pre style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #FF0000; overflow-x: auto; font-size: 12px;'>{System.Net.WebUtility.HtmlEncode(error)}</pre>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Deployment ID: <code>{deployment.Id}</code>
                </p>
            ");
    }

    private string BuildRollbackEmail(Deployment.Deployment deployment)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return BuildEmailTemplate(
            "Rollback Initiated",
            "#FFA500",
            $@"
                <h2 style='color: #FFA500; margin-top: 0;'>Rollback Initiated</h2>
                <p>A deployment rollback has been initiated.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Environment</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.Environment}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Commit</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'><code>{commitShort}</code></td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Backup ID</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{deployment.BackupId ?? "N/A"}</td>
                    </tr>
                </table>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Deployment ID: <code>{deployment.Id}</code>
                </p>
            ");
    }

    private static string BuildEmailTemplate(string title, string accentColor, string content)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
</head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f4f4f4;'>
    <table role='presentation' style='width: 100%; border-collapse: collapse;'>
        <tr>
            <td style='padding: 20px 0;'>
                <table role='presentation' style='max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, {accentColor} 0%, {accentColor}dd 100%); padding: 30px; text-align: center; border-radius: 8px 8px 0 0;'>
                            <h1 style='color: #ffffff; margin: 0; font-size: 24px; font-weight: 600;'>Honua GitOps</h1>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px 30px;'>
                            {content}
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f8f9fa; padding: 20px 30px; text-align: center; border-radius: 0 0 8px 8px; border-top: 1px solid #dee2e6;'>
                            <p style='color: #6c757d; font-size: 12px; margin: 0;'>
                                This is an automated notification from Honua GitOps.<br>
                                Please do not reply to this email.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string GetApprovalReasons(DeploymentPlan plan)
    {
        var reasons = new List<string>();

        if (plan.HasBreakingChanges)
            reasons.Add("<li>Breaking changes detected</li>");

        if (plan.RiskLevel >= RiskLevel.High)
            reasons.Add($"<li>High risk level ({plan.RiskLevel})</li>");

        if (plan.Migrations.Count > 0)
            reasons.Add($"<li>{plan.Migrations.Count} database migration(s)</li>");

        return reasons.Count > 0
            ? string.Join("\n", reasons)
            : "<li>Manual approval required</li>";
    }
}

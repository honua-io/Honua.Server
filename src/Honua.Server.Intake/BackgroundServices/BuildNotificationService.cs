// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Intake.Configuration;
using Honua.Server.Intake.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Interface for sending build notifications to customers.
/// </summary>
public interface IBuildNotificationService
{
    /// <summary>
    /// Sends a notification when a build is queued.
    /// </summary>
    Task SendBuildQueuedAsync(BuildJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification when a build starts.
    /// </summary>
    Task SendBuildStartedAsync(BuildJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification when a build completes successfully.
    /// </summary>
    Task SendBuildCompletedAsync(BuildJob job, BuildResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification when a build fails.
    /// </summary>
    Task SendBuildFailedAsync(BuildJob job, string error, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for sending email notifications about build status to customers.
/// </summary>
public sealed class BuildNotificationService : IBuildNotificationService
{
    private readonly BuildQueueOptions queueOptions;
    private readonly EmailOptions emailOptions;
    private readonly ILogger<BuildNotificationService> logger;
    private readonly ResiliencePipeline retryPipeline;

    public BuildNotificationService(
        IOptions<BuildQueueOptions> queueOptions,
        IOptions<EmailOptions> emailOptions,
        ILogger<BuildNotificationService> logger)
    {
        this.queueOptions = queueOptions?.Value ?? throw new ArgumentNullException(nameof(queueOptions));
        this.emailOptions = emailOptions?.Value ?? throw new ArgumentNullException(nameof(emailOptions));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure retry policy with exponential backoff
        this.retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder().Handle<SmtpException>()
            })
            .Build();
    }

    /// <inheritdoc/>
    public async Task SendBuildQueuedAsync(BuildJob job, CancellationToken cancellationToken = default)
    {
        if (!this.queueOptions.EnableNotifications)
        {
            this.logger.LogDebug("Notifications disabled, skipping queued notification for job {JobId}", job.Id);
            return;
        }

        try
        {
            var subject = $"[Honua] Build Queued - {job.ConfigurationName}";
            var body = BuildQueuedEmailTemplate(job);

            await SendEmailAsync(job.CustomerEmail, subject, body, cancellationToken);

            this.logger.LogInformation("Sent queued notification for build job {JobId}", job.Id);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to send queued notification for build job {JobId}", job.Id);
        }
    }

    /// <inheritdoc/>
    public async Task SendBuildStartedAsync(BuildJob job, CancellationToken cancellationToken = default)
    {
        if (!this.queueOptions.EnableNotifications)
        {
            this.logger.LogDebug("Notifications disabled, skipping started notification for job {JobId}", job.Id);
            return;
        }

        try
        {
            var subject = $"[Honua] Build Started - {job.ConfigurationName}";
            var body = BuildStartedEmailTemplate(job);

            await SendEmailAsync(job.CustomerEmail, subject, body, cancellationToken);

            this.logger.LogInformation("Sent started notification for build job {JobId}", job.Id);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to send started notification for build job {JobId}", job.Id);
        }
    }

    /// <inheritdoc/>
    public async Task SendBuildCompletedAsync(
        BuildJob job,
        BuildResult result,
        CancellationToken cancellationToken = default)
    {
        if (!this.queueOptions.EnableNotifications)
        {
            this.logger.LogDebug("Notifications disabled, skipping completed notification for job {JobId}", job.Id);
            return;
        }

        try
        {
            var subject = $"[Honua] Build Ready - {job.ConfigurationName}";
            var body = BuildCompletedEmailTemplate(job, result);

            await SendEmailAsync(job.CustomerEmail, subject, body, cancellationToken);

            this.logger.LogInformation("Sent completed notification for build job {JobId}", job.Id);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to send completed notification for build job {JobId}", job.Id);
        }
    }

    /// <inheritdoc/>
    public async Task SendBuildFailedAsync(
        BuildJob job,
        string error,
        CancellationToken cancellationToken = default)
    {
        if (!this.queueOptions.EnableNotifications)
        {
            this.logger.LogDebug("Notifications disabled, skipping failed notification for job {JobId}", job.Id);
            return;
        }

        try
        {
            var subject = $"[Honua] Build Failed - {job.ConfigurationName}";
            var body = BuildFailedEmailTemplate(job, error);

            await SendEmailAsync(job.CustomerEmail, subject, body, cancellationToken);

            this.logger.LogInformation("Sent failed notification for build job {JobId}", job.Id);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to send failed notification for build job {JobId}", job.Id);
        }
    }

    // Private helper methods

    private async Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            this.logger.LogWarning("Cannot send email: recipient email is empty");
            return;
        }

        if (!this.emailOptions.Enabled)
        {
            this.logger.LogDebug("Email notifications disabled, skipping email to {Email}", recipientEmail);
            return;
        }

        await this.retryPipeline.ExecuteAsync(async ct =>
        {
            using var message = new MailMessage
            {
                From = new MailAddress(this.emailOptions.FromAddress, this.emailOptions.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(recipientEmail);

            using var client = new SmtpClient(this.emailOptions.SmtpServer, this.emailOptions.SmtpPort)
            {
                EnableSsl = this.emailOptions.UseSsl,
                Timeout = this.emailOptions.TimeoutSeconds * 1000
            };

            if (!string.IsNullOrWhiteSpace(this.emailOptions.SmtpUsername) &&
                !string.IsNullOrWhiteSpace(this.emailOptions.SmtpPassword))
            {
                client.Credentials = new NetworkCredential(
                    this.emailOptions.SmtpUsername,
                    this.emailOptions.SmtpPassword);
            }

            await client.SendMailAsync(message, ct);
        }, cancellationToken);
    }

    // Email templates

    private static string BuildQueuedEmailTemplate(BuildJob job)
    {
        return BuildEmailTemplate(
            "Build Queued",
            "#439FE0",
            $@"
                <h2 style='color: #439FE0; margin-top: 0;'>Build Queued</h2>
                <p>Your Honua Server build has been queued and will start processing shortly.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Configuration</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.ConfigurationName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Tier</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.Tier}</td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Architecture</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.Architecture}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Cloud Platform</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.CloudProvider}</td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Priority</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.Priority}</td>
                    </tr>
                </table>

                <p>You will receive another email when your build starts processing.</p>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Build ID: <code>{job.Id}</code>
                </p>
            ");
    }

    private static string BuildStartedEmailTemplate(BuildJob job)
    {
        return BuildEmailTemplate(
            "Build Started",
            "#439FE0",
            $@"
                <h2 style='color: #439FE0; margin-top: 0;'>Build Started</h2>
                <p>Your Honua Server build is now being processed.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Configuration</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.ConfigurationName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Tier</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.Tier}</td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Architecture</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.Architecture}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Cloud Platform</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.CloudProvider}</td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Started At</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.StartedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
                    </tr>
                </table>

                <p>Build typically takes 30-60 minutes depending on configuration. You will receive an email when it completes.</p>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Build ID: <code>{job.Id}</code>
                </p>
            ");
    }

    private string BuildCompletedEmailTemplate(BuildJob job, BuildResult result)
    {
        var durationMinutes = (int)Math.Ceiling(result.Duration.TotalMinutes);
        var downloadUrl = result.DownloadUrl ?? this.queueOptions.DownloadBaseUrl + "/" + job.Id;

        return BuildEmailTemplate(
            "Build Ready",
            "#36A64F",
            $@"
                <h2 style='color: #36A64F; margin-top: 0;'>Your Honua Server Build is Ready!</h2>
                <p>Your custom Honua Server has been built successfully and is ready to download.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Configuration</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.ConfigurationName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Tier</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.Tier}</td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Architecture</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.Architecture}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Cloud Platform</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.CloudProvider}</td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Build Time</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{durationMinutes} minutes</td>
                    </tr>
                </table>

                <h3>Download Options</h3>

                <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <p style='margin: 0 0 10px 0;'><strong>1. Container Image:</strong></p>
                    <pre style='background-color: #ffffff; padding: 10px; border: 1px solid #dee2e6; overflow-x: auto;'>docker pull {result.ImageUrl ?? "honua.io/" + job.CustomerId + "/" + job.ConfigurationName}</pre>

                    <p style='margin: 20px 0 10px 0;'><strong>2. Standalone Binary:</strong></p>
                    <p style='margin: 0;'><a href='{downloadUrl}' style='color: #439FE0;'>{downloadUrl}</a></p>
                </div>

                {(string.IsNullOrWhiteSpace(result.DeploymentInstructions) ? "" : $@"
                <h3>Deployment Instructions</h3>
                <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    {result.DeploymentInstructions}
                </div>
                ")}

                <p>Need help getting started? Check out our documentation at <a href='https://docs.honua.io' style='color: #439FE0;'>docs.honua.io</a></p>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Build ID: <code>{job.Id}</code>
                </p>
            ");
    }

    private static string BuildFailedEmailTemplate(BuildJob job, string error)
    {
        return BuildEmailTemplate(
            "Build Failed",
            "#FF0000",
            $@"
                <h2 style='color: #FF0000; margin-top: 0;'>Build Failed</h2>
                <p>Unfortunately, your Honua Server build encountered an error and could not be completed.</p>

                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Configuration</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.ConfigurationName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Tier</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.Tier}</td>
                    </tr>
                    <tr style='background-color: #f8f9fa;'>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Architecture</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.Architecture}</td>
                    </tr>
                    <tr>
                        <td style='padding: 10px; border: 1px solid #dee2e6; font-weight: bold;'>Cloud Platform</td>
                        <td style='padding: 10px; border: 1px solid #dee2e6;'>{job.CloudProvider}</td>
                    </tr>
                </table>

                <h3>Error Details</h3>
                <pre style='background-color: #fff5f5; padding: 15px; border-left: 4px solid #FF0000; overflow-x: auto; font-size: 12px;'>{WebUtility.HtmlEncode(error)}</pre>

                {(job.RetryCount < 3 ? @"
                <p style='background-color: #fff5f5; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <strong>Note:</strong> Your build will be automatically retried. You will receive an email if it succeeds or if all retry attempts fail.
                </p>
                " : "")}

                <p>If this problem persists, please contact our support team at <a href='mailto:support@honua.io' style='color: #439FE0;'>support@honua.io</a> with your Build ID.</p>

                <p style='margin-top: 20px; color: #6c757d; font-size: 14px;'>
                    Build ID: <code>{job.Id}</code>
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
                            <h1 style='color: #ffffff; margin: 0; font-size: 24px; font-weight: 600;'>Honua Server</h1>
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
                                This is an automated notification from Honua Server.<br>
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
}

/// <summary>
/// Email configuration options (reused from Core if available, or defined here).
/// </summary>
public sealed class EmailOptions
{
    /// <summary>
    /// Whether email notifications are enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// SMTP server hostname.
    /// </summary>
    public string SmtpServer { get; init; } = string.Empty;

    /// <summary>
    /// SMTP server port.
    /// </summary>
    public int SmtpPort { get; init; } = 587;

    /// <summary>
    /// Whether to use SSL/TLS.
    /// </summary>
    public bool UseSsl { get; init; } = true;

    /// <summary>
    /// SMTP username for authentication.
    /// </summary>
    public string? SmtpUsername { get; init; }

    /// <summary>
    /// SMTP password for authentication.
    /// </summary>
    public string? SmtpPassword { get; init; }

    /// <summary>
    /// From email address.
    /// </summary>
    public string FromAddress { get; init; } = "noreply@honua.io";

    /// <summary>
    /// From display name.
    /// </summary>
    public string FromName { get; init; } = "Honua Server";

    /// <summary>
    /// Timeout in seconds for SMTP operations.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
}

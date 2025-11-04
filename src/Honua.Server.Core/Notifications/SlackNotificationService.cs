// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Server.Core.Deployment;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Resilience;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Honua.Server.Core.Notifications;

/// <summary>
/// Slack-based notification service that sends deployment notifications using Slack webhooks.
/// Uses Slack Block Kit for rich formatting and color-coded messages.
/// Implements resilient error handling - notification failures never block deployments.
/// Uses ResiliencePolicies.CreateRetryPolicy for consistent retry behavior.
/// </summary>
public class SlackNotificationService : INotificationService
{
    private readonly SlackOptions _options;
    private readonly ILogger<SlackNotificationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline _retryPolicy;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initialize SlackNotificationService with configuration and HTTP client
    /// </summary>
    public SlackNotificationService(
        IOptions<NotificationOptions> options,
        ILogger<SlackNotificationService> logger,
        HttpClient httpClient)
    {
        Guard.NotNull(options);
        Guard.NotNull(logger);
        Guard.NotNull(httpClient);

        _options = options.Value.Slack;
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        _jsonOptions = JsonSerializerOptionsRegistry.Web;

        // Configure retry policy using centralized builder
        var retryOptions = options.Value.Retry;
        _retryPolicy = ResiliencePolicies.CreateRetryPolicy(
            maxRetries: retryOptions.MaxRetries,
            initialDelay: TimeSpan.FromMilliseconds(retryOptions.InitialDelayMs),
            logger: logger,
            shouldRetry: ex => ex is HttpRequestException || ex is TaskCanceledException);

        if (!_options.Enabled)
        {
            _logger.LogInformation("Slack notifications are disabled");
        }
        else if (_options.WebhookUrl.IsNullOrWhiteSpace())
        {
            _logger.LogWarning("Slack webhook URL is not configured - notifications will not be sent");
        }
        else
        {
            _logger.LogInformation("Slack notifications enabled with webhook URL configured");
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

        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.Notifications,
            "Slack DeploymentCreated",
            [
                ("notification.type", "deployment.created"),
                ("notification.channel", "slack"),
                ("deployment.id", deployment.Id),
                ("deployment.environment", deployment.Environment)
            ],
            async activity =>
            {
                try
                {
                    var message = BuildDeploymentCreatedMessage(deployment, plan);

                    await PerformanceMeasurement.MeasureAsync(
                        _logger,
                        "Slack webhook delivery",
                        async () => await SendSlackMessageAsync(message, cancellationToken),
                        LogLevel.Information);

                    _logger.LogInformation(
                        "Sent Slack notification for deployment created: {DeploymentId}",
                        deployment.Id);
                }
                catch (Exception ex)
                {
                    LogNotificationError("deployment created", deployment.Id, ex);
                    throw;
                }
            },
            ActivityKind.Client);
    }

    /// <inheritdoc/>
    public async Task NotifyApprovalRequiredAsync(
        Deployment.Deployment deployment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSendNotification())
            return;

        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.Notifications,
            "Slack ApprovalRequired",
            [
                ("notification.type", "deployment.approval_required"),
                ("notification.channel", "slack"),
                ("deployment.id", deployment.Id),
                ("deployment.risk_level", plan.RiskLevel.ToString())
            ],
            async activity =>
            {
                try
                {
                    var message = BuildApprovalRequiredMessage(deployment, plan);

                    await PerformanceMeasurement.MeasureAsync(
                        _logger,
                        "Slack webhook delivery",
                        async () => await SendSlackMessageAsync(message, cancellationToken),
                        LogLevel.Information);

                    _logger.LogInformation(
                        "Sent Slack notification for approval required: {DeploymentId}",
                        deployment.Id);
                }
                catch (Exception ex)
                {
                    LogNotificationError("approval required", deployment.Id, ex);
                    throw;
                }
            },
            ActivityKind.Client);
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
            var message = BuildApprovedMessage(deployment, approver);
            await SendSlackMessageAsync(message, cancellationToken);

            _logger.LogInformation(
                "Sent Slack notification for deployment approved: {DeploymentId} by {Approver}",
                deployment.Id,
                approver);
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
            var message = BuildRejectedMessage(deployment, rejecter, reason);
            await SendSlackMessageAsync(message, cancellationToken);

            _logger.LogInformation(
                "Sent Slack notification for deployment rejected: {DeploymentId} by {Rejecter}",
                deployment.Id,
                rejecter);
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
            var message = BuildDeploymentStartedMessage(deployment);
            await SendSlackMessageAsync(message, cancellationToken);

            _logger.LogInformation(
                "Sent Slack notification for deployment started: {DeploymentId}",
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
            var message = BuildDeploymentCompletedMessage(deployment);
            await SendSlackMessageAsync(message, cancellationToken);

            _logger.LogInformation(
                "Sent Slack notification for deployment completed: {DeploymentId}",
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

        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.Notifications,
            "Slack DeploymentFailed",
            [
                ("notification.type", "deployment.failed"),
                ("notification.channel", "slack"),
                ("deployment.id", deployment.Id),
                ("deployment.environment", deployment.Environment),
                ("deployment.has_error", true)
            ],
            async activity =>
            {
                try
                {
                    var message = BuildDeploymentFailedMessage(deployment, error);

                    await PerformanceMeasurement.MeasureAsync(
                        _logger,
                        "Slack webhook delivery",
                        async () => await SendSlackMessageAsync(message, cancellationToken),
                        LogLevel.Warning);

                    _logger.LogInformation(
                        "Sent Slack notification for deployment failed: {DeploymentId}",
                        deployment.Id);
                }
                catch (Exception ex)
                {
                    LogNotificationError("deployment failed", deployment.Id, ex);
                    throw;
                }
            },
            ActivityKind.Client);
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
            var message = BuildRollbackMessage(deployment);
            await SendSlackMessageAsync(message, cancellationToken);

            _logger.LogInformation(
                "Sent Slack notification for rollback: {DeploymentId}",
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
            _logger.LogDebug("Slack notifications are disabled, skipping notification");
            return false;
        }

        if (_options.WebhookUrl.IsNullOrWhiteSpace())
        {
            _logger.LogWarning("Slack webhook URL is not configured, skipping notification");
            return false;
        }

        return true;
    }

    private async Task SendSlackMessageAsync(SlackMessage message, CancellationToken cancellationToken)
    {
        await _retryPolicy.ExecuteAsync(async ct =>
        {
            var response = await _httpClient.PostAsJsonAsync(
                _options.WebhookUrl,
                message,
                _jsonOptions,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Slack webhook returned {response.StatusCode}: {errorContent}");
            }
        }, cancellationToken);
    }

    private void LogNotificationError(string notificationType, string deploymentId, Exception ex)
    {
        _logger.LogWarning(
            ex,
            "Failed to send Slack notification for {NotificationType} (deployment: {DeploymentId}). " +
            "Deployment will continue normally.",
            notificationType,
            deploymentId);
    }

    private SlackMessage BuildDeploymentCreatedMessage(Deployment.Deployment deployment, DeploymentPlan plan)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;
        var addedCount = plan.Added.Count;
        var modifiedCount = plan.Modified.Count;
        var removedCount = plan.Removed.Count;
        var migrationCount = plan.Migrations.Count;

        return new SlackMessage
        {
            Text = $"Deployment created for *{deployment.Environment}* environment",
            Attachments = new List<SlackAttachment>
            {
                new()
                {
                    Color = "#439FE0", // Blue
                    Blocks = new List<SlackBlock>
                    {
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $":rocket: *Deployment Created*\n*Environment:* {deployment.Environment}\n*Commit:* `{commitShort}`\n*Branch:* {deployment.Branch}\n*Initiated by:* {deployment.InitiatedBy}"
                            }
                        },
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $"*Changes:*\n:heavy_plus_sign: {addedCount} added\n:pencil2: {modifiedCount} modified\n:heavy_minus_sign: {removedCount} removed\n:floppy_disk: {migrationCount} migration(s)"
                            }
                        },
                        new SlackContextBlock
                        {
                            Elements = new List<SlackText>
                            {
                                new() { Type = "mrkdwn", Text = $"Deployment ID: `{deployment.Id}`" },
                                new() { Type = "mrkdwn", Text = $"Risk Level: {plan.RiskLevel}" }
                            }
                        }
                    }
                }
            }
        };
    }

    private SlackMessage BuildApprovalRequiredMessage(Deployment.Deployment deployment, DeploymentPlan plan)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return new SlackMessage
        {
            Text = $"Approval required for deployment to *{deployment.Environment}*",
            Attachments = new List<SlackAttachment>
            {
                new()
                {
                    Color = "#FFA500", // Orange
                    Blocks = new List<SlackBlock>
                    {
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $":warning: *Approval Required*\n*Environment:* {deployment.Environment}\n*Commit:* `{commitShort}`\n*Risk Level:* {plan.RiskLevel}"
                            }
                        },
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $"*Reason:*\n{GetApprovalReason(plan)}"
                            }
                        },
                        new SlackContextBlock
                        {
                            Elements = new List<SlackText>
                            {
                                new() { Type = "mrkdwn", Text = $"Deployment ID: `{deployment.Id}`" }
                            }
                        }
                    }
                }
            }
        };
    }

    private SlackMessage BuildApprovedMessage(Deployment.Deployment deployment, string approver)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return new SlackMessage
        {
            Text = $"Deployment approved for *{deployment.Environment}*",
            Attachments = new List<SlackAttachment>
            {
                new()
                {
                    Color = "#36A64F", // Green
                    Blocks = new List<SlackBlock>
                    {
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $":white_check_mark: *Deployment Approved*\n*Environment:* {deployment.Environment}\n*Commit:* `{commitShort}`\n*Approved by:* {approver}"
                            }
                        },
                        new SlackContextBlock
                        {
                            Elements = new List<SlackText>
                            {
                                new() { Type = "mrkdwn", Text = $"Deployment ID: `{deployment.Id}`" }
                            }
                        }
                    }
                }
            }
        };
    }

    private SlackMessage BuildRejectedMessage(Deployment.Deployment deployment, string rejecter, string reason)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return new SlackMessage
        {
            Text = $"Deployment rejected for *{deployment.Environment}*",
            Attachments = new List<SlackAttachment>
            {
                new()
                {
                    Color = "#FF0000", // Red
                    Blocks = new List<SlackBlock>
                    {
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $":x: *Deployment Rejected*\n*Environment:* {deployment.Environment}\n*Commit:* `{commitShort}`\n*Rejected by:* {rejecter}\n*Reason:* {reason}"
                            }
                        },
                        new SlackContextBlock
                        {
                            Elements = new List<SlackText>
                            {
                                new() { Type = "mrkdwn", Text = $"Deployment ID: `{deployment.Id}`" }
                            }
                        }
                    }
                }
            }
        };
    }

    private SlackMessage BuildDeploymentStartedMessage(Deployment.Deployment deployment)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return new SlackMessage
        {
            Text = $"Deployment started for *{deployment.Environment}*",
            Attachments = new List<SlackAttachment>
            {
                new()
                {
                    Color = "#439FE0", // Blue
                    Blocks = new List<SlackBlock>
                    {
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $":hourglass_flowing_sand: *Deployment Started*\n*Environment:* {deployment.Environment}\n*Commit:* `{commitShort}`\n*Initiated by:* {deployment.InitiatedBy}"
                            }
                        },
                        new SlackContextBlock
                        {
                            Elements = new List<SlackText>
                            {
                                new() { Type = "mrkdwn", Text = $"Deployment ID: `{deployment.Id}`" },
                                new() { Type = "mrkdwn", Text = $"Started: {deployment.StartedAt:yyyy-MM-dd HH:mm:ss} UTC" }
                            }
                        }
                    }
                }
            }
        };
    }

    private SlackMessage BuildDeploymentCompletedMessage(Deployment.Deployment deployment)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;
        var duration = deployment.Duration?.ToString(@"mm\:ss") ?? "N/A";

        return new SlackMessage
        {
            Text = $"Deployment completed successfully for *{deployment.Environment}*",
            Attachments = new List<SlackAttachment>
            {
                new()
                {
                    Color = "#36A64F", // Green
                    Blocks = new List<SlackBlock>
                    {
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $":tada: *Deployment Completed*\n*Environment:* {deployment.Environment}\n*Commit:* `{commitShort}`\n*Duration:* {duration}"
                            }
                        },
                        new SlackContextBlock
                        {
                            Elements = new List<SlackText>
                            {
                                new() { Type = "mrkdwn", Text = $"Deployment ID: `{deployment.Id}`" },
                                new() { Type = "mrkdwn", Text = $"Completed: {deployment.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC" }
                            }
                        }
                    }
                }
            }
        };
    }

    private SlackMessage BuildDeploymentFailedMessage(Deployment.Deployment deployment, string error)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;
        var duration = deployment.Duration?.ToString(@"mm\:ss") ?? "N/A";

        return new SlackMessage
        {
            Text = $"Deployment failed for *{deployment.Environment}*",
            Attachments = new List<SlackAttachment>
            {
                new()
                {
                    Color = "#FF0000", // Red
                    Blocks = new List<SlackBlock>
                    {
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $":x: *Deployment Failed*\n*Environment:* {deployment.Environment}\n*Commit:* `{commitShort}`\n*Duration:* {duration}"
                            }
                        },
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $"*Error:*\n```{TruncateError(error)}```"
                            }
                        },
                        new SlackContextBlock
                        {
                            Elements = new List<SlackText>
                            {
                                new() { Type = "mrkdwn", Text = $"Deployment ID: `{deployment.Id}`" }
                            }
                        }
                    }
                }
            }
        };
    }

    private SlackMessage BuildRollbackMessage(Deployment.Deployment deployment)
    {
        var commitShort = deployment.Commit.Length > 7 ? deployment.Commit[..7] : deployment.Commit;

        return new SlackMessage
        {
            Text = $"Deployment rollback initiated for *{deployment.Environment}*",
            Attachments = new List<SlackAttachment>
            {
                new()
                {
                    Color = "#FFA500", // Orange
                    Blocks = new List<SlackBlock>
                    {
                        new SlackSectionBlock
                        {
                            Text = new SlackText
                            {
                                Type = "mrkdwn",
                                Text = $":arrows_counterclockwise: *Rollback Initiated*\n*Environment:* {deployment.Environment}\n*Commit:* `{commitShort}`"
                            }
                        },
                        new SlackContextBlock
                        {
                            Elements = new List<SlackText>
                            {
                                new() { Type = "mrkdwn", Text = $"Deployment ID: `{deployment.Id}`" },
                                new() { Type = "mrkdwn", Text = deployment.BackupId != null ? $"Backup ID: `{deployment.BackupId}`" : "No backup available" }
                            }
                        }
                    }
                }
            }
        };
    }

    private static string GetApprovalReason(DeploymentPlan plan)
    {
        var reasons = new List<string>();

        if (plan.HasBreakingChanges)
            reasons.Add("Breaking changes detected");

        if (plan.RiskLevel >= RiskLevel.High)
            reasons.Add($"High risk level ({plan.RiskLevel})");

        if (plan.Migrations.Count > 0)
            reasons.Add($"{plan.Migrations.Count} database migration(s)");

        return reasons.Count > 0 ? string.Join("\n", reasons.Select(r => $"• {r}")) : "Manual approval required";
    }

    private static string TruncateError(string error, int maxLength = 500)
    {
        if (error.IsNullOrEmpty())
            return "Unknown error";

        if (error.Length <= maxLength)
            return error;

        return error[..(maxLength - 3)] + "...";
    }
}

// Slack message models for Block Kit

internal class SlackMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("attachments")]
    public List<SlackAttachment>? Attachments { get; set; }
}

internal class SlackAttachment
{
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("blocks")]
    public List<SlackBlock>? Blocks { get; set; }
}

internal abstract class SlackBlock
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

internal class SlackSectionBlock : SlackBlock
{
    public override string Type => "section";

    [JsonPropertyName("text")]
    public SlackText? Text { get; set; }
}

internal class SlackContextBlock : SlackBlock
{
    public override string Type => "context";

    [JsonPropertyName("elements")]
    public List<SlackText>? Elements { get; set; }
}

internal class SlackText
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "mrkdwn";

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

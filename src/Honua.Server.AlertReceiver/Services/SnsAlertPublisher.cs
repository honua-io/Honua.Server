// <copyright file="SnsAlertPublisher.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Honua.Server.AlertReceiver.Models;
using System.Text.Json;
using Honua.Server.AlertReceiver.Extensions;
using Honua.Server.Core.Performance;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Publishes alerts to AWS SNS topics.
/// </summary>
public sealed class SnsAlertPublisher : IAlertPublisher
{
    private readonly IAmazonSimpleNotificationService sns;
    private readonly IConfiguration configuration;
    private readonly ILogger<SnsAlertPublisher> logger;

    public SnsAlertPublisher(
        IAmazonSimpleNotificationService sns,
        IConfiguration configuration,
        ILogger<SnsAlertPublisher> logger)
    {
        this.sns = sns ?? throw new ArgumentNullException(nameof(sns));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default)
    {
        var topicArn = this.GetTopicArn(severity);
        if (topicArn.IsNullOrWhiteSpace())
        {
            this.logger.LogWarning("No SNS topic ARN configured for severity {Severity}, skipping SNS publish", severity);
            return;
        }

        try
        {
            // Format alert message
            var message = FormatAlertMessage(webhook);
            var subject = FormatSubject(webhook, severity);

            // Publish to SNS
            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Subject = subject,
                Message = message,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["severity"] = new MessageAttributeValue { DataType = "String", StringValue = severity },
                    ["status"] = new MessageAttributeValue { DataType = "String", StringValue = webhook.Status },
                    ["alertname"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = webhook.GroupLabels.GetValueOrDefault("alertname", "Unknown")
                    },
                },
            };

            var response = await this.sns.PublishAsync(request, cancellationToken);

            this.logger.LogInformation(
                "Published alert to SNS topic {TopicArn}, MessageId: {MessageId}, Severity: {Severity}, Status: {Status}",
                topicArn,
                response.MessageId,
                severity,
                webhook.Status);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to publish alert to SNS topic {TopicArn}", topicArn);
            throw;
        }
    }

    private string GetTopicArn(string severity)
    {
        // Map severity levels to SNS topic ARNs
        var key = severity.ToLowerInvariant() switch
        {
            "critical" => "Alerts:SNS:CriticalTopicArn",
            "warning" => "Alerts:SNS:WarningTopicArn",
            "database" => "Alerts:SNS:DatabaseTopicArn",
            "storage" => "Alerts:SNS:StorageTopicArn",
            _ => "Alerts:SNS:DefaultTopicArn",
        };

        return this.configuration[key] ?? string.Empty;
    }

    private static string FormatSubject(AlertManagerWebhook webhook, string severity)
    {
        var alertName = webhook.GroupLabels.GetValueOrDefault("alertname", "Unknown Alert");
        var icon = severity.ToLowerInvariant() switch
        {
            "critical" => "🚨",
            "warning" => "⚠️",
            "database" => "🗄️",
            "storage" => "💾",
            _ => "ℹ️",
        };

        var status = webhook.Status.ToUpperInvariant();
        return $"{icon} {status}: {alertName}";
    }

    private static string FormatAlertMessage(AlertManagerWebhook webhook)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Honua Alert Notification");
        sb.AppendLine("========================");
        sb.AppendLine();
        sb.AppendLine($"Status: {webhook.Status.ToUpperInvariant()}");
        sb.AppendLine($"Alert Group: {webhook.GroupLabels.GetValueOrDefault("alertname", "Unknown")}");

        if (webhook.CommonAnnotations.TryGetValue("summary", out var summary))
        {
            sb.AppendLine($"Summary: {summary}");
        }

        sb.AppendLine();
        sb.AppendLine($"Active Alerts: {webhook.Alerts.Count}");
        sb.AppendLine();

        foreach (var alert in webhook.Alerts.Take(10)) // Limit to 10 alerts in message
        {
            sb.AppendLine($"Alert: {alert.Labels.GetValueOrDefault("alertname", "Unknown")}");

            if (alert.Annotations.TryGetValue("description", out var description))
            {
                sb.AppendLine($"  Description: {description}");
            }

            sb.AppendLine($"  Status: {alert.Status}");
            sb.AppendLine($"  Started: {alert.StartsAt:yyyy-MM-dd HH:mm:ss} UTC");

            if (alert.EndsAt.HasValue && alert.Status == "resolved")
            {
                sb.AppendLine($"  Resolved: {alert.EndsAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
                var duration = alert.EndsAt.Value - alert.StartsAt;
                sb.AppendLine($"  Duration: {duration.TotalMinutes:F1} minutes");
            }

            sb.AppendLine();
        }

        if (webhook.Alerts.Count > 10)
        {
            sb.AppendLine($"... and {webhook.Alerts.Count - 10} more alerts");
            sb.AppendLine();
        }

        if (webhook.ExternalUrl.HasValue())
        {
            sb.AppendLine($"AlertManager: {webhook.ExternalUrl}");
        }

        // Add JSON payload for programmatic processing
        sb.AppendLine();
        sb.AppendLine("JSON Payload:");
        sb.AppendLine("-------------");
        sb.AppendLine(JsonSerializer.Serialize(webhook, JsonSerializerOptionsRegistry.WebIndented));

        return sb.ToString();
    }
}

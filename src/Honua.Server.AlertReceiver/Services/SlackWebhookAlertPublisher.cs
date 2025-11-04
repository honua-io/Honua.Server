// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.AlertReceiver.Models;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Publishes alerts to Slack via Incoming Webhooks.
/// </summary>
/// <remarks>
/// Uses Slack's Block Kit attachment format with customizable icons and colors per severity.
/// Limits display to 5 alerts per webhook to avoid message size limits.
/// </remarks>
public sealed class SlackWebhookAlertPublisher : WebhookAlertPublisherBase
{
    public SlackWebhookAlertPublisher(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SlackWebhookAlertPublisher> logger)
        : base(httpClientFactory, configuration, logger, "Slack")
    {
    }

    protected override string? GetEndpoint(AlertManagerWebhook webhook, string severity)
    {
        var key = severity.ToLowerInvariant() switch
        {
            "critical" => "Alerts:Slack:CriticalWebhookUrl",
            "warning" => "Alerts:Slack:WarningWebhookUrl",
            "database" => "Alerts:Slack:DatabaseWebhookUrl",
            "storage" => "Alerts:Slack:StorageWebhookUrl",
            _ => "Alerts:Slack:DefaultWebhookUrl"
        };

        return Configuration[key];
    }

    protected override object BuildPayload(AlertManagerWebhook webhook, string severity)
    {
        var icon = GetSeverityIcon(severity);
        var color = GetSeverityColor(severity);
        var alertName = GetAlertName(webhook);
        var status = webhook.Status.ToUpperInvariant();

        var attachments = new List<object>();

        foreach (var alert in webhook.Alerts.Take(5)) // Limit to 5 alerts
        {
            var fields = new List<object>
            {
                new { title = "Status", value = alert.Status, @short = true },
                new { title = "Severity", value = alert.Labels.GetValueOrDefault("severity", "unknown"), @short = true }
            };

            if (alert.Labels.TryGetValue("api_protocol", out var protocol))
            {
                fields.Add(new { title = "Protocol", value = protocol, @short = true });
            }

            if (alert.Labels.TryGetValue("service_id", out var service))
            {
                fields.Add(new { title = "Service", value = service, @short = true });
            }

            attachments.Add(new
            {
                color = webhook.Status == "firing" ? color : "good",
                title = alert.Labels.GetValueOrDefault("alertname", "Alert"),
                text = alert.Annotations.GetValueOrDefault("description", "No description"),
                fields = fields,
                footer = "Honua Alerts",
                ts = alert.StartsAt.ToUnixTimeSeconds()
            });
        }

        if (webhook.Alerts.Count > 5)
        {
            attachments.Add(new
            {
                color = "warning",
                text = $"_... and {webhook.Alerts.Count - 5} more alerts_"
            });
        }

        return new
        {
            text = $"{icon} *{status}: {alertName}*",
            attachments = attachments
        };
    }

    private static string GetSeverityIcon(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => ":rotating_light:",
            "warning" => ":warning:",
            "database" => ":file_cabinet:",
            "storage" => ":floppy_disk:",
            _ => ":information_source:"
        };
    }

    private static string GetSeverityColor(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => "danger",
            "warning" => "warning",
            _ => "good"
        };
    }
}

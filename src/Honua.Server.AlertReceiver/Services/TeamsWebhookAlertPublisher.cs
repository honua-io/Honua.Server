// <copyright file="TeamsWebhookAlertPublisher.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Honua.Server.AlertReceiver.Models;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Publishes alerts to Microsoft Teams via Incoming Webhooks.
/// </summary>
/// <remarks>
/// Uses Teams MessageCard format with customizable theme colors per severity.
/// Includes action button to view alert in AlertManager.
/// Limits display to 5 alerts per webhook to avoid message size limits.
/// </remarks>
public sealed class TeamsWebhookAlertPublisher : WebhookAlertPublisherBase
{
    public TeamsWebhookAlertPublisher(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TeamsWebhookAlertPublisher> logger)
        : base(httpClientFactory, configuration, logger, "Teams")
    {
    }

    protected override string? GetEndpoint(AlertManagerWebhook webhook, string severity)
    {
        var key = severity.ToLowerInvariant() switch
        {
            "critical" => "Alerts:Teams:CriticalWebhookUrl",
            "warning" => "Alerts:Teams:WarningWebhookUrl",
            "database" => "Alerts:Teams:DatabaseWebhookUrl",
            "storage" => "Alerts:Teams:StorageWebhookUrl",
            _ => "Alerts:Teams:DefaultWebhookUrl",
        };

        return this.Configuration[key];
    }

    protected override object BuildPayload(AlertManagerWebhook webhook, string severity)
    {
        var alertName = this.GetAlertName(webhook);
        var status = webhook.Status.ToUpperInvariant();
        var themeColor = GetThemeColor(severity);

        var sections = new List<object>();

        foreach (var alert in webhook.Alerts.Take(5)) // Limit to 5 alerts
        {
            var facts = new List<object>
            {
                new { name = "Status", value = alert.Status },
                new { name = "Severity", value = alert.Labels.GetValueOrDefault("severity", "unknown") },
                new { name = "Started", value = alert.StartsAt.ToString("yyyy-MM-dd HH:mm:ss UTC") },
            };

            if (alert.Labels.TryGetValue("api_protocol", out var protocol))
            {
                facts.Add(new { name = "Protocol", value = protocol });
            }

            if (alert.Labels.TryGetValue("service_id", out var service))
            {
                facts.Add(new { name = "Service", value = service });
            }

            if (alert.EndsAt.HasValue && alert.Status == "resolved")
            {
                facts.Add(new { name = "Resolved", value = alert.EndsAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC") });
                var duration = alert.EndsAt.Value - alert.StartsAt;
                facts.Add(new { name = "Duration", value = $"{duration.TotalMinutes:F1} minutes" });
            }

            sections.Add(new
            {
                activityTitle = alert.Labels.GetValueOrDefault("alertname", "Alert"),
                activitySubtitle = alert.Annotations.GetValueOrDefault("summary", string.Empty),
                text = alert.Annotations.GetValueOrDefault("description", "No description"),
                facts = facts,
            });
        }

        if (webhook.Alerts.Count > 5)
        {
            sections.Add(new
            {
                text = $"_... and {webhook.Alerts.Count - 5} more alerts_",
            });
        }

        return new
        {
            type = "MessageCard",
            context = "https://schema.org/extensions",
            summary = $"{status}: {alertName}",
            themeColor = themeColor,
            title = $"{status}: {alertName}",
            sections = sections,
            potentialAction = new[]
            {
                new
                {
                    type = "OpenUri",
                    name = "View in AlertManager",
                    targets = new[]
                    {
                        new { os = "default", uri = webhook.ExternalUrl }
                    }
                },
            },
        };
    }

    private static string GetThemeColor(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => "FF0000",
            "warning" => "FFA500",
            _ => "0078D4",
        };
    }
}

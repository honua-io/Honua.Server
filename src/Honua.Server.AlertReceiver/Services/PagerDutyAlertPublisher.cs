// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.AlertReceiver.Models;
using System.Text;
using Honua.Server.AlertReceiver.Extensions;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Publishes alerts to PagerDuty Events API v2.
/// </summary>
/// <remarks>
/// Uses PagerDuty Events API v2 with routing keys per severity.
/// Sends individual events for each alert with deduplication keys.
/// Maps alert status to event actions (trigger/resolve).
/// </remarks>
public sealed class PagerDutyAlertPublisher : WebhookAlertPublisherBase
{
    private const string PagerDutyEventsEndpoint = "https://events.pagerduty.com/v2/enqueue";

    public PagerDutyAlertPublisher(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PagerDutyAlertPublisher> logger)
        : base(httpClientFactory, configuration, logger, "PagerDuty")
    {
    }

    protected override string? GetEndpoint(AlertManagerWebhook webhook, string severity)
    {
        // PagerDuty uses a routing key instead of different endpoints
        // We still need to check if a routing key is configured
        var routingKey = GetRoutingKey(severity);
        if (routingKey.IsNullOrWhiteSpace())
        {
            return null; // Will trigger skip logic in base class
        }

        return PagerDutyEventsEndpoint;
    }

    protected override object BuildPayload(AlertManagerWebhook webhook, string severity)
    {
        // PagerDuty needs individual events per alert, but base class expects one payload
        // We'll handle this by overriding PublishAsync to send multiple events
        throw new NotImplementedException("PagerDuty uses per-alert events. Use PublishAsync override.");
    }

    public override async Task PublishAsync(
        AlertManagerWebhook webhook,
        string severity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhook);

        var routingKey = GetRoutingKey(severity);
        if (routingKey.IsNullOrWhiteSpace())
        {
            Logger.LogDebug(
                "No {Service} routing key configured for severity {Severity}, skipping alert publication",
                ServiceName,
                severity);
            return;
        }

        try
        {
            foreach (var alert in webhook.Alerts)
            {
                await PublishSingleAlert(alert, severity, routingKey, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to publish alert to {Service}", ServiceName);
            throw;
        }
    }

    private async Task PublishSingleAlert(
        Alert alert,
        string severity,
        string routingKey,
        CancellationToken cancellationToken)
    {
        var eventAction = alert.Status switch
        {
            "firing" => "trigger",
            "resolved" => "resolve",
            _ => "trigger"
        };

        var payload = new
        {
            routing_key = routingKey,
            event_action = eventAction,
            dedup_key = alert.Fingerprint,
            payload = new
            {
                summary = alert.Annotations.GetValueOrDefault("summary", alert.Labels.GetValueOrDefault("alertname", "Unknown Alert")),
                severity = MapSeverity(severity),
                source = "honua-server",
                timestamp = alert.StartsAt.ToString("o"),
                custom_details = new
                {
                    alertname = alert.Labels.GetValueOrDefault("alertname", "Unknown"),
                    description = alert.Annotations.GetValueOrDefault("description", ""),
                    protocol = alert.Labels.GetValueOrDefault("api_protocol", ""),
                    service = alert.Labels.GetValueOrDefault("service_id", ""),
                    layer = alert.Labels.GetValueOrDefault("layer_id", ""),
                    generator_url = alert.GeneratorUrl,
                    labels = alert.Labels,
                    annotations = alert.Annotations
                }
            }
        };

        var json = SerializePayload(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            // BUG FIX #36: Remove redundant ConfigureAwait chaining
            var response = await HttpClient.PostAsync(PagerDutyEventsEndpoint, content, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            Logger.LogInformation(
                "Published alert to {Service} - Alert: {AlertName}, Action: {Action}, Severity: {Severity}",
                ServiceName,
                alert.Labels.GetValueOrDefault("alertname", "Unknown"),
                eventAction,
                severity);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            Logger.LogError(
                ex,
                "HTTP error publishing alert to {Service} - Status: {StatusCode}",
                ServiceName,
                ex.StatusCode);
            throw;
        }
    }

    private string GetRoutingKey(string severity)
    {
        var key = severity.ToLowerInvariant() switch
        {
            "critical" => "Alerts:PagerDuty:CriticalRoutingKey",
            "warning" => "Alerts:PagerDuty:WarningRoutingKey",
            "database" => "Alerts:PagerDuty:DatabaseRoutingKey",
            _ => "Alerts:PagerDuty:DefaultRoutingKey"
        };

        return Configuration[key] ?? string.Empty;
    }

    private static string MapSeverity(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => "critical",
            "warning" => "warning",
            "database" => "error",
            "storage" => "error",
            _ => "info"
        };
    }
}

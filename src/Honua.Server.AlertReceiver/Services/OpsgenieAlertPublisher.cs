// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.AlertReceiver.Models;
using System.Net;
using System.Text;
using Honua.Server.AlertReceiver.Extensions;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Publishes alerts to Opsgenie Alert API.
/// </summary>
/// <remarks>
/// Uses Opsgenie Alert API with GenieKey authentication.
/// Creates new alerts for firing status and closes alerts for resolved status.
/// Maps severity to Opsgenie priority levels (P1-P4).
/// Includes comprehensive alert details, labels, and annotations.
/// </remarks>
public sealed class OpsgenieAlertPublisher : WebhookAlertPublisherBase
{
    public OpsgenieAlertPublisher(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpsgenieAlertPublisher> logger)
        : base(httpClientFactory, configuration, logger, "Opsgenie")
    {
    }

    protected override string? GetEndpoint(AlertManagerWebhook webhook, string severity)
    {
        var apiKey = Configuration["Alerts:Opsgenie:ApiKey"];
        if (apiKey.IsNullOrWhiteSpace())
        {
            return null; // Will trigger skip logic in base class
        }

        // Opsgenie endpoint varies by operation, so we return the base URL
        var apiUrl = Configuration["Alerts:Opsgenie:ApiUrl"] ?? "https://api.opsgenie.com";
        return apiUrl;
    }

    protected override object BuildPayload(AlertManagerWebhook webhook, string severity)
    {
        // Opsgenie handles alerts individually with different endpoints for create/close
        // We'll override PublishAsync to handle this complexity
        throw new NotImplementedException("Opsgenie uses per-alert operations. Use PublishAsync override.");
    }

    public override async Task PublishAsync(
        AlertManagerWebhook webhook,
        string severity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhook);

        var apiKey = Configuration["Alerts:Opsgenie:ApiKey"];
        if (apiKey.IsNullOrWhiteSpace())
        {
            Logger.LogDebug(
                "No {Service} API key configured, skipping alert publication",
                ServiceName);
            return;
        }

        var apiUrl = Configuration["Alerts:Opsgenie:ApiUrl"] ?? "https://api.opsgenie.com";

        try
        {
            foreach (var alert in webhook.Alerts)
            {
                if (alert.Status == "firing")
                {
                    await CreateAlert(alert, severity, apiKey, apiUrl, cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (alert.Status == "resolved")
                {
                    await CloseAlert(alert, apiKey, apiUrl, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to publish alert to {Service}", ServiceName);
            throw;
        }
    }

    protected override void AddCustomHeaders(
        StringContent content,
        AlertManagerWebhook webhook,
        string severity)
    {
        var apiKey = Configuration["Alerts:Opsgenie:ApiKey"];
        if (apiKey.HasValue())
        {
            content.Headers.Add("Authorization", $"GenieKey {apiKey}");
        }
    }

    private async Task CreateAlert(
        Alert alert,
        string severity,
        string apiKey,
        string apiUrl,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, string>
        {
            ["severity"] = severity,
            ["fingerprint"] = alert.Fingerprint,
            ["generator_url"] = alert.GeneratorUrl,
            ["starts_at"] = alert.StartsAt.ToString("o")
        };

        foreach (var label in alert.Labels)
        {
            details[$"label_{label.Key}"] = label.Value;
        }

        foreach (var annotation in alert.Annotations)
        {
            details[$"annotation_{annotation.Key}"] = annotation.Value;
        }

        var payload = new
        {
            message = alert.Labels.GetValueOrDefault("alertname", "Unknown Alert"),
            alias = alert.Fingerprint,
            description = alert.Annotations.GetValueOrDefault("description", "No description"),
            priority = MapPriority(severity),
            source = "Honua Server",
            tags = new[]
            {
                $"severity:{severity}",
                $"protocol:{alert.Labels.GetValueOrDefault("api_protocol", "unknown")}",
                $"service:{alert.Labels.GetValueOrDefault("service_id", "unknown")}"
            },
            details = details
        };

        var json = SerializePayload(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v2/alerts")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"GenieKey {apiKey}");

        try
        {
            // BUG FIX #37: Remove redundant ConfigureAwait chaining
            var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            Logger.LogInformation(
                "Published alert to {Service} - Alert: {AlertName}, Severity: {Severity}",
                ServiceName,
                alert.Labels.GetValueOrDefault("alertname", "Unknown"),
                severity);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(
                ex,
                "HTTP error publishing alert to {Service} - Status: {StatusCode}",
                ServiceName,
                ex.StatusCode);
            throw;
        }
    }

    private async Task CloseAlert(
        Alert alert,
        string apiKey,
        string apiUrl,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            source = "Honua Server",
            note = "Alert resolved"
        };

        var json = SerializePayload(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{apiUrl}/v2/alerts/{alert.Fingerprint}/close?identifierType=alias")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"GenieKey {apiKey}");

        try
        {
            // BUG FIX #37: Remove redundant ConfigureAwait chaining
            var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            Logger.LogInformation(
                "Published alert to {Service} - Closed alert with fingerprint: {Fingerprint}",
                ServiceName,
                alert.Fingerprint);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Alert doesn't exist in Opsgenie, which is fine for resolved alerts
            Logger.LogDebug(
                "Alert {Fingerprint} not found in {Service} (already closed or never created)",
                alert.Fingerprint,
                ServiceName);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(
                ex,
                "HTTP error closing alert in {Service} - Status: {StatusCode}",
                ServiceName,
                ex.StatusCode);
            throw;
        }
    }

    private static string MapPriority(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => "P1",
            "warning" => "P3",
            "database" => "P2",
            "storage" => "P2",
            _ => "P4"
        };
    }
}

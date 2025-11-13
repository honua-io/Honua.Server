// <copyright file="AzureEventGridAlertPublisher.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Azure;
using Azure.Messaging.EventGrid;
using Honua.Server.AlertReceiver.Models;
using System.Text.Json;
using Honua.Server.AlertReceiver.Extensions;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Publishes alerts to Azure Event Grid.
/// </summary>
public sealed class AzureEventGridAlertPublisher : IAlertPublisher, IDisposable
{
    private readonly EventGridPublisherClient? client;
    private readonly IConfiguration configuration;
    private readonly ILogger<AzureEventGridAlertPublisher> logger;

    public AzureEventGridAlertPublisher(
        IConfiguration configuration,
        ILogger<AzureEventGridAlertPublisher> logger)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var endpoint = this.configuration["Alerts:Azure:EventGridEndpoint"];
        var accessKey = this.configuration["Alerts:Azure:EventGridAccessKey"];

        if (endpoint.HasValue() && accessKey.HasValue())
        {
            this.client = new EventGridPublisherClient(
                new Uri(endpoint),
                new AzureKeyCredential(accessKey));
        }
    }

    public async Task PublishAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default)
    {
        if (this.client == null)
        {
            this.logger.LogWarning("Azure Event Grid not configured, skipping publish");
            return;
        }

        try
        {
            var events = new List<EventGridEvent>();

            foreach (var alert in webhook.Alerts)
            {
                var eventData = new
                {
                    alertName = alert.Labels.GetValueOrDefault("alertname", "Unknown"),
                    severity = alert.Labels.GetValueOrDefault("severity", severity),
                    status = alert.Status,
                    description = alert.Annotations.GetValueOrDefault("description", "No description"),
                    summary = alert.Annotations.GetValueOrDefault("summary", string.Empty),
                    startsAt = alert.StartsAt,
                    endsAt = alert.EndsAt,
                    labels = alert.Labels,
                    annotations = alert.Annotations,
                    generatorUrl = alert.GeneratorUrl,
                    fingerprint = alert.Fingerprint,
                };

                var gridEvent = new EventGridEvent(
                    subject: $"honua/alerts/{severity}/{alert.Labels.GetValueOrDefault("alertname", "unknown")}",
                    eventType: $"Honua.Alert.{alert.Status}",
                    dataVersion: "1.0",
                    data: eventData);

                events.Add(gridEvent);
            }

            await this.client.SendEventsAsync(events, cancellationToken);

            this.logger.LogInformation(
                "Published {Count} alerts to Azure Event Grid, Severity: {Severity}, Status: {Status}",
                events.Count,
                severity,
                webhook.Status);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to publish alerts to Azure Event Grid");
            throw;
        }
    }

    public void Dispose()
    {
        // EventGridPublisherClient may hold HTTP resources
        if (this.client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

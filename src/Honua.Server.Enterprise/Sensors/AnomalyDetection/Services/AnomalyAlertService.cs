// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using System.Text.Json;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Configuration;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Data;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Sensors.AnomalyDetection.Services;

/// <summary>
/// Service for delivering anomaly alerts via webhooks
/// Integrates with GeoEvent API for alert delivery
/// </summary>
public sealed class AnomalyAlertService : IAnomalyAlertService
{
    private readonly IAnomalyDetectionRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<AnomalyDetectionOptions> _options;
    private readonly ILogger<AnomalyAlertService> _logger;

    public AnomalyAlertService(
        IAnomalyDetectionRepository repository,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<AnomalyDetectionOptions> options,
        ILogger<AnomalyAlertService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AlertDeliveryResult> SendAnomalyAlertsAsync(
        IReadOnlyList<SensorAnomaly> anomalies,
        CancellationToken ct = default)
    {
        if (anomalies.Count == 0)
        {
            _logger.LogDebug("No anomalies to send");
            return new AlertDeliveryResult
            {
                TotalAnomalies = 0,
                AlertsSent = 0,
                AlertsSkipped = 0,
                AlertsFailed = 0
            };
        }

        _logger.LogInformation("Processing {Count} anomalies for alert delivery", anomalies.Count);

        var opts = _options.CurrentValue;
        var alertOpts = opts.AlertDelivery;
        var rateLimitOpts = opts.RateLimit;

        var result = new AlertDeliveryResult
        {
            TotalAnomalies = anomalies.Count
        };

        // Check global rate limit
        if (rateLimitOpts.Enabled)
        {
            var totalAlertCount = await _repository.GetTotalAlertCountAsync(
                rateLimitOpts.RateLimitWindow,
                anomalies.FirstOrDefault()?.TenantId,
                ct);

            if (totalAlertCount >= rateLimitOpts.MaxTotalAlerts)
            {
                _logger.LogWarning(
                    "Global rate limit exceeded: {Count}/{MaxAlerts} alerts sent in last {Window}. Skipping all alerts.",
                    totalAlertCount,
                    rateLimitOpts.MaxTotalAlerts,
                    rateLimitOpts.RateLimitWindow);

                result.AlertsSkipped = anomalies.Count;
                result.Errors = new List<string> { "Global rate limit exceeded" };
                return result;
            }
        }

        foreach (var anomaly in anomalies)
        {
            try
            {
                // Check rate limiting per datastream
                if (rateLimitOpts.Enabled)
                {
                    var canSend = await _repository.CanSendAlertAsync(
                        anomaly.DatastreamId,
                        anomaly.Type,
                        rateLimitOpts.RateLimitWindow,
                        rateLimitOpts.MaxAlertsPerDatastream,
                        anomaly.TenantId,
                        ct);

                    if (!canSend)
                    {
                        _logger.LogDebug(
                            "Rate limit reached for datastream {DatastreamId} - skipping alert",
                            anomaly.DatastreamId);

                        result.AlertsSkipped++;
                        continue;
                    }
                }

                // Send alert via webhooks
                var alert = new AnomalyAlert
                {
                    Anomaly = anomaly,
                    TenantId = anomaly.TenantId,
                    Context = new Dictionary<string, object>
                    {
                        ["source"] = "honua.sensor.anomaly_detection",
                        ["version"] = "1.0"
                    }
                };

                var sent = await SendWebhookAlertsAsync(alert, alertOpts, ct);

                if (sent)
                {
                    // Record alert for rate limiting
                    await _repository.RecordAlertAsync(
                        anomaly.DatastreamId,
                        anomaly.Type,
                        anomaly.TenantId,
                        ct);

                    result.AlertsSent++;

                    _logger.LogInformation(
                        "Alert sent for {Type} anomaly on datastream {DatastreamName}",
                        anomaly.Type,
                        anomaly.DatastreamName);
                }
                else
                {
                    result.AlertsFailed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending alert for datastream {DatastreamId}",
                    anomaly.DatastreamId);

                result.AlertsFailed++;
                result.Errors ??= new List<string>();
                result.Errors.Add($"Datastream {anomaly.DatastreamId}: {ex.Message}");
            }
        }

        _logger.LogInformation(
            "Alert delivery complete: {Sent} sent, {Skipped} skipped, {Failed} failed",
            result.AlertsSent,
            result.AlertsSkipped,
            result.AlertsFailed);

        return result;
    }

    private async Task<bool> SendWebhookAlertsAsync(
        AnomalyAlert alert,
        AlertDeliveryOptions opts,
        CancellationToken ct)
    {
        // If no webhooks configured, just log
        if (string.IsNullOrWhiteSpace(opts.WebhookUrl) && opts.AdditionalWebhooks.Count == 0)
        {
            _logger.LogWarning("No webhook URLs configured - alert will only be logged");
            _logger.LogWarning(
                "Anomaly Alert: {Type} - {Description}",
                alert.Anomaly.Type,
                alert.Anomaly.Description);
            return true;
        }

        var webhooks = new List<string>();
        if (!string.IsNullOrWhiteSpace(opts.WebhookUrl))
        {
            webhooks.Add(opts.WebhookUrl);
        }
        webhooks.AddRange(opts.AdditionalWebhooks);

        var allSucceeded = true;

        foreach (var webhookUrl in webhooks)
        {
            var success = await SendToWebhookAsync(webhookUrl, alert, opts, ct);
            if (!success)
            {
                allSucceeded = false;
            }
        }

        return allSucceeded;
    }

    private async Task<bool> SendToWebhookAsync(
        string webhookUrl,
        AnomalyAlert alert,
        AlertDeliveryOptions opts,
        CancellationToken ct)
    {
        var httpClient = _httpClientFactory.CreateClient("AnomalyAlertWebhook");
        httpClient.Timeout = opts.WebhookTimeout;

        var maxRetries = opts.EnableRetries ? opts.MaxRetries : 1;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            attempt++;

            try
            {
                _logger.LogDebug(
                    "Sending alert to webhook {Url} (attempt {Attempt}/{MaxRetries})",
                    webhookUrl,
                    attempt,
                    maxRetries);

                var response = await httpClient.PostAsJsonAsync(webhookUrl, alert, ct);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Alert successfully sent to webhook {Url}",
                        webhookUrl);
                    return true;
                }

                _logger.LogWarning(
                    "Webhook {Url} returned non-success status: {StatusCode}",
                    webhookUrl,
                    response.StatusCode);

                // Don't retry 4xx errors (client errors)
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending alert to webhook {Url} (attempt {Attempt}/{MaxRetries})",
                    webhookUrl,
                    attempt,
                    maxRetries);
            }

            // Wait before retry
            if (attempt < maxRetries)
            {
                await Task.Delay(opts.RetryDelay, ct);
            }
        }

        return false;
    }
}

/// <summary>
/// Interface for anomaly alert service
/// </summary>
public interface IAnomalyAlertService
{
    /// <summary>
    /// Sends alerts for detected anomalies via configured webhooks
    /// Handles rate limiting and retries
    /// </summary>
    Task<AlertDeliveryResult> SendAnomalyAlertsAsync(
        IReadOnlyList<SensorAnomaly> anomalies,
        CancellationToken ct = default);
}

/// <summary>
/// Result of alert delivery operation
/// </summary>
public sealed record AlertDeliveryResult
{
    public int TotalAnomalies { get; set; }
    public int AlertsSent { get; set; }
    public int AlertsSkipped { get; set; }
    public int AlertsFailed { get; set; }
    public List<string>? Errors { get; set; }

    public bool AllSucceeded => AlertsFailed == 0 && AlertsSent == TotalAnomalies - AlertsSkipped;
}

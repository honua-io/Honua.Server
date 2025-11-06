// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Models;
using Honua.Server.Core.Repositories;
using Honua.Server.Enterprise.Events.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Events.Services;

/// <summary>
/// Service that bridges geofence events to the alert system.
/// Converts geofence events into generic alerts and routes them through the alert pipeline.
/// </summary>
public interface IGeofenceToAlertBridgeService
{
    /// <summary>
    /// Process a geofence event and potentially generate alerts based on matching rules.
    /// </summary>
    Task ProcessGeofenceEventAsync(GeofenceEvent geofenceEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of geofence-to-alert bridge service.
/// </summary>
public sealed class GeofenceToAlertBridgeService : IGeofenceToAlertBridgeService
{
    private readonly IGeofenceAlertRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeofenceToAlertBridgeService> _logger;
    private readonly string _alertReceiverBaseUrl;

    // Cache for recent alerts to prevent duplicates within the deduplication window
    private readonly Dictionary<string, DateTimeOffset> _recentAlerts = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public GeofenceToAlertBridgeService(
        IGeofenceAlertRepository repository,
        IHttpClientFactory httpClientFactory,
        ILogger<GeofenceToAlertBridgeService> logger,
        string alertReceiverBaseUrl)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alertReceiverBaseUrl = alertReceiverBaseUrl ?? throw new ArgumentNullException(nameof(alertReceiverBaseUrl));
    }

    public async Task ProcessGeofenceEventAsync(GeofenceEvent geofenceEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Processing geofence event {EventId}: {EventType} for entity {EntityId} at geofence {GeofenceName}",
                geofenceEvent.Id, geofenceEvent.EventType, geofenceEvent.EntityId, geofenceEvent.GeofenceName);

            // Check if alert should be silenced
            var shouldSilence = await _repository.ShouldSilenceAlertAsync(
                geofenceEvent.GeofenceId,
                geofenceEvent.GeofenceName,
                geofenceEvent.EntityId,
                geofenceEvent.EventType.ToString().ToLowerInvariant(),
                new DateTimeOffset(DateTime.SpecifyKind(geofenceEvent.EventTime, DateTimeKind.Utc)),
                geofenceEvent.TenantId,
                cancellationToken);

            if (shouldSilence)
            {
                _logger.LogInformation(
                    "Geofence event {EventId} silenced by silencing rules",
                    geofenceEvent.Id);

                // Still track the correlation but mark as silenced
                await TrackSilencedEventAsync(geofenceEvent, cancellationToken);
                return;
            }

            // Find matching alert rules
            var matchingRules = await _repository.FindMatchingRulesAsync(
                geofenceEvent.GeofenceId,
                geofenceEvent.GeofenceName,
                geofenceEvent.EntityId,
                geofenceEvent.EntityType,
                geofenceEvent.EventType.ToString().ToLowerInvariant(),
                geofenceEvent.DwellTimeSeconds,
                geofenceEvent.TenantId,
                cancellationToken);

            if (!matchingRules.Any())
            {
                _logger.LogDebug("No matching alert rules found for geofence event {EventId}", geofenceEvent.Id);
                return;
            }

            _logger.LogInformation(
                "Found {RuleCount} matching alert rules for geofence event {EventId}",
                matchingRules.Count, geofenceEvent.Id);

            // Process each matching rule
            foreach (var rule in matchingRules)
            {
                await ProcessAlertRuleAsync(geofenceEvent, rule, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing geofence event {EventId}", geofenceEvent.Id);
            throw;
        }
    }

    private async Task ProcessAlertRuleAsync(GeofenceEvent geofenceEvent, GeofenceAlertRule rule, CancellationToken cancellationToken)
    {
        try
        {
            // Generate alert fingerprint for deduplication
            var fingerprint = GenerateFingerprint(geofenceEvent, rule);

            // Check deduplication window
            if (await IsWithinDeduplicationWindowAsync(fingerprint, rule.DeduplicationWindowMinutes))
            {
                _logger.LogDebug(
                    "Alert fingerprint {Fingerprint} is within deduplication window, skipping",
                    fingerprint);
                return;
            }

            // Convert geofence event to generic alert
            var alert = ConvertToGenericAlert(geofenceEvent, rule, fingerprint);

            // Send alert to alert receiver
            var alertHistoryId = await SendAlertAsync(alert, cancellationToken);

            // Track correlation
            var correlation = new GeofenceAlertCorrelation
            {
                GeofenceEventId = geofenceEvent.Id,
                AlertFingerprint = fingerprint,
                AlertHistoryId = alertHistoryId,
                AlertCreatedAt = DateTimeOffset.UtcNow,
                AlertSeverity = rule.AlertSeverity,
                AlertStatus = "active",
                NotificationChannelIds = rule.NotificationChannelIds,
                WasSilenced = false,
                TenantId = geofenceEvent.TenantId
            };

            await _repository.CreateCorrelationAsync(correlation, cancellationToken);

            // Update deduplication cache
            await UpdateDeduplicationCacheAsync(fingerprint);

            _logger.LogInformation(
                "Successfully created alert {Fingerprint} for geofence event {EventId} using rule {RuleName}",
                fingerprint, geofenceEvent.Id, rule.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing alert rule {RuleName} for geofence event {EventId}",
                rule.Name, geofenceEvent.Id);
        }
    }

    private GenericAlert ConvertToGenericAlert(GeofenceEvent geofenceEvent, GeofenceAlertRule rule, string fingerprint)
    {
        // Apply template substitutions
        var name = ApplyTemplate(rule.AlertNameTemplate, geofenceEvent);
        var description = rule.AlertDescriptionTemplate != null
            ? ApplyTemplate(rule.AlertDescriptionTemplate, geofenceEvent)
            : null;

        var alert = new GenericAlert
        {
            Name = name,
            Severity = rule.AlertSeverity,
            Status = "firing",
            Summary = $"Geofence {geofenceEvent.EventType} event",
            Description = description,
            Source = "geofence-system",
            Service = "geofence-events",
            Fingerprint = fingerprint,
            Timestamp = new DateTimeOffset(DateTime.SpecifyKind(geofenceEvent.EventTime, DateTimeKind.Utc)),
            Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["geofence_id"] = geofenceEvent.GeofenceId.ToString(),
                ["geofence_name"] = geofenceEvent.GeofenceName,
                ["entity_id"] = geofenceEvent.EntityId,
                ["event_type"] = geofenceEvent.EventType.ToString().ToLowerInvariant(),
                ["alert_rule_id"] = rule.Id.ToString(),
                ["alert_rule_name"] = rule.Name
            },
            Context = new Dictionary<string, object>
            {
                ["geofence_event_id"] = geofenceEvent.Id.ToString(),
                ["location_lat"] = geofenceEvent.Location.Y,
                ["location_lon"] = geofenceEvent.Location.X,
            }
        };

        // Add optional fields
        if (geofenceEvent.EntityType != null)
        {
            alert.Labels["entity_type"] = geofenceEvent.EntityType;
        }

        if (geofenceEvent.DwellTimeSeconds.HasValue)
        {
            alert.Labels["dwell_time_seconds"] = geofenceEvent.DwellTimeSeconds.Value.ToString();
            alert.Context["dwell_time_seconds"] = geofenceEvent.DwellTimeSeconds.Value;
        }

        if (geofenceEvent.TenantId != null)
        {
            alert.Labels["tenant_id"] = geofenceEvent.TenantId;
        }

        // Merge custom labels from rule
        if (rule.AlertLabels != null)
        {
            foreach (var (key, value) in rule.AlertLabels)
            {
                alert.Labels[key] = value;
            }
        }

        // Add properties from geofence event
        if (geofenceEvent.Properties != null)
        {
            foreach (var (key, value) in geofenceEvent.Properties)
            {
                alert.Context[$"property_{key}"] = value;
            }
        }

        return alert;
    }

    private string ApplyTemplate(string template, GeofenceEvent geofenceEvent)
    {
        var result = template;

        // Replace placeholders
        result = result.Replace("{entity_id}", geofenceEvent.EntityId);
        result = result.Replace("{geofence_name}", geofenceEvent.GeofenceName);
        result = result.Replace("{geofence_id}", geofenceEvent.GeofenceId.ToString());
        result = result.Replace("{event_type}", geofenceEvent.EventType.ToString().ToLowerInvariant());
        result = result.Replace("{entity_type}", geofenceEvent.EntityType ?? "unknown");

        if (geofenceEvent.DwellTimeSeconds.HasValue)
        {
            result = result.Replace("{dwell_time}", geofenceEvent.DwellTimeSeconds.Value.ToString());
            result = result.Replace("{dwell_time_minutes}", Math.Round(geofenceEvent.DwellTimeSeconds.Value / 60.0, 1).ToString());
        }

        result = result.Replace("{event_time}", geofenceEvent.EventTime.ToString("u"));

        return result;
    }

    private string GenerateFingerprint(GeofenceEvent geofenceEvent, GeofenceAlertRule rule)
    {
        // Create a unique fingerprint based on event and rule
        // This allows the same entity to trigger alerts for different rules,
        // but prevents duplicate alerts for the same rule within the deduplication window
        var fingerprintData = $"geofence:{geofenceEvent.GeofenceId}:entity:{geofenceEvent.EntityId}:rule:{rule.Id}:type:{geofenceEvent.EventType}";

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprintData));
        return $"gf-{Convert.ToHexString(hash)[..40]}"; // Take first 40 chars (20 bytes)
    }

    private async Task<bool> IsWithinDeduplicationWindowAsync(string fingerprint, int windowMinutes)
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (_recentAlerts.TryGetValue(fingerprint, out var lastSentTime))
            {
                var age = DateTimeOffset.UtcNow - lastSentTime;
                if (age.TotalMinutes < windowMinutes)
                {
                    return true; // Within deduplication window
                }

                // Expired, remove from cache
                _recentAlerts.Remove(fingerprint);
            }

            return false;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task UpdateDeduplicationCacheAsync(string fingerprint)
    {
        await _cacheLock.WaitAsync();
        try
        {
            _recentAlerts[fingerprint] = DateTimeOffset.UtcNow;

            // Clean up old entries (older than 24 hours)
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var expiredKeys = _recentAlerts.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredKeys)
            {
                _recentAlerts.Remove(key);
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<long?> SendAlertAsync(GenericAlert alert, CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("AlertReceiver");
            httpClient.BaseAddress = new Uri(_alertReceiverBaseUrl);

            var response = await httpClient.PostAsJsonAsync("/api/alerts", alert, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // Try to extract alert history ID from response if available
            // The exact response format depends on the alert receiver implementation
            if (!string.IsNullOrEmpty(responseContent))
            {
                try
                {
                    var json = JsonDocument.Parse(responseContent);
                    if (json.RootElement.TryGetProperty("id", out var idElement))
                    {
                        return idElement.GetInt64();
                    }
                }
                catch
                {
                    // Response doesn't contain ID, continue without it
                }
            }

            _logger.LogInformation("Successfully sent alert {Fingerprint} to alert receiver", alert.Fingerprint);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to send alert {Fingerprint} to alert receiver", alert.Fingerprint);
            throw;
        }
    }

    private async Task TrackSilencedEventAsync(GeofenceEvent geofenceEvent, CancellationToken cancellationToken)
    {
        try
        {
            var correlation = new GeofenceAlertCorrelation
            {
                GeofenceEventId = geofenceEvent.Id,
                AlertFingerprint = $"silenced-{geofenceEvent.Id}",
                AlertHistoryId = null,
                AlertCreatedAt = DateTimeOffset.UtcNow,
                AlertSeverity = "info",
                AlertStatus = "silenced",
                NotificationChannelIds = null,
                WasSilenced = true,
                TenantId = geofenceEvent.TenantId
            };

            await _repository.CreateCorrelationAsync(correlation, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track silenced event {EventId}", geofenceEvent.Id);
        }
    }
}

/// <summary>
/// Simplified GenericAlert model for sending to alert receiver.
/// Matches the structure expected by the AlertReceiver API.
/// </summary>
internal sealed class GenericAlert
{
    public string Name { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string Status { get; set; } = "firing";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string Source { get; set; } = "unknown";
    public string? Service { get; set; }
    public string? Environment { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset Timestamp { get; set; }
    public string? Fingerprint { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

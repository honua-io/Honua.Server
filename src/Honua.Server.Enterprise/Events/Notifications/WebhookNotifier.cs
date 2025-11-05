using System.Net.Http.Json;
using System.Text.Json;
using Honua.Server.Enterprise.Events.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Events.Notifications;

/// <summary>
/// Sends geofence events to configured webhook URLs
/// </summary>
public class WebhookNotifier : IGeofenceEventNotifier
{
    private readonly HttpClient _httpClient;
    private readonly WebhookNotifierOptions _options;
    private readonly ILogger<WebhookNotifier> _logger;

    public string Name => "Webhook";
    public bool IsEnabled => _options.Enabled && _options.Urls?.Any() == true;

    public WebhookNotifier(
        HttpClient httpClient,
        IOptions<WebhookNotifierOptions> options,
        ILogger<WebhookNotifier> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyAsync(
        GeofenceEvent geofenceEvent,
        Geofence geofence,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Webhook notifier is disabled");
            return;
        }

        var payload = CreatePayload(geofenceEvent, geofence);

        foreach (var webhookUrl in _options.Urls!)
        {
            try
            {
                _logger.LogDebug("Sending geofence event {EventId} to webhook {Url}", geofenceEvent.Id, webhookUrl);

                var response = await _httpClient.PostAsJsonAsync(
                    webhookUrl,
                    payload,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Successfully sent geofence event {EventId} to webhook {Url}",
                        geofenceEvent.Id,
                        webhookUrl);
                }
                else
                {
                    _logger.LogWarning(
                        "Webhook {Url} returned status {StatusCode} for event {EventId}",
                        webhookUrl,
                        response.StatusCode,
                        geofenceEvent.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending geofence event {EventId} to webhook {Url}",
                    geofenceEvent.Id,
                    webhookUrl);
            }
        }
    }

    public async Task NotifyBatchAsync(
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (_options.UseBatchEndpoint && !string.IsNullOrWhiteSpace(_options.BatchUrl))
        {
            await SendBatchAsync(events, cancellationToken);
        }
        else
        {
            // Send individually
            foreach (var (geofenceEvent, geofence) in events)
            {
                await NotifyAsync(geofenceEvent, geofence, cancellationToken);
            }
        }
    }

    private async Task SendBatchAsync(
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        CancellationToken cancellationToken)
    {
        var batchPayload = new
        {
            event_count = events.Count,
            timestamp = DateTime.UtcNow,
            events = events.Select(e => CreatePayload(e.Event, e.Geofence)).ToList()
        };

        try
        {
            _logger.LogDebug("Sending batch of {EventCount} events to webhook {Url}",
                events.Count,
                _options.BatchUrl);

            var response = await _httpClient.PostAsJsonAsync(
                _options.BatchUrl!,
                batchPayload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully sent batch of {EventCount} events to webhook",
                    events.Count);
            }
            else
            {
                _logger.LogWarning(
                    "Batch webhook returned status {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending batch to webhook {Url}", _options.BatchUrl);
        }
    }

    private object CreatePayload(GeofenceEvent geofenceEvent, Geofence geofence)
    {
        return new
        {
            event_id = geofenceEvent.Id,
            event_type = geofenceEvent.EventType.ToString(),
            event_time = geofenceEvent.EventTime,
            entity_id = geofenceEvent.EntityId,
            entity_type = geofenceEvent.EntityType,
            geofence = new
            {
                id = geofence.Id,
                name = geofence.Name,
                description = geofence.Description,
                properties = geofence.Properties
            },
            location = new
            {
                type = "Point",
                coordinates = new[] { geofenceEvent.Location.X, geofenceEvent.Location.Y }
            },
            properties = geofenceEvent.Properties,
            dwell_time_seconds = geofenceEvent.DwellTimeSeconds,
            tenant_id = geofenceEvent.TenantId,
            created_at = geofenceEvent.CreatedAt
        };
    }
}

/// <summary>
/// Configuration options for webhook notifier
/// </summary>
public class WebhookNotifierOptions
{
    public const string SectionName = "GeoEvent:Notifications:Webhook";

    /// <summary>
    /// Whether webhook notifications are enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// List of webhook URLs to POST events to
    /// </summary>
    public List<string>? Urls { get; set; }

    /// <summary>
    /// Optional separate URL for batch events
    /// </summary>
    public string? BatchUrl { get; set; }

    /// <summary>
    /// Whether to use batch endpoint for multiple events
    /// </summary>
    public bool UseBatchEndpoint { get; set; }

    /// <summary>
    /// Timeout for webhook requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for failed webhooks
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Optional authentication header value (e.g., "Bearer token123")
    /// </summary>
    public string? AuthenticationHeader { get; set; }
}

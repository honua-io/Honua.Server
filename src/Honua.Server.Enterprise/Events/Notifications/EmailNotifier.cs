using System.Net;
using System.Net.Mail;
using Honua.Server.Enterprise.Events.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Events.Notifications;

/// <summary>
/// Sends email notifications for geofence events
/// </summary>
public class EmailNotifier : IGeofenceEventNotifier
{
    private readonly EmailNotifierOptions _options;
    private readonly ILogger<EmailNotifier> _logger;

    public string Name => "Email";
    public bool IsEnabled => _options.Enabled && _options.To?.Any() == true;

    public EmailNotifier(
        IOptions<EmailNotifierOptions> options,
        ILogger<EmailNotifier> logger)
    {
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
            _logger.LogDebug("Email notifier is disabled");
            return;
        }

        try
        {
            var subject = BuildSubject(geofenceEvent, geofence);
            var body = BuildBody(geofenceEvent, geofence);

            await SendEmailAsync(subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent email notification for geofence event {EventId} ({EventType}) to {RecipientCount} recipients",
                geofenceEvent.Id,
                geofenceEvent.EventType,
                _options.To!.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification for event {EventId}", geofenceEvent.Id);
        }
    }

    /// <summary>
    /// Sends a batch email notification summarizing multiple geofence events.
    /// </summary>
    /// <param name="events">List of geofence events and their associated geofences to notify about</param>
    /// <param name="cancellationToken">Token to cancel the notification operation</param>
    /// <returns>A task representing the asynchronous notification operation</returns>
    public async Task NotifyBatchAsync(
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || !events.Any())
        {
            return;
        }

        try
        {
            var subject = $"Geofence Events Summary: {events.Count} events";
            var body = BuildBatchBody(events);

            await SendEmailAsync(subject, body, cancellationToken);

            _logger.LogInformation(
                "Sent batch email notification for {EventCount} events to {RecipientCount} recipients",
                events.Count,
                _options.To!.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending batch email notification");
        }
    }

    private async Task SendEmailAsync(string subject, string body, CancellationToken cancellationToken)
    {
        using var message = new MailMessage();

        message.From = new MailAddress(_options.From!, _options.FromName ?? "Honua GeoEvent");
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = _options.UseHtml;

        foreach (var recipient in _options.To!)
        {
            message.To.Add(recipient);
        }

        if (_options.Cc?.Any() == true)
        {
            foreach (var cc in _options.Cc)
            {
                message.CC.Add(cc);
            }
        }

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort);
        client.EnableSsl = _options.UseSsl;

        if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
        {
            client.Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private string BuildSubject(GeofenceEvent geofenceEvent, Geofence geofence)
    {
        return geofenceEvent.EventType switch
        {
            GeofenceEventType.Enter => $"[Geofence Alert] {geofenceEvent.EntityId} entered {geofence.Name}",
            GeofenceEventType.Exit => $"[Geofence Alert] {geofenceEvent.EntityId} exited {geofence.Name}",
            _ => $"[Geofence Alert] {geofenceEvent.EntityId} - {geofence.Name}"
        };
    }

    private string BuildBody(GeofenceEvent geofenceEvent, Geofence geofence)
    {
        if (_options.UseHtml)
        {
            return BuildHtmlBody(geofenceEvent, geofence);
        }

        return $@"
Geofence Event Notification
============================

Event Type: {geofenceEvent.EventType}
Entity ID: {geofenceEvent.EntityId}
Entity Type: {geofenceEvent.EntityType ?? "N/A"}

Geofence: {geofence.Name}
{(geofence.Description != null ? $"Description: {geofence.Description}" : "")}

Event Time: {geofenceEvent.EventTime:yyyy-MM-dd HH:mm:ss} UTC
{(geofenceEvent.DwellTimeSeconds.HasValue ? $"Dwell Time: {FormatDwellTime(geofenceEvent.DwellTimeSeconds.Value)}" : "")}

Location: {geofenceEvent.Location.Y:F6}, {geofenceEvent.Location.X:F6} (lat, lon)

{(geofenceEvent.Properties != null ? $"Additional Properties:\n{FormatProperties(geofenceEvent.Properties)}" : "")}

---
Honua GeoEvent Server
Event ID: {geofenceEvent.Id}
";
    }

    private string BuildHtmlBody(GeofenceEvent geofenceEvent, Geofence geofence)
    {
        var dwellTimeHtml = geofenceEvent.DwellTimeSeconds.HasValue
            ? $"<tr><td><strong>Dwell Time:</strong></td><td>{FormatDwellTime(geofenceEvent.DwellTimeSeconds.Value)}</td></tr>"
            : "";

        var propertiesHtml = geofenceEvent.Properties != null
            ? $@"<h3>Additional Properties</h3><pre>{FormatProperties(geofenceEvent.Properties)}</pre>"
            : "";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0078d4; color: white; padding: 15px; border-radius: 5px; }}
        .content {{ background-color: #f5f5f5; padding: 20px; border-radius: 5px; margin-top: 15px; }}
        table {{ width: 100%; border-collapse: collapse; }}
        td {{ padding: 8px; border-bottom: 1px solid #ddd; }}
        td:first-child {{ font-weight: bold; width: 40%; }}
        .event-enter {{ color: #107c10; }}
        .event-exit {{ color: #d13438; }}
        .footer {{ margin-top: 20px; padding-top: 15px; border-top: 1px solid #ddd; font-size: 0.9em; color: #666; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h2>Geofence Event Notification</h2>
    </div>
    <div class=""content"">
        <table>
            <tr>
                <td><strong>Event Type:</strong></td>
                <td class=""event-{geofenceEvent.EventType.ToString().ToLower()}"">{geofenceEvent.EventType}</td>
            </tr>
            <tr>
                <td><strong>Entity ID:</strong></td>
                <td>{geofenceEvent.EntityId}</td>
            </tr>
            <tr>
                <td><strong>Geofence:</strong></td>
                <td>{geofence.Name}</td>
            </tr>
            <tr>
                <td><strong>Event Time:</strong></td>
                <td>{geofenceEvent.EventTime:yyyy-MM-dd HH:mm:ss} UTC</td>
            </tr>
            {dwellTimeHtml}
            <tr>
                <td><strong>Location:</strong></td>
                <td>{geofenceEvent.Location.Y:F6}, {geofenceEvent.Location.X:F6} (lat, lon)</td>
            </tr>
        </table>
        {propertiesHtml}
    </div>
    <div class=""footer"">
        <p>Honua GeoEvent Server<br>Event ID: {geofenceEvent.Id}</p>
    </div>
</body>
</html>
";
    }

    private string BuildBatchBody(List<(GeofenceEvent Event, Geofence Geofence)> events)
    {
        var enterCount = events.Count(e => e.Event.EventType == GeofenceEventType.Enter);
        var exitCount = events.Count(e => e.Event.EventType == GeofenceEventType.Exit);

        if (_options.UseHtml)
        {
            var eventsHtml = string.Join("\n", events.Select((e, i) =>
                $@"<tr>
                    <td>{i + 1}</td>
                    <td class=""event-{e.Event.EventType.ToString().ToLower()}"">{e.Event.EventType}</td>
                    <td>{e.Event.EntityId}</td>
                    <td>{e.Geofence.Name}</td>
                    <td>{e.Event.EventTime:HH:mm:ss}</td>
                </tr>"));

            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0078d4; color: white; padding: 15px; border-radius: 5px; }}
        .summary {{ background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin-top: 15px; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
        th, td {{ padding: 10px; text-align: left; border-bottom: 1px solid #ddd; }}
        th {{ background-color: #0078d4; color: white; }}
        .event-enter {{ color: #107c10; font-weight: bold; }}
        .event-exit {{ color: #d13438; font-weight: bold; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h2>Geofence Events Summary</h2>
    </div>
    <div class=""summary"">
        <p><strong>Total Events:</strong> {events.Count}</p>
        <p><strong>Enter Events:</strong> {enterCount} | <strong>Exit Events:</strong> {exitCount}</p>
        <p><strong>Time:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
    </div>
    <table>
        <thead>
            <tr>
                <th>#</th>
                <th>Type</th>
                <th>Entity</th>
                <th>Geofence</th>
                <th>Time</th>
            </tr>
        </thead>
        <tbody>
            {eventsHtml}
        </tbody>
    </table>
</body>
</html>
";
        }

        // Plain text version
        var eventsList = string.Join("\n", events.Select((e, i) =>
            $"{i + 1}. {e.Event.EventType} - {e.Event.EntityId} @ {e.Geofence.Name} ({e.Event.EventTime:HH:mm:ss})"));

        return $@"
Geofence Events Summary
=======================

Total Events: {events.Count}
Enter Events: {enterCount}
Exit Events: {exitCount}
Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

Events:
{eventsList}

---
Honua GeoEvent Server
";
    }

    private string FormatDwellTime(int seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        }
        if (timeSpan.TotalMinutes >= 1)
        {
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        }
        return $"{timeSpan.Seconds}s";
    }

    private string FormatProperties(Dictionary<string, object> properties)
    {
        return string.Join("\n", properties.Select(kvp => $"  {kvp.Key}: {kvp.Value}"));
    }
}

/// <summary>
/// Configuration options for email notifier
/// </summary>
public class EmailNotifierOptions
{
    public const string SectionName = "GeoEvent:Notifications:Email";

    /// <summary>
    /// Whether email notifications are enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// SMTP server hostname
    /// </summary>
    public string SmtpHost { get; set; } = "localhost";

    /// <summary>
    /// SMTP server port
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Whether to use SSL/TLS
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// SMTP username (if authentication required)
    /// </summary>
    public string? SmtpUsername { get; set; }

    /// <summary>
    /// SMTP password (if authentication required)
    /// </summary>
    public string? SmtpPassword { get; set; }

    /// <summary>
    /// From email address
    /// </summary>
    public string From { get; set; } = "geoevent@example.com";

    /// <summary>
    /// From display name
    /// </summary>
    public string? FromName { get; set; }

    /// <summary>
    /// List of recipient email addresses
    /// </summary>
    public List<string>? To { get; set; }

    /// <summary>
    /// List of CC email addresses
    /// </summary>
    public List<string>? Cc { get; set; }

    /// <summary>
    /// Whether to use HTML email formatting
    /// </summary>
    public bool UseHtml { get; set; } = true;
}

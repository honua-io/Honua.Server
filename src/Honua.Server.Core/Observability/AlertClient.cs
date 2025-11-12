// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Client for sending alerts directly to the Alert Receiver service.
/// Bypasses Prometheus/AlertManager for immediate alerting.
/// </summary>
public interface IAlertClient
{
    Task SendAlertAsync(string name, string severity, string description, Dictionary<string, string>? labels = null, CancellationToken cancellationToken = default);
    Task SendCriticalAlertAsync(string name, string description, Dictionary<string, string>? labels = null, CancellationToken cancellationToken = default);
}

public sealed class AlertClient : IAlertClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertClient> _logger;
    private readonly string? _environment;
    private readonly string? _serviceName;

    public AlertClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AlertClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AlertReceiver");
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _environment = configuration["Environment"] ?? "production";
        _serviceName = configuration["ServiceName"] ?? "honua-api";

        ConfigureHttpClient();
    }

    public async Task SendAlertAsync(
        string name,
        string severity,
        string description,
        Dictionary<string, string>? labels = null,
        CancellationToken cancellationToken = default)
    {
        var alertUrl = _configuration["Alerts:ReceiverUrl"];
        if (string.IsNullOrWhiteSpace(alertUrl))
        {
            _logger.LogWarning("Alert receiver URL not configured, skipping alert: {Name}", name);
            return;
        }

        try
        {
            var alert = new
            {
                name = name,
                severity = severity,
                status = "firing",
                summary = name,
                description = description,
                source = "honua-api",
                service = _serviceName,
                environment = _environment,
                labels = labels ?? new Dictionary<string, string>(),
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(alert);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var endpoint = new Uri(new Uri(alertUrl, UriKind.Absolute), "/api/alerts");
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Alert sent successfully: {Name} ({Severity})", name, severity);
            }
            else
            {
                _logger.LogWarning("Failed to send alert: {Name}, Status: {StatusCode}", name, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending alert: {Name}", name);
            // Don't throw - alerting failures shouldn't break the application
        }
    }

    public Task SendCriticalAlertAsync(
        string name,
        string description,
        Dictionary<string, string>? labels = null,
        CancellationToken cancellationToken = default)
    {
        return SendAlertAsync(name, "critical", description, labels, cancellationToken);
    }

    private void ConfigureHttpClient()
    {
        var token = _configuration["Alerts:ReceiverToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(5); // Fast timeout - alerting is best-effort
    }
}

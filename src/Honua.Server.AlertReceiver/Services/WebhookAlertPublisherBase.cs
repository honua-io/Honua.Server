// <copyright file="WebhookAlertPublisherBase.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Net.Http;
using System.Text;
using System.Text.Json;
using Honua.Server.AlertReceiver.Models;
using Honua.Server.AlertReceiver.Extensions;
using Honua.Server.Core.Performance;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Abstract base class for alert publishers that use HTTP webhooks.
/// </summary>
/// <remarks>
/// This base class handles common webhook patterns:
/// - HTTP client management via IHttpClientFactory
/// - Endpoint resolution by severity
/// - JSON serialization and HTTP POST operations
/// - Standardized error handling and logging
/// - Skip logic for missing/unconfigured endpoints
///
/// Derived classes remain independent and customize:
/// - Payload format (Slack blocks, Teams cards, PagerDuty events, etc.)
/// - Endpoint configuration keys and resolution strategies
/// - Optional authentication headers and tokens
/// - Custom serialization options and content types
///
/// Design Principles:
/// - Low coupling: Publishers don't depend on each other
/// - Single Responsibility: Base handles HTTP infrastructure only
/// - Open/Closed: Extensible without modification
/// - Template Method: Derived classes control business logic
/// </remarks>
public abstract class WebhookAlertPublisherBase : IAlertPublisher
{
    protected readonly HttpClient HttpClient;
    protected readonly IConfiguration Configuration;
    protected readonly ILogger Logger;
    protected readonly string ServiceName;

    protected WebhookAlertPublisherBase(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger logger,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        this.HttpClient = httpClientFactory.CreateClient(serviceName);
        this.Configuration = configuration;
        this.Logger = logger;
        this.ServiceName = serviceName;
    }

    public virtual async Task PublishAsync(
        AlertManagerWebhook webhook,
        string severity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhook);

        var endpoint = this.GetEndpoint(webhook, severity);
        if (endpoint.IsNullOrWhiteSpace())
        {
            this.Logger.LogDebug(
                "No {Service} endpoint configured for severity {Severity}, skipping alert publication",
                this.ServiceName,
                severity);
            return;
        }

        try
        {
            var payload = this.BuildPayload(webhook, severity);
            var json = this.SerializePayload(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Allow derived classes to add custom headers (auth, etc.)
            this.AddCustomHeaders(content, webhook, severity);

            // RESOURCE LEAK FIX: Add timeout to prevent hanging connections
            // Use configuration-based timeout with fallback to 30 seconds
            var timeoutSeconds = this.Configuration.GetValue($"Alerts:Publishers:{this.ServiceName}:TimeoutSeconds", 30);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var response = await this.HttpClient.PostAsync(endpoint, content, linkedCts.Token).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            this.Logger.LogInformation(
                "Published alert to {Service} - Alert: {AlertName}, Status: {Status}, Severity: {Severity}",
                this.ServiceName,
                this.GetAlertName(webhook),
                webhook.Status,
                severity);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // RESOURCE LEAK FIX: Timeout occurred (not user cancellation)
            this.Logger.LogError(
                ex,
                "Timeout publishing alert to {Service} endpoint {Endpoint}",
                this.ServiceName,
                endpoint);
            throw new TimeoutException($"Request to {this.ServiceName} timed out after configured duration", ex);
        }
        catch (HttpRequestException ex)
        {
            this.Logger.LogError(
                ex,
                "HTTP error publishing alert to {Service} - Status: {StatusCode}",
                this.ServiceName,
                ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to publish alert to {Service}", this.ServiceName);
            throw;
        }
    }

    /// <summary>
    /// Build the service-specific payload for the alert.
    /// </summary>
    /// <remarks>
    /// Implement this to create the payload structure for your service
    /// (Slack blocks, Teams adaptive cards, PagerDuty events, etc.).
    /// This is where service-specific business logic lives.
    /// </remarks>
    /// <param name="webhook">The alert webhook data</param>
    /// <param name="severity">The alert severity level</param>
    /// <returns>The payload object to be serialized</returns>
    protected abstract object BuildPayload(AlertManagerWebhook webhook, string severity);

    /// <summary>
    /// Get the webhook endpoint URL for this alert.
    /// </summary>
    /// <remarks>
    /// Implement this to resolve the endpoint from configuration.
    /// Different services may use different configuration key structures.
    /// Return null or empty string to skip publishing.
    /// </remarks>
    /// <param name="webhook">The alert webhook data</param>
    /// <param name="severity">The alert severity level</param>
    /// <returns>The webhook endpoint URL, or null/empty to skip</returns>
    protected abstract string? GetEndpoint(AlertManagerWebhook webhook, string severity);

    /// <summary>
    /// Serialize the payload to JSON.
    /// </summary>
    /// <remarks>
    /// Override this if your service needs custom JSON serialization options.
    /// Default uses web defaults (camelCase, ignores nulls).
    /// </remarks>
    /// <param name="payload">The payload object to serialize</param>
    /// <returns>JSON string representation</returns>
    protected virtual string SerializePayload(object payload)
    {
        return JsonSerializer.Serialize(payload, JsonSerializerOptionsRegistry.Web);
    }

    /// <summary>
    /// Add custom HTTP headers to the request.
    /// </summary>
    /// <remarks>
    /// Override this to add authentication headers, custom content types, etc.
    /// Default implementation does nothing.
    /// </remarks>
    /// <param name="content">The HTTP content to add headers to</param>
    /// <param name="webhook">The alert webhook data</param>
    /// <param name="severity">The alert severity level</param>
    protected virtual void AddCustomHeaders(
        StringContent content,
        AlertManagerWebhook webhook,
        string severity)
    {
        // Default: no custom headers
    }

    /// <summary>
    /// Extract alert name from webhook for logging.
    /// </summary>
    /// <remarks>
    /// Override if your service needs different alert name extraction logic.
    /// </remarks>
    /// <param name="webhook">The alert webhook data</param>
    /// <returns>The alert name</returns>
    protected virtual string GetAlertName(AlertManagerWebhook webhook)
    {
        return webhook.GroupLabels?.GetValueOrDefault("alertname") ?? "Unknown Alert";
    }
}

// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.AlertReceiver.Models;
using Honua.Server.AlertReceiver.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.AlertReceiver.Controllers;

/// <summary>
/// Receives alerts from Prometheus AlertManager and forwards to SNS.
/// </summary>
[ApiController]
[Route("alert")]
public sealed class AlertController : ControllerBase
{
    private readonly IAlertPublisher _alertPublisher;
    private readonly ILogger<AlertController> _logger;

    public AlertController(
        IAlertPublisher alertPublisher,
        ILogger<AlertController> logger)
    {
        _alertPublisher = alertPublisher ?? throw new ArgumentNullException(nameof(alertPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Receives critical alerts from AlertManager.
    /// </summary>
    [HttpPost("critical")]
    [Authorize]
    public async Task<IActionResult> Critical([FromBody] AlertManagerWebhook webhook, CancellationToken cancellationToken)
    {
        return await ProcessAlert(webhook, "critical", cancellationToken);
    }

    /// <summary>
    /// Receives warning alerts from AlertManager.
    /// </summary>
    [HttpPost("warning")]
    [Authorize]
    public async Task<IActionResult> Warning([FromBody] AlertManagerWebhook webhook, CancellationToken cancellationToken)
    {
        return await ProcessAlert(webhook, "warning", cancellationToken);
    }

    /// <summary>
    /// Receives database alerts from AlertManager.
    /// </summary>
    [HttpPost("database")]
    [Authorize]
    public async Task<IActionResult> Database([FromBody] AlertManagerWebhook webhook, CancellationToken cancellationToken)
    {
        return await ProcessAlert(webhook, "database", cancellationToken);
    }

    /// <summary>
    /// Receives storage alerts from AlertManager.
    /// </summary>
    [HttpPost("storage")]
    [Authorize]
    public async Task<IActionResult> Storage([FromBody] AlertManagerWebhook webhook, CancellationToken cancellationToken)
    {
        return await ProcessAlert(webhook, "storage", cancellationToken);
    }

    /// <summary>
    /// Receives default alerts from AlertManager.
    /// </summary>
    [HttpPost("default")]
    [Authorize]
    public async Task<IActionResult> Default([FromBody] AlertManagerWebhook webhook, CancellationToken cancellationToken)
    {
        return await ProcessAlert(webhook, "default", cancellationToken);
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "honua-alert-receiver" });
    }

    private async Task<IActionResult> ProcessAlert(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken)
    {
        // BUG FIX #9: SECURITY - Downgraded alert detail logging from Information to Debug level
        // Previous implementation logged full alert details at Information level, which could expose:
        // - Secrets embedded in alert descriptions or labels
        // - Customer identifiers and PII from Alertmanager annotations
        // - Internal system details that violate compliance requirements
        // Production environments should set minimum log level to Information or Warning to prevent exposure.

        _logger.LogInformation(
            "Received {Severity} alert: {AlertName}, Status: {Status}, Alerts: {Count}",
            severity,
            webhook.GroupLabels.GetValueOrDefault("alertname", "Unknown"),
            webhook.Status,
            webhook.Alerts.Count);

        // BUG FIX #9: Moved detailed alert logging to Debug level (only visible when debugging)
        // This prevents sensitive data in descriptions/labels from appearing in production logs
        foreach (var alert in webhook.Alerts)
        {
            _logger.LogDebug(
                "Alert detail - Name: {AlertName}, Status: {Status}, Severity: {Severity}, Description: {Description}",
                alert.Labels.GetValueOrDefault("alertname", "Unknown"),
                alert.Status,
                alert.Labels.GetValueOrDefault("severity", "unknown"),
                alert.Annotations.GetValueOrDefault("description", "No description"));
        }

        // Publish to SNS
        await _alertPublisher.PublishAsync(webhook, severity, cancellationToken);

        return Ok(new { status = "received", alertCount = webhook.Alerts.Count });
    }
}

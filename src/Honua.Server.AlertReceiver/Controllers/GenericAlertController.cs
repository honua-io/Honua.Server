// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.AlertReceiver.Models;
using Honua.Server.AlertReceiver.Services;
using Honua.Server.AlertReceiver.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Honua.Server.AlertReceiver.Controllers;

/// <summary>
/// Receives generic alerts from any source (application code, logs, health checks, etc.).
/// Not tied to Prometheus/AlertManager.
/// </summary>
[ApiController]
[Route("api/alerts")]
public sealed class GenericAlertController : ControllerBase
{
    private readonly IAlertPublisher _alertPublisher;
    private readonly IAlertDeduplicator _deduplicator;
    private readonly IAlertPersistenceService _persistenceService;
    private readonly IAlertSilencingService _silencingService;
    private readonly IAlertMetricsService _metricsService;
    private readonly ILogger<GenericAlertController> _logger;

    public GenericAlertController(
        IAlertPublisher alertPublisher,
        IAlertDeduplicator deduplicator,
        IAlertPersistenceService persistenceService,
        IAlertSilencingService silencingService,
        IAlertMetricsService metricsService,
        ILogger<GenericAlertController> logger)
    {
        _alertPublisher = alertPublisher ?? throw new ArgumentNullException(nameof(alertPublisher));
        _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _silencingService = silencingService ?? throw new ArgumentNullException(nameof(silencingService));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Send a single alert (authenticated via JWT).
    /// </summary>
    [HttpPost]
    [Authorize]
    [EnableRateLimiting("alert-ingestion")]
    public async Task<IActionResult> SendAlert([FromBody] GenericAlert alert, CancellationToken cancellationToken)
    {
        // Validate model state
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Alert validation failed: {ValidationErrors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        // Comprehensive validation for labels: keys, values, and protection against injection attacks
        // This validates against SQL injection, XSS, JSON injection, control characters, and null bytes
        if (!AlertInputValidator.ValidateLabels(alert.Labels, out var sanitizedLabels, out var labelErrors))
        {
            _logger.LogWarning(
                "Alert label validation failed - Name: {Name}, Source: {Source}, Errors: {Errors}",
                alert.Name, alert.Source, string.Join("; ", labelErrors));

            return BadRequest(new {
                error = "Label validation failed",
                details = labelErrors,
                guidance = "Label keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                          "Values must not contain control characters or null bytes."
            });
        }

        // Replace labels with sanitized versions (control characters removed)
        if (sanitizedLabels != null && sanitizedLabels.Count > 0)
        {
            alert.Labels = sanitizedLabels;
        }

        // Comprehensive validation for context: keys, values, and protection against injection attacks
        if (!AlertInputValidator.ValidateContext(alert.Context, out var sanitizedContext, out var contextErrors))
        {
            _logger.LogWarning(
                "Alert context validation failed - Name: {Name}, Source: {Source}, Errors: {Errors}",
                alert.Name, alert.Source, string.Join("; ", contextErrors));

            return BadRequest(new {
                error = "Context validation failed",
                details = contextErrors,
                guidance = "Context keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                          "String values must not contain control characters or null bytes."
            });
        }

        // Replace context with sanitized versions (control characters removed)
        if (sanitizedContext != null && sanitizedContext.Count > 0)
        {
            alert.Context = sanitizedContext;
        }

        var startTime = DateTime.UtcNow;
        var fingerprint = alert.Fingerprint ?? GenerateFingerprint(alert);

        // Record fingerprint length for monitoring and capacity planning
        _metricsService.RecordFingerprintLength(fingerprint.Length);

        // CRITICAL: Reject fingerprints exceeding 256 characters instead of silently truncating.
        // Silent truncation can cause hash collisions and incorrect deduplication, leading to alert storms.
        // The 256-character limit is enforced by the database schema and must be validated before persistence.
        if (fingerprint.Length > 256)
        {
            _logger.LogWarning(
                "Alert fingerprint exceeds maximum length of 256 characters - Name: {Name}, Source: {Source}, FingerprintLength: {Length}",
                alert.Name, alert.Source, fingerprint.Length);

            _metricsService.RecordAlertSuppressed("fingerprint_too_long", alert.Severity);

            return BadRequest(new
            {
                error = "Fingerprint exceeds maximum length of 256 characters",
                fingerprintLength = fingerprint.Length,
                maxLength = 256,
                details = "Alert fingerprints must be 256 characters or less to ensure proper deduplication. " +
                         "If using a custom fingerprint, consider using a hash (e.g., SHA256) of your identifier. " +
                         "Auto-generated fingerprints are always within the limit."
            });
        }

        alert.Fingerprint = fingerprint;

        try
        {
            _logger.LogInformation(
                "Received generic alert - Name: {Name}, Severity: {Severity}, Source: {Source}, Service: {Service}",
                alert.Name, alert.Severity, alert.Source, alert.Service);

            // Record metrics
            _metricsService.RecordAlertReceived(alert.Source, alert.Severity);

            // Check if silenced
            if (await _silencingService.IsAlertSilencedAsync(alert))
            {
                _metricsService.RecordAlertSuppressed("silenced", alert.Severity);
                await _persistenceService.SaveAlertAsync(alert, Array.Empty<string>(), true, "silenced");
                return Ok(new { status = "silenced", alertName = alert.Name, fingerprint });
            }

            // Check if acknowledged
            if (await _silencingService.IsAlertAcknowledgedAsync(fingerprint))
            {
                _metricsService.RecordAlertSuppressed("acknowledged", alert.Severity);
                await _persistenceService.SaveAlertAsync(alert, Array.Empty<string>(), true, "acknowledged");
                return Ok(new { status = "acknowledged", alertName = alert.Name, fingerprint });
            }

            // Check deduplication
            var routingSeverity = MapSeverityToRoute(alert.Severity);
            var (shouldSend, reservationId) = await _deduplicator.ShouldSendAlertAsync(fingerprint, routingSeverity, cancellationToken);
            if (!shouldSend)
            {
                _metricsService.RecordAlertSuppressed("deduplication", alert.Severity);
                await _persistenceService.SaveAlertAsync(alert, Array.Empty<string>(), true, "deduplication");
                return Ok(new { status = "deduplicated", alertName = alert.Name, fingerprint });
            }

            // Convert to AlertManager format for compatibility
            var webhook = GenericAlertAdapter.ToAlertManagerWebhook(alert);

            // Publish to all configured providers
            var publishedTo = new List<string>();
            bool publishingSucceeded = false;
            string? publishingError = null;

            try
            {
                await _alertPublisher.PublishAsync(webhook, routingSeverity, cancellationToken);
                publishedTo.Add("all_configured_providers");
                publishingSucceeded = true;

                // Record success in deduplicator only if publishing succeeded
                await _deduplicator.RecordAlertAsync(fingerprint, routingSeverity, reservationId, cancellationToken);
            }
            catch (Exception publishEx)
            {
                publishingError = publishEx.Message;
                _logger.LogError(publishEx, "Failed to publish alert: {Name}", alert.Name);
                _metricsService.RecordAlertSuppressed("publish_failure", alert.Severity);
                await _deduplicator.ReleaseReservationAsync(reservationId, cancellationToken);
            }

            // Record latency
            var latency = DateTime.UtcNow - startTime;
            _metricsService.RecordAlertLatency("composite", latency);

            // Persist alert with suppression status if publishing failed
            await _persistenceService.SaveAlertAsync(
                alert,
                publishedTo.ToArray(),
                !publishingSucceeded,
                publishingSucceeded ? null : "publish_failure");

            // Return appropriate status based on publishing result
            if (publishingSucceeded)
            {
                return Ok(new { status = "sent", alertName = alert.Name, fingerprint, publishedTo });
            }
            else
            {
                return Ok(new {
                    status = "failed",
                    alertName = alert.Name,
                    fingerprint,
                    publishedTo = Array.Empty<string>(),
                    error = publishingError
                });
            }
        }
        catch (AlertPersistenceException ex)
        {
            _logger.LogError(ex, "Alert persistence failure while processing alert: {Name}", alert.Name);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "Alert persistence unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process generic alert: {Name}", alert.Name);
            return StatusCode(500, new { error = "Failed to process alert" });
        }
    }

    private static string GenerateFingerprint(GenericAlert alert)
    {
        var key = $"{alert.Source}:{alert.Name}:{alert.Service ?? "default"}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Send a single alert via webhook (signature-validated, no auth required).
    /// This endpoint is designed for external webhook sources (e.g., monitoring tools).
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [EnableRateLimiting("webhook-ingestion")]
    public async Task<IActionResult> SendAlertWebhook([FromBody] GenericAlert alert, CancellationToken cancellationToken)
    {
        // Signature validation is handled by WebhookSignatureMiddleware
        // If we reach here, the signature has been validated

        // Validate model state
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Webhook alert validation failed: {ValidationErrors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        // Re-run the same validation as SendAlert to ensure consistency
        // Comprehensive validation for labels
        if (!AlertInputValidator.ValidateLabels(alert.Labels, out var sanitizedLabels, out var labelErrors))
        {
            _logger.LogWarning(
                "Webhook alert label validation failed - Name: {Name}, Source: {Source}, Errors: {Errors}",
                alert.Name, alert.Source, string.Join("; ", labelErrors));

            return BadRequest(new {
                error = "Label validation failed",
                details = labelErrors,
                guidance = "Label keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                          "Values must not contain control characters or null bytes."
            });
        }

        if (sanitizedLabels != null && sanitizedLabels.Count > 0)
        {
            alert.Labels = sanitizedLabels;
        }

        // Comprehensive validation for context
        if (!AlertInputValidator.ValidateContext(alert.Context, out var sanitizedContext, out var contextErrors))
        {
            _logger.LogWarning(
                "Webhook alert context validation failed - Name: {Name}, Source: {Source}, Errors: {Errors}",
                alert.Name, alert.Source, string.Join("; ", contextErrors));

            return BadRequest(new {
                error = "Context validation failed",
                details = contextErrors,
                guidance = "Context keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                          "String values must not contain control characters or null bytes."
            });
        }

        if (sanitizedContext != null && sanitizedContext.Count > 0)
        {
            alert.Context = sanitizedContext;
        }

        return await SendAlert(alert, cancellationToken);
    }

    /// <summary>
    /// Send a batch of alerts (authenticated via JWT).
    /// </summary>
    [HttpPost("batch")]
    [Authorize]
    [EnableRateLimiting("alert-batch-ingestion")]
    public async Task<IActionResult> SendAlertBatch([FromBody] GenericAlertBatch batch, CancellationToken cancellationToken)
    {
        // Validate model state
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Alert batch validation failed: {ValidationErrors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        // Validate batch size
        if (batch.Alerts == null || batch.Alerts.Count == 0)
        {
            return BadRequest(new { error = "Batch must contain at least one alert" });
        }

        if (batch.Alerts.Count > 100)
        {
            return BadRequest(new { error = "Maximum 100 alerts per batch" });
        }

        // Validate each alert in the batch with comprehensive security checks
        for (int i = 0; i < batch.Alerts.Count; i++)
        {
            var alert = batch.Alerts[i];

            // Validate and sanitize labels
            if (!AlertInputValidator.ValidateLabels(alert.Labels, out var sanitizedLabels, out var labelErrors))
            {
                _logger.LogWarning(
                    "Alert batch label validation failed - AlertIndex: {Index}, Name: {Name}, Errors: {Errors}",
                    i, alert.Name, string.Join("; ", labelErrors));

                return BadRequest(new {
                    error = $"Alert {i} label validation failed",
                    alertIndex = i,
                    alertName = alert.Name,
                    details = labelErrors,
                    guidance = "Label keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                              "Values must not contain control characters or null bytes."
                });
            }

            if (sanitizedLabels != null && sanitizedLabels.Count > 0)
            {
                alert.Labels = sanitizedLabels;
            }

            // Validate and sanitize context
            if (!AlertInputValidator.ValidateContext(alert.Context, out var sanitizedContext, out var contextErrors))
            {
                _logger.LogWarning(
                    "Alert batch context validation failed - AlertIndex: {Index}, Name: {Name}, Errors: {Errors}",
                    i, alert.Name, string.Join("; ", contextErrors));

                return BadRequest(new {
                    error = $"Alert {i} context validation failed",
                    alertIndex = i,
                    alertName = alert.Name,
                    details = contextErrors,
                    guidance = "Context keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                              "String values must not contain control characters or null bytes."
                });
            }

            if (sanitizedContext != null && sanitizedContext.Count > 0)
            {
                alert.Context = sanitizedContext;
            }
        }

        var tasks = new List<Task>();
        var errors = new List<string>();
        var publishedGroups = 0;

        try
        {
            _logger.LogInformation("Received generic alert batch with {Count} alerts", batch.Alerts.Count);

            // Group alerts by severity for routing
            var severityGroups = batch.Alerts.GroupBy(a => MapSeverityToRoute(a.Severity)).ToList();

            foreach (var group in severityGroups)
            {
                var webhook = GenericAlertAdapter.ToAlertManagerWebhook(group.ToList());
                tasks.Add(PublishGroupAsync(webhook, group.Key));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            // Determine overall status
            var totalGroups = severityGroups.Count;
            var status = publishedGroups == totalGroups ? "sent" :
                         publishedGroups == 0 ? "failed" :
                         "partial_success";

            return Ok(new {
                status,
                alertCount = batch.Alerts.Count,
                publishedGroups,
                totalGroups,
                errors = errors.Any() ? errors.ToArray() : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process generic alert batch");
            return StatusCode(500, new { error = "Failed to process alerts" });
        }

        async Task PublishGroupAsync(AlertManagerWebhook webhook, string severity)
        {
            try
            {
                await _alertPublisher.PublishAsync(webhook, severity, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref publishedGroups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish alert batch for severity: {Severity}", severity);
                lock (errors)
                {
                    errors.Add($"{severity}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "generic-alerts" });
    }

    private static string MapSeverityToRoute(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" or "crit" or "fatal" => "critical",
            "high" or "error" or "err" => "critical",
            "medium" or "warning" or "warn" => "warning",
            "low" or "info" or "information" => "warning",
            _ => "warning"
        };
    }
}

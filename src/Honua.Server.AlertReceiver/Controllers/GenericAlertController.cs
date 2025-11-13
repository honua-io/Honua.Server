// <copyright file="GenericAlertController.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

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
    private readonly IAlertPublisher alertPublisher;
    private readonly IAlertDeduplicator deduplicator;
    private readonly IAlertPersistenceService persistenceService;
    private readonly IAlertSilencingService silencingService;
    private readonly IAlertMetricsService metricsService;
    private readonly ILogger<GenericAlertController> logger;

    public GenericAlertController(
        IAlertPublisher alertPublisher,
        IAlertDeduplicator deduplicator,
        IAlertPersistenceService persistenceService,
        IAlertSilencingService silencingService,
        IAlertMetricsService metricsService,
        ILogger<GenericAlertController> logger)
    {
        this.alertPublisher = alertPublisher ?? throw new ArgumentNullException(nameof(alertPublisher));
        this.deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
        this.persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        this.silencingService = silencingService ?? throw new ArgumentNullException(nameof(silencingService));
        this.metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        if (!this.ModelState.IsValid)
        {
            this.logger.LogWarning(
                "Alert validation failed: {ValidationErrors}",
                string.Join("; ", this.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return this.BadRequest(this.ModelState);
        }

        // Comprehensive validation for labels: keys, values, and protection against injection attacks
        // This validates against SQL injection, XSS, JSON injection, control characters, and null bytes
        if (!AlertInputValidator.ValidateLabels(alert.Labels, out var sanitizedLabels, out var labelErrors))
        {
            this.logger.LogWarning(
                "Alert label validation failed - Name: {Name}, Source: {Source}, Errors: {Errors}",
                alert.Name,
                alert.Source,
                string.Join("; ", labelErrors));

            return this.BadRequest(new
            {
                error = "Label validation failed",
                details = labelErrors,
                guidance = "Label keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                          "Values must not contain control characters or null bytes.",
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
            this.logger.LogWarning(
                "Alert context validation failed - Name: {Name}, Source: {Source}, Errors: {Errors}",
                alert.Name,
                alert.Source,
                string.Join("; ", contextErrors));

            return this.BadRequest(new
            {
                error = "Context validation failed",
                details = contextErrors,
                guidance = "Context keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                          "String values must not contain control characters or null bytes.",
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
        this.metricsService.RecordFingerprintLength(fingerprint.Length);

        // CRITICAL: Reject fingerprints exceeding 256 characters instead of silently truncating.
        // Silent truncation can cause hash collisions and incorrect deduplication, leading to alert storms.
        // The 256-character limit is enforced by the database schema and must be validated before persistence.
        if (fingerprint.Length > 256)
        {
            this.logger.LogWarning(
                "Alert fingerprint exceeds maximum length of 256 characters - Name: {Name}, Source: {Source}, FingerprintLength: {Length}",
                alert.Name,
                alert.Source,
                fingerprint.Length);

            this.metricsService.RecordAlertSuppressed("fingerprint_too_long", alert.Severity);

            return this.BadRequest(new
            {
                error = "Fingerprint exceeds maximum length of 256 characters",
                fingerprintLength = fingerprint.Length,
                maxLength = 256,
                details = "Alert fingerprints must be 256 characters or less to ensure proper deduplication. " +
                         "If using a custom fingerprint, consider using a hash (e.g., SHA256) of your identifier. " +
                         "Auto-generated fingerprints are always within the limit.",
            });
        }

        alert.Fingerprint = fingerprint;

        try
        {
            this.logger.LogInformation(
                "Received generic alert - Name: {Name}, Severity: {Severity}, Source: {Source}, Service: {Service}",
                alert.Name,
                alert.Severity,
                alert.Source,
                alert.Service);

            // Record metrics
            this.metricsService.RecordAlertReceived(alert.Source, alert.Severity);

            // Check if silenced
            if (await this.silencingService.IsAlertSilencedAsync(alert))
            {
                this.metricsService.RecordAlertSuppressed("silenced", alert.Severity);
                await this.persistenceService.SaveAlertAsync(alert, Array.Empty<string>(), true, "silenced");
                return this.Ok(new { status = "silenced", alertName = alert.Name, fingerprint });
            }

            // Check if acknowledged
            if (await this.silencingService.IsAlertAcknowledgedAsync(fingerprint))
            {
                this.metricsService.RecordAlertSuppressed("acknowledged", alert.Severity);
                await this.persistenceService.SaveAlertAsync(alert, Array.Empty<string>(), true, "acknowledged");
                return this.Ok(new { status = "acknowledged", alertName = alert.Name, fingerprint });
            }

            // Check deduplication
            var routingSeverity = MapSeverityToRoute(alert.Severity);
            var (shouldSend, reservationId) = await this.deduplicator.ShouldSendAlertAsync(fingerprint, routingSeverity, cancellationToken);
            if (!shouldSend)
            {
                this.metricsService.RecordAlertSuppressed("deduplication", alert.Severity);
                await this.persistenceService.SaveAlertAsync(alert, Array.Empty<string>(), true, "deduplication");
                return this.Ok(new { status = "deduplicated", alertName = alert.Name, fingerprint });
            }

            // Convert to AlertManager format for compatibility
            var webhook = GenericAlertAdapter.ToAlertManagerWebhook(alert);

            // Publish to all configured providers
            var publishedTo = new List<string>();
            bool publishingSucceeded = false;
            string? publishingError = null;

            try
            {
                await this.alertPublisher.PublishAsync(webhook, routingSeverity, cancellationToken);
                publishedTo.Add("all_configured_providers");
                publishingSucceeded = true;

                // Record success in deduplicator only if publishing succeeded
                await this.deduplicator.RecordAlertAsync(fingerprint, routingSeverity, reservationId, cancellationToken);
            }
            catch (Exception publishEx)
            {
                publishingError = publishEx.Message;
                this.logger.LogError(publishEx, "Failed to publish alert: {Name}", alert.Name);
                this.metricsService.RecordAlertSuppressed("publish_failure", alert.Severity);
                await this.deduplicator.ReleaseReservationAsync(reservationId, cancellationToken);
            }

            // Record latency
            var latency = DateTime.UtcNow - startTime;
            this.metricsService.RecordAlertLatency("composite", latency);

            // Persist alert with suppression status if publishing failed
            await this.persistenceService.SaveAlertAsync(
                alert,
                publishedTo.ToArray(),
                !publishingSucceeded,
                publishingSucceeded ? null : "publish_failure");

            // Return appropriate status based on publishing result
            if (publishingSucceeded)
            {
                return this.Ok(new { status = "sent", alertName = alert.Name, fingerprint, publishedTo });
            }
            else
            {
                return this.Ok(new
                {
                    status = "failed",
                    alertName = alert.Name,
                    fingerprint,
                    publishedTo = Array.Empty<string>(),
                    error = publishingError,
                });
            }
        }
        catch (AlertPersistenceException ex)
        {
            this.logger.LogError(ex, "Alert persistence failure while processing alert: {Name}", alert.Name);
            return this.StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "Alert persistence unavailable" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to process generic alert: {Name}", alert.Name);
            return this.StatusCode(500, new { error = "Failed to process alert" });
        }
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
        if (!this.ModelState.IsValid)
        {
            this.logger.LogWarning(
                "Webhook alert validation failed: {ValidationErrors}",
                string.Join("; ", this.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return this.BadRequest(this.ModelState);
        }

        // Re-run the same validation as SendAlert to ensure consistency
        // Comprehensive validation for labels
        if (!AlertInputValidator.ValidateLabels(alert.Labels, out var sanitizedLabels, out var labelErrors))
        {
            this.logger.LogWarning(
                "Webhook alert label validation failed - Name: {Name}, Source: {Source}, Errors: {Errors}",
                alert.Name,
                alert.Source,
                string.Join("; ", labelErrors));

            return this.BadRequest(new
            {
                error = "Label validation failed",
                details = labelErrors,
                guidance = "Label keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                          "Values must not contain control characters or null bytes.",
            });
        }

        if (sanitizedLabels != null && sanitizedLabels.Count > 0)
        {
            alert.Labels = sanitizedLabels;
        }

        // Comprehensive validation for context
        if (!AlertInputValidator.ValidateContext(alert.Context, out var sanitizedContext, out var contextErrors))
        {
            this.logger.LogWarning(
                "Webhook alert context validation failed - Name: {Name}, Source: {Source}, Errors: {Errors}",
                alert.Name,
                alert.Source,
                string.Join("; ", contextErrors));

            return this.BadRequest(new
            {
                error = "Context validation failed",
                details = contextErrors,
                guidance = "Context keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                          "String values must not contain control characters or null bytes.",
            });
        }

        if (sanitizedContext != null && sanitizedContext.Count > 0)
        {
            alert.Context = sanitizedContext;
        }

        return await this.SendAlert(alert, cancellationToken);
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
        if (!this.ModelState.IsValid)
        {
            this.logger.LogWarning("Alert batch validation failed: {ValidationErrors}",
                string.Join("; ", this.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return this.BadRequest(this.ModelState);
        }

        // Validate batch size
        if (batch.Alerts == null || batch.Alerts.Count == 0)
        {
            return this.BadRequest(new { error = "Batch must contain at least one alert" });
        }

        if (batch.Alerts.Count > 100)
        {
            return this.BadRequest(new { error = "Maximum 100 alerts per batch" });
        }

        // Validate each alert in the batch with comprehensive security checks
        for (int i = 0; i < batch.Alerts.Count; i++)
        {
            var alert = batch.Alerts[i];

            // Validate and sanitize labels
            if (!AlertInputValidator.ValidateLabels(alert.Labels, out var sanitizedLabels, out var labelErrors))
            {
                this.logger.LogWarning(
                    "Alert batch label validation failed - AlertIndex: {Index}, Name: {Name}, Errors: {Errors}",
                    i,
                    alert.Name,
                    string.Join("; ", labelErrors));

                return this.BadRequest(new
                {
                    error = $"Alert {i} label validation failed",
                    alertIndex = i,
                    alertName = alert.Name,
                    details = labelErrors,
                    guidance = "Label keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                              "Values must not contain control characters or null bytes.",
                });
            }

            if (sanitizedLabels != null && sanitizedLabels.Count > 0)
            {
                alert.Labels = sanitizedLabels;
            }

            // Validate and sanitize context
            if (!AlertInputValidator.ValidateContext(alert.Context, out var sanitizedContext, out var contextErrors))
            {
                this.logger.LogWarning(
                    "Alert batch context validation failed - AlertIndex: {Index}, Name: {Name}, Errors: {Errors}",
                    i,
                    alert.Name,
                    string.Join("; ", contextErrors));

                return this.BadRequest(new
                {
                    error = $"Alert {i} context validation failed",
                    alertIndex = i,
                    alertName = alert.Name,
                    details = contextErrors,
                    guidance = "Context keys must contain only alphanumeric characters, underscore, hyphen, and dot. " +
                              "String values must not contain control characters or null bytes.",
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
            this.logger.LogInformation("Received generic alert batch with {Count} alerts", batch.Alerts.Count);

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

            return this.Ok(new
            {
                status,
                alertCount = batch.Alerts.Count,
                publishedGroups,
                totalGroups,
                errors = errors.Any() ? errors.ToArray() : null,
            });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to process generic alert batch");
            return this.StatusCode(500, new { error = "Failed to process alerts" });
        }

        async Task PublishGroupAsync(AlertManagerWebhook webhook, string severity)
        {
            try
            {
                await this.alertPublisher.PublishAsync(webhook, severity, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref publishedGroups);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to publish alert batch for severity: {Severity}", severity);
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
        return this.Ok(new { status = "healthy", service = "generic-alerts" });
    }

    private static string GenerateFingerprint(GenericAlert alert)
    {
        var key = $"{alert.Source}:{alert.Name}:{alert.Service ?? "default"}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string MapSeverityToRoute(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" or "crit" or "fatal" => "critical",
            "high" or "error" or "err" => "critical",
            "medium" or "warning" or "warn" => "warning",
            "low" or "info" or "information" => "warning",
            _ => "warning",
        };
    }
}

// <copyright file="AlertHistoryController.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Models;
using Honua.Server.AlertReceiver.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.AlertReceiver.Controllers;

/// <summary>
/// API for querying alert history, acknowledgements, and silencing rules.
/// </summary>
[ApiController]
[Route("api/alerts")]
public sealed class AlertHistoryController : ControllerBase
{
    private readonly IAlertPersistenceService persistenceService;
    private readonly IAlertSilencingService silencingService;
    private readonly IAlertEscalationService? escalationService;
    private readonly ILogger<AlertHistoryController> logger;

    public AlertHistoryController(
        IAlertPersistenceService persistenceService,
        IAlertSilencingService silencingService,
        ILogger<AlertHistoryController> logger,
        IAlertEscalationService? escalationService = null)
    {
        this.persistenceService = persistenceService;
        this.silencingService = silencingService;
        this.logger = logger;
        this.escalationService = escalationService;
    }

    /// <summary>
    /// Get recent alerts.
    /// </summary>
    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int limit = 100,
        [FromQuery] string? severity = null)
    {
        // Validate limit (max 1000)
        if (limit < 1 || limit > 1000)
        {
            return this.BadRequest(new { error = "Limit must be between 1 and 1000" });
        }

        // Validate severity format
        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (severity.Length > 50)
            {
                return this.BadRequest(new { error = "Severity must be 50 characters or less" });
            }

            var validSeverities = new[] { "critical", "high", "medium", "low", "info", "warning", "error", "crit", "err", "warn", "fatal", "information" };
            if (!validSeverities.Contains(severity.ToLowerInvariant()))
            {
                return this.BadRequest(new { error = $"Invalid severity: {severity}. Valid values: critical, high, medium, low, info, warning, error" });
            }
        }

        try
        {
            var alerts = await this.persistenceService.GetRecentAlertsAsync(limit, severity);
            return this.Ok(new { alerts, count = alerts.Count });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get alert history");
            return this.StatusCode(500, new { error = "Failed to retrieve alert history" });
        }
    }

    /// <summary>
    /// Get alert by fingerprint.
    /// </summary>
    [HttpGet("history/{fingerprint}")]
    [Authorize]
    public async Task<IActionResult> GetByFingerprint(string fingerprint)
    {
        // Validate fingerprint format
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return this.BadRequest(new { error = "Fingerprint is required" });
        }

        if (fingerprint.Length > 256)
        {
            return this.BadRequest(new { error = "Fingerprint must be 256 characters or less" });
        }

        // Validate fingerprint contains only valid characters (alphanumeric, dash, underscore)
        if (!System.Text.RegularExpressions.Regex.IsMatch(fingerprint, @"^[a-zA-Z0-9\-_]+$"))
        {
            return this.BadRequest(new { error = "Fingerprint contains invalid characters. Only alphanumeric, dash, and underscore are allowed" });
        }

        try
        {
            var alert = await this.persistenceService.GetAlertByFingerprintAsync(fingerprint);
            if (alert == null)
            {
                return this.NotFound(new { error = "Alert not found" });
            }

            return this.Ok(alert);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get alert by fingerprint: {Fingerprint}", fingerprint);
            return this.StatusCode(500, new { error = "Failed to retrieve alert" });
        }
    }

    /// <summary>
    /// Acknowledge an alert.
    /// </summary>
    [HttpPost("acknowledge")]
    [Authorize]
    public async Task<IActionResult> Acknowledge([FromBody] AcknowledgeRequest request)
    {
        if (!this.ModelState.IsValid)
        {
            this.logger.LogWarning(
                "Acknowledge request validation failed: {ValidationErrors}",
                string.Join("; ", this.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return this.BadRequest(this.ModelState);
        }

        // Additional validation for date range
        if (request.ExpiresInMinutes.HasValue)
        {
            if (request.ExpiresInMinutes.Value < 1 || request.ExpiresInMinutes.Value > 43200)
            {
                return this.BadRequest(new { error = "ExpiresInMinutes must be between 1 and 43200 (30 days)" });
            }
        }

        try
        {
            await this.silencingService.AcknowledgeAlertAsync(
                request.Fingerprint,
                request.AcknowledgedBy,
                request.Comment,
                request.ExpiresInMinutes.HasValue ? TimeSpan.FromMinutes(request.ExpiresInMinutes.Value) : null);

            return this.Ok(new { status = "acknowledged", fingerprint = request.Fingerprint });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to acknowledge alert: {Fingerprint}", request.Fingerprint);
            return this.StatusCode(500, new { error = "Failed to acknowledge alert" });
        }
    }

    /// <summary>
    /// Create a silencing rule.
    /// </summary>
    [HttpPost("silence")]
    [Authorize]
    public async Task<IActionResult> CreateSilence([FromBody] CreateSilenceRequest request)
    {
        if (!this.ModelState.IsValid)
        {
            this.logger.LogWarning(
                "Create silence request validation failed: {ValidationErrors}",
                string.Join("; ", this.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return this.BadRequest(this.ModelState);
        }

        // Additional validation for date range
        var startsAt = request.StartsAt ?? DateTime.UtcNow;
        if (request.EndsAt <= startsAt)
        {
            return this.BadRequest(new { error = "EndsAt must be after StartsAt" });
        }

        // Validate silencing rule duration (max 1 year)
        var duration = request.EndsAt - startsAt;
        if (duration.TotalDays > 365)
        {
            return this.BadRequest(new { error = "Silencing rule duration cannot exceed 365 days" });
        }

        // Validate matchers
        if (request.Matchers == null || request.Matchers.Count == 0)
        {
            return this.BadRequest(new { error = "At least one matcher is required" });
        }

        if (request.Matchers.Count > 50)
        {
            return this.BadRequest(new { error = "Maximum 50 matchers allowed" });
        }

        foreach (var (key, value) in request.Matchers)
        {
            if (key.Length > 256)
            {
                return this.BadRequest(new { error = $"Matcher key '{key.Substring(0, Math.Min(50, key.Length))}...' exceeds 256 character limit" });
            }
            if (value.Length > 1000)
            {
                return this.BadRequest(new { error = $"Matcher value for key '{key}' exceeds 1000 character limit" });
            }
        }

        try
        {
            await this.silencingService.CreateSilencingRuleAsync(
                request.Name,
                request.Matchers,
                request.CreatedBy,
                startsAt,
                request.EndsAt,
                request.Comment);

            return this.Ok(new { status = "created", name = request.Name });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to create silencing rule: {Name}", request.Name);
            return this.StatusCode(500, new { error = "Failed to create silencing rule" });
        }
    }

    /// <summary>
    /// Get active silencing rules.
    /// </summary>
    [HttpGet("silences")]
    [Authorize]
    public async Task<IActionResult> GetSilences()
    {
        try
        {
            var rules = await this.silencingService.GetActiveSilencingRulesAsync();
            return this.Ok(new { rules, count = rules.Count });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get silencing rules");
            return this.StatusCode(500, new { error = "Failed to retrieve silencing rules" });
        }
    }

    /// <summary>
    /// Deactivate a silencing rule.
    /// </summary>
    [HttpDelete("silences/{ruleId}")]
    [Authorize]
    public async Task<IActionResult> DeleteSilence(long ruleId)
    {
        try
        {
            await this.silencingService.DeactivateSilencingRuleAsync(ruleId);
            return this.Ok(new { status = "deactivated", ruleId });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to deactivate silencing rule: {RuleId}", ruleId);
            return this.StatusCode(500, new { error = "Failed to deactivate silencing rule" });
        }
    }

    /// <summary>
    /// Acknowledge an alert (stops escalation).
    /// </summary>
    [HttpPost("{alertId}/acknowledge")]
    [Authorize]
    public async Task<IActionResult> AcknowledgeAlert(long alertId, [FromBody] AcknowledgeAlertRequest request)
    {
        if (this.escalationService == null)
        {
            return this.BadRequest(new { error = "Alert escalation is not enabled" });
        }

        if (!this.ModelState.IsValid)
        {
            return this.BadRequest(this.ModelState);
        }

        try
        {
            await this.escalationService.AcknowledgeAlertAsync(alertId, request.AcknowledgedBy, request.Notes);
            return this.Ok(new { status = "acknowledged", alertId });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to acknowledge alert: {AlertId}", alertId);
            return this.StatusCode(500, new { error = "Failed to acknowledge alert" });
        }
    }

    /// <summary>
    /// Get escalation status for an alert.
    /// </summary>
    [HttpGet("{alertId}/escalation")]
    [Authorize]
    public async Task<IActionResult> GetEscalationStatus(long alertId)
    {
        if (this.escalationService == null)
        {
            return this.BadRequest(new { error = "Alert escalation is not enabled" });
        }

        try
        {
            var status = await this.escalationService.GetEscalationStatusAsync(alertId);
            if (status == null)
            {
                return this.NotFound(new { error = "No escalation found for this alert" });
            }

            return this.Ok(status);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get escalation status for alert: {AlertId}", alertId);
            return this.StatusCode(500, new { error = "Failed to retrieve escalation status" });
        }
    }

    /// <summary>
    /// Get escalation history for an alert.
    /// </summary>
    [HttpGet("{alertId}/escalation/history")]
    [Authorize]
    public async Task<IActionResult> GetEscalationHistory(long alertId)
    {
        if (this.escalationService == null)
        {
            return this.BadRequest(new { error = "Alert escalation is not enabled" });
        }

        try
        {
            var history = await this.escalationService.GetEscalationHistoryAsync(alertId);
            return this.Ok(new { events = history, count = history.Count });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get escalation history for alert: {AlertId}", alertId);
            return this.StatusCode(500, new { error = "Failed to retrieve escalation history" });
        }
    }

    /// <summary>
    /// Cancel escalation for an alert.
    /// </summary>
    [HttpPost("{alertId}/escalation/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelEscalation(long alertId, [FromBody] CancelEscalationRequest request)
    {
        if (this.escalationService == null)
        {
            return this.BadRequest(new { error = "Alert escalation is not enabled" });
        }

        if (!this.ModelState.IsValid)
        {
            return this.BadRequest(this.ModelState);
        }

        try
        {
            await this.escalationService.CancelEscalationAsync(alertId, request.Reason);
            return this.Ok(new { status = "cancelled", alertId });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to cancel escalation for alert: {AlertId}", alertId);
            return this.StatusCode(500, new { error = "Failed to cancel escalation" });
        }
    }
}

/// <summary>
/// Request to acknowledge an alert.
/// </summary>
public sealed class AcknowledgeAlertRequest
{
    public string AcknowledgedBy { get; set; } = string.Empty;

    public string? Notes { get; set; }
}

/// <summary>
/// Request to cancel an escalation.
/// </summary>
public sealed class CancelEscalationRequest
{
    public string Reason { get; set; } = string.Empty;
}
